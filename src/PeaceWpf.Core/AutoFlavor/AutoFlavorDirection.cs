namespace PeaceWpf.Core.AutoFlavor;

using PeaceWpf.Core.Model;

/// <summary>
/// 自动风味想要「朝哪个方向改」。方向由用户选，程序不做内容识别——
/// 只负责算出实际该给九条风味多少增益、落在哪个频率、多宽的 Q。
/// </summary>
public enum AutoFlavorDirection
{
    /// <summary>FPS、战术射击：抬脚步和枪声，收低频与齿音。</summary>
    Competitive,

    /// <summary>流行通用：微笑曲线，低频有量、高频有空气，中频不动。</summary>
    PopGeneral,

    /// <summary>摇滚金属：吉他切分和鼓点抬起来，低频收住，镲片不刺。</summary>
    RockMetal,

    /// <summary>女声电子：女声明亮、低频有量、空气感足。</summary>
    FemaleElectronic,

    /// <summary>男声民谣：民谣低频更浑厚，适合男声曲风；人声靠 2–4 kHz 的清晰度顶出来。</summary>
    MaleFolk,

    /// <summary>人声增强：男女声一起顶出来，低频和刺耳的部分都让路。</summary>
    VoiceGeneral,

    /// <summary>爵士蓝调：中频温暖、齿音收着，低频松而不冲。</summary>
    JazzBlues,

    /// <summary>古典舒缓：基本不动，只收刺耳、留一点空间。</summary>
    ClassicalGentle,
}

/// <summary>
/// 「要不要额外强人声」这一档。曲风方向和强人声是两件事：
/// 女声电子 / 男声民谣按电子和民谣的目标走，名字只是建议，不因为名字里有男女就加补偿；
/// 只有「人声增强」这条会额外抬人声，而且男女两条主带一起抬。
/// </summary>
public enum AutoFlavorVoiceBias
{
    /// <summary>不额外强人声，严格按目标曲线。</summary>
    None,

    /// <summary>只强偏低一侧（男声条）。留给将来按名字细分的场景。</summary>
    Lower,

    /// <summary>只强偏高一侧（女声条）。留给将来按名字细分的场景。</summary>
    Higher,

    /// <summary>男女一起强：男声条和女声条同时抬。</summary>
    Both,
}

/// <summary>
/// 一个方向的完整定义：九条目标的相对形状，以及每条自己的增益上下限。
///
/// 顺序与 <see cref="ToneBands.All"/> 一致（爆炸 / 余震 / 男声 / 女声 / 脚步 / 枪声 / 齿音 / 空间 / 尖啸）。
/// 目标值是「相对 1 kHz 希望听到的样子」，实际增益由实测与它的差算出来。
/// TargetDb 由 <see cref="AutoFlavorProfileSource"/> 从公开配方算出来，不是手填的；
/// 上下限仍然是判断：竞技方向不许抬爆炸，人声方向不许留低频——这是性格，不是算法。
/// </summary>
public sealed record AutoFlavorDirectionInfo(
    AutoFlavorDirection Direction,
    string Label,
    string Hint,
    IReadOnlyList<double> TargetDb,
    IReadOnlyList<double> MinGainDb,
    IReadOnlyList<double> MaxGainDb,
    IReadOnlyList<string> TargetKeys,
    FlavorProfileSource Source)
{
    /// <summary>这个方向主要动哪几条（一行，名称跟随本方向的视角）。</summary>
    public string TargetsInline => string.Join("  ", ResolveNames());

    /// <summary>同上一份，一行一条。</summary>
    public string TargetsMultiLine => string.Join(Environment.NewLine, ResolveNames());

    /// <summary>这条目标从哪来：原样引用只写来源，派生值前面标一下。</summary>
    public string SourceText => Source.IsDerived ? $"派生 · {Source.Label}" : Source.Label;

    /// <summary>
    /// 这个方向要不要额外强人声。只认「人声增强」一条。
    ///
    /// 女声电子 / 男声民谣是曲风方向：一个是电子、一个是民谣，男女只是说这种曲风
    /// 通常配什么声音，曲线不该为了名字去抬 700 Hz 或 2 kHz。所以它们不参与人声补偿。
    /// </summary>
    public AutoFlavorVoiceBias VoiceBias => Direction switch
    {
        AutoFlavorDirection.VoiceGeneral => AutoFlavorVoiceBias.Both,
        _ => AutoFlavorVoiceBias.None,
    };

    /// <summary>
    /// 悬浮提示：一行列这个方向会动的乐器，再一行写出处。竞技定位列的是声音来源（爆炸、脚步…），
    /// 因为那时命名视角是游戏名。
    /// </summary>
    public string HoverText
    {
        get
        {
            var instrument = FlavorNaming.PrefersInstrumentNaming(Direction);
            var overrideKey = FlavorNaming.OverrideKeyFor(Direction);

            var parts = new List<string>(TargetKeys.Count);
            foreach (var target in TargetKeys)
            {
                if (ToneBands.Find(target) is not { } band)
                {
                    continue;
                }

                parts.Add(instrument
                    ? string.Join("、", FlavorNaming.InstrumentNamesOf(band, overrideKey))
                    : band.Label);
            }

            return string.Join("  ", parts) + Environment.NewLine + "来源：" + SourceText;
        }
    }

    private IReadOnlyList<string> ResolveNames()
    {
        var instrument = FlavorNaming.PrefersInstrumentNaming(Direction);
        var overrideKey = FlavorNaming.OverrideKeyFor(Direction);
        var names = new List<string>(TargetKeys.Count);
        foreach (var target in TargetKeys)
        {
            if (ToneBands.Find(target) is { } band)
            {
                names.Add(FlavorNaming.NameOf(band, instrument, overrideKey));
            }
        }

        return names;
    }
}

