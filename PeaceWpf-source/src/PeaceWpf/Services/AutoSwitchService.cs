using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using PeaceWpf.Core.Model;
using PeaceWpf.ViewModels;

namespace PeaceWpf.Services;

/// <summary>
/// 「自动切换」的运行侧：一个前台进程轮询器 + 一把全局热键。
///
/// 界面只负责改配置，这里负责在后台盯着：
///   · 热键按下 → 套用对应组合；
///   · 前台进程换了 → 如果这个进程绑了某一组，就套用那一组。
/// 切走（离开绑定进程）不恢复默认，保持用户最后听到的那套曲线。
/// </summary>
public sealed class AutoSwitchService : IDisposable
{
    /// <summary>热键 id 从这里往后排，避开 WM_HOTKEY 里别的用途。</summary>
    private const int BaseHotkeyId = 0x5100;

    private const int WmHotkey = 0x0312;

    /// <summary>MOD_NOREPEAT：按住不放只触发一次。</summary>
    private const uint ModNoRepeat = 0x4000;

    private readonly MainViewModel _viewModel;
    private readonly Window _window;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<int, UiAutoSwitch> _registered = new();
    private readonly Dictionary<uint, string> _processNames = new();

    private HwndSource? _source;
    private IntPtr _handle = IntPtr.Zero;
    private string? _lastForeground;
    private bool _started;
    private bool _applying;
    private bool _hotkeysSuspended;

    public AutoSwitchService(MainViewModel viewModel, Window window)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _window = window ?? throw new ArgumentNullException(nameof(window));

        // 800 ms：切窗口比人反应快一点就够了，再密只是白烧 CPU。
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _timer.Tick += (_, _) => PollForeground();
    }

    /// <summary>上一次刷新时注册失败的热键说明；空列表表示都挂上了。</summary>
    public IReadOnlyList<string> HotkeyErrors { get; private set; } = Array.Empty<string>();

    /// <summary>热键重新注册过（界面要刷新提示）。</summary>
    public event EventHandler? HotkeysChanged;

    /// <summary>
    /// 打开设置页时把所有热键摘掉：正在录键、正在改组合的时候，
    /// 按到别的热键就跳曲线会很烦。关掉设置页再挂回去。
    /// </summary>
    public void SuspendHotkeys()
    {
        _hotkeysSuspended = true;
        UnregisterAll();
        HotkeyErrors = Array.Empty<string>();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>设置页关掉了：按现在的组合重新挂热键。</summary>
    public void ResumeHotkeys()
    {
        if (!_hotkeysSuspended)
        {
            return;
        }

        _hotkeysSuspended = false;
        RefreshHotkeys();
    }

    /// <summary>挂在主窗口上；主窗口收进托盘时窗口还在，热键照样有效。</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        _handle = new WindowInteropHelper(_window).Handle;
        if (PresentationSource.FromVisual(_window) is HwndSource source)
        {
            _source = source;
            source.AddHook(OnWindowMessage);
        }

        _viewModel.AutoSwitchProfilesChanged += OnProfilesChanged;
        RefreshHotkeys();
        _timer.Start();
    }

    /// <summary>配置改过之后重挂热键，并让进程绑定重新判断一次。</summary>
    public void Refresh()
    {
        RefreshHotkeys();
        _lastForeground = null;
        _processNames.Clear();

        // 这里不立刻判一次：刚绑完进程时前台可能正是被选的那个窗口，
        // 马上套用会在设置界面上吓人一跳。等下一个轮询周期再按当时的窗口决定。
    }

    private void OnProfilesChanged(object? sender, EventArgs e) => Refresh();

    private void RefreshHotkeys()
    {
        if (_hotkeysSuspended)
        {
            UnregisterAll();
            HotkeyErrors = Array.Empty<string>();
            return;
        }

        UnregisterAll();
        var errors = new List<string>();

        if (_handle != IntPtr.Zero)
        {
            var seen = new HashSet<(HotkeyModifiers Modifiers, int Key)>();
            var index = 0;

            foreach (var profile in _viewModel.AutoSwitchProfiles)
            {
                if (profile is null || !profile.HasHotkey)
                {
                    continue;
                }

                var signature = (profile.HotkeyModifiers, profile.HotkeyVirtualKey);
                if (!seen.Add(signature))
                {
                    errors.Add($"热键 {HotkeyFormat.Format(profile.HotkeyModifiers, profile.HotkeyVirtualKey)} 被两组共用，后一组没生效。");
                    continue;
                }

                var id = BaseHotkeyId + index++;
                var modifiers = (uint)profile.HotkeyModifiers;
                if (RegisterHotKey(_handle, id, modifiers | ModNoRepeat, (uint)profile.HotkeyVirtualKey))
                {
                    _registered[id] = profile;
                }
                else
                {
                    errors.Add($"热键 {HotkeyFormat.Format(profile.HotkeyModifiers, profile.HotkeyVirtualKey)} 被别的程序占用了。");
                }
            }
        }

        HotkeyErrors = errors;
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        if (_registered.TryGetValue(id, out var profile))
        {
            handled = true;
            Apply(profile, "热键");
        }

        return IntPtr.Zero;
    }

    /// <summary>看看最前面那个窗口是谁的进程；换了进程才判断一次。</summary>
    private void PollForeground()
    {
        if (_applying)
        {
            return;
        }

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return;
        }

        // 焦点回到本程序：清掉记忆。用户可能手动改了曲线，再切回游戏时应该重新套上那一组。
        if (pid == (uint)Environment.ProcessId)
        {
            _lastForeground = null;
            return;
        }

        var name = ResolveProcessName(pid);
        if (string.IsNullOrEmpty(name) || string.Equals(name, _lastForeground, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastForeground = name;

        var profile = _viewModel.AutoSwitchProfiles.FirstOrDefault(item =>
            item is not null && ProcessMatches(item.ProcessName, name));
        if (profile is null)
        {
            return;
        }

        Apply(profile, "进程 " + name);
    }

    private bool ProcessMatches(string? configured, string actual)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        return string.Equals(TrimExe(configured), TrimExe(actual), StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimExe(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private string? ResolveProcessName(uint pid)
    {
        if (_processNames.TryGetValue(pid, out var cached))
        {
            return cached;
        }

        try
        {
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName;
            _processNames[pid] = name;

            // 进程 id 会被复用；缓存别无限长，留最近几百个足够。
            if (_processNames.Count > 512)
            {
                _processNames.Clear();
                _processNames[pid] = name;
            }

            return name;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private void Apply(UiAutoSwitch profile, string source)
    {
        _applying = true;
        try
        {
            if (!_viewModel.ApplyAutoSwitch(profile, out var message))
            {
                _viewModel.StatusText = message;
                return;
            }

            _viewModel.StatusText = source + "触发 · " + message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // 自动动作不能把界面或计时器带崩。
            _viewModel.StatusText = "自动切换失败：" + ex.Message;
        }
        finally
        {
            _applying = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _viewModel.AutoSwitchProfilesChanged -= OnProfilesChanged;

        foreach (var id in _registered.Keys)
        {
            UnregisterHotKey(_handle, id);
        }

        _registered.Clear();
        _source?.RemoveHook(OnWindowMessage);
        _source = null;
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys)
        {
            UnregisterHotKey(_handle, id);
        }

        _registered.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
