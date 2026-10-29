namespace PeaceWpf.Core.Protocol;

/// <summary>
/// 设备、滤波器类型等后端定义的枚举值。
/// 数值取自 Peace.au3 的 $SpeakerTypes（第 383 行）与 $FilterTypes（第 384 行）。
/// </summary>
public static class PeaceConstants
{
    /// <summary>
    /// 推子数量的合法范围，取自 Peace.au3 第 381 行：
    /// $SlidersMinNumber = 7、$SlidersMaxNumber = 31。
    /// 用户在设置里可以在 7–31 之间自定义，默认 13。
    /// </summary>
    public const int MinBands = 7;

    /// <summary>推子数量上限，见 Peace.au3 第 381 行。协议层写段仍受它约束。</summary>
    public const int MaxBands = 31;

    /// <summary>Peace 能显示的滑杆数。超过只影响 Peace 界面显示不全，不影响声音。</summary>
    public const int PeaceDisplayBands = 31;

    /// <summary>增益的合理范围（dB），用于校验读回来的值。Peace 默认 dB Limit = 30。</summary>
    public const double MinGainDb = -30;

    /// <summary>增益的合理上限（dB）。</summary>
    public const double MaxGainDb = 30;

    /// <summary>频率的合理范围（Hz），保护读回来的脏数据。</summary>
    public const double MinFrequencyHz = 10;

    /// <summary>频率的合理上限（Hz）。</summary>
    public const double MaxFrequencyHz = 24000;

    /// <summary>曲线频率轴的左端。R1 规定两个端点固定在这里与 <see cref="MaxEditableFrequencyHz"/>。</summary>
    public const double MinEditableFrequencyHz = 20;

    /// <summary>曲线频率轴的右端。</summary>
    public const double MaxEditableFrequencyHz = 20000;

    /// <summary>Peace 新建推子时的默认 Q（$QualityDefault，Peace.au3 第 2517 行）。</summary>
    public const double DefaultQuality = 1.41;

    /// <summary>界面上允许的最小 Q。后端下限是 0.01，这里留一点余量避免除零。</summary>
    public const double MinQuality = 0.10;

    /// <summary>界面上允许的最大 Q。后端对最高值不设硬上限，这里收在 30。</summary>
    public const double MaxQuality = 30.0;

    /// <summary>
    /// 点编辑只使用 PK（峰值）滤波器。
    /// 序号直接对应 $FilterTypes 的下标，0 就是 PK。
    /// </summary>
    public const int PeakingFilter = 0;

    /// <summary>
    /// 滤波器类型名，顺序与 $FilterTypes 完全一致（Peace.au3 第 384 行）：
    /// PK、LPQ、HPQ、BP、LS、HS、NO、AP、LSC、HSC、BWLP、BWHP、LRLP、LRHP、LSCQ、HSCQ、LSQ、HSQ。
    ///
    /// 协议里 SetFilterType 的 lParam 是类型序号 + 1，GetFilterType 返回的也是序号 + 1，
    /// 所以这里 0 就是 PK，不能再像旧版本那样把 0 当成“无”。
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
}