/// <summary>
/// 方向表。目标形状不再手写，来自 <see cref="AutoFlavorProfileSource"/> 里那张公开配方表：
/// 六条原样引用（SnipSound 的 Pop / Rock / Hip-Hop / Classical / Vocal / Podcast），
/// 三条派生（女声电子、男声民谣、爵士蓝调的合成链写在来源里）。
///
/// 数值的量级比早期的手调表小一档，这是照搬公开配方的直接结果，不是调松了：
/// 原配方是对已混音的音乐再调味，量级本来就在 ±2..±8 dB；强度滑杆负责放大。
/// </summary>
public static class AutoFlavorDirections
{
    /// <summary>与风味条数一致。</summary>
    public const int Count = FlavorSet.Count;

    /// <summary>下拉顺序。</summary>
    public static IReadOnlyList<AutoFlavorDirectionInfo> All { get; } = new[]
    {
        Create(
            AutoFlavorDirection.Competitive,
            "竞技定位",
            "低频闷响压下去，2–4 kHz 的定位线索顶出来；参考播客的清晰度配方。",
            min: new[] { -9.0, -8.0, -6.0, -6.0, -4.0, -4.0, -6.0, -4.0, -9.0 },
            max: new[] { 0.0, 0.0, 4.0, 4.0, 8.0, 6.0, 0.0, 2.0, 0.0 },
            targets: new[] { "boom", "step", "gun" }),
        Create(
            AutoFlavorDirection.PopGeneral,
            "流行通用",
            "微笑曲线：低频有量、高频有空气，中频基本不动；什么都听得下去，但不出彩。",
            min: new[] { -4.0, -4.0, -4.0, -4.0, -4.0, -4.0, -6.0, -4.0, -9.0 },
            max: new[] { 4.0, 3.0, 5.0, 5.0, 4.0, 4.0, 0.0, 4.0, 2.0 },
            targets: new[] { "boom", "gun", "air" }),
        Create(
            AutoFlavorDirection.RockMetal,
            "摇滚金属",
            "低频更重、镲片更亮，中频让位；吉他和鼓点冲起来。",
            min: new[] { -3.0, -4.0, -4.0, -4.0, -4.0, -5.0, -6.0, -6.0, -9.0 },
            max: new[] { 6.0, 5.0, 4.0, 4.0, 5.0, 5.0, 0.0, 2.0, 0.0 },
            targets: new[] { "boom", "gun", "air" }),
        Create(
            AutoFlavorDirection.FemaleElectronic,
            "女声电子",
            "女声的存在感与空气感一起抬，低频给量；由 Pop 和 Vocal 合成。",
            min: new[] { -4.0, -4.0, -5.0, -4.0, -4.0, -4.0, -6.0, -3.0, -9.0 },
            max: new[] { 7.0, 6.0, 3.0, 5.0, 3.0, 3.0, 0.0, 5.0, 2.0 },
            targets: new[] { "voiceF", "boom", "gun", "air" }),
        Create(
            AutoFlavorDirection.MaleFolk,
            "男声民谣",
            "温和打底，人声的清晰度落在 1–2 kHz；不抢高频。",
            min: new[] { -5.0, -5.0, -3.0, -5.0, -4.0, -5.0, -6.0, -4.0, -9.0 },
            max: new[] { 2.0, 2.0, 6.0, 2.0, 3.0, 2.0, 0.0, 3.0, 0.0 },
            targets: new[] { "voiceM", "step", "gun", "air" }),
        Create(
            AutoFlavorDirection.VoiceGeneral,
            "人声增强",
            "男女声一起顶出来，250 Hz 的浑浊让路；音乐基本不能听。",
            min: new[] { -6.0, -6.0, -3.0, -3.0, -4.0, -5.0, -5.0, -5.0, -9.0 },
            max: new[] { 0.0, 0.0, 6.0, 6.0, 2.0, 1.0, 0.0, 1.0, 0.0 },
            targets: new[] { "voiceM", "voiceF", "sibil" }),
        Create(
            AutoFlavorDirection.JazzBlues,
            "爵士蓝调",
            "低频暖、中频不浑、高频有空气；由古典掺一点 Hip-Hop。",
            min: new[] { -4.0, -4.0, -4.0, -4.0, -4.0, -5.0, -6.0, -4.0, -9.0 },
            max: new[] { 5.0, 4.0, 4.0, 4.0, 3.0, 3.0, 0.0, 3.0, 2.0 },
            targets: new[] { "boom", "voiceM", "air" }),
        Create(
            AutoFlavorDirection.ClassicalGentle,
            "古典舒缓",
            "几乎不动，只留一点低频和空气；保留动态。",
            min: new[] { -3.0, -3.0, -3.0, -3.0, -3.0, -4.0, -5.0, -3.0, -9.0 },
            max: new[] { 3.0, 3.0, 3.0, 3.0, 2.0, 2.0, 0.0, 5.0, 1.0 },
            targets: new[] { "boom", "air", "sibil" }),
    };

