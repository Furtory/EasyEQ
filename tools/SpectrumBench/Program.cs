using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PeaceWpf.Controls;
using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;

namespace SpectrumBench;

/// <summary>
/// 一次性测量：频谱这条链每帧在 CPU 上花多少时间。
///
/// 三块：
/// 1. 生产 SpectrumAnalyzer（8192 点 / 96 带）一帧多慢 —— 这是 UI 线程上真实付的成本。
/// 2. 参数化复刻同算法，扫不同 FFT 点数 / 带数的代价 —— 回答“更细要付多少”。
/// 3. EqGraphView.OnRender 的纯几何构建耗时，以及 RenderTargetBitmap 软件光栅化的上限。
///
/// 它不启动主程序，不碰音频设备，也不改生产代码。
/// </summary>
internal static class Program
{
    private const int SampleRate = 48000;

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var report = new StringBuilder();

        report.AppendLine("# 频谱链路基准");
        report.AppendLine();
        report.AppendLine($"- 时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"- 采样率：{SampleRate} Hz");
        report.AppendLine($"- .NET：{Environment.Version} / {Environment.OSVersion}");
        report.AppendLine($"- 处理器：{Environment.ProcessorCount} 逻辑核");
        report.AppendLine();

        // ---------- 0. 纯 FFT 单次 ----------
        Console.WriteLine("[0/3] 纯 FFT …");
        var fft32768P50 = 0.0;
        foreach (var n in new[] { 8192, 16384, 32768 })
        {
            var buffer = new Complex[n];
            for (var i = 0; i < n; i++)
            {
                buffer[i] = new Complex(Math.Cos(i * 0.31), Math.Sin(i * 0.37));
            }

            var fftStats = Measure(() => SpectrumAnalyzer.Fft(buffer), warmup: 2, iterations: 8);
            if (n == 32768)
            {
                fft32768P50 = fftStats.P50;
            }

            Console.WriteLine($"[0/3] FFT {n}: P50 {fftStats.P50:F0} µs");
        }

        Console.WriteLine();

        // ---------- 1. 生产分析器 ----------
        Console.WriteLine("[1/3] 生产分析器 …");
        var production = new SpectrumAnalyzer(SampleRate);
        FeedProduction(production);
        var productionStats = Measure(() => production.Snapshot(), warmup: 120, iterations: 800);

        report.AppendLine("## 1. 现状：SpectrumAnalyzer.Snapshot()（8192 点 / 96 带）");
        report.AppendLine();
        report.AppendLine("一次调用 = 窗口化 8192 点 + FFT + 96 带取峰 + 1/3 倍频程平滑 + 时间平滑 + Clone。");
        report.AppendLine();
        report.AppendLine("| 配置 | 平均 | P50 | P90 | P99 | 最大 |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|");
        report.AppendLine(Row("8192 / 96（现状）", productionStats));
        report.AppendLine();
        report.AppendLine($"- 30 fps 下 UI 线程占用（按 P50）：{Percent(productionStats.P50, 33_333):F1}%");
        report.AppendLine($"- 60 fps 下 UI 线程占用（按 P50）：{Percent(productionStats.P50, 16_666):F1}%");
        report.AppendLine($"- 30 fps 最差帧 P99 占用：{Percent(productionStats.P99, 33_333):F1}%");
        report.AppendLine();

        // ---------- 2. 扩展组合 ----------
        report.AppendLine("## 2. 加大 FFT 点数 / 带数的代价（同算法复刻）");
        report.AppendLine();
        report.AppendLine("时间窗 = FFT 点数 / 采样率；bin = 采样率 / FFT 点数。");
        report.AppendLine();
        report.AppendLine("| 配置 | 时间窗 | bin | 平均 | P50 | P90 | P99 | 最大 |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");

        var configs = new (int Fft, int Bands)[]
        {
            (8192, 96),
            (8192, 192),
            (16384, 192),
            (16384, 256),
        };

        var combos = new List<(string Name, Stats Stats)>();
        foreach (var (fft, bands) in configs)
        {
            Console.WriteLine($"[2/3] {fft} / {bands} …");
            var analyzer = new ParamAnalyzer(SampleRate, fft, bands);
            FeedParam(analyzer, fft);
            var iterations = fft >= 32768 ? 80 : 400;
            var stats = Measure(() => analyzer.Snapshot(), warmup: fft >= 32768 ? 20 : 60, iterations: iterations);
            combos.Add(($"{fft} / {bands}", stats));

            var windowMs = fft * 1000.0 / SampleRate;
            var binHz = (double)SampleRate / fft;
            report.AppendLine($"| {fft} / {bands} | {windowMs:F1} ms | {binHz:F2} Hz | {stats.Mean:F1} | {stats.P50:F1} | {stats.P90:F1} | {stats.P99:F1} | {stats.Max:F1} |");
        }

        report.AppendLine();

        // 复刻版自身与生产版核对，确保复刻可信。
        var replica = combos[0].Stats;
        var delta = productionStats.P50 > 0 ? (replica.P50 - productionStats.P50) / productionStats.P50 * 100 : 0;
        report.AppendLine($"复刻 8192/96 与生产版 P50 差：{delta:+0.0;-0.0}%（用来验证复刻可信度；表内其余行为复刻版）。");
        report.AppendLine();

        // ---------- 2.5 电平一致性 ----------
        Console.WriteLine("[2.5/3] 电平一致性 …");
        var toneRows = new List<(int Bands, double PeakDb)>();
        foreach (var bands in new[] { 96, 192, 384 })
        {
            var toneAnalyzer = new ParamAnalyzer(SampleRate, 8192, bands);
            var toneLength = SpectrumAnalyzer.FftSize * 2;
            var tone = new float[toneLength];
            for (var i = 0; i < toneLength; i++)
            {
                tone[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / SampleRate));
            }

            toneAnalyzer.Feed(tone);
            toneRows.Add((bands, toneAnalyzer.Snapshot().Max()));
        }

