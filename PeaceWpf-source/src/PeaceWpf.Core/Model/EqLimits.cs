namespace PeaceWpf.Core.Model;

/// <summary>
/// 均衡器本身的取值范围与滤波器类型表。程序不依赖 Peace 运行，
/// 这里只是把数值定死：类型名沿用 Peace 的 $FilterTypes 顺序，
/// 这样它写出来的 <c>.peace</c> 预设，装了 Peace 的机器也能直接打开。
/// </summary>
public static class EqLimits
{
    /// <summary>滑杆数量的下界。界面上一共给到这么多段，够用。</summary>
    public const int MinBands = 7;

    /// <summary>滑杆数量的上界（界面与 .peace 预设共用；声音本身没有这个限制）。</summary>
    public const int MaxBands = 31;

    /// <summary>Peace 界面能显示的滑杆数。超过只影响它的显示，不影响声音。</summary>
    public const int PeaceDisplayBands = 31;

    /// <summary>增益的合理范围（dB），用于校验读回来的值。</summary>
    public const double MinGainDb = -30;

    /// <summary>增益的合理上限（dB）。</summary>
    public const double MaxGainDb = 30;

    /// <summary>
    /// 预增益（整条曲线一起平移的那个量）的下界，dB。和右侧推子的行程一致。
    /// 它和单段增益不是一回事：单段最多 ±30，预增益只有 ±20。
    /// </summary>
    public const double MinPreAmpDb = -20;

    /// <summary>预增益的上界，dB。</summary>
    public const double MaxPreAmpDb = 20;

    /// <summary>频率的合理范围（Hz），保护读回来的脏数据。</summary>
    public const double MinFrequencyHz = 10;

    /// <summary>频率的合理上限（Hz）。</summary>
    public const double MaxFrequencyHz = 24000;

    /// <summary>曲线频率轴的左端。两个端点固定在这里与 <see cref="MaxEditableFrequencyHz"/>。</summary>
    public const double MinEditableFrequencyHz = 20;

    /// <summary>曲线频率轴的右端。</summary>
    public const double MaxEditableFrequencyHz = 20000;

    /// <summary>新建推子时的默认 Q。</summary>
    public const double DefaultQuality = 1.41;

    /// <summary>界面上允许的最小 Q。引擎下限是 0.01，这里留一点余量避免除零。</summary>
    public const double MinQuality = 0.10;

    /// <summary>界面上允许的最大 Q。引擎对最高值不设硬上限，这里收在 30。</summary>
    public const double MaxQuality = 30.0;

    /// <summary>点编辑默认使用的峰值滤波器，序号对应 <see cref="FilterTypeNames"/> 的下标。</summary>
    public const int PeakingFilter = 0;

    /// <summary>低架滤波器（不带 Q）。</summary>
    public const int LowShelfFilter = 4;

    /// <summary>高架滤波器（不带 Q）。</summary>
    public const int HighShelfFilter = 5;

    /// <summary>低架滤波器（拐点居中，Q 作坡度）。</summary>
    public const int LowShelfCenteredFilter = 8;

    /// <summary>高架滤波器（拐点居中，Q 作坡度）。</summary>
    public const int HighShelfCenteredFilter = 9;

    /// <summary>低架滤波器（带 Q）。</summary>
    public const int LowShelfQFilter = 16;

    /// <summary>高架滤波器（带 Q）。</summary>
    public const int HighShelfQFilter = 17;

    /// <summary>
    /// 滤波器类型名，顺序与 Peace 的 $FilterTypes 完全一致，0 就是 PK。
    /// 写配置时可以直接把这个名字拼进 EqualizerAPO 的 Filter 行。
    /// </summary>
    public static IReadOnlyList<string> FilterTypeNames { get; } = new[]
    {
        "PK",   // 0  峰值
        "LPQ",  // 1  低通
        "HPQ",  // 2  高通
        "BP",   // 3  带通
        "LS",   // 4  低架
        "HS",   // 5  高架
        "NO",   // 6  陷波
        "AP",   // 7  全通
        "LSC",  // 8  低架（增益按 Q）
        "HSC",  // 9  高架（增益按 Q）
        "BWLP", // 10 Butterworth 低通
        "BWHP", // 11 Butterworth 高通
        "LRLP", // 12 Linkwitz-Riley 低通
        "LRHP", // 13 Linkwitz-Riley 高通
        "LSCQ", // 14 低架
        "HSCQ", // 15 高架
        "LSQ",  // 16 低架
        "HSQ",  // 17 高架
    };

    /// <summary>序号转名字，越界时返回 "?"，不抛异常（界面上某个坏值不该炸掉整张曲线）。</summary>
    public static string FilterName(int index) =>
        index >= 0 && index < FilterTypeNames.Count ? FilterTypeNames[index] : "?";

    /// <summary>名字转序号，找不到返回 -1。</summary>
    public static int FilterIndex(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }

        for (var i = 0; i < FilterTypeNames.Count; i++)
        {
            if (string.Equals(FilterTypeNames[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>这个序号是不是架式滤波器（低架或高架）。</summary>
    public static bool IsShelfFilter(int index) =>
        index is LowShelfFilter or HighShelfFilter or LowShelfCenteredFilter or HighShelfCenteredFilter
            or LowShelfQFilter or HighShelfQFilter;

    /// <summary>这个序号是不是低架。</summary>
    public static bool IsLowShelf(int index) =>
        index is LowShelfFilter or LowShelfCenteredFilter or LowShelfQFilter;

    /// <summary>这个序号是不是高架。</summary>
    public static bool IsHighShelf(int index) =>
        index is HighShelfFilter or HighShelfCenteredFilter or HighShelfQFilter;

    /// <summary>
    /// 点模型能不能原样表达这个类型。不能的类型（低通、高通、带通、陷波、全通、巴特沃斯……）
    /// 读进来会被丢掉，保存时统一写成 PK。AutoEQ 只用峰值和架式，所以够用。
    /// </summary>
    public static bool IsSupportedFilter(int index) =>
        index == PeakingFilter || IsShelfFilter(index);

    /// <summary>把任意整数收进合法类型范围；越界当成 PK。</summary>
    public static int ClampFilter(int index) =>
        index >= 0 && index < FilterTypeNames.Count ? index : PeakingFilter;
}
