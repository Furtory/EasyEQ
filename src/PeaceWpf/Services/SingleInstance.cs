using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PeaceWpf.Services;

/// <summary>
/// 单实例：第二个实例启动时，把已经在运行的那个主窗口叫出来并聚焦，然后自己退出。
///
/// 为什么必须这样做：两个实例同时写同一批文件（peace.txt / .peace / EsportsMix），
/// 谁后写谁生效，而且两边都会去碰 Peace 的消息窗口——轻则互相覆盖，重则抢权限。
/// 只允许一个实例，问题从源头消失。
/// </summary>
public static class SingleInstance
{
    /// <summary>按会话隔离（同一台机器上不同用户各跑一份互不影响）。</summary>
    private const string MutexName = @"Local\EasyEQ.SingleInstance";

    /// <summary>
    /// 激活事件：第二个实例 Set 一下，已经在跑的那个自己去把窗口叫回来。
    /// 收在托盘里时主窗口是隐藏的，从外面 ShowWindow 会把它拽出个半生不熟的状态。
    /// </summary>
    private const string ActivateEventName = @"Local\EasyEQ.Activate";

    private const int SwRestore = 9;

    /// <summary>必须一直持有到进程退出，否则 GC 回收后第二个实例就能进来。</summary>
    private static Mutex? _mutex;

    /// <summary>第一个实例才建；第二个实例只打开它、Set 一下就退出。</summary>
    private static EventWaitHandle? _activateEvent;

    /// <summary>拿到单实例锁返回 true；已经有一个在跑返回 false。</summary>
    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);

        if (createdNew)
        {
            _activateEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ActivateEventName);
        }

        return createdNew;
    }

    /// <summary>
    /// 开始监听激活请求。回调在线程池线程上跑，调用方自己切回 UI 线程。
    /// 线程是后台的，进程退出时不会把它留下。
    /// </summary>
    public static void ListenForActivation(Action onActivate)
    {
        if (_activateEvent is not { } handle)
        {
            return;
        }

        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    handle.WaitOne();
                    onActivate();
                }
                catch (Exception ex) when (ex is ObjectDisposedException or AbandonedMutexException)
                {
                    return;
                }
            }
        })
        {
            IsBackground = true,
            Name = "EasyEQ.Activate",
        };

        thread.Start();
    }

    /// <summary>
    /// 找出已经在运行的那个实例，让它把窗口叫出来。
    /// 优先用激活事件：窗口收在托盘里时是隐藏的，找句柄的那条路会落空。
    /// </summary>
    public static void ActivateExisting()
    {
        if (TryPokeRunningInstance())
        {
            return;
        }

        ActivateByWindowHandle();
    }

    /// <summary>给正在跑的实例发一个激活信号；事件不存在就返回 false。</summary>
    private static bool TryPokeRunningInstance()
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(ActivateEventName, out var handle))
            {
                return false;
            }

            using (handle)
            {
                handle.Set();
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or IOException
            or WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    /// <summary>
    /// 老办法：按进程找主窗口再恢复。
    /// 只在激活事件不可用时兜底——比如正在跑的是没升级过的旧版本。
    /// </summary>
    private static void ActivateByWindowHandle()
    {
        var self = Environment.ProcessId;

        foreach (var process in Process.GetProcessesByName("EasyEQ"))
        {
            try
            {
                if (process.Id == self)
                {
                    continue;
                }

                var handle = FindMainWindow(process.Id);
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                if (IsIconic(handle))
                {
                    ShowWindow(handle, SwRestore);
                }

                SetForegroundWindow(handle);
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException)
            {
                // 拿不到句柄就换下一个，绝不因为激活失败而卡住退出流程。
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// 优先用 MainWindowHandle；它偶尔是 0（窗口刚建好、或收在托盘里），
    /// 这时枚举该进程的顶层窗口兜底。
    /// </summary>
    private static IntPtr FindMainWindow(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process.MainWindowHandle;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return IntPtr.Zero;
        }

        var found = IntPtr.Zero;
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var owner);
            if (owner != (uint)processId || !IsWindowVisible(handle))
            {
                return true;
            }

            found = handle;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
}
