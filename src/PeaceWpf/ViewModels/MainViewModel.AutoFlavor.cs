using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using PeaceWpf.Core.AutoFlavor;
using PeaceWpf.Core.Model;

namespace PeaceWpf.ViewModels;

/// <summary>
/// 自动风味：录制 ↔ 暂停，强度一条滑杆，对比 / 应用 / 取消。
///
/// 关键设计：录制期间声音是「校准链」（左右 + 低音，风味全 0），
/// 图上画的是算出来的建议。所以拖滑杆只改图、不改声，也不会自己测自己。
/// 整个过程不碰草稿，只有「应用」才进撤回栈、才置脏。
/// </summary>
public partial class MainViewModel
{
    private readonly AutoFlavorRecorder _autoFlavor = new();

    private DispatcherTimer? _autoFlavorFrameTimer;
    private DispatcherTimer? _autoFlavorRefreshTimer;
    private DispatcherTimer? _autoFlavorCountdownTimer;
    private DispatcherTimer? _autoFlavorStartTimer;

    private AutoFlavorPhase _autoFlavorPhase = AutoFlavorPhase.Off;
    private AutoFlavorSuggestion? _autoFlavorSuggestion;
    private FlavorSet? _autoFlavorAudio;
    private FlavorSet? _autoFlavorDisplay;
    private bool _autoFlavorCompareFlat;

    /// <summary>人声检测器：只在男声/女声方向接上，其余方向是 null（等于全程没有标记）。</summary>
    private IVoiceDetector? _voiceDetector;
    private bool _voiceDetectorProbed;
    private string? _voiceDetectorError;
    private int _selectionBeforeAuto = -1;
    private int _autoFlavorCountdown;
    private long _autoFlavorSessionMs;

    /// <summary>是不是在自动风味模式里。</summary>
    public bool IsAutoFlavorMode => _autoFlavorPhase != AutoFlavorPhase.Off;

    /// <summary>
    /// 叠在曲线图上的那行人声提示。只在人声方向和自动风味模式里出现：
    /// 录制中是进度，暂停后是结果；方向没有人声标识、或者结果还没出来，就是空串。
    /// </summary>
    public string AutoFlavorVoiceHint
    {
        get
        {
            if (!IsAutoFlavorMode)
            {
                return string.Empty;
            }

            var info = AutoFlavorDirections.Get(_uiState.AutoFlavorDirection);
            if (info.VoiceBias == AutoFlavorVoiceBias.None)
            {
                return string.Empty;
            }

            if (_autoFlavorPhase is AutoFlavorPhase.Idle or AutoFlavorPhase.Countdown)
            {
                return "人声检测：开始录制后生效";
            }

            if (_autoFlavorPhase == AutoFlavorPhase.Recording)
            {
                return _autoFlavor.VoiceDetector is null
                    ? "人声检测不可用，按曲线生成"
                    : "人声检测中…";
            }

            if (_autoFlavor.VoiceDetector is null)
            {
                return string.IsNullOrWhiteSpace(_voiceDetectorError)
                    ? "人声检测不可用，按曲线生成"
                    : "人声检测不可用：" + _voiceDetectorError;
            }

            var suggestion = _autoFlavorSuggestion;
            if (suggestion is null || !suggestion.HasResult)
            {
                return string.Empty;
            }

            return suggestion.VoiceBoostDb > 0.01
                ? $"{info.Label}固定补 {suggestion.VoiceBoostDb:0.0} dB"
                    + (suggestion.VoiceRatio > 0.01 ? $"（测到人声 {suggestion.VoiceRatio * 100:0}%）" : string.Empty)
                : "按曲线生成";
        }
    }

    /// <summary>提示条要不要露出来：有字就露。</summary>
    public bool ShowAutoFlavorVoiceHint => !string.IsNullOrEmpty(AutoFlavorVoiceHint);

    /// <summary>手动那排（风味下拉 + 应用到其他配置 + 自动风味按钮）什么时候露出来。</summary>
    public bool ShowManualFlavorGroup => IsFlavorEditable && !IsAutoFlavorMode;

    /// <summary>模式内那排。</summary>
    public bool ShowAutoFlavorControls => IsAutoFlavorMode;

