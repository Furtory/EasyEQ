namespace PeaceWpf.Core.AutoFlavor;

/// <summary>
/// 自动风味的界面状态。Off 是没进这个功能；Idle 是进来了但还没录。
/// 暂停不是结束：数据一直在，继续录制接着累加。
/// </summary>
public enum AutoFlavorPhase
{
    /// <summary>不在自动风味模式里。</summary>
    Off,

    /// <summary>已进入，还没开始录。</summary>
    Idle,

    /// <summary>倒数 5→1。</summary>
    Countdown,

    /// <summary>正在录。</summary>
    Recording,

    /// <summary>暂停，数据还在。</summary>
    Paused,
}
