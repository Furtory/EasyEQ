namespace PeaceWpf.Core.AutoEq;

/// <summary>
/// AutoEQ 结果库里一份等化的三种形态。顺序与 Peace 的 <c>$AutoEQTypes</c> 一致。
/// </summary>
public enum AutoEqType
{
    /// <summary>参数 EQ：任意频率的峰值 / 架式滤波器。</summary>
    Parametric = 0,

    /// <summary>固定频带 EQ：10 个固定频率、Q 1.41 的峰值滤波器。</summary>
    FixedBand = 1,

    /// <summary>图形 EQ：127 个频点的卷积曲线，Peace 用 GraphicEQ 命令写。</summary>
    Graphic = 2,
}

/// <summary>类型的中文名和短名。</summary>
public static class AutoEqTypes
{
    /// <summary>下拉框里的显示名，顺序同 <see cref="AutoEqType"/>。</summary>
    public static IReadOnlyList<string> DisplayNames { get; } = new[]
    {
        "参数 EQ（推荐）",
        "固定频带 EQ",
        "图形 EQ",
    };

    /// <summary>取显示名，越界时退回参数 EQ。</summary>
    public static string DisplayName(AutoEqType type) =>
        (int)type >= 0 && (int)type < DisplayNames.Count ? DisplayNames[(int)type] : DisplayNames[0];

    /// <summary>取短名，写状态栏用。</summary>
    public static string ShortName(AutoEqType type) => type switch
    {
        AutoEqType.Parametric => "参数",
        AutoEqType.FixedBand => "固定频带",
        AutoEqType.Graphic => "图形",
        _ => "参数",
    };
}