    /// <summary>暂停且有建议时，对比 / 应用 / 取消才出现。</summary>
    public bool ShowAutoFlavorResult =>
        _autoFlavorPhase == AutoFlavorPhase.Paused && _autoFlavorSuggestion is { HasResult: true };

    /// <summary>录制按钮上的字。</summary>
    public string AutoFlavorButtonText => _autoFlavorPhase switch
    {
        AutoFlavorPhase.Idle => "开始录制",
        AutoFlavorPhase.Countdown => _autoFlavorCountdown.ToString(),
        AutoFlavorPhase.Recording => "暂停录制",
        AutoFlavorPhase.Paused => "继续录制",
        _ => "开始录制",
    };

    /// <summary>四个方向，给下拉用。</summary>
    public IReadOnlyList<AutoFlavorDirectionInfo> AutoFlavorChoices => AutoFlavorDirections.All;

    /// <summary>当前选中的方向。</summary>
    public AutoFlavorDirectionInfo SelectedAutoFlavorDirection
    {
        get => AutoFlavorDirections.Get(_uiState.AutoFlavorDirection);
        set
        {
            if (value is null || value.Direction == _uiState.AutoFlavorDirection)
            {
                return;
            }

            _uiState.AutoFlavorDirection = value.Direction;
            PersistState();
            OnPropertyChanged();
            OnPropertyChanged(nameof(FlavorNamingOverrideKey));
            ApplyNamingForDirection(value.Direction);
            _autoFlavor.VoiceDetector = VoiceDetectorForCurrentDirection();
            RefreshAutoFlavor(force: true);
        }
    }

    /// <summary>强度滑杆（0–100）。</summary>
    public double AutoFlavorStrengthPercent
    {
        get => _uiState.AutoFlavorStrength * 100;
        set
        {
            var next = Math.Clamp(value, 0, 100) / 100;
            if (Math.Abs(next - _uiState.AutoFlavorStrength) < 1e-6)
            {
                return;
            }

            _uiState.AutoFlavorStrength = next;
            PersistState();
            OnPropertyChanged();
            RefreshAutoFlavor(force: true);
        }
    }

    /// <summary>图上画的风味：模式里是建议（或对比时的全 0），平时就是草稿里的那套。</summary>
    public FlavorSet DisplayFlavors => _autoFlavorDisplay ?? Flavors;

    /// <summary>风味的拖拽手柄什么时候能拖：模式里不能拖（那是程序算的，不是用户改的）。</summary>
    public bool FlavorHandlesEditable => IsFlavorEditable && !IsAutoFlavorMode;

    /// <summary>模式里把每条风味的类型、频率、增益、Q 标在图上；平时不标。</summary>
    public bool FlavorLabelsVisible => IsAutoFlavorMode;

    /// <summary>
    /// 乐器视角下仍然保留自己名字的那一条（自动风味选男声 / 女声时）。
    /// 返回频谱键；空串表示都跟着视角换。
    /// </summary>
    public string FlavorNamingOverrideKey =>
        IsAutoFlavorMode ? FlavorNaming.OverrideKeyFor(_uiState.AutoFlavorDirection) : string.Empty;

    /// <summary>
    /// 风味命名视角：false 是游戏叫法（爆炸、脚步…），true 是乐器叫法（低音提琴、长笛…）。
    /// 跟着「左右」预设走——每个预设记自己的，切预设就跟着换。
    /// </summary>
    public bool IsInstrumentNaming
    {
        get => _uiState.GetInstrumentNaming(_mainConfigName);
        set
        {
            if (value == _uiState.GetInstrumentNaming(_mainConfigName))
            {
                return;
            }

            _uiState.SetInstrumentNaming(_mainConfigName, value);
            PersistState();
            OnPropertyChanged();
        }
    }

    /// <summary>写进 peace.txt 的风味：模式里是校准链（全 0）或建议。</summary>
    private FlavorSet WriterFlavors => _autoFlavorAudio ?? Flavors;

