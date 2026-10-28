using System;

namespace PeaceWpf.Core.Audio;

/// <summary>
/// 连续扫频（对数 chirp）的传递函数分析。
///
/// 和单频点测不同，这里不挑「每个频点响多久」，而是把整段扫频当一次激励：
/// 录回来的信号除以已知的参考信号，就是通道的传递函数。逆变换里那个冲激出现在
/// 「延迟」的位置——而延迟只影响相位，不影响幅度，所以**不需要对齐**，
/// 声卡延迟多少都不会把曲线拉歪。这正是它比点测省事的地方。
///
/// 频率分辨率 = 采样率 / FFT 长度。5 秒 @48 kHz 约 0.19 Hz，
/// 房间模态的带宽一般 5–10 Hz，足够把它看清楚。
/// </summary>
public static class ChirpSweepAnalyzer
{
    /// <summary>分析用的采样率：扫频本身就是按这个频率生成的。</summary>
    private const int AnalysisRate = ChirpPreviewPlayer.SampleRate;

    /// <summary>归一化参考频段：700–1400 Hz 的平均功率当 0 dB。</summary>
    private const double NormalizeLowHz = 700;
    private const double NormalizeHighHz = 1400;

    /// <summary>
    /// 从一次录音里算出一条连续响应曲线。
    /// </summary>
    /// <param name="capture">麦克风录到的样本。</param>
    /// <param name="captureRate">录音采样率；不是 48 kHz 会先降采样。</param>
    /// <param name="reference">播放用的参考扫频（必须是同一段波形）。</param>
    /// <param name="perOctave">曲线上每倍频程取几个点，默认 48（约 0.7%）。</param>
    /// <returns>按频率升序的 (Hz, dB) 列表；录得太短或全是静音时返回 null。</returns>
    public static IReadOnlyList<(double Hz, double Db)>? Analyze(
        float[] capture,
        int captureRate,
        float[] reference,
        out double delayMs,
        int perOctave = 48)
    {
        delayMs = 0;

        if (capture is null || reference is null || capture.Length == 0 || reference.Length == 0)
        {
            return null;
        }

        var mono = Resample(capture, captureRate, AnalysisRate);
        if (mono.Length < AnalysisRate / 10)
        {
            return null;
        }

        // 参考信号也要落在同一采样率上；两者长度取整到 2 的幂，卷起来才没有循环混叠。
        var length = NextPowerOfTwo(Math.Max(mono.Length, reference.Length) * 2);

        var recIm = new double[length];
        var recRe = new double[length];
        for (var i = 0; i < mono.Length && i < length; i++)
        {
            recRe[i] = mono[i];
        }

        var refIm = new double[length];
        var refRe = new double[length];
        for (var i = 0; i < reference.Length && i < length; i++)
        {
            refRe[i] = reference[i];
        }

        Fft(recRe, recIm, false);
        Fft(refRe, refIm, false);

        // 顺带量延迟：H = R·conj(S)/|S|² 的逆变换是个冲激，冲激在哪就说明录到的比放出的晚了多久。
        // 实时曲线拿这个值去修正「现在听到的其实是几秒前的频率」，下次绘制就不会错位。
        var hRe = new double[length];
        var hIm = new double[length];
        for (var i = 0; i < length; i++)
        {
            var sr = refRe[i] * refRe[i] + refIm[i] * refIm[i];
            if (sr < 1e-12)
            {
                continue;
            }

            hRe[i] = ((recRe[i] * refRe[i]) + (recIm[i] * refIm[i])) / sr;
            hIm[i] = ((recIm[i] * refRe[i]) - (recRe[i] * refIm[i])) / sr;
        }

        Fft(hRe, hIm, true);

        var peakIndex = 0;
        var peakAbs = 0.0;
        for (var i = 0; i < length; i++)
        {
            var abs = (hRe[i] * hRe[i]) + (hIm[i] * hIm[i]);
            if (abs > peakAbs)
            {
                peakAbs = abs;
                peakIndex = i;
            }
        }

        // 后半段是负延迟（录音缓冲区比播放起点早），按环绕折回去。
        delayMs = peakIndex <= length / 2
            ? peakIndex * 1000.0 / AnalysisRate
            : (peakIndex - length) * 1000.0 / AnalysisRate;

        // 峰值频率点的功率谱密度：|R/S|。参考频谱在某频点太小时不除，直接丢掉那个点，
        // 免得除出天文数字把整条曲线拉爆。
        var power = new double[length / 2];
        var binHz = (double)AnalysisRate / length;
        var peak = 0.0;

        for (var i = 1; i < length / 2; i++)
        {
            var sr = refRe[i] * refRe[i] + refIm[i] * refIm[i];
            if (sr < 1e-12)
            {
                continue;
            }

            var rr = recRe[i] * recRe[i] + recIm[i] * recIm[i];
            power[i] = rr / sr;
            if (power[i] > peak)
            {
                peak = power[i];
            }
        }

        if (peak <= 0)
        {
            return null;
        }

        // 按 log 频率分箱：每倍频程取 perOctave 个点，箱内取功率平均（不是 dB 平均）。
        var result = new System.Collections.Generic.List<(double Hz, double Db)>();
        var octaves = Math.Log(20000.0 / 20.0, 2);
        var count = (int)Math.Round(octaves * perOctave);

        var sum = new double[count];
        var hits = new int[count];

        for (var i = 1; i < power.Length; i++)
        {
            var hz = i * binHz;
            if (hz < 20 || hz > 20000 || power[i] <= 0)
            {
                continue;
            }

            var index = (int)Math.Round(Math.Log(hz / 20.0, 2) * perOctave);
            if (index < 0 || index >= count)
            {
                continue;
            }

            sum[index] += power[i];
            hits[index]++;
        }

        var db = new double[count];
        for (var i = 0; i < count; i++)
        {
            db[i] = hits[i] == 0 ? double.NaN : 10 * Math.Log10(sum[i] / hits[i]);
        }

        // 归一化：700–1400 Hz 的平均值当 0 dB。和点测那条线的口径一致，两条才叠得起来。
        var normSum = 0.0;
        var normCount = 0;
        for (var i = 0; i < count; i++)
        {
            var hz = 20 * Math.Pow(2, i / (double)perOctave);
            if (hz >= NormalizeLowHz && hz <= NormalizeHighHz && !double.IsNaN(db[i]))
            {
                normSum += db[i];
                normCount++;
            }
        }

        if (normCount == 0)
        {
            return null;
        }

        var reference0 = normSum / normCount;
        for (var i = 0; i < count; i++)
        {
            if (double.IsNaN(db[i]))
            {
                continue;
            }

            var hz = 20 * Math.Pow(2, i / (double)perOctave);
            result.Add((hz, db[i] - reference0));
        }

        return result.Count < 8 ? null : result;
    }

