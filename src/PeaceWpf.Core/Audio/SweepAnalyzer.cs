namespace PeaceWpf.Core.Audio;

/// <summary>
/// 用哪套算法把录音变成频响。
///
/// <see cref="BroadbandRms"/> 是旧实现，原样保留：靠能量起伏分段，每段取宽带 RMS。
/// <see cref="Coherent"/> 是新的：同步音定原点、每块局部对齐、只探目标频点、算纯度、
/// 分带噪声底。两套并存是为了拿同一段录音直接对比，不是过渡期代码。
/// </summary>
public enum SweepEngine
{
    /// <summary>旧的宽带 RMS。对得上任何录音，但会把窗内的噪声和别的声源一起算进去。</summary>
    BroadbandRms,

    /// <summary>单频相干检测 + 纯度判据 + 分带噪声底 + 局部对齐。</summary>
    Coherent,
}

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

    /// <summary>局部对齐时最多把窗口挪多少毫秒。</summary>
    public const int MaxAlignMs = 30;

    /// <summary>局部对齐的搜索步长（毫秒）。</summary>
    public const int AlignStepMs = 3;

    /// <summary>对齐时，比最大值差不到这个值就算「一样好」，取最靠近 0 的偏移。</summary>
    private const double AlignTieDb = 0.25;

    private static SweepEngine _engine = ReadEngine();

    /// <summary>
    /// 当前用哪套算法。默认新的相干引擎；设环境变量 <c>PEACEWPF_SWEEP_ENGINE=legacy</c>
    /// 可以退回旧的宽带 RMS——做对比测试时不用改代码。
    /// </summary>
    public static SweepEngine Engine
    {
        get => _engine;
        set => _engine = value;
    }

    /// <summary>给人看的引擎名字。</summary>
    public static string EngineLabel(SweepEngine engine) =>
        engine == SweepEngine.Coherent ? "单频相干" : "宽带 RMS";

    private static SweepEngine ReadEngine()
    {
        var value = Environment.GetEnvironmentVariable("PEACEWPF_SWEEP_ENGINE");
        return string.Equals(value, "legacy", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "rms", StringComparison.OrdinalIgnoreCase)
            ? SweepEngine.BroadbandRms
            : SweepEngine.Coherent;
    }

    /// <summary>
    /// 按 <see cref="Engine"/> 选一条路。
    ///
    /// 两条路都是完整实现，不是「新的接管旧的」：旧的分析器对任何录音都能跑，
    /// 新的需要一份对得上的时间表。所以调用方要把播放时用的那份 <paramref name="schedule"/> 传进来。
    /// </summary>
    public static SweepMeasurement? AnalyzeAuto(
        float[] samples,
        int sampleRate,
        out string? error,
        IReadOnlyList<double>? frequencies = null,
        SweepSchedule? schedule = null) =>
        Engine == SweepEngine.Coherent
            ? AnalyzeCoherent(samples, sampleRate, out error, frequencies, schedule)
            : Analyze(samples, sampleRate, out error, frequencies);

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

        // 和相干引擎一个口径：尾部不够长就少量几块，不整轮作废。
        var blockSamples = Math.Max(1, (int)((long)sampleRate * SweepPlan.BlockMs / 1000));
        var count = (int)Math.Clamp((samples.Length - originSample) / blockSamples, 0, frequencies.Count);
        if (count == 0)
        {
            error = $"扫频起点认在第 {originSample / (double)sampleRate:0.0} 秒，"
                + $"录音有 {samples.Length / (double)sampleRate:0.0} 秒，一个频点都放不下。";
            return null;
        }

        var points = new List<MeasurementPoint>(count);
        for (var i = 0; i < count; i++)
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

        var used = new double[count];
        for (var i = 0; i < count; i++)
        {
            used[i] = frequencies[i];
        }

        return new SweepMeasurement(points, noiseFloor, sampleRate, used);
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

        // 同步音是整段录音里唯一能持续 280 ms 的东西，但它不一定比噪声底高多少。
        // 实测这台设备上 1 kHz 的同步音只有 −31 dB，噪声底 −43，而门限正好是 −43+12=−31:
        // 差 1 dB 就整段认不出来，起点被拖到十几秒后某个响块上，于是报「录音比扫频短」。
        // 所以同步音这一段单独放宽 6 dB，并且只扫开头 3 秒——它本来就排在最前面，
        // 没有理由扫完全程去赌。
        var relaxed = threshold - 6;
        var searchFrames = (int)Math.Min(frameDb.Length, 3.0 * sampleRate / frameSamples);

        var index = 0;
        while (index < searchFrames)
        {
            if (frameDb[index] < relaxed)
            {
                index++;
                continue;
            }

            var start = index;
            while (index < searchFrames && frameDb[index] >= relaxed)
            {
                index++;
            }

            if (index - start >= syncFrames)
            {
                // 起点按「同步音开头 + 同步音长度 + 间隔」算，不按检测到的那段尾巴算：
                // 门限放宽后这段可能被前后噪声拖长，用尾巴会把整条时间轴整体往后推。
                return (start * frameSamples)
                    + (sampleRate * (SweepPlan.SyncMs + SweepPlan.SyncGapMs) / 1000);
            }
        }

        index = 0;
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

    /// <summary>
    /// 相干分析：每一块只问一个问题——我播的这个频点，在这里有多响。
    ///
    /// 和旧实现的四件事不一样：
    /// 1. 电平用 Goertzel 探目标频点，不用宽带 RMS。别的频率、宽带噪声不算进来，
    ///    噪声「垫高」读数的偏置也就没了。
    /// 2. 每块在 ±30 ms 内找能量最大的位置再取窗。两块声卡是两个时钟域，长扫频会漂。
    /// 3. 噪声底按频点算（静音段上同一根谱线的功率，再按 1/3 倍频程取中位），
    ///    低频隆隆声不会把整条门限抬死。
    /// 4. 算纯度：目标频点功率 / 窗口总功率。音乐、说话混进来时总功率涨、目标不涨，
    ///    这个数会掉下去——旧实现里这种窗和干净窗一样被照单全收。
    /// </summary>
    /// <param name="samples">录音，单声道。</param>
    /// <param name="sampleRate">录音采样率。</param>
    /// <param name="error">失败原因，成功为 null。</param>
    /// <param name="frequencies">这轮扫了哪些频点。</param>
    /// <param name="schedule">播放时用的时间表。必须和播放端同一份，否则块对不上。</param>
    public static SweepMeasurement? AnalyzeCoherent(
        float[] samples,
        int sampleRate,
        out string? error,
        IReadOnlyList<double>? frequencies = null,
        SweepSchedule? schedule = null)
    {
        error = null;
        frequencies ??= SweepPlan.Frequencies;
        schedule ??= SweepSchedule.Build(frequencies, SweepTiming.Coherent);

        ArgumentNullException.ThrowIfNull(samples);

        if (sampleRate < 8000)
        {
            error = "录音采样率太低，测不了。";
            return null;
        }

        if (schedule.Blocks.Count != frequencies.Count)
        {
            error = "时间表和频点表对不上，不能分析。";
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

        var originSample = FindOriginSample(frameDb, threshold, sampleRate, frameSamples);
        if (originSample < 0)
        {
            error = NoSignalHint(frameDb)
                ?? "没听到扫频的声音：检查输出设备、音量，以及麦克风有没有被静音。";
            return null;
        }

        // 录音尾部不够长不该让整轮白跑：能完整量几块就量几块。
        // 16 kHz 往上没录到、或者设备尾巴掉了几百毫秒，都是常态，
        // 少画一段高频远好过整轮作废（作废之后界面还停在半截状态）。
        var count = 0;
        while (count < schedule.Blocks.Count)
        {
            var block = schedule.Blocks[count];
            if (originSample + block.StartSample(sampleRate) + block.ToneSamples(sampleRate) > samples.Length)
            {
                break;
            }

            count++;
        }

        if (count == 0)
        {
            error = $"扫频起点认在第 {originSample / (double)sampleRate:0.0} 秒，"
                + $"录音有 {samples.Length / (double)sampleRate:0.0} 秒，一个频点都放不下。"
                + "起点认晚了通常是同步音太轻：把音量调大一点再测。";
            return null;
        }

        var used = new double[count];
        for (var i = 0; i < count; i++)
        {
            used[i] = frequencies[i];
        }

        var noiseRegion = FindNoiseRegion(frameDb, threshold, sampleRate, frameSamples, originSample);
        var noisePowers = BuildNoiseFloors(samples, sampleRate, used, noiseRegion, noiseFloor);

        var points = new List<MeasurementPoint>(count);
        for (var i = 0; i < count; i++)
        {
            var block = schedule.Blocks[i];
            var blockStart = originSample + block.StartSample(sampleRate);
            var measureStart = block.MeasureStartSample(sampleRate);
            var measureEnd = block.MeasureEndSample(sampleRate);

            var (offsetSamples, tonePower) = AlignBlock(
                samples, blockStart, measureStart, measureEnd, block.FrequencyHz, sampleRate);

            var windowStart = blockStart + measureStart + offsetSamples;
            var windowLength = Math.Max(sampleRate / 100, blockStart + measureEnd - windowStart);
            var windowPower = ToneProbe.WindowPower(samples, windowStart, windowLength);

            var purity = tonePower > 0 && windowPower > 0
                ? 10 * Math.Log10(tonePower / windowPower)
                : double.NegativeInfinity;

            var raw = ToneProbe.PowerToDb(tonePower);
            var noise = Math.Max(noisePowers[i], 1e-20);
            var snr = tonePower > 0 ? 10 * Math.Log10(tonePower / noise) : -60;

            points.Add(new MeasurementPoint(
                block.FrequencyHz,
                raw,
                Math.Max(-60, snr),
                raw)
            {
                PurityDb = purity,
                AlignOffsetMs = offsetSamples * 1000.0 / sampleRate,
            });
        }

        if (points.All(point => point.RawDb < noiseFloor + SweepPlan.GateThresholdDb))
        {
            error = NoSignalHint(frameDb)
                ?? "没听到扫频的声音：检查输出设备、音量，以及麦克风有没有被静音。";
            return null;
        }

        Normalize(points);

        var floors = new double[count];
        for (var i = 0; i < floors.Length; i++)
        {
            floors[i] = ToneProbe.PowerToDb(noisePowers[i]);
        }

        return new SweepMeasurement(points, noiseFloor, sampleRate, used)
        {
            NoiseFloorsDb = floors,
            EngineName = EngineLabel(SweepEngine.Coherent),
        };
    }

    /// <summary>
    /// 噪声参考取哪一段录音。
    ///
    /// 首选播放前的前导（<see cref="SweepMeasureService"/> 开播前先录 500 ms），那段里只有环境声。
    /// 没有前导（合成信号、或者录音被截过）就退到最长的一段安静帧——通常是同步音之后那 200 ms。
    /// 都找不到就返回空，调用方退回宽带噪声底。
    /// </summary>
    private static (int Start, int Length) FindNoiseRegion(
        double[] frameDb,
        double threshold,
        int sampleRate,
        int frameSamples,
        int originSample)
    {
        var syncSpanMs = SweepPlan.SyncMs + SweepPlan.SyncGapMs + 40;
        var preRollEnd = originSample - (int)((long)sampleRate * syncSpanMs / 1000);
        var minimum = sampleRate / 5;

        if (preRollEnd >= minimum)
        {
            var length = Math.Min(preRollEnd, sampleRate / 2);
            return (preRollEnd - length, length);
        }

        var bestStart = -1;
        var bestLength = 0;
        var runStart = -1;
        for (var frame = 0; frame <= frameDb.Length; frame++)
        {
            var quiet = frame < frameDb.Length && frameDb[frame] < threshold;
            if (quiet)
            {
                if (runStart < 0)
                {
                    runStart = frame;
                }

                continue;
            }

            if (runStart >= 0)
            {
                var length = frame - runStart;
                if (length > bestLength)
                {
                    bestLength = length;
                    bestStart = runStart;
                }

                runStart = -1;
            }
        }

        if (bestStart < 0 || bestLength * frameSamples < sampleRate / 10)
        {
            return (0, 0);
        }

        return (bestStart * frameSamples, Math.Min(bestLength * frameSamples, sampleRate / 2));
    }

    /// <summary>
    /// 每个频点自己的噪声底。
    ///
    /// 在静音段上对同一根谱线做同一个 Goertzel，得到的才是「这个频点平时有多少能量」。
    /// 单次 periodogram 的方差很大，所以再按 1/3 倍频程取中位——环境噪声在频率上是连续变化的，
    /// 这样既压住方差，又不会把 50 Hz 工频那种窄峰抹平到整个低频段。
    /// </summary>
    private static double[] BuildNoiseFloors(
        float[] samples,
        int sampleRate,
        IReadOnlyList<double> frequencies,
        (int Start, int Length) region,
        double broadbandFloorDb)
    {
        var powers = new double[frequencies.Count];

        if (region.Length < 32)
        {
            Array.Fill(powers, Math.Max(ToneProbe.DbToPower(broadbandFloorDb), 1e-20));
            return powers;
        }

        for (var i = 0; i < frequencies.Count; i++)
        {
            powers[i] = Math.Max(
                ToneProbe.NarrowbandPower(samples, region.Start, region.Length, frequencies[i], sampleRate),
                1e-20);
        }

        return MedianWithinThirdOctave(frequencies, powers);
    }

    /// <summary>在 ±1/3 倍频程的邻点里取中位（对数域取，gear 不改偏）。</summary>
    private static double[] MedianWithinThirdOctave(IReadOnlyList<double> frequencies, double[] powers)
    {
        var result = new double[powers.Length];
        var low = Math.Pow(2, -1.0 / 3);
        var high = Math.Pow(2, 1.0 / 3);
        var scratch = new List<double>(32);

        for (var i = 0; i < frequencies.Count; i++)
        {
            var center = frequencies[i];
            if (center <= 0)
            {
                result[i] = powers[i];
                continue;
            }

            scratch.Clear();
            for (var j = 0; j < frequencies.Count; j++)
            {
                var ratio = frequencies[j] / center;
                if (ratio >= low && ratio <= high)
                {
                    scratch.Add(Math.Log10(Math.Max(powers[j], 1e-20)));
                }
            }

            if (scratch.Count == 0)
            {
                result[i] = powers[i];
                continue;
            }

            scratch.Sort();
            result[i] = Math.Pow(10, scratch[scratch.Count / 2]);
        }

        return result;
    }

    /// <summary>
    /// 在某一块里找测量窗应该落在哪。
    ///
    /// 播放和录音各走各的时钟，几百毫秒的块没事，一分半的长扫频就会漂几十毫秒。
    /// 固定网格吃到漂移就会把窗口挪进静音，读数偏低。这里以「目标频点的能量最大」为准，
    /// 在 ±30 ms 内挪一挪；落点差不多时取最靠近 0 的那个，免得为了 0.1 dB 去追一个反射。
    /// </summary>
    private static (int Offset, double Power) AlignBlock(
        float[] samples,
        int blockStart,
        int measureStart,
        int measureEnd,
        double frequencyHz,
        int sampleRate)
    {
        var step = Math.Max(1, sampleRate * AlignStepMs / 1000);
        var limit = sampleRate * MaxAlignMs / 1000;
        var minimumLength = sampleRate / 100;

        var offsets = new List<int>(1 + (2 * (MaxAlignMs / AlignStepMs))) { 0 };
        for (var distance = step; distance <= limit; distance += step)
        {
            offsets.Add(distance);
            offsets.Add(-distance);
        }

        var powers = new double[offsets.Count];
        var best = double.NegativeInfinity;
        for (var k = 0; k < offsets.Count; k++)
        {
            var start = blockStart + measureStart + offsets[k];
            var length = blockStart + measureEnd - start;
            if (start < 0 || length < minimumLength || start + length > samples.Length)
            {
                powers[k] = double.NegativeInfinity;
                continue;
            }

            powers[k] = ToneProbe.NarrowbandPower(samples, start, length, frequencyHz, sampleRate);
            if (powers[k] > best)
            {
                best = powers[k];
            }
        }

        if (!double.IsFinite(best) || best <= 0)
        {
            return (0, 0);
        }

        var tieFloor = best * ToneProbe.DbToPower(-AlignTieDb);
        for (var k = 0; k < offsets.Count; k++)
        {
            if (powers[k] >= tieFloor)
            {
                return (offsets[k], powers[k]);
            }
        }

        return (0, powers[0] > 0 ? powers[0] : 0);
    }
}
