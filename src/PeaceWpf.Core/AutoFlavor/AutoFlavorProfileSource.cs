namespace PeaceWpf.Core.AutoFlavor;

using PeaceWpf.Core.Model;

/// <summary>
/// 一条方向目标的「出处」：一个公开的 10 段 EQ 配方，加上它是原样引用还是派生。
///
/// 为什么要有这个类型：方向表以前是手写的「普遍怎么调」，没有出处，改不动也证不了。
/// 现在每一格数值都能追到下面这张 GridDb —— 它不是我们定的，是别人公开写的配方。
/// </summary>
/// <param name="Label">来源名，界面上直接显示（例如「SnipSound · Pop」）。</param>
/// <param name="ShortLabel">短名，塞进气泡标题用（例如「Pop」「派生 · Pop + Vocal」）。</param>
/// <param name="Url">能看到原文配方的地方。</param>
/// <param name="GridDb">按 <see cref="AutoFlavorProfileSource.GridHz"/> 排列的原始 dB 值；原配方没提的段补 0。</param>
/// <param name="DerivedChain">派生时怎么写出来的；原样引用时为 null。</param>
public sealed record FlavorProfileSource(
    string Label,
    string ShortLabel,
    string Url,
    IReadOnlyList<double> GridDb,
    string? DerivedChain)
{
    /// <summary>是不是派生值。界面上用它区分「有出处」和「推出来的」。</summary>
    public bool IsDerived => DerivedChain is not null;
}

/// <summary>
/// 方向目标的来源表与换算规则。
///
/// 选源的原则只有一条：**公开写了每一段多少 dB**。四个参考站点里，SnipSound 的
/// Equalizer 页面逐条列出了 Pop / Rock / Hip-Hop / Classical / Vocal / Podcast 的
/// 频段与增益，其余三站只给预设名（ToolChamp 仅公开 Podcast 一条，EditClips 与
/// SoundYak 的正文抓不到）。所以原样引用的只有这六条。
///
/// 没有采用 AlexWabita/equalizer-apo-profiles：它确实有 28 套曲风配方，但 README
/// 里只有文字描述、没有逐段数值，而且明确写着是给**室内低音炮系统**调的、用于耳机时
/// 「所有低频增益减 3–4 dB」。拿它当数值来源等于把低音炮的量搬到耳机上。
/// 它真正有用的是频率区间的含义（20–60 低音炮体感 / 60–250 厚度 / 250–2k 人声 /
/// 2–5k 清晰度 / 5–16k 空气），这套区间划分和下面各配方的走向一致，可以互相印证。
///
/// 换算：原配方的 10 段是非等距的，我们的九条风味落在它们中间，所以在对数频率上
/// 线性插值（和 <see cref="AutoFlavorDirections.TargetAt"/> 同一套规则），
/// 两端不外推 —— 17000 Hz 取 16000 Hz 的值，80 Hz 在 62 和 125 之间插。
/// </summary>
public static class AutoFlavorProfileSource
{
    /// <summary>原配方用的 10 段中心频率（SnipSound / ToolChamp 都是这套 ISO 倍频程点）。</summary>
    public static IReadOnlyList<double> GridHz { get; } = new[] { 31.0, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    /// <summary>SnipSound 的 Pop：31/62 +4、4k/8k +2，中频不动（原文称 smile curve）。</summary>
    private static FlavorProfileSource Pop => new(
        "SnipSound · Pop",
        "Pop",
        "https://snipsound.com/tools/audio-equalizer",
        new[] { 4.0, 4, 0, 0, 0, 0, 0, 2, 2, 0 },
        null);

    /// <summary>SnipSound 的 Rock：31/62 +5、4k +3、8k +4。</summary>
    private static FlavorProfileSource Rock => new(
        "SnipSound · Rock",
        "Rock",
        "https://snipsound.com/tools/audio-equalizer",
        new[] { 5.0, 5, 0, 0, 0, 0, 0, 3, 4, 0 },
        null);

    /// <summary>SnipSound 的 Hip-Hop：31 +7、62 +5、500 −2、8k +3。</summary>
    private static FlavorProfileSource HipHop => new(
        "SnipSound · Hip-Hop",
        "Hip-Hop",
        "https://snipsound.com/tools/audio-equalizer",
        new[] { 7.0, 5, 0, 0, -2, 0, 0, 0, 3, 0 },
        null);

    /// <summary>SnipSound 的 Classical：62 +2、250/500 +1、8k +2。</summary>
    private static FlavorProfileSource Classical => new(
        "SnipSound · Classical",
        "Classical",
        "https://snipsound.com/tools/audio-equalizer",
        new[] { 0.0, 2, 0, 1, 1, 0, 0, 0, 2, 0 },
        null);

    /// <summary>SnipSound 的 Vocal Boost：250 −3、2k +3、4k +4。</summary>
    private static FlavorProfileSource Vocal => new(
        "SnipSound · Vocal Boost",
        "Vocal",
        "https://snipsound.com/tools/audio-equalizer",
        new[] { 0.0, 0, 0, -3, 0, 0, 3, 4, 0, 0 },
        null);

    /// <summary>
    /// 由 Podcast 配方派生：原配方是 31 −6、62 −3、250 −2、4k +3，抬的是语音清晰度。
    /// 游戏里定位线索（脚步）的中心在 2.5 kHz，原配方在那里没有抬升，所以把 4 kHz 的
    /// +3 复制一份到 2 kHz —— 让抬升区盖住 2–4 kHz。这是唯一一处对原配方的改动。
    /// </summary>
    private static FlavorProfileSource PodcastForPositioning => new(
        "SnipSound · Podcast（抬升区平移到 2–4 kHz）",
        "Podcast",
        "https://snipsound.com/tools/audio-equalizer",
        new[] { -6.0, -3, 0, -2, 0, 0, 3, 3, 0, 0 },
        "Podcast 原配方 4k +3 → 同时给 2k +3，其余不动，让抬升区覆盖游戏脚步所在的 2–4 kHz。");

    /// <summary>派生：Pop 与 Vocal Boost 按 45/55 合成 —— 电子给低频与空气，Vocal 给女声存在感。</summary>
    private static FlavorProfileSource FemaleElectronicDerived =>
        Blend("SnipSound · Pop + Vocal Boost（派生）", "派生 · Pop + Vocal", Pop, Vocal, 0.55,
            "0.45×Pop + 0.55×Vocal：低频与空气取 Pop，女声的 2–4 kHz 存在感取 Vocal，250 Hz 由 Vocal 的 −3 带出一点削减。");

    /// <summary>派生：Classical 与 Vocal Boost 按 65/35 合成 —— 温和打底，清晰度落在 1–2 kHz。</summary>
    private static FlavorProfileSource MaleFolkDerived =>
        Blend("SnipSound · Classical + Vocal Boost（派生）", "派生 · Classical + Vocal", Classical, Vocal, 0.35,
            "0.65×Classical + 0.35×Vocal：保留古典的温和底子，借用 Vocal 的 2–4 kHz 让人声清楚；没有人在 700 Hz 抬升，所以男声靠的是清晰度不是厚度。");

    /// <summary>派生：Classical 掺一点 Hip-Hop 的中低频清理 —— 低频暖、中频不浑。</summary>
    private static FlavorProfileSource JazzBluesDerived =>
        Blend("SnipSound · Classical + Hip-Hop（派生）", "派生 · Classical + Hip-Hop", Classical, HipHop, 0.15,
            "0.85×Classical + 0.15×Hip-Hop：古典提供整体温和，Hip-Hop 的 500 Hz −2 带出一点中低频清理，低频因此有量但不糊。");

    /// <summary>按权重合成两条配方。</summary>
    private static FlavorProfileSource Blend(
        string label,
        string shortLabel,
        FlavorProfileSource keep,
        FlavorProfileSource take,
        double weight,
        string chain)
    {
        var values = new double[Math.Min(keep.GridDb.Count, take.GridDb.Count)];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (keep.GridDb[i] * (1 - weight)) + (take.GridDb[i] * weight);
        }

        return new FlavorProfileSource(label, shortLabel, keep.Url, values, chain);
    }