    /// <summary>
    /// 把多遍的曲线合在一起：按功率平均。驻波在换位置后会移动，平均之后互相抵消；
    /// 设备本身的毛病不变，会被保留下来。这就是「排除驻波」的根据。
    /// </summary>
    public static IReadOnlyList<(double Hz, double Db)>? Merge(
        System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<(double Hz, double Db)>> curves)
    {
        if (curves is null || curves.Count == 0)
        {
            return null;
        }

        if (curves.Count == 1)
        {
            return curves[0];
        }

        var first = curves[0];
        var result = new System.Collections.Generic.List<(double Hz, double Db)>(first.Count);

        for (var i = 0; i < first.Count; i++)
        {
            var hz = first[i].Hz;
            var sum = 0.0;
            var hits = 0;

            foreach (var curve in curves)
            {
                if (i >= curve.Count)
                {
                    continue;
                }

                sum += Math.Pow(10, curve[i].Db / 10);
                hits++;
            }

            if (hits == 0)
            {
                continue;
            }

            result.Add((hz, 10 * Math.Log10(sum / hits)));
        }

        return result;
    }

    /// <summary>默认每倍频程点数。</summary>
    public const int DefaultPerOctave = 48;

    /// <summary>每倍频程点数上限：再多也画不到像素里去。</summary>
    public const int MaxPerOctave = 384;

    /// <summary>
    /// 扫频越长，频率分辨率越高（分辨率 ≈ 1/时长），曲线就该画得越密。
    /// 5 秒 → 48 点/倍频程，10 秒 → 96，20 秒 → 192，40 秒以上 → 384（封顶）。
    /// </summary>
    public static int PerOctaveFor(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0)
        {
            return DefaultPerOctave;
        }

