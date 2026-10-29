namespace PeaceWpf.Core.AutoFlavor;

using PeaceWpf.Core.Model;

/// <summary>
/// 强度滑杆的解算结果：一条滑杆同时决定味道浓度和抗干扰力度。
///
/// 为什么同向：强度低＝改动小，零星爆音带不偏；强度高＝改动大，才需要把不可信的数据扔掉。
/// 越用力越挑食，这两件事不矛盾。映射用平方曲线，低强度几乎不动、高强度才明显收紧。
/// </summary>
public readonly record struct AutoFlavorStrength(
    double GainScale,
    double RejectPercent,
    double QuietPercent,
    double TransientBaseMs)
{
    /// <summary>默认强度：60%。</summary>
    public const double Default = 0.6;

    /// <summary>把 0–1 的滑杆位置解算成四个数。</summary>
    public static AutoFlavorStrength From(double strength)
    {
        var s = double.IsFinite(strength) ? Math.Clamp(strength, 0, 1) : Default;
        return new AutoFlavorStrength(
            GainScale: s,
            RejectPercent: 0.02 + (0.18 * s * s),
            QuietPercent: 0.40 * s * s,
            TransientBaseMs: 120 + (200 * s));
    }

    /// <summary>
    /// 某条带真正剔多少：base × (1.6 − 0.8·w)，w 是「这条带有多需要改」（0–1）。
    /// 需要改的带少剔（多留数据、估得更稳），不需要改的带多剔（错了也没人听得出）。
    /// </summary>
    public double RejectFor(double importance)
    {
        var w = double.IsFinite(importance) ? Math.Clamp(importance, 0, 1) : 0;
        var percent = RejectPercent * (1.6 - (0.8 * w));
        return Math.Clamp(percent, 0, 0.30);
    }

    /// <summary>
    /// 这条带允许「多长的高出」还算爆音：低频放宽、高频收紧。
    /// 80 Hz 一个周期就 12.5 ms，加上余响本来就长；8192 点 FFT 的 171 ms 时间窗也把低频抹得更开。
    /// </summary>
    public double TransientLimitMs(double frequencyHz)
    {
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0)
        {
            return TransientBaseMs;
        }

        var factor = Math.Pow(1000.0 / frequencyHz, 0.4);
        return Math.Clamp(TransientBaseMs * factor, 80, 500);
    }
}

/// <summary>
/// 一次识别算完之后给界面的东西。不可变：界面上一次改一帧，方便比较。
/// </summary>
/// <param name="Flavors">九条的频率、增益、Q。</param>
/// <param name="UsedFrames">参与计算的帧数（已经过安静门、瞬态门、剔除）。</param>
/// <param name="TotalFrames">录到的总帧数。</param>
/// <param name="HasResult">数据够不够出一份建议。</param>
/// <param name="Reason">不够时为什么，直接写进状态栏。</param>
/// <param name="VoiceRatio">有效帧里人声帧占多少（0–1）；方向不是人声向时一直是 0。</param>
/// <param name="VoiceBoostDb">这次实际叠了多少人声补偿；0 表示没启用。</param>
public sealed record AutoFlavorSuggestion(
    FlavorSet Flavors,
    int UsedFrames,
    int TotalFrames,
    bool HasResult,
    string? Reason,
    double VoiceRatio = 0,
    double VoiceBoostDb = 0)
{
    /// <summary>没有建议时用的空结果。</summary>
    public static AutoFlavorSuggestion None(string reason, int totalFrames = 0) =>
        new(FlavorSet.CreateDefault(), 0, totalFrames, false, reason);
}
