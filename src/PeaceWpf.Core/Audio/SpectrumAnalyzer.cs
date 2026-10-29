using System.Numerics;

namespace PeaceWpf.Core.Audio;

/// <summary>
/// 把一段单声道浮点采样切成对数分带的频谱。
///
/// 这里只做数学：环缓冲、Hann 窗、FFT、转 dB、1/3 倍频程带通、时间平滑。
/// 抓音频的活在 <see cref="LoopbackCaptureService"/>，两者分开是为了让自测不用声卡也能跑。
///
/// 标定：满幅正弦（振幅 1.0）在它自己的带中心约 0 dB，半幅约 -4.5 dB。
/// 低频精确到 0 dB；1 kHz 附近会高出约 1.5 dB，因为 1/3 倍频程窗把主瓣的邻格也一并积进去了。
/// Hann 窗的相干增益是 0.5，单边谱峰幅度是 A·N/2，所以除以 N/4 正好还原成振幅 A。
///
/// 每条带子不是“取带内最高的那一格”，而是把 1/3 倍频程的三角窗套在谱上做积分：
/// 带子变密时，同一条带覆盖的 bin 范围变窄，但窗的形状不变，所以正弦的读数不随带数漂。
/// 早先“带内取峰 + 按带数平均”的做法在 192 条带时会把正弦压低约 3 dB，
/// 就是因为平均窗口随带数变宽、而能量只集中在一条带上。
/// </summary>
public sealed class SpectrumAnalyzer
{
    /// <summary>
    /// FFT 点数。8192 在 48 kHz 上是 5.9 Hz 一格，低频（20–150 Hz）才分得开。
    /// 2048 时那一格是 23.4 Hz，带子再多也只能在 150 Hz 以下挤进 6 个格，看着就是一条平的。
    /// 代价是时间窗从 43 ms 变成 171 ms：低频跟得慢一点，频谱只是背景，值。
    /// </summary>
    public const int FftSize = 8192;

    /// <summary>分多少条带子。192 条铺满 1180 宽的窗口，一条六像素左右。</summary>
    public const int BandCount = 192;

    public const double MinFrequencyHz = 20;
    public const double MaxFrequencyHz = 20000;

    /// <summary>没有声音时的底噪，也是画图的下边界。</summary>
    public const double FloorDb = -90;

    /// <summary>每次取快照最多回落多少 dB。上升是即时的，回落要慢，看起来才像在“衰减”。</summary>
    public const double ReleaseDbPerSnapshot = 3;

    /// <summary>
    /// 带通的宽度，单位是倍频程。1/3 倍频程是频谱显示的通行做法：
    /// 宽度固定，所以低频平滑得窄、高频平滑得宽，和耳朵的分辨率同构。
    /// 不做这一步的话，带子各打各的，画出来是一条锯齿。
    /// </summary>
    public const double SmoothingOctaves = 1.0 / 3.0;

    private readonly int _sampleRate;
    private readonly double[] _window;
    private readonly double[] _ring;
    private readonly double[] _windowed;
    private readonly Complex[] _fft;
    private readonly float[] _bands;

    /// <summary>每条带子从哪一格开始，以及套在谱上的三角窗权重（峰值归一到 1）。</summary>
    private readonly int[] _windowStart;
    private readonly double[][] _windowWeights;

    private readonly object _feedGate = new();
    private readonly object _computeGate = new();

    private int _write;
    private bool _filled;

