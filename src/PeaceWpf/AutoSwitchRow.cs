using System.ComponentModel;
using System.Windows.Input;
using PeaceWpf.Core.Model;
using PeaceWpf.Services;

namespace PeaceWpf;

/// <summary>
/// 「自动切换」列表的一行，包着一份 <see cref="UiAutoSwitch"/>。
///
/// 界面只跟这个对象打交道；它每改一次就 raise <see cref="Changed"/>，
/// 窗口收到就立刻写盘 —— 这个页面没有「确定 / 取消」，也没有撤销。
/// </summary>
public sealed class AutoSwitchRow : INotifyPropertyChanged
{
    private int _order;
    private bool _isActive;
    private bool _hasHotkeyConflict;
    private bool _hasProcessConflict;
    private bool _isVoiceWake;
    private bool _isDragging;
    private bool _isCapturing;
    private string _captureText = string.Empty;
    private HotkeyModifiers _captureModifiers;
    private Key _captureKey = Key.None;

    public AutoSwitchRow(UiAutoSwitch data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Data.EnsureId();
    }

    public UiAutoSwitch Data { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>数据改了，窗口要立刻保存。</summary>
    public event EventHandler? Changed;

    public string MainPreset
    {
        get => Data.MainPreset;
        set => SetData(Data.MainPreset, value ?? string.Empty, next => Data.MainPreset = next, nameof(MainPreset));
    }

    public string BassPreset
    {
        get => Data.BassPreset;
        set => SetData(Data.BassPreset, value ?? string.Empty, next => Data.BassPreset = next, nameof(BassPreset));
    }

    public bool BassEnabled
    {
        get => Data.BassEnabled;
        set
        {
            if (Data.BassEnabled == value)
            {
                return;
            }

            Data.BassEnabled = value;
            Notify(nameof(BassEnabled));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>录键状态：显示「按下按键…」，行尾出现勾和叉。</summary>
    public bool IsCapturing => _isCapturing;

    /// <summary>热键框里显示的字：没录键时是存盘值，录键时是正在抓的组合。</summary>
    public string HotkeyText => _isCapturing
        ? _captureText
        : HotkeyFormat.Format(Data.HotkeyModifiers, Data.HotkeyVirtualKey);

    public bool HasHotkey => Data.HasHotkey;

    /// <summary>已经绑了热键、而且不在录键：框里常驻一个叉，点它解绑。</summary>
    public bool ShowHotkeyClear => Data.HasHotkey && !_isCapturing;

    /// <summary>
    /// 这一组在列表里的序号，从 1 开始。
    /// 热键和进程是「排在前面的那一组生效」，所以序号就是优先级。
    /// </summary>
    public int Order
    {
        get => _order;
        set
        {
            if (_order == value)
            {
                return;
            }

            _order = value;
            Notify(nameof(Order));
        }
    }

    /// <summary>正在被拖动：整行变淡，看得出它正跟着鼠标走。</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging == value)
            {
                return;
            }

            _isDragging = value;
            Notify(nameof(IsDragging));
        }
    }

    /// <summary>当前正在用的就是这一组：勾选框打勾，整行蓝框。</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            Notify(nameof(IsActive));
        }
    }

    /// <summary>和另一行曲线一致、热键也绑成同一个：热键框描红。</summary>
    public bool HasHotkeyConflict
    {
        get => _hasHotkeyConflict;
        set
        {
            if (_hasHotkeyConflict == value)
            {
                return;
            }

            _hasHotkeyConflict = value;
            Notify(nameof(HasHotkeyConflict));
        }
    }

    /// <summary>
    /// 语音唤醒绑在这一行。全局只有一个：由窗口按三元组统一设置，
    /// 同三元组的多行会一起亮，所以这里只做显示，不让绑定逻辑散在行里。
    /// </summary>
    public bool IsVoiceWake
    {
        get => _isVoiceWake;
        set
        {
            if (_isVoiceWake == value)
            {
                return;
            }

            _isVoiceWake = value;
            Notify(nameof(IsVoiceWake));
        }
    }

    /// <summary>和另一行曲线一致、进程也绑成同一个：进程框描红。</summary>
    public bool HasProcessConflict
    {
        get => _hasProcessConflict;
        set
        {
            if (_hasProcessConflict == value)
            {
                return;
            }

            _hasProcessConflict = value;
            Notify(nameof(HasProcessConflict));
        }
    }

    public string ProcessText => string.IsNullOrWhiteSpace(Data.ProcessName) ? "未绑定进程" : Data.ProcessName!;

    public bool HasProcess => Data.HasProcess;

    /// <summary>点热键框：进入录键状态。</summary>
    public void BeginCapture()
    {
        _isCapturing = true;
        _captureModifiers = HotkeyModifiers.None;
        _captureKey = Key.None;
        _captureText = "按下按键…";
        Notify(nameof(IsCapturing));
        Notify(nameof(ShowHotkeyClear));
        Notify(nameof(HotkeyText));
    }

    /// <summary>只按了修饰键：先显示「Ctrl + …」，不当作抓完。</summary>
    public void NoteModifiers(ModifierKeys modifiers)
    {
        if (!_isCapturing)
        {
            return;
        }

        var text = HotkeyFormat.DescribeModifiers(modifiers);
        _captureText = string.IsNullOrEmpty(text) ? "按下按键…" : text;
        Notify(nameof(HotkeyText));
    }

    /// <summary>抓到一个实键。勾上之前都还只是候选，不写盘。</summary>
    public void Capture(Key key, ModifierKeys modifiers)
    {
        if (!_isCapturing || HotkeyFormat.IsModifierKey(key))
        {
            return;
        }

        _captureModifiers = HotkeyFormat.ToHotkeyModifiers(modifiers);
        _captureKey = key;
        _captureText = HotkeyFormat.Format(modifiers, key);
        Notify(nameof(HotkeyText));
    }

    /// <summary>候选键是哪个。撞了重复的时候窗口要拿它去比对；没有候选就返回 false。</summary>
    public bool TryGetCaptureKey(out HotkeyModifiers modifiers, out int virtualKey)
    {
        modifiers = _captureModifiers;
        virtualKey = _captureKey == Key.None ? 0 : KeyInterop.VirtualKeyFromKey(_captureKey);
        return _isCapturing && virtualKey > 0;
    }

    /// <summary>
    /// 丢掉刚按下的候选：框里清空，还留在录键状态，可以接着按另一个组合键。
    /// 撞了重复的热键时用 —— 这一次输入不算数。
    /// </summary>
    public void ClearCaptureDraft()
    {
        if (!_isCapturing)
        {
            return;
        }

        _captureModifiers = HotkeyModifiers.None;
        _captureKey = Key.None;
        _captureText = string.Empty;
        Notify(nameof(HotkeyText));
    }

    /// <summary>按勾：候选写进数据并写盘。没有候选时等于取消。</summary>
    public void CommitCapture()
    {
        if (!_isCapturing)
        {
            return;
        }

        if (_captureKey != Key.None)
        {
            Data.HotkeyModifiers = _captureModifiers;
            Data.HotkeyVirtualKey = KeyInterop.VirtualKeyFromKey(_captureKey);
        }

        ResetCapture();
        Notify(nameof(HotkeyText));
        Notify(nameof(HasHotkey));
        Notify(nameof(ShowHotkeyClear));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按叉：丢掉候选，回到存盘值。</summary>
    public void CancelCapture()
    {
        if (!_isCapturing)
        {
            return;
        }

        ResetCapture();
        Notify(nameof(HotkeyText));
    }

    /// <summary>清掉热键绑定（点叉或重新录了空）。</summary>
    public void ClearHotkey()
    {
        if (!Data.HasHotkey)
        {
            return;
        }

        Data.ClearHotkey();
        Notify(nameof(HotkeyText));
        Notify(nameof(HasHotkey));
        Notify(nameof(ShowHotkeyClear));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>绑上进程名（选择器给的是不带 .exe 的进程名）。</summary>
    public void SetProcess(string? processName)
    {
        var next = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();
        if (string.Equals(Data.ProcessName, next, StringComparison.Ordinal))
        {
            return;
        }

        Data.ProcessName = next;
        Notify(nameof(ProcessText));
        Notify(nameof(HasProcess));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearProcess() => SetProcess(null);

    private void ResetCapture()
    {
        _isCapturing = false;
        _captureModifiers = HotkeyModifiers.None;
        _captureKey = Key.None;
        _captureText = string.Empty;
        Notify(nameof(IsCapturing));
        Notify(nameof(ShowHotkeyClear));
    }

    private void SetData(string current, string next, Action<string> assign, string propertyName)
    {
        if (string.Equals(current, next, StringComparison.Ordinal))
        {
            return;
        }

        assign(next);
        Notify(propertyName);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Notify(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
