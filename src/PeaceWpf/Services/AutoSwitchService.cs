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

    /// <summary>语音唤醒用的检测器。懒加载，只在真的绑了组合时才建。</summary>
    private PeaceWpf.Core.AutoFlavor.SileroVoiceDetector? _voiceDetector;

    /// <summary>
    /// 语音接管中：只要还在听见人声，热键和进程绑定都不许把配置切走。
    /// 连续 <see cref="VoiceHoldMs"/> 毫秒没再听到人声才放开，放开时再把组合切回去。
    /// </summary>
    private bool _voiceHolds;

    /// <summary>语音期间按下的热键 / 切到的绑定窗口：说完之后要切到这里（最后指向的那一组）。</summary>
    private UiAutoSwitch? _voicePending;

    /// <summary>
    /// 缓冲下来的那一组是被什么点名的（「热键 Ctrl+1」/「进程 firefox」）。
    /// 语音结束后切过去时写进通知，让人知道这次切换的由头是什么。
    /// </summary>
    private string? _voicePendingSource;

    /// <summary>
    /// 进语音之前正在用的那套（左右 + 低音 + 叠加），外加当时处在第几组（0 表示不在任何一组）
    /// 和是谁点亮的（「热键 Ctrl+1」「进程 firefox」）。说完没人动就切回它，通知也照这些报。
    /// </summary>
    private (string Main, string Bass, bool BassOn, int ComboIndex, string? Source)? _voiceRestore;

    private long _lastVoiceAt;

    /// <summary>
    /// 多久没听见人声就放开接管。要比采集那边的 3 秒触发去抖宽一点，
    /// 否则两次触发之间会被误判成「安静了」。
    /// </summary>
    private const int VoiceHoldMs = 5000;

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
        _viewModel.SpectrumCapture.VoiceWakeDetected += OnVoiceWakeDetected;
        RefreshHotkeys();
        UpdateVoiceWake();
        _timer.Start();
    }

    /// <summary>配置改过之后重挂热键，并让进程绑定重新判断一次。</summary>
    public void Refresh()
    {
        RefreshHotkeys();
        UpdateVoiceWake();
        _lastForeground = null;
        _processNames.Clear();

        // 这里不立刻判一次：刚绑完进程时前台可能正是被选的那个窗口，
        // 马上套用会在设置界面上吓人一跳。等下一个轮询周期再按当时的窗口决定。
    }

    /// <summary>
    /// 有人声：切到绑定的那个组合。检测跑在采集线程上，这里先切回界面线程再动 ViewModel。
    /// </summary>
    private void OnVoiceWakeDetected(object? sender, EventArgs e)
    {
        _window.Dispatcher.BeginInvoke(() =>
        {
            var key = _viewModel.VoiceWakeKey;
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            _lastVoiceAt = Environment.TickCount64;
            if (_voiceHolds)
            {
                // 已经在语音接管里：只续上计时，不重复切。
                return;
            }

            var profile = _viewModel.AutoSwitchProfiles.FirstOrDefault(item =>
                item is not null && string.Equals(item.ComboKey, key, StringComparison.Ordinal));
            if (profile is null)
            {
                return;
            }

            // 进语音前正在用的那套先记下来：说完没人动过，就切回它。
            // 顺带记下当时在第几组（可能根本不在任何一组里），结束时直接报这个数。
            _voiceRestore = (
                _viewModel.MainPresetName,
                _viewModel.BassPresetName,
                _viewModel.IsBassLayerEnabled,
                _viewModel.ActiveComboIndex,
                _viewModel.ActiveComboSource);
            _voicePending = null;
            _voicePendingSource = null;

            Apply(profile, "语音唤醒");
            _voiceHolds = true;
        });
    }

    /// <summary>
    /// 语音是不是还在接管。超过 <see cref="VoiceHoldMs"/> 没听见人声就算放开；
    /// 真正放开（并把组合切回去）在下一次轮询里做，这里只回答「现在算不算还在接管」。
    /// </summary>
    private bool VoiceHolds() =>
        _voiceHolds && Environment.TickCount64 - _lastVoiceAt <= VoiceHoldMs;

    /// <summary>
    /// 说完了：把组合切回去。
    ///
    /// 语音期间按过热键或切到别的绑定窗口 → 切到最后指向的那一组；
    /// 期间没人动过 → 按现在聚焦的窗口；那个窗口没绑定 → 切回进语音前的那一套。
    /// </summary>
    private void EndVoiceHold()
    {
        _voiceHolds = false;

        var pending = _voicePending;
        var pendingSource = _voicePendingSource;
        _voicePending = null;
        _voicePendingSource = null;

        var target = pending;
        string? source = pending is not null
            ? (pendingSource is not null ? "语音结束 · 缓冲的" + pendingSource : "语音结束")
            : null;
        string? osdText = null;

        // 还原时要把唤醒前那组的序号和「是谁点亮的」一起报出来。
        var restoreIndex = 0;
        string? restoreSource = null;

        // 语音期间没人动过：先看现在聚焦的窗口绑了哪一组。
        if (target is null)
        {
            target = FindForegroundProfile(out var foregroundName);
            if (target is not null)
            {
                source = "语音结束 · 当前进程 " + (foregroundName ?? "未知");
            }
        }

        // 前台窗口没绑定：还原进语音之前那套。
        if (target is null)
        {
            var restore = _voiceRestore;
            target = ProfileForCurves(restore is { } value
                ? (value.Main, value.Bass, value.BassOn)
                : null);

            if (target is not null)
            {
                restoreIndex = restore?.ComboIndex ?? 0;
                restoreSource = restore?.Source;
                source = "语音结束 · 还原进语音前";

                // 三种说法固定：热键 / 进程 / 之前那个自定义曲线，不套括号。
                osdText = restoreIndex <= 0
                    ? "语音结束 · 还原 之前 激活的自定义曲线"
                    : string.IsNullOrWhiteSpace(restoreSource)
                        ? $"语音结束 · 还原 之前 激活的第 {restoreIndex} 个组合"
                        : $"语音结束 · 还原 {restoreSource} 激活的第 {restoreIndex} 个组合";
            }
        }

        _voiceRestore = null;

        if (target is not null)
        {
            Apply(target, source ?? "语音结束", osdText);

            // 还原用的是临时组合，走不通列表索引；把记下的号交回去，
            // 下一次语音唤醒才还能报出「第 N 组」。
            if (restoreIndex > 0)
            {
                _viewModel.RememberActiveCombo(restoreIndex, restoreSource);
            }
        }
    }

    /// <summary>当前最前面的窗口绑了哪一组；本程序自己、桌面、没绑定都返回 null。</summary>
    /// <param name="processName">前台进程名，取通知说明用；拿不到就是 null。</param>
    private UiAutoSwitch? FindForegroundProfile(out string? processName)
    {
        processName = null;

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId)
        {
            return null;
        }

        var name = ResolveProcessName(pid);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        processName = name;
        return _viewModel.AutoSwitchProfiles.FirstOrDefault(item =>
            item is not null && ProcessMatches(item.ProcessName, name));
    }

    /// <summary>按「左右 + 低音 + 叠加」找回那一组；配置里已经没有就临时造一份来切。</summary>
    private UiAutoSwitch? ProfileForCurves((string Main, string Bass, bool BassOn)? curves)
    {
        if (curves is not { } value
            || (string.IsNullOrWhiteSpace(value.Main) && string.IsNullOrWhiteSpace(value.Bass)))
        {
            return null;
        }

        return _viewModel.AutoSwitchProfiles.FirstOrDefault(item => item is not null
                   && string.Equals(item.MainPreset, value.Main, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(item.BassPreset, value.Bass, StringComparison.OrdinalIgnoreCase)
                   && item.BassEnabled == value.BassOn)
               ?? new UiAutoSwitch
               {
                   MainPreset = value.Main,
                   BassPreset = value.Bass,
                   BassEnabled = value.BassOn,
               };
    }

    /// <summary>
    /// 按当前绑定决定要不要挂检测器。没绑就把检测器摘掉，采集那边连降采样都不做。
    /// 模型加载失败就静默当没绑，不影响热键和进程绑定。
    /// </summary>
    private void UpdateVoiceWake()
    {
        // 绑定变了就重新算：原来那次的接管状态不再算数。
        _voiceHolds = false;
        _lastVoiceAt = 0;
        _voicePending = null;
        _voiceRestore = null;

        var capture = _viewModel.SpectrumCapture;
        if (string.IsNullOrEmpty(_viewModel.VoiceWakeKey))
        {
            capture.VoiceWake = null;
            return;
        }

        if (_voiceDetector is null)
        {
            try
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "silero_vad.onnx");
                if (System.IO.File.Exists(path))
                {
                    _voiceDetector = new PeaceWpf.Core.AutoFlavor.SileroVoiceDetector(path);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _voiceDetector = null;
            }
        }

        capture.VoiceWake = _voiceDetector;
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
            if (VoiceHolds())
            {
                // 语音期间不马上切：记住最后按的那一把，安静下来再切过去。
                // 顺带记下是哪把热键，语音结束后通知里要写清楚这次切换的由头。
                var hotkey = HotkeyFormat.Format(profile.HotkeyModifiers, profile.HotkeyVirtualKey);
                _voicePending = profile;
                _voicePendingSource = "热键 " + hotkey;
                _viewModel.StatusText = $"语音接管中：说完再切到「{hotkey}」那一组。";
                return IntPtr.Zero;
            }

            Apply(
                profile,
                "热键 " + HotkeyFormat.Format(profile.HotkeyModifiers, profile.HotkeyVirtualKey));
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

        // 语音早就停了：这一轮先把组合切回去，再照常看窗口。
        if (_voiceHolds && !VoiceHolds())
        {
            EndVoiceHold();
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

        // 语音接管期间只记下「切到了哪一组绑定的窗口」，等安静下来再一起切过去。
        if (VoiceHolds())
        {
            _voicePending = _viewModel.AutoSwitchProfiles.FirstOrDefault(item =>
                item is not null && ProcessMatches(item.ProcessName, name));
            _voicePendingSource = "进程 " + name;
            return;
        }

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

    private void Apply(UiAutoSwitch profile, string source, string? osdText = null)
    {
        _applying = true;
        try
        {
            if (!_viewModel.ApplyAutoSwitch(profile, out var message, source, osdText))
            {
                _viewModel.StatusText = message;
                return;
            }

            _viewModel.StatusText = source + " 触发 · " + message;
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
        _viewModel.SpectrumCapture.VoiceWakeDetected -= OnVoiceWakeDetected;
        _viewModel.SpectrumCapture.VoiceWake = null;

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
