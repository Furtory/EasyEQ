namespace PeaceWpf.Core.Audio;

/// <summary>
/// 一个频点在时间表里的样子：它在播放轴上从哪开始、响多久、测量窗取哪一段。
///
/// 旧实现里每个频点都是 260 ms 响 + 120 ms 静音，块长固定 380 ms，分析器拿
/// 「原点 + i × 380 ms」就能算出每一段。低频加长之后块长不再相等，所以把时间表
/// 提成独立的数据结构：播放和录音分析都读同一份，谁都不许自己乘。
/// </summary>
/// <param name="FrequencyHz">这个频点的频率。</param>
/// <param name="ToneMs">响多久。</param>
/// <param name="GapMs">响完之后静音多久。</param>
/// <param name="StartMs">从第一个频点起算，这个块在哪开始。</param>
/// <param name="MeasureStartMs">从块起点算，测量窗从哪开始（跳过淡入和系统延迟）。</param>
/// <param name="MeasureEndMs">从块起点算，测量窗到哪结束。</param>
public readonly record struct SweepBlock(
    double FrequencyHz,
    int ToneMs,
    int GapMs,
    double StartMs,
    double MeasureStartMs,
    double MeasureEndMs)
{
    /// <summary>块起点折算成样本位置（相对第一个频点）。</summary>
    public int StartSample(int sampleRate) => (int)Math.Round(StartMs * sampleRate / 1000.0);

    /// <summary>响声长度折算成样本数。</summary>
    public int ToneSamples(int sampleRate) => Math.Max(1, (int)Math.Round(ToneMs * (double)sampleRate / 1000.0));

    /// <summary>测量窗起点折算成样本位置（相对块起点）。</summary>
    public int MeasureStartSample(int sampleRate) => (int)Math.Round(MeasureStartMs * sampleRate / 1000.0);

    /// <summary>测量窗终点折算成样本位置（相对块起点）。</summary>
    public int MeasureEndSample(int sampleRate) => (int)Math.Round(MeasureEndMs * sampleRate / 1000.0);
}

/// <summary>
/// 每个频点响多久、隔多久。
///
/// 两套现成的：
/// - <see cref="Legacy"/>：全部 260 / 120，和旧实现逐字节一致，用来做对比测试；
/// - <see cref="Coherent"/>：40 Hz 以下按「至少 10 个完整周期」加长，别的照旧。
///
/// 低频加长的理由不是「多听一会更准」——单频正弦的稳态幅度 260 ms 就够解出来。
/// 是房间模态：小房间 30 Hz 的 T60 常常 0.5–1.5 秒，260 ms 的响声只把模态激励到一部分，
/// 读数取决于在衰减的哪一段取窗，跨遍方差大。加长到 10 个周期以上，读到的才是
/// 「这个频点被推起来之后有多响」，而不是「刚好在这一刻有多响」。
/// </summary>
public sealed class SweepTiming
{
    /// <summary>低频加长的起点。这个频率以下才动时长。</summary>
    public const double LowFrequencyHz = 100;

    /// <summary>低频单点最多响多久，防止时间表失控。</summary>
    public const int MaxLowFrequencyToneMs = 600;

    private readonly Func<double, int> _tone;
    private readonly Func<double, int> _gap;

    private SweepTiming(string name, Func<double, int> tone, Func<double, int> gap)
    {
        Name = name;
        _tone = tone;
        _gap = gap;
    }

    /// <summary>给人看的名字，诊断和摘要里用。</summary>
    public string Name { get; }

    /// <summary>旧时间表：每个频点都是 260 ms 响、120 ms 静音。</summary>
    public static SweepTiming Legacy { get; } = new(
        "均匀 260/120",
        _ => SweepPlan.ToneMs,
        _ => SweepPlan.GapMs);

    /// <summary>新时间表：40 Hz 以下按整周期加长，其余不变。</summary>
    public static SweepTiming Coherent { get; } = new(
        "低频加长",
        CoherentToneMs,
        _ => SweepPlan.GapMs);

    /// <summary>低频加长到底开没开（环境变量 <c>PEACEWPF_SWEEP_LOWFREQ=0</c> 可以关掉）。</summary>
    public static bool LowFrequencyExtensionEnabled { get; } =
        !string.Equals(Environment.GetEnvironmentVariable("PEACEWPF_SWEEP_LOWFREQ"), "0", StringComparison.OrdinalIgnoreCase);