        report.AppendLine("## 2.5 电平一致性（半幅 1 kHz 正弦，峰值带读数）");
        report.AppendLine();
        report.AppendLine("半幅正弦理论读数就是 -6.0 dB。带数变化不该改变它。");
        report.AppendLine();
        report.AppendLine("| 带数 | 峰值 dB |");
        report.AppendLine("|---:|---:|");
        foreach (var (bands, peakDb) in toneRows)
        {
            report.AppendLine($"| {bands} | {peakDb:F2} |");
        }

        report.AppendLine();

        // ---------- 3. 渲染 ----------
        Console.WriteLine("[3/3] 渲染 …");
        var view = new EqGraphView
        {
            Width = 1200,
            Height = 600,
            Points = EqPointList.CreateDefault(),
            GainRange = 15,
            IsEditable = false,
            PreAmpDb = 0,
            ShowGrid = true,
            Spectrum = MakeSpectrum(192),
        };

        view.Measure(new Size(1200, 600));
        view.Arrange(new Rect(0, 0, 1200, 600));

        var onRender = typeof(EqGraphView).GetMethod("OnRender", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("找不到 EqGraphView.OnRender。");
        var drawGrid = typeof(EqGraphView).GetMethod("DrawGrid", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("找不到 EqGraphView.DrawGrid。");
        var drawSpectrum = typeof(EqGraphView).GetMethod("DrawSpectrum", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("找不到 EqGraphView.DrawSpectrum。");

        var visual = new DrawingVisual();
        var geometryOnlyStats = Measure(
            () =>
            {
                using var context = visual.RenderOpen();
                onRender.Invoke(view, new object?[] { context });
            },
            warmup: 40,
            iterations: 300);

        var gridStats = Measure(
            () =>
            {
                using var context = visual.RenderOpen();
                drawGrid.Invoke(view, new object?[] { context });
            },
            warmup: 40,
            iterations: 300);

        var spectrumStats = Measure(
            () =>
            {
                using var context = visual.RenderOpen();
                drawSpectrum.Invoke(view, new object?[] { context });
            },
            warmup: 40,
            iterations: 300);

        var bitmap = new RenderTargetBitmap(1200, 600, 96, 96, PixelFormats.Pbgra32);
        var frame = 0;
        var rasterStats = Measure(
            () =>
            {
                view.Spectrum = MakeSpectrum(96, frame++);
                bitmap.Render(view);
            },
            warmup: 20,
            iterations: 120);

        // OnRender 里最重的一块是 DrawCurve：每帧把点列表重新算成 900 点响应曲线。
        // 单独量一次，看它占 OnRender 的多少。
        var defaultSnapshot = EqPointList.CreateDefault().ToSnapshot();
        var curveStats = Measure(
            () => EqResponse.Curve(defaultSnapshot, 900, EqGraphView.MinHz, EqGraphView.MaxHz),
            warmup: 40,
            iterations: 400);

        report.AppendLine("## 3. EqGraphView 渲染成本（1200×600，96 带频谱 + 10 个 EQ 点）");
        report.AppendLine();
        report.AppendLine("| 路径 | 平均 | P50 | P90 | P99 | 最大 |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|");
        report.AppendLine(Row("纯 OnRender（几何构建，无光栅化）", geometryOnlyStats));
        report.AppendLine(Row("其中：网格与刻度文字（DrawGrid）", gridStats));
        report.AppendLine(Row("其中：频谱曲线与面积（DrawSpectrum）", spectrumStats));
        report.AppendLine(Row("其中：仅响应曲线（900 点）", curveStats));
        report.AppendLine(Row("RenderTargetBitmap（含软件光栅化）", rasterStats));
        report.AppendLine();
        report.AppendLine($"- 纯几何构建在 30 fps 下占 UI 线程：{Percent(geometryOnlyStats.P50, 33_333):F3}%");
        report.AppendLine($"- 纯几何构建在 60 fps 下占 UI 线程：{Percent(geometryOnlyStats.P50, 16_666):F3}%");
        report.AppendLine("- 第二行是 WPF 的软件光栅化路径，真实运行时由 D3D 合成，这行是上限而不是实测。");
        report.AppendLine();

        // ---------- 4. 结论 ----------
        report.AppendLine("## 4. 结论");
        report.AppendLine();
        report.AppendLine($"- 一帧 FFT（现状）P50 约 {productionStats.P50:F0} µs；纯 FFT 16384 点约 {combos[2].Stats.P50:F0} µs（整帧），32768 点纯 FFT 约 {fft32768P50:F0} µs。");
        report.AppendLine($"- 纯几何构建 P50 约 {geometryOnlyStats.P50:F0} µs，对 30/60 fps 来说几乎为零。");
        report.AppendLine("- 判断：GPU 化不会改善这两项；真正的改动点是刷新节奏、时间窗和带数/点数取舍。");

        var text = report.ToString();
        Console.WriteLine(text);

        var outPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "SpectrumBench", "result.md");
        File.WriteAllText(outPath, text, Encoding.UTF8);
        Console.WriteLine($"已写入 {outPath}");

        return 0;
    }

    private static void FeedProduction(SpectrumAnalyzer analyzer)
    {
        var random = new Random(1);
        var block = new float[4096];
        var total = 0;
        while (total < SampleRate)
        {
            var count = Math.Min(block.Length, SampleRate - total);
            for (var i = 0; i < count; i++)
            {
                var t = (total + i) / (double)SampleRate;
                block[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 1000 * t) + 0.1 * (random.NextDouble() * 2 - 1));
            }

            analyzer.Feed(block.AsSpan(0, count));
            total += count;
        }
    }

