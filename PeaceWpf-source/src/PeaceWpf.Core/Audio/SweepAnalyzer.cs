namespace PeaceWpf.Core.Audio;

/// <summary>
/// 把一段录音分析成「每个频点响了多少」。
///
/// 输入是扫频期间录下来的单声道采样，输出是一串相对电平。它不知道扫频是怎么播的，
/// 也不需要知道：分段全靠录音里的能量起伏，所以播放和录音的延迟、缓冲深度都不参与计算。
///
/// 这一步是纯数学，自测里可以直接喂一段合成的信号验证，不需要声卡。
/// </summary>
public static class SweepAnalyzer
{
    /// <summary>绝对电平的下限，对应数字静音。</summary>
    public const double SilenceDb = -140;

    /// <summary>
    /// 分析。
    /// </summary>
    /// <param name="samples">录音，单声道，-1 到 1。</param>
    /// <param name="sampleRate">采样率。</param>
    /// <param name="error">失败时的原因，成功为 null。</param>
    /// <param name="frequencies">这次扫了哪些频率，默认用 <see cref="SweepPlan.Frequencies"/>。</param>
    public static SweepMeasurement? Analyze(
        float[] samples,
        int sampleRate,
        out string? error,
        IReadOnlyList<double>? frequencies = null)
    {
        error = null;
        frequencies ??= SweepPlan.Frequencies;

        ArgumentNullException.ThrowIfNull(samples);

        if (sampleRate < 8000)
        {
            error = "录音采样率太低，测不了。";
            return null;
        }

        var frameSamples = Math.Max(1, sampleRate * SweepPlan.GateFrameMs / 1000);
        var frameCount = samples.Length / frameSamples;
        if (frameCount < 8)
        {
            error = "录音太短，一个频点都没录到。";
            return null;
        }

        var frameDb = new double[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            frameDb[frame] = FrameLevel(samples, frame * frameSamples, frameSamples);
        }

        var noiseFloor = Percentile(frameDb, 0.20);
        var threshold = noiseFloor + SweepPlan.GateThresholdDb;

        // 时间原点：优先认同步音，没有就退回第一个上升沿。
        // 不能靠「数出了几段」来对齐——20–56 Hz 这类音箱放不出来的频点会整段消失，
        // 后面所有频点就会整体前移，最后报「录到 52 段，应该是 61 段」。
        var originSample = FindOriginSample(frameDb, threshold, sampleRate, frameSamples);
        if (originSample < 0)
        {
            error = NoSignalHint(frameDb)
                ?? "没听到扫频的声音：检查输出设备、音量，以及麦克风有没有被静音。";
            return null;
        }

        var blockSamples = Math.Max(1, (int)((long)sampleRate * SweepPlan.BlockMs / 1000));
        var needed = originSample + ((long)blockSamples * frequencies.Count);
        if (needed > samples.Length)
        {
            error = $"录音比扫频短：需要约 {(needed - originSample) / (double)sampleRate:0.0} 秒的扫频，"
                + $"只录到 {(samples.Length - originSample) / (double)sampleRate:0.0} 秒。";
            return null;
        }

        var points = new List<MeasurementPoint>(frequencies.Count);
        for (var i = 0; i < frequencies.Count; i++)
        {
            var start = originSample + (i * blockSamples);
            var raw = WindowDb(samples,
                start + (sampleRate * SweepPlan.MeasureSkipMs / 1000),
                start + (sampleRate * SweepPlan.MeasureEndMs / 1000));
            points.Add(new MeasurementPoint(frequencies[i], raw, raw - noiseFloor, raw));
        }

        if (points.All(point => point.RawDb < noiseFloor + SweepPlan.GateThresholdDb))
        {
            error = NoSignalHint(frameDb)
                ?? "没听到扫频的声音：检查输出设备、音量，以及麦克风有没有被静音。";
            return null;
        }

        Normalize(points);

        return new SweepMeasurement(points, noiseFloor, sampleRate, frequencies);
    }

    /// <summary>
    /// 算第一个频点在录音里的样本位置。
    ///
    /// 有同步音就用它：同步音是 1 kHz、接近满幅、400 ms，比任何频点都长也响得多，
    /// 找到它结束的帧，再空出 <see cref="SweepPlan.SyncGapMs"/> 就是第一个频点。
    /// 没有同步音（旧素材、自测合成的信号）就退回第一个上升沿。
    /// 返回 -1 表示整段都没有超过阈值的声音。
    /// </summary>
    private static int FindOriginSample(double[] frameDb, double threshold, int sampleRate, int frameSamples)
    {
        var syncFrames = Math.Max(2, (int)((long)sampleRate * SweepPlan.SyncMs / 1000 / frameSamples * 0.7));
        var index = 0;
        while (index < frameDb.Length)
        {
            if (frameDb[index] < threshold)
            {
                index++;
                continue;
            }

            var start = index;
            while (index < frameDb.Length && frameDb[index] >= threshold)
            {
                index++;
            }

            // 同步音比单个频点长得多（400 ms 对 260 ms），用长度把它和扫频段分开。
            if (index - start >= syncFrames)
            {
                return (index * frameSamples) + (sampleRate * SweepPlan.SyncGapMs / 1000);
            }
        }

        for (var i = 0; i < frameDb.Length; i++)
        {
            if (frameDb[i] < threshold)
            {
                continue;
            }

            if (i == 0 || frameDb[i - 1] < threshold)
            {
                return i * frameSamples;
            }
        }

        return -1;
    }