    /// <summary>
    /// 试听时低音层叠不叠加：自动风味模式里只听「叠加低音修正」这一个开关，
    /// 不看编辑态的那套规则——识别期间听到的必须是真实链路。
    /// </summary>
    public bool PreviewIncludeBass => IsAutoFlavorMode ? IsBassLayerEnabled : PreviewOverlayBass;

    /// <summary>右下角那组「撤回 / 保存 / 取消」什么时候露出来。</summary>
    public bool ShowDraftActions => IsActiveDraftDirty && !IsAutoFlavorMode;

    /// <summary>进入自动风味：手动那排收起来，露出方向 / 强度 / 开始录制。</summary>
    [RelayCommand]
    private void EnterAutoFlavor()
    {
        if (!IsFlavorEditable || IsAutoFlavorMode)
        {
            return;
        }

        EnsureAutoFlavorTimers();
        // 记住进模式时的选择：本来就是没选，退出也要回到没选。
        _selectionBeforeAuto = SelectedFlavorIndex;
        _autoFlavor.Clear();
        _autoFlavorSessionMs = 0;
        _autoFlavorSuggestion = null;
        _autoFlavorCompareFlat = false;
        _autoFlavorAudio = null;
        _autoFlavorDisplay = null;
        _autoFlavorPhase = AutoFlavorPhase.Idle;
        _autoFlavor.AvailableBytes = AvailableMemory;
        _spectrumCapture.AutoFlavor = _autoFlavor;

        NotifyAutoFlavorModeChanged();
        ApplyNamingForDirection(_uiState.AutoFlavorDirection);
        StatusText = "自动风味：选一个方向，点「开始录制」。录制时声音会暂时变成不带风味的校准曲线。";
    }

    /// <summary>切方向时顺手把命名视角也切过去：竞技用游戏叫法，其余用乐器叫法。</summary>
    private void ApplyNamingForDirection(AutoFlavorDirection direction)
    {
        var instrument = FlavorNaming.PrefersInstrumentNaming(direction);
        if (IsInstrumentNaming == instrument)
        {
            return;
        }

        _uiState.SetInstrumentNaming(_mainConfigName, instrument);
        PersistState();
        OnPropertyChanged(nameof(IsInstrumentNaming));
    }

    /// <summary>开始录制 / 暂停 / 继续，一个按钮三个状态。</summary>
    [RelayCommand]
    private void ToggleAutoFlavorRecording()
    {
        switch (_autoFlavorPhase)
        {
            case AutoFlavorPhase.Idle:
                BeginAutoFlavorCountdown();
                break;

            // 倒计时里再点一次：不等了，立刻开录。
            case AutoFlavorPhase.Countdown:
                _autoFlavorCountdownTimer?.Stop();
                StartAutoFlavorRecording();
                break;

            case AutoFlavorPhase.Recording:
                PauseAutoFlavor();
                break;

            case AutoFlavorPhase.Paused:
                ResumeAutoFlavor();
                break;
        }
    }

    /// <summary>对比：在「建议」和「全 0（校准链）」之间来回。不进撤回、不置脏。</summary>
    [RelayCommand]
    private void ToggleAutoFlavorCompare()
    {
        if (_autoFlavorPhase != AutoFlavorPhase.Paused || _autoFlavorSuggestion is not { HasResult: true } suggestion)
        {
            return;
        }

        _autoFlavorCompareFlat = !_autoFlavorCompareFlat;
        var next = _autoFlavorCompareFlat ? FlavorSet.CreateDefault() : suggestion.Flavors;
        _autoFlavorAudio = next;
        _autoFlavorDisplay = next;
        SchedulePreview();
        OnPropertyChanged(nameof(DisplayFlavors));
    }

    /// <summary>应用：把建议提交进草稿（一步撤回），退出模式。保存仍是另一件事。</summary>
    [RelayCommand]
    private void ApplyAutoFlavor()
    {
        if (_autoFlavorSuggestion is not { HasResult: true } suggestion)
        {
            return;
        }

        var flavors = suggestion.Flavors;
        _autoFlavorAudio = null;
        _autoFlavorDisplay = null;
        ExitAutoFlavorMode();

        ApplyFlavor(flavors, "自动风味已套用；按保存才会写进预设。");
    }

