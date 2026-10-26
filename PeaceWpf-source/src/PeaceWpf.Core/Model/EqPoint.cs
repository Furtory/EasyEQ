namespace PeaceWpf.Core.Model;

/// <summary>
/// 曲线上的一个点：频率、增益、Q、滤波器类型、是不是端点，以及一个在编辑会话内稳定的 Id。
///
/// 点是不可变的。改一个点就产生一个新实例，整条点列表也是一份可以整体比较的快照，
/// 撤回/重做只需要存列表，不必推演“这步改了什么”。
/// </summary>
/// <param name="Id">稳定标识。同一次编辑会话内不变，拖动时靠它认人。</param>
/// <param name="FrequencyHz">中心频率，Hz。架式滤波器是拐点频率。</param>
/// <param name="GainDb">增益，dB。</param>
/// <param name="Quality">Q 值。</param>
/// <param name="IsEndpoint">是否为固定频率的端点（20 Hz / 20 kHz），端点不能删、不能左右拖。</param>
/// <param name="FilterType">
/// 滤波器类型，序号对应 <see cref="EqLimits.FilterTypeNames"/>。默认 PK；
/// AutoEQ 带进来的低架/高架会带着 4/5/8/9 这类值，保存时原样写回。
/// </param>
public sealed record EqPoint(
    int Id,
    double FrequencyHz,
    double GainDb,
    double Quality,
    bool IsEndpoint,
    int FilterType = EqLimits.PeakingFilter)
{
    /// <summary>端点不可删除；普通点可以。</summary>
    public bool IsDeletable => !IsEndpoint;

    /// <summary>三个数值都必须是有限数，读到 NaN 时界面不该跟着烂。</summary>
    public bool IsFinite =>
        double.IsFinite(FrequencyHz) && double.IsFinite(GainDb) && double.IsFinite(Quality);

    /// <summary>是不是峰值滤波器。图形化编辑只直接改得动这一类。</summary>
    public bool IsPeaking => FilterType == EqLimits.PeakingFilter;

    /// <summary>是不是架式滤波器（低架/高架家族）。</summary>
    public bool IsShelf => EqLimits.IsShelfFilter(FilterType);

    /// <summary>类型名，写配置和状态栏用。</summary>
    public string FilterName => EqLimits.FilterName(FilterType);
}