    public SpectrumAnalyzer(int sampleRate = 48000)
    {
        if (sampleRate < 8000)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "采样率至少要 8 kHz。");
        }

        _sampleRate = sampleRate;
        _window = BuildHannWindow(FftSize);
        _ring = new double[FftSize];
        _windowed = new double[FftSize];
        _fft = new Complex[FftSize];
        _bands = new float[BandCount];
        BuildBandWindows(sampleRate, FftSize, BandCount, out _windowStart, out _windowWeights);
        Array.Fill(_bands, (float)FloorDb);
    }

    public int SampleRate => _sampleRate;

    /// <summary>抓音频的线程往这里灌采样。多线程调用是安全的。</summary>
    public void Feed(ReadOnlySpan<float> mono)
    {
        lock (_feedGate)
        {
            foreach (var sample in mono)
            {
                _ring[_write] = sample;
                _write++;

                if (_write == FftSize)
                {
                    _write = 0;
                    _filled = true;
                }
            }
        }
    }

    /// <summary>
    /// 取一帧 dB 值（长度 <see cref="BandCount"/>）。
    /// 频率平滑已经在带通里做掉了，这里只剩时间上的快升慢落。
    /// 还没攒够 <see cref="FftSize"/> 个采样时返回全底噪。
    /// </summary>
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

                for (var i = 0; i < FftSize; i++)
                {
                    _windowed[i] = _ring[(_write + i) % FftSize] * _window[i];
                }
            }

            for (var i = 0; i < FftSize; i++)
            {
                _fft[i] = new Complex(_windowed[i], 0);
            }

            Fft(_fft);

            const double scale = FftSize / 4.0;

            for (var band = 0; band < BandCount; band++)
            {
                var start = _windowStart[band];
                var weights = _windowWeights[band];
                var energy = 0.0;

                for (var i = 0; i < weights.Length; i++)
                {
                    var magnitude = _fft[start + i].Magnitude / scale;
                    energy += magnitude * magnitude * weights[i];
                }

                // 能量域转 dB：幅度平方再开根，等价于 20·log10(幅度)。
                var db = energy > 1e-18 ? Math.Max(FloorDb, 10 * Math.Log10(energy)) : FloorDb;
                var previous = _bands[band];
                var smoothed = db >= previous ? db : Math.Max(db, previous - ReleaseDbPerSnapshot);
                _bands[band] = (float)Math.Max(smoothed, FloorDb);
            }

            return (float[])_bands.Clone();
        }
    }

    /// <summary>换设备或重新开始抓的时候清掉旧数据。</summary>
    public void Reset()
    {
        lock (_computeGate)
        {
            lock (_feedGate)
            {
                Array.Clear(_ring);
                _write = 0;
                _filled = false;
            }

            Array.Fill(_bands, (float)FloorDb);
        }
    }

    /// <summary>
    /// 给每条带子算它要积的谱窗：以带中心为顶点、半宽为 1/6 倍频程的三角窗，
    /// 落在 bin 网格上之后按最大值归一到 1，这样满幅正弦在自己的带上是 0 dB。
    ///
    /// 低频的带比一格还窄，所以半宽至少取一格：宁可几条带子共用一格、看起来平一点，
    /// 也不让它们因为窗里没有 bin 而读到 -90 dB。
    /// 这一步只跟采样率、FFT 点数和带数有关，每帧不用重算。
    /// </summary>
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
            var center = BandFrequencyAt(band + 0.5);
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

            // 离散化后三角形的峰不一定正好落在某个 bin 上；按峰值归一，标定才稳。
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

    /// <summary>第 index 条带子的下边界频率。index 取 0…<see cref="BandCount"/>，对数是等分的。</summary>
    public static double BandFrequency(int index) => BandFrequencyAt(index);

    /// <summary>同上，但允许半点：带中心是 band + 0.5。</summary>
    private static double BandFrequencyAt(double index)
    {
        var t = Math.Clamp(index, 0.0, (double)BandCount) / BandCount;
        return MinFrequencyHz * Math.Exp(t * Math.Log(MaxFrequencyHz / MinFrequencyHz));
    }

    /// <summary>周期 Hann 窗（除以 N，不是 N-1）。相干增益 0.5。</summary>
    public static double[] BuildHannWindow(int size)
    {
        var window = new double[size];
        for (var i = 0; i < size; i++)
        {
            window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / size);
        }

        return window;
    }

    /// <summary>原地基 2 FFT。长度必须是 2 的幂；自测直接调它验数学。</summary>
    public static void Fft(Complex[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

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
}