    /// <summary>取消：退出模式、清空数据、声音回到进入前。</summary>
    [RelayCommand]
    private void CancelAutoFlavor()
    {
        if (!IsAutoFlavorMode)
        {
            return;
        }

        _autoFlavorAudio = null;
        _autoFlavorDisplay = null;
        ExitAutoFlavorMode();
        SchedulePreview();
        StatusText = "已退出自动风味，录制数据已清空。";
    }

    private void BeginAutoFlavorCountdown()
    {
        // 先把声音切成校准链：左右 + 低音（按叠加开关），风味全 0。
        _autoFlavorAudio = FlavorSet.CreateDefault();
        _autoFlavorDisplay = FlavorSet.CreateDefault();
        SchedulePreview();

        _autoFlavorPhase = AutoFlavorPhase.Countdown;
        _autoFlavorCountdown = 5;
        EnsureAutoFlavorTimers();
        _autoFlavorCountdownTimer!.Start();
        NotifyAutoFlavorModeChanged();
    }

    private void TickAutoFlavorCountdown()
    {
        if (_autoFlavorPhase != AutoFlavorPhase.Countdown)
        {
            _autoFlavorCountdownTimer?.Stop();
            return;
        }

        _autoFlavorCountdown--;
        OnPropertyChanged(nameof(AutoFlavorButtonText));

        if (_autoFlavorCountdown > 0)
        {
            return;
        }

        _autoFlavorCountdownTimer!.Stop();
        _autoFlavorStartTimer!.Start();
    }

    private void StartAutoFlavorRecording()
    {
        _autoFlavorStartTimer!.Stop();

        // 已经有过数据（暂停后继续）就不再从零开始计时。
        if (_autoFlavor.FrameCount == 0)
        {
            _autoFlavorSessionMs = 0;
        }

        _autoFlavorPhase = AutoFlavorPhase.Recording;
        _autoFlavor.VoiceDetector = VoiceDetectorForCurrentDirection();
        _autoFlavor.Start();
        _autoFlavorFrameTimer!.Start();
        _autoFlavorRefreshTimer!.Start();
        NotifyAutoFlavorModeChanged();
        StatusText = "录制中：每 100 ms 记一帧，声音保持校准链。";
    }

    private void PauseAutoFlavor()
    {
        _autoFlavorPhase = AutoFlavorPhase.Paused;
        _autoFlavor.Pause();
        _autoFlavorFrameTimer?.Stop();
        _autoFlavorRefreshTimer?.Stop();

        _autoFlavorSuggestion = ComputeAutoFlavorSuggestion();
        _autoFlavorCompareFlat = false;
        _autoFlavorAudio = _autoFlavorSuggestion.HasResult ? _autoFlavorSuggestion.Flavors : FlavorSet.CreateDefault();
        _autoFlavorDisplay = _autoFlavorAudio;
        SchedulePreview();
        NotifyAutoFlavorModeChanged();

        StatusText = AutoFlavorStatusText(_autoFlavorSuggestion);
    }

    /// <summary>
    /// 当前方向要不要人声检测：只有男声/女声方向接模型，其余返回 null（全程没有标记）。
    /// 模型只尝试加载一次；没文件、加载失败都退回 null，结果就是「没测到人声」，
    /// 自动风味照常按曲线走 —— 附加功能坏了不能把主流程拖下去。
    /// </summary>
    private IVoiceDetector? VoiceDetectorForCurrentDirection()
    {
        var info = AutoFlavorDirections.Get(_uiState.AutoFlavorDirection);
        if (info.VoiceBias == AutoFlavorVoiceBias.None)
        {
            return null;
        }

        if (_voiceDetectorProbed)
        {
            return _voiceDetector;
        }

        _voiceDetectorProbed = true;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "silero_vad.onnx");
            if (File.Exists(path))
            {
                _voiceDetector = new SileroVoiceDetector(path);
            }
            else
            {
                _voiceDetectorError = "模型文件不在";
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _voiceDetectorError = ex.Message;
            _voiceDetector = null;
        }

