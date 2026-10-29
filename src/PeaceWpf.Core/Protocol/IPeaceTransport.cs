namespace PeaceWpf.Core.Protocol;

/// <summary>
/// 把"怎么把消息送到后端"和"发什么消息"分开。
/// 真实现走 Win32，测试实现直接记账，这样桥接逻辑可以脱离后端单测。
/// </summary>
public interface IPeaceTransport
{
    /// <summary>是否已经拿到后端窗口句柄。</summary>
    bool IsConnected { get; }

    /// <summary>最近一次失败原因，方便界面上直接显示。</summary>
    string? LastError { get; }

    /// <summary>尝试连接后端（按窗口标题查找）。</summary>
    bool TryConnect();

    /// <summary>断开并清空句柄。</summary>
    void Disconnect();

    /// <summary>
    /// 同步发送一条消息并返回 LRESULT。
    /// 连接丢失或后端超时无响应时抛 <see cref="PeaceBridgeException"/>。
    /// </summary>
    int Send(uint message, int wParam, int lParam);
}
