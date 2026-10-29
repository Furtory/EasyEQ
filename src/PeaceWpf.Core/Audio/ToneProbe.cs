namespace PeaceWpf.Core.Audio;

/// <summary>
/// 单频探针：Goertzel。
///
/// 旧实现拿窗口内的宽带 RMS 当电平。那是「这一窗里有多少能量」，不是「这个频点有多响」：
/// 底噪、说话、音乐、隔壁的电钻，只要落在窗里就会被算进去。而且噪声是加性的，
/// 6 dB 信噪比时读数会偏高约 1 dB，12 dB 时也有 0.3 dB——低频、远场、薄弱的点被系统性垫高。
///
/// Goertzel 只回答一个问题：这个窗口里，f 这一根谱线的幅度是多少。
/// 别的频率一概不算，所以宽带噪声和别的声源不会被当成这个频点的响度。
///
/// 取窗有两个细节：
/// - 窗长够 3 个周期以上时，截到整数个周期再用矩形窗。整周期里 sin 与自己的正交项严格抵消，
///   20 Hz 只给 160 ms（3.2 个周期）也能解准，不需要为了正负半周对齐再拖长时间。
/// - 不够 3 个周期时才退回 Hann 窗——短窗的泄漏比整周期误差更糟。
///
/// 返回值的单位是「均方」，和原来 <see cref="SweepAnalyzer.WindowDb"/> 的 dBFS 是同一把尺子：
/// 幅度 0.5 的正弦两种算法都给 -9.03 dB，新旧结果可以直接对着看。
/// </summary>
public static class ToneProbe
{
    /// <summary>整数周期至少要凑够几个才值得截。少于这个数就走 Hann 窗。</summary>
    public const double MinimumWholeCycles = 3;

    /// <summary>
    /// 这个窗口里 f 这一根谱线的均方功率。
    /// </summary>
    /// <param name="samples">录音。</param>
    /// <param name="start">窗口起点（样本）。</param>
    /// <param name="length">窗口长度（样本）。</param>
    /// <param name="frequencyHz">要探的频率。</param>
    /// <param name="sampleRate">采样率。</param>
    public static double NarrowbandPower(
        ReadOnlySpan<float> samples,
        int start,
        int length,
        double frequencyHz,
        int sampleRate)
    {
        if (length < 32 || frequencyHz <= 0 || sampleRate <= 0)
        {
            return 0;
        }

        start = Math.Max(0, start);
        if (start + length > samples.Length)
        {
            length = samples.Length - start;
        }

        if (length < 32)
        {
            return 0;
        }

        // 整周期截断：窗长里能放下几个完整周期就截几个。
        var cycles = length * frequencyHz / sampleRate;
        var wholeCycles = cycles >= MinimumWholeCycles;
        var count = length;
        if (wholeCycles)
        {
            var snapped = (int)Math.Round(Math.Floor(cycles) * sampleRate / frequencyHz);
            if (snapped >= 32 && snapped <= length)
            {
                count = snapped;
            }
            else
            {
                wholeCycles = false;
            }
        }

        var omega = 2 * Math.PI * frequencyHz / sampleRate;
        var coefficient = 2 * Math.Cos(omega);
        var weighted = 0.0;
        double s1 = 0;
        double s2 = 0;

        // Hann 用周期窗（分母 count 而不是 count-1）：窗口能量正好是 0.5，归一化不用特殊处理。
        var denominator = Math.Max(1, count);

        for (var n = 0; n < count; n++)
        {
            var weight = wholeCycles
                ? 1.0
                : 0.5 - (0.5 * Math.Cos(2 * Math.PI * n / denominator));

            weighted += weight;

            var s0 = (samples[start + n] * weight) + (coefficient * s1) - s2;
            s2 = s1;
            s1 = s0;
        }

        if (weighted <= 1e-12)
        {
            return 0;
        }

        var real = s1 - (s2 * Math.Cos(omega));
        var imaginary = s2 * Math.Sin(omega);
        var magnitude = Math.Sqrt((real * real) + (imaginary * imaginary));

        // |X| = A·Σw/2 → A = 2|X|/Σw；均方功率 = A²/2。
        var amplitude = 2 * magnitude / weighted;
        return amplitude * amplitude / 2;
    }

    /// <summary>
    /// 窗口内的宽带功率（方差，不归一化、不加窗）。纯度判据的分母就是它。
    ///
    /// 减掉均值再算：有些声卡/麦克风带直流偏置，直接用均方会把一个恒定的偏移算成能量，
    /// 纯度被无端压低，整条测量都会被判成「脏」。目标频点本身在偏移下不受影响——
    /// Goertzel 在那个频率上与直流正交——所以只在这里去掉就好。
    /// </summary>
    public static double WindowPower(ReadOnlySpan<float> samples, int start, int length)
    {
        start = Math.Max(0, start);
        if (length <= 0 || start >= samples.Length)
        {
            return 0;
        }

        length = Math.Min(length, samples.Length - start);

        var sum = 0.0;
        for (var i = 0; i < length; i++)
        {
            sum += samples[start + i];
        }

        var mean = sum / length;
        var variance = 0.0;
        for (var i = 0; i < length; i++)
        {
            var value = samples[start + i] - mean;
            variance += value * value;
        }

        return variance / length;
    }

    /// <summary>均方功率换成 dBFS（和 20·log10(RMS) 等价）。</summary>
    public static double PowerToDb(double power) =>
        power > 1e-20 ? 10 * Math.Log10(power) : SweepAnalyzer.SilenceDb;

    /// <summary>dBFS 换成均方功率。</summary>
    public static double DbToPower(double db) => Math.Pow(10, db / 10);
}
