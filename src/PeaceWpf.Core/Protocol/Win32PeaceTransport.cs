using System.Runtime.InteropServices;

namespace PeaceWpf.Core.Protocol;

/// <summary>
/// 真正的 Win32 传输：按窗口标题找到 Peace 的隐藏消息窗口，用 SendMessageTimeout 发消息。
///
/// 用 SendMessageTimeout 而不是 SendMessage，是因为后端卡住时前者会超时返回，
/// 后者会把前端一起拖死。
/// </summary>
public sealed class Win32PeaceTransport : IPeaceTransport
{
    /// <summary>后端消息窗口的固定标题，见 Peace.au3 第 815 行。</summary>
    public const string WindowTitle = "Peace window messages";

    private const uint SmtoBlock = 0x0001;
    private const uint SmtoAbortIfHung = 0x0002;

    private IntPtr _hwnd = IntPtr.Zero;

    public bool IsConnected => _hwnd != IntPtr.Zero;

    public string? LastError { get; private set; }

    /// <summary>单条消息的等待上限，默认 2 秒。</summary>
    public uint TimeoutMs { get; init; } = 2000;

    public bool TryConnect()
    {
        _hwnd = NativeMethods.FindWindowW(null, WindowTitle);
        if (_hwnd == IntPtr.Zero)
        {
            LastError = "没有找到 Peace 的消息窗口，后端可能没有启动。";
            return false;
        }

        LastError = null;
        return true;
    }

    public void Disconnect()
    {
        _hwnd = IntPtr.Zero;
    }

    public int Send(uint message, int wParam, int lParam)
    {
        if (_hwnd == IntPtr.Zero && !TryConnect())
        {
            throw new PeaceBridgeException(LastError ?? "连接失败。");
        }

        var ok = NativeMethods.SendMessageTimeoutW(
            _hwnd,
            message,
            (nint)wParam,
            (nint)lParam,
            SmtoBlock | SmtoAbortIfHung,
            TimeoutMs,
            out var result);

        if (ok == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            _hwnd = IntPtr.Zero;
            LastError = error == 5
                ? "被 Windows 的权限隔离拦住：Peace 的权限级别和本程序不一致，两边要以同样的权限运行。"
                : $"Peace 在 {TimeoutMs} 毫秒内没有响应（Win32 错误 {error}）。";
            throw new PeaceBridgeException(LastError);
        }

        // LRESULT 在 64 位下是 64 位，只取低 32 位还原成 ActionDone 的整数。
        return unchecked((int)result.ToInt64());
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr FindWindowW(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
