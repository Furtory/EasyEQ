namespace PeaceWpf.Core.Protocol;

/// <summary>
/// 单段推子的动作编号，对应 Peace.au3 第 24338–24411 行的 Switch Number($wParam)。
/// 发往 PeaceMessages.Slider(index)，值放在 lParam。
/// </summary>
public enum SliderAction
{
    /// <summary>直接设置增益（dB）。</summary>
    SetGainDb = 0,

    /// <summary>以千分之一 dB 设置增益，精度更高。</summary>
    SetGainMilliDb = 1,

    /// <summary>相对当前值增减增益（dB）。</summary>
    SetGainDeltaDb = 2,

    /// <summary>以千分之一 dB 相对增减。</summary>
    SetGainDeltaMilliDb = 3,

    /// <summary>读增益（dB）。</summary>
    GetGainDb = 4,

    /// <summary>读增益（千分之一 dB）。</summary>
    GetGainMilliDb = 5,

    /// <summary>直接设置中心频率（Hz）。</summary>
    SetFrequencyHz = 6,

    /// <summary>相对增减频率（Hz）。</summary>
    SetFrequencyDeltaHz = 7,

    /// <summary>读中心频率（Hz）。</summary>
    GetFrequencyHz = 8,

    /// <summary>以千分之一设置 Q 值。</summary>
    SetQualityMilli = 9,

    /// <summary>以千分之一相对增减 Q 值。</summary>
    SetQualityDeltaMilli = 10,

    /// <summary>读 Q 值（千分之一）。</summary>
    GetQualityMilli = 11,

    /// <summary>启用该段（返回 1=已启用，2=本来就是启用状态）。</summary>
    Enable = 12,

    /// <summary>停用该段（返回 1=已停用，2=本来就是停用状态）。</summary>
    Disable = 13,

    /// <summary>切换启用状态（返回 1=启用，2=停用）。</summary>
    ToggleEnabled = 14,

    /// <summary>读启用状态（返回 1=启用，2=停用）。</summary>
    GetEnabled = 15,

    /// <summary>设置滤波器类型，lParam = 类型序号 + 1（1 基于）。</summary>
    SetFilterType = 16,

    /// <summary>读滤波器类型，返回类型序号 + 1。</summary>
    GetFilterType = 17,
}