    /// <summary>某个方向的来源。</summary>
    public static FlavorProfileSource For(AutoFlavorDirection direction) => direction switch
    {
        AutoFlavorDirection.Competitive => PodcastForPositioning,
        AutoFlavorDirection.PopGeneral => Pop,
        AutoFlavorDirection.RockMetal => Rock,
        AutoFlavorDirection.FemaleElectronic => FemaleElectronicDerived,
        AutoFlavorDirection.MaleFolk => MaleFolkDerived,
        AutoFlavorDirection.VoiceGeneral => Vocal,
        AutoFlavorDirection.JazzBlues => JazzBluesDerived,
        _ => Classical,
    };

    /// <summary>在任意频点上取来源值：对数插值，两端不外推。</summary>
    public static double Sample(FlavorProfileSource source, double frequencyHz)
    {
        ArgumentNullException.ThrowIfNull(source);

        var grid = source.GridDb;
        var hz = GridHz;
        if (grid.Count == 0 || hz.Count == 0 || !double.IsFinite(frequencyHz) || frequencyHz <= 0)
        {
            return 0;
        }

        if (frequencyHz <= hz[0])
        {
            return grid[0];
        }

        if (frequencyHz >= hz[^1])
        {
            return grid[^1];
        }

        for (var i = 1; i < hz.Count && i < grid.Count; i++)
        {
            if (frequencyHz > hz[i])
            {
                continue;
            }

            var low = hz[i - 1];
            var high = hz[i];
            var t = Math.Log(frequencyHz / low) / Math.Log(high / low);
            return grid[i - 1] + ((grid[i] - grid[i - 1]) * t);
        }

        return grid[^1];
    }

    /// <summary>
    /// 某个方向在九条风味中心上的目标值 —— 就是方向表里的 TargetDb。
    /// 数值由来源算出来，不是另抄一份，所以两者永远对得上。
    ///
    /// 唯一的例外是齿音和尖啸：来源配方里的 8k/16k 提升本意是空气感，
    /// 但这两条风味的定义就是「刺耳」，增益上限本来就是 0。目标值若为正，
    /// 会被上限吃掉、白算一遍，所以在这里压回不抬。
    /// </summary>
    public static double[] TargetsFor(AutoFlavorDirection direction)
    {
        var source = For(direction);
        var targets = new double[ToneBands.All.Count];
        for (var i = 0; i < targets.Length; i++)
        {
            var band = ToneBands.All[i];
            var value = Sample(source, band.DefaultFrequencyHz);
            targets[i] = band.Key is "sibil" or "shriek" ? Math.Min(0, value) : value;
        }

        return targets;
    }
}