    private static void FeedParam(ParamAnalyzer analyzer, int fftSize)
    {
        var random = new Random(1);
        var block = new float[4096];
        var total = 0;
        var target = Math.Max(fftSize * 2, SampleRate);
        while (total < target)
        {
            var count = Math.Min(block.Length, SampleRate - total);
            for (var i = 0; i < count; i++)
            {
                var t = (total + i) / (double)SampleRate;
                block[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 1000 * t) + 0.1 * (random.NextDouble() * 2 - 1));
            }

            analyzer.Feed(block.AsSpan(0, count));
            total += count;
        }
    }

    private static float[] MakeSpectrum(int count, int seed = 0)
    {
        var random = new Random(seed + 1);
        var values = new float[count];
        for (var i = 0; i < count; i++)
        {
            // 低频偏强、高频偏弱，看起来像真频谱而不是一片噪声。
            var tilt = -60.0 * i / count;
            values[i] = (float)(-75 - tilt + random.NextDouble() * 40);
        }

        return values;
    }

    private static Stats Measure(Action action, int warmup, int iterations)
    {
        for (var i = 0; i < warmup; i++)
        {
            action();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var samples = new double[iterations];
        var stopwatch = new Stopwatch();
        for (var i = 0; i < iterations; i++)
        {
            stopwatch.Restart();
            action();
            stopwatch.Stop();
            samples[i] = stopwatch.Elapsed.TotalMicroseconds;
        }

        return Stats.From(samples);
    }

    private static string Row(string name, Stats stats) =>
        $"| {name} | {stats.Mean:F1} | {stats.P50:F1} | {stats.P90:F1} | {stats.P99:F1} | {stats.Max:F1} |";

    private static double Percent(double microseconds, double budgetMicroseconds) =>
        microseconds / budgetMicroseconds * 100;

    private readonly record struct Stats(double Mean, double P50, double P90, double P99, double Max)
    {
        public static Stats From(double[] samples)
        {
            Array.Sort(samples);
            return new Stats(
                samples.Average(),
                Percentile(samples, 0.50),
                Percentile(samples, 0.90),
                Percentile(samples, 0.99),
                samples[^1]);
        }

        private static double Percentile(double[] sorted, double q)
        {
            if (sorted.Length == 0)
            {
                return 0;
            }

            var index = Math.Clamp((int)Math.Round(q * (sorted.Length - 1)), 0, sorted.Length - 1);
            return sorted[index];
        }
    }

    /// <summary>
    /// 与生产 <see cref="SpectrumAnalyzer"/> 逐行等价的参数化复刻。
    /// 唯一目的是扫不同 (FFT 点数, 带数) 组合的耗时；逻辑改了就得同步改这里，
    /// 结果里那一行“与生产版差”就是用来兜住漂移的。
    /// </summary>
    private sealed class ParamAnalyzer
    {
        private const double MinFrequencyHz = 20;
        private const double MaxFrequencyHz = 20000;
        private const double FloorDb = -90;
        private const double ReleaseDbPerSnapshot = 3;
        private const double SmoothingOctaves = 1.0 / 3.0;

        private readonly int _sampleRate;
        private readonly int _fftSize;
        private readonly int _bandCount;
        private readonly double[] _window;
        private readonly double[] _ring;
        private readonly double[] _windowed;
        private readonly Complex[] _fft;
        private readonly float[] _bands;
        private readonly int[] _windowStart;
        private readonly double[][] _windowWeights;

        private readonly object _feedGate = new();
        private readonly object _computeGate = new();

        private int _write;
        private bool _filled;

        public ParamAnalyzer(int sampleRate, int fftSize, int bandCount)
        {
            _sampleRate = sampleRate;
            _fftSize = fftSize;
            _bandCount = bandCount;
            _window = BuildHannWindow(fftSize);
            _ring = new double[fftSize];
            _windowed = new double[fftSize];
            _fft = new Complex[fftSize];
            _bands = new float[bandCount];
            BuildBandWindows(sampleRate, fftSize, bandCount, out _windowStart, out _windowWeights);
            Array.Fill(_bands, (float)FloorDb);
        }

        public void Feed(ReadOnlySpan<float> mono)
        {
            lock (_feedGate)
            {
                foreach (var sample in mono)
                {
                    _ring[_write] = sample;
                    _write++;

                    if (_write == _fftSize)
                    {
                        _write = 0;
                        _filled = true;
                    }
                }
            }
        }

        public float[] Snapshot()
        {
            lock (_computeGate)
            {
                lock (_feedGate)
                {
                    if (!_filled)
                    {
                        return (float[])_bands.Clone();
                    }

                    for (var i = 0; i < _fftSize; i++)
                    {
                        _windowed[i] = _ring[(_write + i) % _fftSize] * _window[i];
                    }
                }

                for (var i = 0; i < _fftSize; i++)
                {
                    _fft[i] = new Complex(_windowed[i], 0);
                }

                Fft(_fft);

                const double scaleDivisor = 4.0;
                var scale = _fftSize / scaleDivisor;

                for (var band = 0; band < _bandCount; band++)
                {
                    var start = _windowStart[band];
                    var weights = _windowWeights[band];
                    var energy = 0.0;

                    for (var i = 0; i < weights.Length; i++)
                    {
                        var magnitude = _fft[start + i].Magnitude / scale;
                        energy += magnitude * magnitude * weights[i];
                    }

                    var db = energy > 1e-18 ? Math.Max(FloorDb, 10 * Math.Log10(energy)) : FloorDb;
                    var previous = _bands[band];
                    var smoothed = db >= previous ? db : Math.Max(db, previous - ReleaseDbPerSnapshot);
                    _bands[band] = (float)Math.Max(smoothed, FloorDb);
                }

                return (float[])_bands.Clone();
            }
        }

        /// <summary>每条带子要积的 1/3 倍频程三角窗；带数只影响带的位置，不影响正弦读数。</summary>
        private static void BuildBandWindows(
            int sampleRate,
            int fftSize,
            int bandCount,
            out int[] starts,
            out double[][] weightsByBand)
        {
            var binHz = (double)sampleRate / fftSize;
            var maxBin = fftSize / 2;
            var halfWidthRatio = Math.Pow(2, SmoothingOctaves / 2) - 1;

            starts = new int[bandCount];
            weightsByBand = new double[bandCount][];

            for (var band = 0; band < bandCount; band++)
            {
                var center = BandFrequency(band + 0.5, bandCount);
                var halfWidth = Math.Max(center * halfWidthRatio, binHz);

                var from = (int)Math.Floor((center - halfWidth) / binHz);
                var to = (int)Math.Ceiling((center + halfWidth) / binHz);
                from = Math.Clamp(from, 1, maxBin - 1);
                to = Math.Clamp(to, from + 1, maxBin);

                var weights = new double[to - from];
                for (var bin = from; bin < to; bin++)
                {
                    var distance = Math.Abs((bin * binHz) - center);
                    weights[bin - from] = Math.Max(0, 1 - (distance / halfWidth));
                }

                var peak = 0.0;
                foreach (var weight in weights)
                {
                    peak = Math.Max(peak, weight);
                }

                if (peak > 0)
                {
                    for (var i = 0; i < weights.Length; i++)
                    {
                        weights[i] /= peak;
                    }
                }
                else
                {
                    weights[0] = 1;
                }

                starts[band] = from;
                weightsByBand[band] = weights;
            }
        }

        private static double[] BuildHannWindow(int size)
        {
            var window = new double[size];
            for (var i = 0; i < size; i++)
            {
                window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / size);
            }

            return window;
        }

        private static void Fft(Complex[] buffer)
        {
            var n = buffer.Length;
            if (n < 2 || (n & (n - 1)) != 0)
            {
                throw new ArgumentException("FFT 长度必须是 2 的幂。", nameof(buffer));
            }

            for (int i = 1, j = 0; i < n; i++)
            {
                var bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                {
                    j ^= bit;
                }

                j ^= bit;

                if (i < j)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            for (var length = 2; length <= n; length <<= 1)
            {
                var angle = -2 * Math.PI / length;
                var step = new Complex(Math.Cos(angle), Math.Sin(angle));
                var half = length >> 1;

                for (var start = 0; start < n; start += length)
                {
                    var w = Complex.One;
                    for (var k = 0; k < half; k++)
                    {
                        var even = buffer[start + k];
                        var odd = buffer[start + k + half] * w;
                        buffer[start + k] = even + odd;
                        buffer[start + k + half] = even - odd;
                        w *= step;
                    }
                }
            }
        }

        private static double BandFrequency(double index, int bandCount)
        {
            var t = Math.Clamp(index, 0.0, (double)bandCount) / bandCount;
            return MinFrequencyHz * Math.Exp(t * Math.Log(MaxFrequencyHz / MinFrequencyHz));
        }
    }
}