    /// <summary>
    /// 分不出段的时候，先判断「麦克风到底有没有收到扫频」。
    ///
    /// 判据是整段录音的动态范围：扫频有响声有静音，电平会拉开几十 dB；
    /// 只有环境声的话，最响的 10% 和最静的 10% 差不多平。
    ///
    /// 不能用「按 380 ms 固定网格比响声窗和静音窗」——播放到录音之间隔着 WASAPI 缓冲和
    /// 声卡的延迟，网格一旦错位，静音窗就会落进下一段的响声里，于是永远得出「一样响」。
    /// 返回 null 表示「录音确实有起伏」，那就不是这个原因。
    /// </summary>
    private static string? NoSignalHint(double[] frameDb)
    {
        if (frameDb is null || frameDb.Length < 16)
        {
            return null;
        }

        var loud = Percentile(frameDb, 0.90);
        var quiet = Percentile(frameDb, 0.10);
        var range = loud - quiet;

        return range < 6
            ? $"麦克风没收到扫频：整段录音几乎是平的（最响的 10% 和最静的 10% 只差 {range:0.0} dB）。"
              + "这不是音量问题——检查播放设备是不是你正在听的那个，或者把麦克风对准喇叭。"
            : null;
    }

    /// <summary>一段采样的 RMS 电平（dBFS）；全零时返回 <see cref="SilenceDb"/>。诊断和测量共用。</summary>
    internal static double WindowDb(float[] samples, int from, int to)
    {
        from = Math.Max(0, from);
        to = Math.Min(samples.Length, to);
        if (to <= from)
        {
            return SilenceDb;
        }

        var sum = 0.0;
        for (var i = from; i < to; i++)
        {
            sum += samples[i] * (double)samples[i];
        }

        var rms = Math.Sqrt(sum / (to - from));
        return rms > 1e-9 ? 20 * Math.Log10(rms) : SilenceDb;
    }

    /// <summary>
    /// 归一化到 1 kHz：把 1 kHz 附近（±1/3 倍频程）那些点的中位数当 0 dB。
    ///
    /// 绝对电平没有意义——麦克风增益、系统音量、说话人灵敏度都混在里面。
    /// EQ 要的是「相对 1 kHz 哪里多哪里少」，所以只保留形状。
    /// </summary>
    private static void Normalize(List<MeasurementPoint> points)
    {
        var reference = ReferenceLevel(points);
        if (!double.IsFinite(reference))
        {
            return;
        }

        for (var i = 0; i < points.Count; i++)
        {
            points[i] = points[i] with { LevelDb = points[i].RawDb - reference };
        }
    }

    private static double ReferenceLevel(List<MeasurementPoint> points)
    {
        var near = points
            .Where(point => point.FrequencyHz >= 1000 / Math.Pow(2, 1.0 / 3) && point.FrequencyHz <= 1000 * Math.Pow(2, 1.0 / 3))
            .Where(point => double.IsFinite(point.RawDb))
            .Select(point => point.RawDb)
            .OrderBy(value => value)
            .ToList();

        if (near.Count > 0)
        {
            return near[near.Count / 2];
        }

        // 没有 1 kHz 附近的点（扫频被改了），退回最近的那一个。
        var fallback = points
            .Where(point => double.IsFinite(point.RawDb))
            .MinBy(point => Math.Abs(Math.Log(point.FrequencyHz / 1000)));

        return fallback?.RawDb ?? double.NaN;
    }

    private static double FrameLevel(float[] samples, int start, int count)
    {
        var end = Math.Min(start + count, samples.Length);
        if (end <= start)
        {
            return SilenceDb;
        }

        var sum = 0.0;
        for (var i = start; i < end; i++)
        {
            var value = samples[i];
            sum += value * value;
        }

        var rms = Math.Sqrt(sum / (end - start));
        return rms > 1e-9 ? 20 * Math.Log10(rms) : SilenceDb;
    }

    private static double Percentile(double[] values, double fraction)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var index = (int)Math.Clamp(Math.Round(fraction * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[index];
    }
}