    /// <summary>按分析引擎挑时间表。旧引擎永远用旧时间表，这样它分析的就是它认识的东西。</summary>
    public static SweepTiming ForEngine(SweepEngine engine) =>
        engine == SweepEngine.Coherent && LowFrequencyExtensionEnabled ? Coherent : Legacy;

    /// <summary>这个频点响多久（毫秒）。</summary>
    public int ToneMsFor(double frequencyHz) => _tone(frequencyHz);

    /// <summary>这个频点之后静音多久（毫秒）。</summary>
    public int GapMsFor(double frequencyHz) => _gap(frequencyHz);

    /// <summary>
    /// 低频按「至少 10 个完整周期」取整到 20 ms；高频不动。
    ///
    /// 20 Hz → 500 ms（10 个周期），31.9 Hz → 320 ms，40 Hz 以上回到 260 ms。
    /// 上限 600 ms：再长就只是在等房间，不是在测喇叭。
    /// </summary>
    private static int CoherentToneMs(double frequencyHz)
    {
        if (frequencyHz >= LowFrequencyHz)
        {
            return SweepPlan.ToneMs;
        }

        var cyclesMs = 10.0 / Math.Max(1, frequencyHz) * 1000.0;
        var rounded = (int)Math.Round(cyclesMs / 20.0) * 20;
        return Math.Clamp(rounded, SweepPlan.ToneMs, MaxLowFrequencyToneMs);
    }
}

/// <summary>
/// 一整条扫频的时间表：同步音 + 每个频点的起点/时长/测量窗。
///
/// 起点都相对「第一个频点开始」，和 <see cref="SweepAnalyzer"/> 用同步音定出来的
/// 时间原点直接相加。播放端和录音端各按自己的采样率折算，误差不到一个样本。
/// </summary>
public sealed class SweepSchedule
{
    private SweepSchedule(IReadOnlyList<SweepBlock> blocks, SweepTiming timing)
    {
        Blocks = blocks;
        Timing = timing;
        TotalMs = TotalMsFor(blocks);

        var uniform = true;
        foreach (var block in blocks)
        {
            if (block.ToneMs != SweepPlan.ToneMs || block.GapMs != SweepPlan.GapMs)
            {
                uniform = false;
                break;
            }
        }

        IsUniform = uniform;
    }

    /// <summary>按频率表和时间表排一遍。默认用旧时间表，调用方要新行为得自己传。</summary>
    public static SweepSchedule Build(IReadOnlyList<double> frequencies, SweepTiming? timing = null)
    {
        timing ??= SweepTiming.Legacy;

        var blocks = new SweepBlock[frequencies.Count];
        var cursor = 0.0;
        for (var i = 0; i < frequencies.Count; i++)
        {
            var hz = frequencies[i];
            var tone = Math.Max(SweepPlan.MeasureSkipMs + 40, timing.ToneMsFor(hz));
            var gap = Math.Max(1, timing.GapMsFor(hz));

            // 测量窗：跳过前 80 ms，到响声结束前 20 ms 为止。旧的 80–240 就是这条公式在 260 ms 上的结果。
            var measureStart = (double)SweepPlan.MeasureSkipMs;
            var measureEnd = Math.Max(measureStart + 40, tone - 20.0);

            blocks[i] = new SweepBlock(hz, tone, gap, cursor, measureStart, measureEnd);
            cursor += tone + gap;
        }

        return new SweepSchedule(blocks, timing);
    }

    /// <summary>每个频点一块，按频率升序。</summary>
    public IReadOnlyList<SweepBlock> Blocks { get; }

    /// <summary>这次用的时间表。</summary>
    public SweepTiming Timing { get; }

    /// <summary>整条要播多久（毫秒）：同步音 + 间隔 + 所有块。</summary>
    public double TotalMs { get; private init; }

    /// <summary>
    /// 是不是每个块都和旧实现一样（260/120）。
    /// 只有均匀表才让旧分析引擎对得上——诊断里拿同一段录音做新旧对比要靠它。
    /// </summary>
    public bool IsUniform { get; }

    /// <summary>最后一个频点响完时的位置（毫秒，相对第一个频点）。</summary>
    public double LastToneEndMs => Blocks.Count == 0 ? 0 : Blocks[^1].StartMs + Blocks[^1].ToneMs;

    /// <summary>整条扫频（含同步音）的时长；给进度条和超时用。</summary>
    public static double TotalMsFor(IReadOnlyList<SweepBlock> blocks)
    {
        var sum = 0.0;
        foreach (var block in blocks)
        {
            sum += block.ToneMs + block.GapMs;
        }

        return SweepPlan.SyncMs + SweepPlan.SyncGapMs + sum;
    }
}