        return _voiceDetector;
    }

    /// <summary>
    /// 暂停后状态栏的一句话。人声方向多一句检测结果：把「加了多少」写出来，
    /// 免得用户以为是曲线自己变了；其余方向保持和以前一模一样。
    /// </summary>
    private string AutoFlavorStatusText(AutoFlavorSuggestion suggestion)
    {
        if (!suggestion.HasResult)
        {
            return suggestion.Reason ?? "还没有足够的数据。";
        }

        var text = $"已暂停，{suggestion.UsedFrames} 帧参与计算。可以对比、应用，或者继续录制。";
        var info = AutoFlavorDirections.Get(_uiState.AutoFlavorDirection);
        if (info.VoiceBias == AutoFlavorVoiceBias.None)
        {
            return text;
        }

        if (_autoFlavor.VoiceDetector is null)
        {
            var why = string.IsNullOrWhiteSpace(_voiceDetectorError)
                ? "人声检测不可用"
                : $"人声检测不可用（{_voiceDetectorError}）";
            return text + " " + why + "，按曲线生成。";
        }

        return suggestion.VoiceBoostDb > 0.01
            ? $"{text} {info.Label}固定补 {suggestion.VoiceBoostDb:0.0} dB"
                + (suggestion.VoiceRatio > 0.01 ? $"（测到人声 {suggestion.VoiceRatio * 100:0}%）" : string.Empty)
            : text + " 按曲线生成。";
    }

    private void ResumeAutoFlavor()
    {
        _autoFlavorPhase = AutoFlavorPhase.Recording;
        _autoFlavor.VoiceDetector = VoiceDetectorForCurrentDirection();
        _autoFlavor.Start();
        _autoFlavorAudio = FlavorSet.CreateDefault();
        _autoFlavorDisplay = _autoFlavorSuggestion?.Flavors;
        _autoFlavorCompareFlat = false;
        _autoFlavorFrameTimer!.Start();
        _autoFlavorRefreshTimer!.Start();
        SchedulePreview();
        NotifyAutoFlavorModeChanged();
        StatusText = "继续录制：接着上一次的数据往下记。";
    }

    private void PumpAutoFlavorFrame()
    {
        if (_autoFlavorPhase != AutoFlavorPhase.Recording)
        {
            return;
        }

        _autoFlavorSessionMs += AutoFlavorRecorder.FrameMs;
        _autoFlavor.CaptureFrame(_autoFlavorSessionMs);

        if (!_autoFlavor.LimitReached)
        {
            return;
        }

        PauseAutoFlavor();
        StatusText = "内存到顶，已自动停止录制；已有数据照样能出建议。";
    }

    /// <summary>
    /// 拖动方向 / 强度，或每 500 ms 实时刷新。
    /// 录制中只更新图（声音是校准链），暂停时连声音一起更新。
    /// </summary>
    private void RefreshAutoFlavor(bool force = false)
    {
        if (!IsAutoFlavorMode)
        {
            return;
        }

        if (_autoFlavorPhase == AutoFlavorPhase.Paused && !force)
        {
            return;
        }

        var suggestion = ComputeAutoFlavorSuggestion();
        _autoFlavorSuggestion = suggestion;
        if (_autoFlavorPhase == AutoFlavorPhase.Paused)
        {
            // 拖强度会重算补偿，状态栏那句数字跟着变，不然会停在旧值上。
            StatusText = AutoFlavorStatusText(suggestion);
            if (!_autoFlavorCompareFlat)
            {
                _autoFlavorAudio = suggestion.HasResult ? suggestion.Flavors : FlavorSet.CreateDefault();
                _autoFlavorDisplay = _autoFlavorAudio;
            }
        }
        else
        {
            _autoFlavorDisplay = suggestion.HasResult ? suggestion.Flavors : FlavorSet.CreateDefault();
        }

        OnPropertyChanged(nameof(DisplayFlavors));
        OnPropertyChanged(nameof(ShowAutoFlavorResult));
        OnPropertyChanged(nameof(AutoFlavorVoiceHint));
        OnPropertyChanged(nameof(ShowAutoFlavorVoiceHint));
    }

    private AutoFlavorSuggestion ComputeAutoFlavorSuggestion()
    {
        if (_autoFlavor.FrameCount < AutoFlavorAnalyzer.MinFrames)
        {
            return AutoFlavorSuggestion.None(
                $"已录 {_autoFlavor.FrameCount} 帧，至少要 {AutoFlavorAnalyzer.MinFrames} 帧",
                _autoFlavor.FrameCount);
        }

        var set = new EqLayerSet(_mainDraft.Current, _bassDraft.Current);
        set.TryMerge(IsBassLayerEnabled, FlavorSet.CreateDefault(), out var merged, out _);
        var gains = AutoFlavorAnalyzer.EqGainByBand(merged);

        return AutoFlavorAnalyzer.Estimate(
            _autoFlavor,
            _uiState.AutoFlavorDirection,
            _uiState.AutoFlavorStrength,
            gains);
    }

    private void ExitAutoFlavorMode()
    {
        _autoFlavorPhase = AutoFlavorPhase.Off;
        _autoFlavor.Pause();
        _autoFlavor.Clear();
        _autoFlavorSessionMs = 0;
        _autoFlavorSuggestion = null;
        _autoFlavorCompareFlat = false;
        _spectrumCapture.AutoFlavor = null;
        _autoFlavor.VoiceDetector = null;

        _autoFlavorFrameTimer?.Stop();
        _autoFlavorRefreshTimer?.Stop();
        _autoFlavorCountdownTimer?.Stop();
        _autoFlavorStartTimer?.Stop();

        // 下拉在自动风味里是收起来的，选不了；退出时把进入前的选择原样放回去。
        // 进模式时没选，退出后仍然没选——不替用户挑一条。
        if (_selectionBeforeAuto == ToneBands.AllFlavorsIndex)
        {
            SelectedBand = ToneBands.AllOption;
        }
        else if (_selectionBeforeAuto >= 0 && _selectionBeforeAuto < ToneBands.All.Count)
        {
            SelectedBand = ToneBands.All[_selectionBeforeAuto];
        }
        else
        {
            SelectedBand = null;
            SelectedFlavorIndex = -1;
        }

        NotifyAutoFlavorModeChanged();
    }

    private void NotifyAutoFlavorModeChanged()
    {
        OnPropertyChanged(nameof(IsAutoFlavorMode));
        OnPropertyChanged(nameof(ShowManualFlavorGroup));
        OnPropertyChanged(nameof(ShowAutoFlavorControls));
        OnPropertyChanged(nameof(ShowAutoFlavorResult));
        OnPropertyChanged(nameof(AutoFlavorButtonText));
        OnPropertyChanged(nameof(DisplayFlavors));
        OnPropertyChanged(nameof(FlavorHandlesEditable));
        OnPropertyChanged(nameof(FlavorLabelsVisible));
        OnPropertyChanged(nameof(FlavorNamingOverrideKey));
        OnPropertyChanged(nameof(PreviewIncludeBass));
        OnPropertyChanged(nameof(ShowDraftActions));
        OnPropertyChanged(nameof(AutoFlavorVoiceHint));
        OnPropertyChanged(nameof(ShowAutoFlavorVoiceHint));
    }

    private void EnsureAutoFlavorTimers()
    {
        if (_autoFlavorFrameTimer is not null)
        {
            return;
        }

        _autoFlavorFrameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutoFlavorRecorder.FrameMs) };
        _autoFlavorFrameTimer.Tick += (_, _) => PumpAutoFlavorFrame();

        _autoFlavorRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _autoFlavorRefreshTimer.Tick += (_, _) => RefreshAutoFlavor();

        _autoFlavorCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoFlavorCountdownTimer.Tick += (_, _) => TickAutoFlavorCountdown();

        _autoFlavorStartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoFlavorStartTimer.Tick += (_, _) => StartAutoFlavorRecording();
    }

    /// <summary>系统可用物理内存（字节）。拿不到就返回 <see cref="long.MaxValue"/>，只按 1 GB 上限走。</summary>
    private static long AvailableMemory()
    {
        try
        {
            var status = new MemoryStatusEx();
            return GlobalMemoryStatusEx(status) ? (long)status.ullAvailPhys : long.MaxValue;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return long.MaxValue;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);
}