        return (int)Math.Clamp(Math.Round(seconds * 9.6), DefaultPerOctave, MaxPerOctave);
    }

    /// <summary>
    /// 按「点」平滑一条曲线（只在画图时用）：窗口半径按点数算，
    /// 这样换了扫频时长（分辨率变了）之后，同一个档位的手感不变。
    ///
    /// 窗口内按**功率**平均：dB 直接平均会把深坑抬得过分。
    /// 平滑只影响显示——合并、判断驻波都用原始数据，免得把真实窄峰磨掉之后误判。
    /// </summary>
    /// <param name="halfWidthPoints">窗口半径（点）。0 或负数原样返回。</param>
    public static IReadOnlyList<(double Hz, double Db)> SmoothByPoints(
        IReadOnlyList<(double Hz, double Db)> curve,
        int halfWidthPoints)
    {
        if (curve is null)
        {
            return System.Array.Empty<(double Hz, double Db)>();
        }

        if (curve.Count < 3 || halfWidthPoints <= 0)
        {
            return curve;
        }

        var result = new System.Collections.Generic.List<(double Hz, double Db)>(curve.Count);
        for (var i = 0; i < curve.Count; i++)
        {
            var from = Math.Max(0, i - halfWidthPoints);
            var to = Math.Min(curve.Count - 1, i + halfWidthPoints);

            var sum = 0.0;
            for (var j = from; j <= to; j++)
            {
                sum += Math.Pow(10, curve[j].Db / 10);
            }

            result.Add((curve[i].Hz, 10 * Math.Log10(sum / (to - from + 1))));
        }

        return result;
    }

    /// <summary>整数倍降采样；不是整数倍就线性重采样。够用于幅度响应。</summary>
    private static float[] Resample(float[] input, int fromRate, int toRate)
    {
        if (fromRate <= 0 || toRate <= 0)
        {
            return input;
        }

        if (fromRate == toRate)
        {
            return input;
        }

        if (fromRate % toRate == 0)
        {
            // 整数倍：盒式平均，顺带压掉一部分高频混叠。
            var factor = fromRate / toRate;
            var output = new float[input.Length / factor];
            for (var i = 0; i < output.Length; i++)
            {
                var sum = 0.0;
                var start = i * factor;
                for (var j = 0; j < factor && start + j < input.Length; j++)
                {
                    sum += input[start + j];
                }

                output[i] = (float)(sum / factor);
            }

            return output;
        }

        var ratio = (double)fromRate / toRate;
        var length = (int)(input.Length / ratio);
        var resampled = new float[Math.Max(1, length)];
        for (var i = 0; i < resampled.Length; i++)
        {
            var position = i * ratio;
            var index = (int)position;
            var frac = position - index;
            var a = input[Math.Min(index, input.Length - 1)];
            var b = input[Math.Min(index + 1, input.Length - 1)];
            resampled[i] = (float)(a + (b - a) * frac);
        }

        return resampled;
    }

    private static int NextPowerOfTwo(int value)
    {
        var n = 1;
        while (n < value)
        {
            n <<= 1;
        }

        return n;
    }

    /// <summary>原地基 2 FFT。</summary>
    private static void Fft(double[] re, double[] im, bool inverse)
    {
        var n = re.Length;
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
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = 2 * Math.PI / len * (inverse ? 1 : -1);
            var wRe = Math.Cos(angle);
            var wIm = Math.Sin(angle);

            for (var i = 0; i < n; i += len)
            {
                double curRe = 1, curIm = 0;
                for (var j = 0; j < len / 2; j++)
                {
                    var uRe = re[i + j];
                    var uIm = im[i + j];
                    var vRe = re[i + j + len / 2] * curRe - im[i + j + len / 2] * curIm;
                    var vIm = re[i + j + len / 2] * curIm + im[i + j + len / 2] * curRe;

                    re[i + j] = uRe + vRe;
                    im[i + j] = uIm + vIm;
                    re[i + j + len / 2] = uRe - vRe;
                    im[i + j + len / 2] = uIm - vIm;

                    var nextRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = nextRe;
                }
            }
        }

        if (inverse)
        {
            for (var i = 0; i < n; i++)
            {
                re[i] /= n;
                im[i] /= n;
            }
        }
    }
}
