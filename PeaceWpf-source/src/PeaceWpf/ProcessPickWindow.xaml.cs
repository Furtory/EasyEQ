using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PeaceWpf;

/// <summary>
/// 绑定进程用的全屏选择器：黑罩压暗，鼠标下面的窗口用蓝框圈出来，左键确认、右键 / Esc 取消。
///
/// 为什么不用 WindowFromPoint：这个窗口本身就盖在最上面，WindowFromPoint 先拿到的是它自己。
/// 改成 EnumWindows 按 Z 序从上往下找第一个「包含鼠标、又不是本进程」的顶层窗口，
/// 遮罩就可以放心地接收点击。
/// </summary>
public partial class ProcessPickWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;

    private readonly DispatcherTimer _timer;

    private IntPtr _target;
    private string? _targetProcess;
    private string? _targetTitle;

    public ProcessPickWindow()
    {
        InitializeComponent();

        // 铺满整个虚拟屏幕，多显示器也能选。
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Cursor = Cursors.Cross;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _timer.Tick += (_, _) => UpdateTarget();

        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            UpdateTarget();
            _timer.Start();
        };

        Closed += (_, _) => _timer.Stop();
        PreviewKeyDown += OnPreviewKeyDown;
        Root.MouseLeftButtonDown += OnConfirm;
        Root.MouseRightButtonDown += (_, _) => Cancel();
    }

    /// <summary>选中的进程名（不带 .exe）；取消时是 null。</summary>
    public string? PickedProcess { get; private set; }

    public string? PickedTitle => _targetTitle;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
        }
    }

    private void OnConfirm(object sender, MouseButtonEventArgs e)
    {
        // 点之前再看一眼：鼠标可能刚好在这两帧之间移到了别的窗口上。
        UpdateTarget();

        if (_target == IntPtr.Zero || string.IsNullOrWhiteSpace(_targetProcess))
        {
            return;
        }

        PickedProcess = _targetProcess;
        DialogResult = true;
    }

    private void Cancel()
    {
        PickedProcess = null;
        DialogResult = false;
    }

    private void UpdateTarget()
    {
        GetCursorPos(out var cursor);

        var found = FindWindowAt(cursor.X, cursor.Y, out var processName, out var title);
        if (found == IntPtr.Zero)
        {
            _target = IntPtr.Zero;
            _targetProcess = null;
            _targetTitle = null;
            Highlight.Visibility = Visibility.Collapsed;
            TargetText.Text = "鼠标下面没有可绑定的窗口";
            return;
        }

        _target = found;
        _targetProcess = processName;
        _targetTitle = title;

        var rect = GetFrameBounds(found);
        var topLeft = PointFromScreen(new Point(rect.Left, rect.Top));
        var bottomRight = PointFromScreen(new Point(rect.Right, rect.Bottom));
        var width = Math.Max(0, bottomRight.X - topLeft.X);
        var height = Math.Max(0, bottomRight.Y - topLeft.Y);

        if (width < 1 || height < 1)
        {
            Highlight.Visibility = Visibility.Collapsed;
            TargetText.Text = "这个窗口太小，换个地方试试";
            return;
        }

        Highlight.Width = width;
        Highlight.Height = height;
        Canvas.SetLeft(Highlight, topLeft.X);
        Canvas.SetTop(Highlight, topLeft.Y);
        Highlight.Visibility = Visibility.Visible;

        var name = string.IsNullOrWhiteSpace(processName) ? "（未知进程）" : processName;
        TargetText.Text = string.IsNullOrWhiteSpace(title)
            ? name
            : name + " · " + title;
    }

    /// <summary>按 Z 序找鼠标下第一个顶层窗口；跳过本程序自己和看不到的壳窗口。</summary>
    private IntPtr FindWindowAt(int x, int y, out string? processName, out string? title)
    {
        processName = null;
        title = null;

        var ownPid = (uint)Environment.ProcessId;
        IntPtr result = IntPtr.Zero;
        string? resultProcess = null;
        string? resultTitle = null;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
            {
                return true;
            }

            var style = GetWindowLong(hwnd, GwlExStyle);
            if ((style & WsExToolWindow) != 0 || (style & WsExTransparent) != 0)
            {
                return true;
            }

            if (IsCloaked(hwnd))
            {
                return true;
            }

            var rect = GetFrameBounds(hwnd);
            if (rect.Right - rect.Left < 24 || rect.Bottom - rect.Top < 24)
            {
                return true;
            }

            if (x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom)
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0 || pid == ownPid)
            {
                return true;
            }

            var className = GetClass(hwnd);
            if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")
            {
                return true;
            }

            result = hwnd;
            resultProcess = ProcessNameOf(pid);
            resultTitle = GetTitle(hwnd);
            return false;
        }, IntPtr.Zero);

        processName = resultProcess;
        title = resultTitle;
        return result;
    }

    private static NativeRect GetFrameBounds(IntPtr hwnd)
    {
        // DWM 的扩展边框不含那圈看不见的拉伸手柄；拿不到时退回 GetWindowRect。
        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out NativeRect bounds, Marshal.SizeOf<NativeRect>()) != 0)
        {
            GetWindowRect(hwnd, out bounds);
        }

        return bounds;
    }

    private static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static string? ProcessNameOf(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string? GetTitle(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        var length = GetWindowText(hwnd, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString() : null;
    }

    private static string GetClass(IntPtr hwnd)
    {
        var buffer = new StringBuilder(128);
        GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