    /// <summary>按枚举取定义；越界退回第一个（界面上永远不该越界）。</summary>
    public static AutoFlavorDirectionInfo Get(AutoFlavorDirection direction)
    {
        foreach (var item in All)
        {
            if (item.Direction == direction)
            {
                return item;
            }
        }

        return All[0];
    }

    /// <summary>按顺序取定义。</summary>
    public static AutoFlavorDirectionInfo At(int index) =>
        All[Math.Clamp(index, 0, All.Count - 1)];

    /// <summary>
    /// 任意频点的目标值：以九条默认中心为锚点，在对数频率上线性插值。
    /// 超出两端就取端点值——不 extrapolate，免得高频被无限放大。
    /// </summary>
    public static double TargetAt(AutoFlavorDirection direction, double frequencyHz)
    {
        var info = Get(direction);
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0)
        {
            return 0;
        }

        var first = ToneBands.All[0].DefaultFrequencyHz;
        var last = ToneBands.All[^1].DefaultFrequencyHz;
        if (frequencyHz <= first)
        {
            return info.TargetDb[0];
        }

        if (frequencyHz >= last)
        {
            return info.TargetDb[^1];
        }

        for (var i = 1; i < ToneBands.All.Count; i++)
        {
            var high = ToneBands.All[i].DefaultFrequencyHz;
            if (frequencyHz > high)
            {
                continue;
            }

            var low = ToneBands.All[i - 1].DefaultFrequencyHz;
            var t = Math.Log(frequencyHz / low) / Math.Log(high / low);
            return info.TargetDb[i - 1] + ((info.TargetDb[i] - info.TargetDb[i - 1]) * t);
        }

        return info.TargetDb[^1];
    }

    private static AutoFlavorDirectionInfo Create(
        AutoFlavorDirection direction,
        string label,
        string hint,
        double[] min,
        double[] max,
        string[] targets)
    {
        var target = AutoFlavorProfileSource.TargetsFor(direction);
        if (target.Length != Count || min.Length != Count || max.Length != Count)
        {
            throw new ArgumentException("方向表的长度必须和风味条数一致。");
        }

        return new AutoFlavorDirectionInfo(
            direction,
            label,
            hint,
            target,
            min,
            max,
            targets,
            AutoFlavorProfileSource.For(direction));
    }
}
