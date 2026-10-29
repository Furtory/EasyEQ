namespace PeaceWpf.Core.Protocol;

/// <summary>
/// Peace.exe 的外部控制消息表。
///
/// 全部取自 Source code\Peace\Peace.au3 第 813–849 行的 GUIRegisterMsg 注册。
/// 后端是一个标题固定为 "Peace window messages" 的隐藏窗口，
/// 对它 SendMessage 即可遥控，返回值为 ActionDone 的整数结果。
///
/// 约定：wParam 是动作编号，lParam 是参数或返回值载体，具体语义见各处理器。
/// </summary>
public static class PeaceMessages
{
    /// <summary>WM_APP 基址。</summary>
    public const uint WmApp = 0x8000;

    /// <summary>WM_COPYDATA，用于按名字切换/保存预设和选择设备。</summary>
    public const uint WmCopyData = 0x004A;

    /// <summary>单实例探测：wParam=1 时返回 1 表示已有实例。</summary>
    public const uint Main = WmApp;

    /// <summary>InterfaceActions：wParam 0=显示 1=隐藏 2=切换 3=查状态 4=最小化 5=还原 6=切换最小化 8/9/10/11=长列表。</summary>
    public const uint Interface = WmApp + 1;

    /// <summary>ConfigurationSwitch：预设切换、保存、热键配置等。</summary>
    public const uint Configuration = WmApp + 2;

    /// <summary>EqualizerState：EQ 总开关与静音状态。</summary>
    public const uint EqualizerState = WmApp + 3;

    /// <summary>MainRenderDevice：系统音量、峰值、多声道峰值、静音。</summary>
    public const uint MainRenderDevice = WmApp + 4;

    /// <summary>MainPreAmplifying：第 0 声道（全部扬声器）的 preamp。</summary>
    public const uint MainPreAmplifying = WmApp + 5;

    /// <summary>EffectsPanel：低音、高音、上下混、交叉馈送、立体声扩展等。</summary>
    public const uint EffectsPanel = WmApp + 6;

    /// <summary>DeviceActions：wParam 0=选全部设备 1=启动 Configurator。</summary>
    public const uint Device = WmApp + 7;

    /// <summary>TargetSpeaker：wParam 0=切声道组合 1=查组合 2..21=各扬声器开关与状态。</summary>
    public const uint TargetSpeaker = WmApp + 20;

    /// <summary>PreAmplifying：当前声道组合的 preamp，wParam 0..5。</summary>
    public const uint PreAmplifying = WmApp + 21;

    /// <summary>EqualizationSliders：全部推子的批量操作，wParam 0..15。</summary>
    public const uint EqualizationSliders = WmApp + 22;

    /// <summary>
    /// 第 index 段推子（从 0 开始）。
    ///
    /// 这里有坑，必须照抄：注册循环是 WM_APP+100+index（Peace.au3 第 828–830 行），
    /// 但 EqualizationSlider 一进来就执行 $Msg -= $WM_APP+101（第 24332 行）。
    /// 结果是 WM_APP+100 会被判成负索引直接返回 -3，
    /// 所以第 0 段实际要发 WM_APP+101，第 i 段是 WM_APP+101+i。
    /// </summary>
    public static uint Slider(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return WmApp + 101u + (uint)index;
    }
}
