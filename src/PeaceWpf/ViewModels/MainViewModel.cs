using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeaceWpf.Core.Audio;
using PeaceWpf.Core.AutoEq;
using PeaceWpf.Core.Calibration;
using PeaceWpf.Core.Editing;
using PeaceWpf.Core.Integration;
using PeaceWpf.Core.Model;
using PeaceWpf.Theme;

namespace PeaceWpf.ViewModels;

/// <summary>
/// 主界面模型（电竞调音台）。
///
/// 三个模式：均衡是左右音量滑块；左右和低音是两条 EQ 曲线，各自对应一个 .peace 预设文件。
/// 保存时把两层分别写回各自的 .peace（Peace 能直接打开），再把合并结果写成 peace.txt 让它生效。
/// 叠加开关一改就保存，不弹询问。
///
/// 程序不依赖 Peace 运行：只认 EqualizerAPO 的 config 目录。
/// 唯一需要确认的是 config.txt 里挂了 peace.txt，没挂就在界面上点一下接上。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly UiState _uiState;
    private readonly EqDraft _mainDraft;
    private readonly EqDraft _bassDraft;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _qualityTimer;
    private readonly LoopbackCaptureService _spectrumCapture = new();
    private readonly DispatcherTimer _spectrumTimer;
    private readonly DispatcherTimer _liveSaveTimer;

    /// <summary>非独立模式下把推子推给正在运行的 Peace；独立模式下不用它。</summary>
    private readonly PeaceSync _peaceSync = new();

    /// <summary>试听：把草稿写进 peace.txt 的节流器。手指停 150 ms 才写一次，拖动过程中不刷盘。</summary>
    private readonly DispatcherTimer _previewTimer;

    /// <summary>试听前 peace.txt 原本的内容。取消时用它还原，声音才会退回去。</summary>
    private string? _previewOriginal;

    /// <summary>试听前 peace.txt 在不在；原本不在的话，取消时就把它删掉，别留个空文件。</summary>
    private bool _previewOriginalExisted;

    /// <summary>这次试听是不是已经写过盘（没写过就不用还原）。</summary>
    private bool _previewActive;

    private bool _previewWriting;
    private bool _previewPending;

    /// <summary>取消时先把试听关掉：还原过程中属性回写会再触发一次试听，得让它闭嘴。</summary>
    private bool _previewSuspended;

    /// <summary>
    /// 自动EQ 的「试听扫频」正临时把低音层叠进 peace.txt。
    ///
    /// 这段期间主窗口的预览必须停手：它按「编辑左右时不叠低音」的规则写盘，
    /// 一旦插进来，扫频听到的就是半截叠加、半截没有。
    /// </summary>
    private bool _sweepPreviewActive;

    /// <summary>试听扫频前的 peace.txt 内容与存在性；扫频放完要原样还回去。</summary>
    private string? _sweepBackup;
    private bool _sweepBackupExisted;

    /// <summary>试听写入与还原互斥，免得“还原”被上一次还在飞的试听写盖掉。</summary>
    private readonly SemaphoreSlim _peaceWriteGate = new(1, 1);
    private string? _previewLastText;
    private DateTime _previewLastAt = DateTime.MinValue;

    /// <summary>最近一次试听写盘的任务；退出编辑时要等它落完，再按勾选重写。</summary>
    private Task _previewWriteTask = Task.CompletedTask;

    private const int PreviewIntervalMs = 150;

    private bool _loading;
    private bool _presetSwitch;

    /// <summary>正在拖动排序：这时下拉里的条目在挪，不能把它当成“用户换了预设”。</summary>
    private bool _presetReorder;
    private readonly bool _elevated = IsRunningElevated();
    private double _savedPreAmpDb;
    private double _savedBalance;
    private bool _savedBassLayerEnabled = true;
    private bool _qualityPending;
    private DateTime _qualityAt = DateTime.MinValue;
    private bool _warnedAboutFilters;
    private string? _configDirectory;
    private string? _configFilePath;
    private string _mainConfigName = "EsportsMain.peace";
    private string _bassConfigName = "EsportsBass.peace";
    private string _deviceLine = "Device: all";

    // ---- 调试信息：动作 / 错误各留一段最近历史，供设置页「调试信息」和复制用 ----
    private readonly List<(DateTime At, string Text)> _actionLog = new();
    private readonly List<(DateTime At, string Text)> _errorLog = new();
    private const int DebugLogLimit = 20;

    private string _statusText = "正在载入配置…";

    /// <summary>
    /// 调试框「动作」类：最近一次操作的结果。每改一次都追加进内部历史，
    /// 文案里像失败的会同时记一笔到错误历史。
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (!SetProperty(ref _statusText, value))
            {
                return;
            }

            RememberStatus(value);
        }
    }

    /// <summary>找到了 EqualizerAPO 的 config 目录，读写都有落点。</summary>
    [ObservableProperty]
    private bool _isApoFound;

    /// <summary>
    /// <c>config.txt</c> 里有没有 <c>Include: peace.txt</c>。没有的话，写进去的滤波器引擎根本不读，
    /// 界面一切正常、声音一点不变 —— 这种沉默失败最坑，所以单独拎出来在顶栏提示。
    /// </summary>
    [ObservableProperty]
    private bool _isApoIncluded;

    /// <summary>EqualizerAPO 的 config 目录能不能写。写不了时保存一定失败，界面上要提前说。</summary>
    [ObservableProperty]
    private bool _isConfigWritable = true;

    /// <summary>
    /// 独立模式：打开时写自己的 <c>easyeq.txt</c>，Peace 写它的 <c>peace.txt</c>，互不覆盖；
    /// 关闭（默认）时写 <c>peace.txt</c>，Peace 在跑就把推子同步过去。
    /// </summary>
    public bool IndependentMode => _uiState.IndependentMode;

    /// <summary>当前模式实际写的配置文件名。</summary>
    public string ConfigFileName => EapoConfig.FileNameFor(_uiState.IndependentMode);

    [ObservableProperty]
    private double _preAmpDb;

    [ObservableProperty]
    private double _gainRangeDb = 15;

    [ObservableProperty]
    private MainMode _mode = MainMode.None;

    [ObservableProperty]
    private double _balance;

    [ObservableProperty]
    private bool _isBassLayerEnabled = true;

    [ObservableProperty]
    private bool _hasNonPeakingFilters;

    [ObservableProperty]
    private int _nonPeakingFilterCount;

    [ObservableProperty]
    private string _selectedMainPreset = "EsportsMain.peace";

    [ObservableProperty]
    private string _selectedBassPreset = "EsportsBass.peace";

    /// <summary>曲线底部那条实时频谱的开关。关掉就停采集，CPU 回落。</summary>
    [ObservableProperty]
    private bool _isSpectrumEnabled = true;

    /// <summary>无延迟防爆音：链路末尾挂 ClipOnly3，只在快破音时削峰。</summary>
    [ObservableProperty]
    private bool _isPeakGuardEnabled;

    /// <summary>当前这一帧频谱（dB，按对数频率等分）。null 时曲线不画频谱。</summary>
    [ObservableProperty]
    private float[]? _spectrum;

    /// <summary>频谱的状态说明，落在状态栏右边；正常时是空的。</summary>
    [ObservableProperty]
    private string _spectrumHint = string.Empty;

    /// <summary>右侧电平表的当前峰值（dB）。-90 就是没有声音。</summary>
    [ObservableProperty]
    private double _peakDb = -90;

    public MainViewModel()
    {
        _uiState = UiStateStore.Load();
        _gainRangeDb = _uiState.GainRangeDb;
        _mainDraft = new EqDraft();
        _bassDraft = new EqDraft();
        _mainDraft.Changed += (_, _) => RaiseDraftState();
        _bassDraft.Changed += (_, _) => RaiseDraftState();
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _qualityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _qualityTimer.Tick += (_, _) => FlushQualityIfIdle();
        _qualityTimer.Start();

        // 16 ms ≈ 60 fps，和显示刷新对齐；一帧 FFT 约 0.2 ms，远没到瓶颈。
        _spectrumTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _spectrumTimer.Tick += (_, _) => PumpSpectrum();
        _isSpectrumEnabled = _uiState.SpectrumEnabled;
        _isPeakGuardEnabled = _uiState.PeakGuardEnabled;

        // 平衡和增益是“拖完就生效”，停手 450 ms 再写一次文件，拖动过程中不反复写盘。
        _liveSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _liveSaveTimer.Tick += (_, _) => FlushLiveSave();

        // 试听：曲线和开关的改动也要立刻出声，所以同样走“写 peace.txt”，只是节流更短。
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs) };
        _previewTimer.Tick += (_, _) => OnPreviewTick();

        _savedBalance = Math.Clamp(_uiState.Balance, -1, 1);
        _balance = _savedBalance;
        _savedBassLayerEnabled = _uiState.BassLayerEnabled;
        _isBassLayerEnabled = _savedBassLayerEnabled;
        _mainConfigName = string.IsNullOrWhiteSpace(_uiState.MainConfig) ? "EsportsMain.peace" : _uiState.MainConfig;
        _bassConfigName = string.IsNullOrWhiteSpace(_uiState.BassConfig) ? "EsportsBass.peace" : _uiState.BassConfig;
        _selectedMainPreset = _mainConfigName;
        _selectedBassPreset = _bassConfigName;
    }

    /// <summary>有脏改动时问用户“保存 / 放弃 / 留下”。由窗口注入。</summary>
    public Func<string, string, DirtyChoice>? ConfirmDirty { get; set; }

    /// <summary>需要让用户知道、但不需要做决定的消息。</summary>
    public Action<string, string>? Notify { get; set; }

    /// <summary>问用户要一行字（重命名 / 复制预设）。返回 null 表示取消。由窗口注入。</summary>
    public Func<string, string, string, string?>? PromptText { get; set; }

    /// <summary>破坏性操作前要用户点头（删预设）。由窗口注入。</summary>
    public Func<string, string, bool>? ConfirmAction { get; set; }

    /// <summary>预设目录（设置窗口用）。</summary>
    public string ConfigDirectory => _configDirectory ?? "（没找到 EqualizerAPO 的 config 目录）";

    /// <summary>下拉栏里的预设文件。</summary>
    public ObservableCollection<string> AvailablePresets { get; } = new();

    private EqDraft ActiveDraft => Mode == MainMode.BassLayer ? _bassDraft : _mainDraft;

    public EqPointList Points => Mode switch
    {
        MainMode.MainLayer => _mainDraft.Current,
        MainMode.BassLayer => _bassDraft.Current,
        _ => new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved).MergedForDisplay(_savedBassLayerEnabled, DisplayFlavors),
    };

    /// <summary>主层当前草稿。自动EQ 预览期间草稿里就是测得的曲线。</summary>
    public EqPointList MainPoints => _mainDraft.Current;

    /// <summary>低音层当前草稿。</summary>
    public EqPointList BassPoints => _bassDraft.Current;

    /// <summary>
    /// 曲线图用的预增益。自动EQ 预览期间传 0：图上直接画矫正后的形状，
    /// 不再整体平移；真正的预增益只体现在右侧推子（仍绑定 <see cref="PreAmpDb"/>）。
    /// </summary>
    public double GraphPreAmpDb => _calibrationPreview is null ? PreAmpDb : 0;

    /// <summary>
    /// 编辑时垫在底下的那条参考线：编辑低音时叠上左右，方便边看整体边调；
    /// 编辑左右时什么都不叠（不掺低音），退出编辑后由合并后的整条曲线代替。
    /// </summary>
    public EqPointList? ReferencePoints => Mode switch
    {
        MainMode.BassLayer => _mainDraft.Current,
        MainMode.MainLayer => _bassDraft.Current,
        _ => null,
    };

    public bool IsEditing => Mode != MainMode.None;

    /// <summary>
    /// 试听时低音层叠不叠加：编辑低音时一律把左右叠进来（听的是整体），
    /// 编辑左右时一律不叠低音（只听正在改的这条），退出全部编辑后才回到「叠加低音修正」勾选设置。
    /// </summary>
    public bool PreviewOverlayBass => Mode switch
    {
        MainMode.BassLayer => true,
        MainMode.MainLayer => false,
        _ => IsBassLayerEnabled,
    };

    public bool IsMainLayerMode => Mode == MainMode.MainLayer;

    public bool IsBassLayerMode => Mode == MainMode.BassLayer;

    public bool IsEditable => Mode is MainMode.MainLayer or MainMode.BassLayer;

    /// <summary>风味只在「左右」层可调：它绑的是左右预设，低音层不显示。</summary>
    public bool IsFlavorEditable => IsEditable;

    /// <summary>下拉里的九条类型。</summary>
    /// <summary>下拉里的九条类型，最前面多一个「全部」（把九条控制点一起放出来）。</summary>
    public IReadOnlyList<ToneBand> FlavorChoices { get; } = new[] { ToneBands.AllOption }.Concat(ToneBands.All).ToArray();

    /// <summary>「全部」在下拉里对应的特殊下标：让九条手柄都画出来、都能拖。</summary>
    public const int AllFlavorsIndex = ToneBands.AllFlavorsIndex;

    /// <summary>当前左右预设的风味：就是左右草稿里那一半，和点共用撤回栈、保存和取消。</summary>
    public FlavorSet Flavors => _mainDraft.CurrentFlavors;

    /// <summary>
    /// 某个预设记住的风味（只读，给预设下拉的缩略图用）。没记过就是全平的一套，
    /// 缩略图据此不画彩线——和真实行为一致，没风味就是没风味。
    /// </summary>
    public FlavorSet PresetFlavors(string preset) =>
        _uiState.TryGetMainFlavors(preset, out var flavors) ? flavors : FlavorSet.CreateDefault();

    /// <summary>
    /// 目标左右预设是不是已经有一套非平的风味。覆盖前要问一句：风味只存在本地状态里，
    /// 被盖掉就没有第二份可以还原。
    /// </summary>
    public bool TargetPresetHasFlavors(string preset) =>
        _uiState.TryGetMainFlavors(preset, out var flavors) && !flavors.IsFlat;

    /// <summary>
    /// 把一套风味复制到另一个左右预设名下：只写风味，不切配置、不动曲线，也不碰当前草稿。
    /// 目标预设要等下次被选中才生效——peace.txt 只在它成为当前配置时才会写。
    /// </summary>
    public bool CopyFlavorsTo(string preset, FlavorSet flavors)
    {
        ArgumentNullException.ThrowIfNull(flavors);

        if (string.IsNullOrWhiteSpace(preset)
            || string.Equals(preset, _mainConfigName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _uiState.SetMainFlavors(preset, flavors);
        PersistState();
        return true;
    }

    /// <summary>九条风味的颜色，顺序与 <see cref="ToneBands.All"/> 一致。</summary>
    public IReadOnlyList<Brush> FlavorBrushes => TonePalette.Brushes;

    /// <summary>下拉选中的类型。</summary>
    [ObservableProperty]
    private ToneBand? _selectedBand = ToneBands.Default;

    /// <summary>曲线上选中的那条风味下标；-1 表示没选。</summary>
    [ObservableProperty]
    private int _selectedFlavorIndex = ToneBands.IndexOf(ToneBands.Default.Key);

    public bool IsReadOnly => !IsEditing;

    /// <summary>配置目录不可写时挂警示条。</summary>
    public bool IsConfigReadOnly => !IsConfigWritable;

    /// <summary>顶栏那条红字：不可写、且分得清「没提权」和「提了权仍不可写」。</summary>
    public string ConfigWarning
    {
        get
        {
            if (IsConfigWritable)
            {
                return string.Empty;
            }

            if (ConfigPermission.IsLowIntegrity())
            {
                return "配置目录不可写 · 程序跑在低完整性下，点「检查权限」→「以管理员身份重启」";
            }

            return _elevated
                ? "配置目录不可写（已是管理员）· 点右边「检查权限」看是哪一类原因"
                : "配置目录不可写 · 点右边「检查权限」：可能是没提权，也可能是文件被占着";
        }
    }

    public bool HasConfigWarning => !IsConfigWritable;

    /// <summary>能找到 config 目录、但入口文件没挂上 peace.txt：这时候要人点一下。</summary>
    public bool HasApoWarning => IsApoFound && !IsApoIncluded;

    public string ApoWarning => HasApoWarning
        ? $"EqualizerAPO 还没加载 {ConfigFileName}（config.txt 里缺 Include），点右边接上"
        : string.Empty;

    /// <summary>
    /// 只有曲线改动、叠加开关、增益算“未保存”。增益在任何时候都算：
    /// 它属于「左右」这份配置，拖了就要按保存或取消，和曲线是同一套规矩。
    /// 平衡仍是编辑态才算，维持原来的习惯。
    /// </summary>
    public bool IsDirty =>
        _mainDraft.IsDirty
        || _bassDraft.IsDirty
        || IsBassLayerEnabled != _savedBassLayerEnabled
        || Math.Abs(PreAmpDb - _savedPreAmpDb) > 0.001
        || (IsEditing && Math.Abs(Balance - _savedBalance) > 0.0005);

    /// <summary>
    /// 当前正在编辑的这一层有没有没保存的改动。
    /// 左右 / 低音各有一份撤回记录，撤回、还原、保存、取消跟着当前层显示：
    /// 换到没动过的那层，它们就该收起来，而不是被另一层的脏标记顶出来。
    /// </summary>
    public bool IsActiveDraftDirty => Mode switch
    {
        MainMode.MainLayer => _mainDraft.IsDirty
            || IsBassLayerEnabled != _savedBassLayerEnabled
            || Math.Abs(PreAmpDb - _savedPreAmpDb) > 0.001
            || Math.Abs(Balance - _savedBalance) > 0.0005,
        MainMode.BassLayer => _bassDraft.IsDirty,
        _ => IsDirty,
    };

    /// <summary>最近一次改动落在哪一层：撤回/重做优先跟着它走，编低音时改风味也能退。</summary>
    private EqDraft? _lastEditDraft;

    /// <summary>撤回/重做作用于哪一层：最后改过的那层，没有就跟着当前层。</summary>
    private EqDraft UndoTarget => _lastEditDraft ?? ActiveDraft;

    public bool CanUndo => IsEditable && UndoTarget.CanUndo;

    public bool CanRedo => IsEditable && UndoTarget.CanRedo;

    public string PointsSummary =>
        $"左右 {_mainDraft.Current.Count} 点 · 低音 {_bassDraft.Current.Count} 点";

    /// <summary>当前真正生效的点数：编辑中按本层的试听规则，退出编辑后按勾选。</summary>
    public int MergedPointCount
    {
        get
        {
            var layers = new EqLayerSet(_mainDraft.Current, _bassDraft.Current);
            return layers.TryMerge(PreviewOverlayBass, Flavors, out var merged, out _) ? merged.Count : 0;
        }
    }

    /// <summary>合并后超过 Peace 的滑杆数时提示；不阻止保存。</summary>
    public string PeaceOverflowHint =>
        MergedPointCount > EqLimits.PeaceDisplayBands
            ? $"合并 {MergedPointCount} 个点 · Peace 只显示前 {EqLimits.PeaceDisplayBands} 个，声音完整生效"
            : string.Empty;

    public string BalanceSummary
    {
        get
        {
            var value = Math.Clamp(Balance, -1, 1);
            if (Math.Abs(value) < 0.005)
            {
                return "居中";
            }

            var side = value < 0 ? "左" : "右";
            var percent = Math.Abs(value) * 100;
            var decoded = Math.Abs(value) * EapoConfig.BalanceMaxDb;
            return $"偏{side} {percent:0}% · 另一侧 -{decoded:0.0} dB";
        }
    }

    public string NonPeakingHint => NonPeakingFilterCount == 0
        ? string.Empty
        : $"有 {NonPeakingFilterCount} 个点模型表达不了的滤波器（低通 / 高通 / 带通等），保存会写成 PK";

    // ---- 调试信息的分类汇总：曲线 / 环境 / 警告 / 错误；动作走 StatusText ----

    /// <summary>调试框第一类「曲线」：两层各自的预设名与点数、预增益、低音是否叠加。</summary>
    public string CurveSummary =>
        $"左右 {_mainConfigName} · {_mainDraft.Current.Count} 点"
        + $" · 低音 {_bassConfigName} · {_bassDraft.Current.Count} 点"
        + $" · 增益 {PreAmpDb:0.0} dB · 低音{(IsBassLayerEnabled ? "已叠加" : "未叠加")}";

    /// <summary>环境类：显示在「连接状态」那一行下面，不进调试框。回答「为什么保存了没效果」。</summary>
    public string EnvironmentSummary =>
        $"{ConfigDirectory} · {(IsConfigWritable ? "可写" : "只读")}"
        + $" · {(IsApoIncluded ? "已 Include" : "没 Include")}"
        + $" · {(_elevated ? "已提权" : "普通权限")}"
        + $" · 写入 {ConfigFileName}";

    /// <summary>调试框「动作」类：最近几条操作回执，带时间。</summary>
    public string ActionsSummary => FormatDebugLog(_actionLog);

    /// <summary>调试框「警告」类：当前持续存在、但还不算失败的状况。</summary>
    public string WarningsSummary
    {
        get
        {
            var items = new List<string>();
            if (!string.IsNullOrWhiteSpace(PeaceOverflowHint))
            {
                items.Add(PeaceOverflowHint);
            }

            if (!string.IsNullOrWhiteSpace(NonPeakingHint))
            {
                items.Add(NonPeakingHint);
            }

            if (!string.IsNullOrWhiteSpace(SpectrumHint))
            {
                items.Add(SpectrumHint);
            }

            if (HasConfigWarning)
            {
                items.Add(ConfigWarning);
            }

            if (HasApoWarning)
            {
                items.Add(ApoWarning);
            }

            return items.Count == 0 ? "（无）" : string.Join(Environment.NewLine, items);
        }
    }

    /// <summary>调试框「错误」类：最近几条失败。</summary>
    public string ErrorsSummary => FormatDebugLog(_errorLog);

    /// <summary>「复制调试信息」：五类一次带走，粘出去就能定位。</summary>
    public string BuildDebugReport() =>
        string.Join(
            Environment.NewLine,
            $"== 环境 == {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            EnvironmentSummary,
            string.Empty,
            "== 曲线 ==",
            CurveSummary,
            string.Empty,
            "== 动作 ==",
            ActionsSummary,
            string.Empty,
            "== 警告 ==",
            WarningsSummary,
            string.Empty,
            "== 错误 ==",
            ErrorsSummary);

    private static string FormatDebugLog(List<(DateTime At, string Text)> log)
    {
        if (log.Count == 0)
        {
            return "（无）";
        }

        var start = Math.Max(0, log.Count - 6);
        return string.Join(
            Environment.NewLine,
            log.Skip(start).Select(entry => $"{entry.At:HH:mm:ss} {entry.Text}"));
    }

    /// <summary>记一条动作；像失败的再记一份到错误历史。</summary>
    private void RememberStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var entry = (DateTime.Now, text.Trim());
        _actionLog.Add(entry);
        if (_actionLog.Count > DebugLogLimit)
        {
            _actionLog.RemoveAt(0);
        }

        if (LooksLikeError(text))
        {
            _errorLog.Add(entry);
            if (_errorLog.Count > DebugLogLimit)
            {
                _errorLog.RemoveAt(0);
            }

            OnPropertyChanged(nameof(ErrorsSummary));
        }

        OnPropertyChanged(nameof(ActionsSummary));
    }

    private static bool LooksLikeError(string text) =>
        text.Contains("失败", StringComparison.Ordinal)
        || text.Contains("异常", StringComparison.Ordinal)
        || text.Contains("错误", StringComparison.Ordinal)
        || text.Contains("写不进", StringComparison.Ordinal)
        || text.Contains("读不了", StringComparison.Ordinal)
        || text.Contains("接不上", StringComparison.Ordinal)
        || text.Contains("没生效", StringComparison.Ordinal)
        || text.Contains("不可用", StringComparison.Ordinal);

    public string BassLayerSummary => IsBassLayerEnabled ? "已叠加" : "未叠加";

    public async Task InitializeAsync()
    {
        try
        {
            _configDirectory = EapoConfig.FindConfigDirectory(_uiState.ConfigDirectoryOverride);
            _configFilePath = _configDirectory is null ? null : Path.Combine(_configDirectory, ConfigFileName);
            IsConfigWritable = _configDirectory is not null && EapoConfig.CanWrite(_configDirectory);
            OnPropertyChanged(nameof(IsConfigReadOnly));
            WriteStartupReport();

            CleanupStaleTutorialPresets();
            RefreshPresets();
            RefreshApoState();

            var data = await Task.Run(TryReadConfig).ConfigureAwait(false);
            var main = LoadLayer(_mainConfigName);
            var bass = LoadLayer(_bassConfigName);

            Post(() =>
            {
                ApplyLoaded(data, main, bass);
                AutoSwitchPeaceToMix();
            });
        }
        catch (Exception ex)
        {
            Post(() => StatusText = "载入失败：" + ex.GetType().Name + " " + ex.Message);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsEditing)
        {
            StatusText = "正在编辑，先保存或取消再重新读取。";
            return;
        }

        StatusText = "正在重新读取…";
        var data = await Task.Run(TryReadConfig).ConfigureAwait(true);
        ApplyLoaded(data, LoadLayer(_mainConfigName), LoadLayer(_bassConfigName));
    }

    [RelayCommand]
    private Task ToggleMainLayerAsync() => RequestModeAsync(MainMode.MainLayer);

    [RelayCommand]
    private Task ToggleBassLayerAsync() => RequestModeAsync(MainMode.BassLayer);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!IsDirty)
        {
            return;
        }

        if (!await SaveCoreAsync())
        {
            return;
        }

        // 保存成功的那句话先留住：退出编辑收尾时会改状态栏，成功了再换回来。
        var savedMessage = StatusText;

        // 保存 = 这次编辑结束：模式收回只读，保存 / 取消按钮跟着消失。
        // 这时已经不脏，LeaveModeAsync 不会再问保存还是放弃，只负责退模式和收声音。
        if (IsEditing && await LeaveModeAsync())
        {
            StatusText = savedMessage;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!IsDirty)
        {
            return;
        }

        CancelEdit();

        // 取消 = 连编辑模式一起收掉。只把草稿丢回去的话，按钮会因为「不脏」而消失，
        // 曲线和推子却还挂在编辑态，看着像取消了、其实没退出来。
        // 顺序不能反：先回退增益再退模式，否则回退那一下会把只读态重新带进编辑。
        var settled = await LeaveModeAsync();

        if (settled)
        {
            StatusText = "已放弃改动，回到上次保存的状态。";
        }
    }

    [RelayCommand]
    private void Undo()
    {
        if (!IsEditable)
        {
            return;
        }

        FlushQualityNow();
        var draft = UndoTarget;
        var undone = draft.Undo();
        if (!undone && !ReferenceEquals(draft, ActiveDraft))
        {
            undone = ActiveDraft.Undo();
        }

        if (undone)
        {
            StatusText = "已撤回一步。";
        }
    }

    [RelayCommand]
    private void Redo()
    {
        if (!IsEditable)
        {
            return;
        }

        FlushQualityNow();
        var draft = UndoTarget;
        var redone = draft.Redo();
        if (!redone && !ReferenceEquals(draft, ActiveDraft))
        {
            redone = ActiveDraft.Redo();
        }

        if (redone)
        {
            StatusText = "已重做一步。";
        }
    }

    // ---- 风味：独立叠加层，九条固定推子 ----

    /// <summary>曲线手柄点中某条风味：下拉跟着切到它。</summary>
    public void SelectFlavor(int index)
    {
        if (index == AllFlavorsIndex)
        {
            SelectedFlavorIndex = AllFlavorsIndex;
            return;
        }

        if (index >= 0 && index < ToneBands.All.Count)
        {
            SelectedFlavorIndex = index;
        }
    }

    /// <summary>右键风味下拉用：回到「没选」，主曲线这才恢复可调。</summary>
    public void ClearFlavorSelection()
    {
        SelectedBand = null;
        SelectedFlavorIndex = -1;
        StatusText = "风味：未选择。主曲线可拖动、点空白可加点。";
    }

    /// <summary>拖动中改频率和增益：只改草稿（立刻出声），松手才压撤回栈。</summary>
    public void PreviewFlavor(int index, double frequencyHz, double gainDb) =>
        ApplyFlavor(
            Flavors.MoveFrequency(index, frequencyHz).WithGain(index, gainDb),
            commit: false);

    /// <summary>松手：整段拖动算一步撤回，顺手报一次完整数值。</summary>
    public void CommitFlavor(int index)
    {
        _mainDraft.Commit();

        if (index < 0 || index >= ToneBands.All.Count)
        {
            return;
        }

        var band = ToneBands.All[index];
        var state = _mainDraft.CurrentFlavors[index];
        StatusText = $"{band.Label} · {EqFormat.Frequency(state.FrequencyHz)} · "
            + $"{state.GainDb:+0.0;-0.0;0.0} dB · Q {state.Quality:0.00}";
    }

    /// <summary>滚轮改 Q。</summary>
    public void ChangeFlavorQuality(int index, double quality)
    {
        if (index < 0 || index >= ToneBands.All.Count)
        {
            return;
        }

        var next = Flavors.SetQuality(index, quality);
        if (next.Equals(Flavors))
        {
            return;
        }

        ApplyFlavor(next, $"{ToneBands.All[index].Label} Q {next[index].Quality:0.00}");
    }

    /// <summary>右键风味手柄：把这一条的增益归零（频率和 Q 保留），算一步撤回。</summary>
    public void ResetFlavor(int index)
    {
        if (index < 0 || index >= ToneBands.All.Count)
        {
            return;
        }

        var band = ToneBands.All[index];
        if (Math.Abs(Flavors[index].GainDb) < 0.005)
        {
            StatusText = $"{band.Label} 本来就是 0 dB。";
            return;
        }

        ApplyFlavor(Flavors.WithGain(index, 0), $"{band.Label} 增益已归零");
    }

    /// <summary>
    /// 换一套风味：改草稿 → 立刻出声 → （commit 时）压一步撤回。
    /// 脏标记、保存/取消、Ctrl+Z 都由草稿统一管，风味和曲线是同一套规矩；
    /// commit=false 用于拖动中的每一帧，松手时再压栈，整段拖动只算一步。
    /// </summary>
    private void ApplyFlavor(FlavorSet next, string? message = null, bool commit = true)
    {
        if (next.Equals(Flavors))
        {
            return;
        }

        _mainDraft.PreviewFlavors(next);

        if (message is not null)
        {
            StatusText = message;
        }

        if (commit)
        {
            _mainDraft.Commit();
            _lastEditDraft = _mainDraft;
        }
    }

    /// <summary>整条曲线抬升 / 压低已经并进右侧增益推子：拖它才是真的平移，见 <see cref="PreAmpDb"/>。</summary>

    // ---- 曲线控件入口 ----

    public int CreatePoint(double frequencyHz) => CreatePoint(frequencyHz, 0);

    /// <summary>
    /// 在曲线空白处建点：落点就是鼠标位置 —— 频率和增益都照实给，Q 由 Core 按邻居自己挑。
    /// </summary>
    public int CreatePoint(double frequencyHz, double gainDb)
    {
        if (!IsEditable)
        {
            return -1;
        }

        FlushQualityNow();
        if (!ActiveDraft.Current.TryAdd(frequencyHz, gainDb, out var list, out var created))
        {
            StatusText = "这个位置离已有的点太近，换一个地方点。";
            return -1;
        }

        ActiveDraft.Preview(list);
        return created.Id;
    }

    public void PreviewPoint(int id, double frequencyHz, double gainDb)
    {
        if (!IsEditable)
        {
            return;
        }

        ActiveDraft.Preview(ActiveDraft.Current.Move(id, frequencyHz, gainDb));
    }

    public void CommitPoint()
    {
        if (!IsEditable)
        {
            return;
        }

        ActiveDraft.Commit();
        _lastEditDraft = ActiveDraft;
    }

    public void DeletePoint(int id)
    {
        if (!IsEditable)
        {
            return;
        }

        FlushQualityNow();
        var next = ActiveDraft.Current.Remove(id);
        if (next is null)
        {
            StatusText = "端点和最后两个点不能删。";
            return;
        }

        ActiveDraft.Preview(next);
        ActiveDraft.Commit();
        _lastEditDraft = ActiveDraft;
    }

    /// <summary>
    /// 右键首尾端点：增益归零。端点不能删，所以右键在它们身上只做这一件事。
    /// </summary>
    public void ResetPoint(int id)
    {
        if (!IsEditable)
        {
            return;
        }

        FlushQualityNow();
        var point = ActiveDraft.Current.Find(id);
        if (point is null)
        {
            return;
        }

        var next = ActiveDraft.Current.Move(id, null, 0);
        if (next.Equals(ActiveDraft.Current))
        {
            StatusText = $"{EqFormat.Frequency(point.FrequencyHz)} 本来就在 0 dB。";
            return;
        }

        ActiveDraft.Preview(next);
        ActiveDraft.Commit();
        _lastEditDraft = ActiveDraft;
        StatusText = $"{EqFormat.Frequency(point.FrequencyHz)} 增益已归零";
    }

    public void ChangePointQuality(int id, double quality)
    {
        if (!IsEditable)
        {
            return;
        }

        var next = ActiveDraft.Current.SetQuality(id, quality);
        if (next.Equals(ActiveDraft.Current))
        {
            return;
        }

        ActiveDraft.Preview(next);
        _qualityPending = true;
        _qualityAt = DateTime.UtcNow;
    }

    // ---- AutoEQ ----

    /// <summary>
    /// 把一份 AutoEQ 等化套到「左右」层：整层换成它的滤波器、预增益跟着设好，
    /// 算成一步可撤回的改动，立刻试听。返回一句给人看的结果。
    ///
    /// 不在「左右」模式会先切过去。左右↔低音切换不询问保存，
    /// 改动留在各自层的撤回栈里；真要保存或放弃，退出全部编辑时才问。
    /// </summary>
    public async Task<string> ApplyAutoEqAsync(AutoEqProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.CanApplyToPoints)
        {
            return "图形 EQ 是卷积曲线，点模型写不出来；换「参数 EQ」或「固定频带 EQ」再套用。";
        }

        if (Mode != MainMode.MainLayer)
        {
            await RequestModeAsync(MainMode.MainLayer);
            if (Mode != MainMode.MainLayer)
            {
                return "还没进到「左右」层：先处理掉未保存的改动，再套一次。";
            }
        }

        FlushQualityNow();

        var points = profile.ToPointList();
        if (points.Count <= 2)
        {
            return "这份等化里没有能用的滤波器。";
        }

        var wanted = profile.PreAmpDb;
        var preAmp = Math.Clamp(wanted, -20, 20);

        _mainDraft.Preview(points);
        _mainDraft.Commit();
        PreAmpDb = Math.Round(preAmp, 1);
        RaiseDerived();

        var clamped = Math.Abs(wanted - preAmp) > 0.05;
        var message = $"已套用 {profile.Device.Name} · {AutoEqTypes.ShortName(profile.Type)} EQ · "
            + $"{points.Count - 2} 个滤波器 · 预增益 {PreAmpDb:+0.0;-0.0;0.0} dB";
        if (clamped)
        {
            message += $"（原值 {wanted:0.0} dB 超出滑杆范围，夹到 ±20）";
        }

        StatusText = message + " · 正在试听，保存才写回预设";
        return StatusText;
    }

    /// <summary>
    /// 把一份 AutoEQ 等化另存成一条新曲线（新的 .peace 预设），名字默认取当前「左右」层的预设名。
    ///
    /// 和 <see cref="ApplyAutoEqAsync"/> 不同：只写一个新文件，不动当前层、不切模式。
    /// 同名不会覆盖已有预设，自动按「名字 2」「名字 3」往后排。
    /// 预增益在 .peace 里没有位置，套用这条曲线时再按滑杆设。
    /// </summary>
    public async Task<string> CreateCurveFromAutoEqAsync(AutoEqProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.CanApplyToPoints)
        {
            return "图形 EQ 是卷积曲线，点模型写不出来；换「参数 EQ」或「固定频带 EQ」再创建。";
        }

        if (_configDirectory is null)
        {
            return "没找到 EqualizerAPO 的配置目录，没法新建曲线。";
        }

        if (PromptText is null)
        {
            return "没有能用的输入框，创建不了。";
        }

        var points = profile.ToPointList();
        if (points.Count <= 2)
        {
            return "这份等化里没有能用的滤波器。";
        }

        var directory = _configDirectory;
        var typed = PromptText("创建到新曲线", "新曲线的名字：", _mainConfigName);
        if (typed is null)
        {
            return "已取消。";
        }

        if (!TryNormalizePresetName(typed, out var name, out var problem))
        {
            Notify?.Invoke("这个名字不能用", problem);
            return "名字没通过：" + problem;
        }

        var final = MakeUniquePresetName(directory, name);
        var path = Path.Combine(directory, final);

        var (ok, error) = await Task.Run(() =>
        {
            var saved = PeaceConfig.Save(path, points.Points, out var failure);
            return (saved, failure);
        });

        if (!ok)
        {
            StatusText = DescribeSaveFailure("创建新曲线失败", path, error);
            return StatusText;
        }

        RefreshPresets();
        PersistState();

        var renamed = string.Equals(final, name, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"（{name} 已存在，改叫 {final}）";

        var message = $"已创建新曲线 {final}{renamed} · {points.Count - 2} 个滤波器";
        if (Math.Abs(profile.PreAmpDb) > 0.05)
        {
            message += $" · 预增益 {profile.PreAmpDb:+0.0;-0.0;0.0} dB 套用时再设";
        }

        StatusText = message;
        return message;
    }

    /// <summary>同名预设已经在了，就按「名字 2」「名字 3」往后排，绝不覆盖现有文件。</summary>
    private static string MakeUniquePresetName(string directory, string name)
    {
        if (!File.Exists(Path.Combine(directory, name)))
        {
            return name;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        for (var index = 2; ; index++)
        {
            var candidate = $"{stem} {index}{PeaceConfig.Extension}";
            if (!File.Exists(Path.Combine(directory, candidate)))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// 把此刻的全部设置打成一个备份对象，供设置窗口导出成 JSON。
    ///
    /// 先 PersistState 把内存里的曲线和开关刷进状态——导出的必须是眼前这一份，
    /// 不能是上一次保存时的旧快照。
    /// </summary>
    public SettingsBackup CaptureBackup()
    {
        PersistState();

        return new SettingsBackup
        {
            Version = typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? string.Empty,
            ExportedAt = DateTimeOffset.Now,
            ConfigDirectory = _configDirectory,
            Mode = Mode.ToString(),
            PreAmpDb = PreAmpDb,
            CalibrateDevice = CalibrateDeviceMemory.Load(),
            UiState = _uiState,
        };
    }

    /// <summary>
    /// 把一份备份灌回当前会话：界面状态、两层曲线、预设名、平衡、频谱开关、预增益、校准设备一起换。
    /// 有没保存的改动时不动——先让用户处置，免得把正在调的东西吞掉。
    /// </summary>
    public string ImportBackup(SettingsBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);

        if (IsEditing)
        {
            return "还有没保存的改动：先保存或取消，再导入。";
        }

        _uiState.CopyFrom(backup.UiState);

        _loading = true;
        try
        {
            _mainConfigName = string.IsNullOrWhiteSpace(_uiState.MainConfig) ? "EsportsMain.peace" : _uiState.MainConfig;
            _bassConfigName = string.IsNullOrWhiteSpace(_uiState.BassConfig) ? "EsportsBass.peace" : _uiState.BassConfig;

            GainRangeDb = _uiState.GainRangeDb;
            IsSpectrumEnabled = _uiState.SpectrumEnabled;
            IsPeakGuardEnabled = _uiState.PeakGuardEnabled;

            _savedBalance = Math.Clamp(_uiState.Balance, -1, 1);
            Balance = _savedBalance;

            _savedBassLayerEnabled = _uiState.BassLayerEnabled;
            IsBassLayerEnabled = _savedBassLayerEnabled;

            PreAmpDb = Math.Round(Math.Clamp(backup.PreAmpDb, EqLimits.MinPreAmpDb, EqLimits.MaxPreAmpDb), 1);
            _savedPreAmpDb = PreAmpDb;

            var layers = _uiState.ToLayerSet();
            if (layers is not null)
            {
                _mainDraft.AcceptSaved(layers.Main);
                _bassDraft.AcceptSaved(layers.Bass);
            }
        }
        finally
        {
            _loading = false;
        }

        CalibrateDeviceMemory.Save(backup.CalibrateDevice);

        RefreshPresets();
        RefreshApoState();
        PersistState();
        RaiseDerived();
        ApplySpectrumSetting();

        StatusText = $"已导入设置：左右 {_uiState.MainPoints.Count} 点（{_mainConfigName}）· "
            + $"低音 {_uiState.BassPoints.Count} 点（{_bassConfigName}）· 预增益 {PreAmpDb:+0.0;-0.0;0.0} dB";
        return StatusText;
    }

    // ---- 自动EQ：扫频 + 麦克风 ----

    /// <summary>
    /// 自动EQ 上次导入的麦克风矫正曲线文件；null 表示不矫正。
    /// 只存路径不存内容：曲线文件可能被用户换掉（换了麦克风、重新下载），每次打开都重新读。
    /// </summary>
    public string? MicCalibrationFile => _uiState.MicCalibrationFile;

    /// <summary>
    /// 记住（或忘掉）麦克风矫正曲线。传 null 表示清除，下次打开自动EQ 不再自动套上。
    /// 不在这里校验文件能不能读：那是窗口打开时的事，这里只负责记路径。
    /// </summary>
    public void SetMicCalibrationFile(string? path)
    {
        _uiState.MicCalibrationFile = string.IsNullOrWhiteSpace(path) ? null : path;
        UiStateStore.Save(_uiState);
    }

    /// <summary>测量期间 peace.txt 原本的内容；null 表示还没进入测量态。</summary>
    private string? _calibrationBackup;

    /// <summary>测量前 peace.txt 在不在；原本不在的话还原时删掉。</summary>
    private bool _calibrationBackupExisted;

    /// <summary>是不是已经把 peace.txt 临时写成平直了。</summary>
    private bool _calibrationFlat;

    /// <summary>标定用的临时 peace.txt 备份：标定写完、测完就还原。</summary>
    private string? _probeBackup;
    private bool _probeBackupExisted;

    /// <summary>
    /// 进入测量态：把 peace.txt 临时写成「什么都没有」。
    ///
    /// 扫频信号也会经过 EqualizerAPO，如果当前层还挂着 EQ，测到的就是「EQ 之后」的声音，
    /// 算出来的补偿会把现有曲线再叠一遍。所以要先把整条链拉平，测完再还原。
    ///
    /// 返回 false 时 measurement 仍然可以做，只是结果里会带着当前的 EQ（界面上要提示）。
    /// </summary>
    public async Task<(bool Flat, string? Message)> BeginCalibrationMeasurementAsync()
    {
        if (_calibrationFlat)
        {
            return (true, null);
        }

        var path = _configFilePath;
        if (path is null)
        {
            return (false, "没找到 EqualizerAPO 的配置文件，测到的是「当前声音 + 当前 EQ」。");
        }

        if (!IsConfigWritable)
        {
            return (false, "EqualizerAPO 的配置目录写不进去，没法临时把 EQ 拉平；测到的是当前声音。");
        }

        var flat = EqPointList.CreateDefault();
        var text = EapoConfig.Build(flat.Points, 0, 0, _deviceLine);

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    var existed = File.Exists(path);
                    var previous = existed ? File.ReadAllText(path) : null;
                    EapoConfig.ClearReadOnly(path);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    return (Previous: previous, Existed: existed, Error: (string?)null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return (Previous: (string?)null, Existed: false, Error: (string?)ex.Message);
                }
            }).ConfigureAwait(true);

            _calibrationBackup = result.Previous;
            _calibrationBackupExisted = result.Existed;

            if (result.Error is not null)
            {
                return (false, "临时拉平失败：" + result.Error);
            }

            _calibrationFlat = true;
            _previewLastText = text;

            // 给引擎一点时间重新加载配置，不然前几个频点测到的还是旧曲线。
            await Task.Delay(250).ConfigureAwait(true);
            return (true, null);
        }
        finally
        {
            _peaceWriteGate.Release();
        }
    }

    /// <summary>
    /// 第二轮起的测量准备：保留当前「左右」层，但显式不写低音层。
    ///
    /// 低频矫正不能进测量：设备不一定发得出那个声音，叠进去测到的就不是主设备本身，
    /// 反相也跟着算错。第一轮已经拉平；这一轮只把主层当前 EQ 写进去，
    /// 低音层留给「试听扫频」临时叠加。
    ///
    /// 返回 Flat=false：这一轮不用还原——文件内容本来就是主层。
    /// </summary>
    public async Task<(bool Flat, string? Message)> BeginCalibrationMeasurementKeepEqAsync()
    {
        if (_calibrationFlat)
        {
            return (false, null);
        }

        var path = _configFilePath;
        if (path is null)
        {
            return (false, "没找到 EqualizerAPO 的配置文件，测到的是「当前声音 + 当前 EQ」。");
        }

        if (!IsConfigWritable)
        {
            return (false, "EqualizerAPO 的配置目录写不进去；测到的是当前声音。");
        }

        // 只用主层，不合并低音层——这就是「测量过程中不叠加低频」。
        var text = EapoConfig.Build(_mainDraft.Current.Points, PreAmpDb, Balance, _deviceLine);

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var error = await Task.Run(() =>
            {
                try
                {
                    EapoConfig.ClearReadOnly(path);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    return (string?)null;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return ex.Message;
                }
            }).ConfigureAwait(true);

            if (error is not null)
            {
                return (false, "重写测量用的 peace.txt 失败：" + error);
            }

            _previewLastText = text;

            // 给引擎一点时间重新加载，不然前几个频点测到的还是上一套曲线。
            await Task.Delay(250).ConfigureAwait(true);
            return (false, null);
        }
        finally
        {
            _peaceWriteGate.Release();
        }
    }

    /// <summary>
    /// 标定：临时把 peace.txt 写成「一段 4 kHz +12 dB」，让自动EQ 测一轮，验证
    /// EqualizerAPO 到底有没有按配置文件处理音频。
    ///
    /// 背景：配置文件和实际声音不一致时，拟合出来的曲线看着对、实物没动，用户只能对着
    /// 「怎么调都不平」的图猜。这里用一个确定的 +12 dB 把它测出来，测不到就当场说清楚。
    ///
    /// 返回 Flat=false 之类的语义不适用：调用方负责在测完后调 <see cref="EndCalibrationProbeAsync"/> 还原。
    /// </summary>
    public async Task<(bool Ok, string? Message)> WriteCalibrationProbeAsync()
    {
        var path = _configFilePath;
        if (path is null)
        {
            return (false, "没找到 EqualizerAPO 的配置文件，标定没法写。");
        }

        if (!IsConfigWritable)
        {
            return (false, "EqualizerAPO 的配置目录写不进去，标定没法写。");
        }

        // Preamp 必须先压 -12：扫频信号约 -6 dBFS，再叠 +12 dB 会顶到 +6 dBFS 被硬削，
        // 削波会把该测到的增益吃掉大半，标定就会误报「APO 没生效」。
        // 压完之后峰值还是 -6 dBFS，相对增益不受影响（参考点是 1 kHz 中位数）。
        var text = new StringBuilder()
            .AppendLine("Device: all")
            .AppendLine("Preamp: -12 dB")
            .AppendLine("Channel: all")
            .AppendLine("Filter 1: ON PK Fc 4000 Hz Gain 12.0 dB Q 1.00")
            .ToString();

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    var existed = File.Exists(path);
                    var previous = existed ? File.ReadAllText(path) : null;
                    EapoConfig.ClearReadOnly(path);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    return (Previous: previous, Existed: existed, Error: (string?)null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return (Previous: (string?)null, Existed: false, Error: (string?)ex.Message);
                }
            }).ConfigureAwait(true);

            _probeBackup = result.Previous;
            _probeBackupExisted = result.Existed;

            if (result.Error is not null)
            {
                return (false, "写标定配置失败：" + result.Error);
            }

            // 给引擎时间重新加载。这里要等够：APO 重载会重置输出流，紧接着开始播放会被截断
            // （实测等 0.4 秒只录到 7 秒、等 1.5 秒录到 18 秒，扫频 23 秒都没放完）。4 秒是踩出来的下限。
            await Task.Delay(4000).ConfigureAwait(true);
            return (true, null);
        }
        finally
        {
            _peaceWriteGate.Release();
        }
    }

    /// <summary>标定结束：把 peace.txt 还原成标定前的内容。</summary>
    public async Task EndCalibrationProbeAsync()
    {
        var path = _configFilePath;
        var original = _probeBackup;
        var existed = _probeBackupExisted;
        _probeBackup = null;
        _probeBackupExisted = false;

        if (path is null)
        {
            return;
        }

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await Task.Run(() =>
            {
                try
                {
                    if (existed && original is not null)
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.WriteAllText(path, original, new UTF8Encoding(false));
                    }
                    else if (File.Exists(path))
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.Delete(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    StatusText = "还原标定配置失败：" + ex.Message;
                }
            }).ConfigureAwait(true);
        }
        finally
        {
            _peaceWriteGate.Release();
        }
    }

    /// <summary>退出测量态，把 peace.txt 还原成测量前的内容。</summary>
    public async Task EndCalibrationMeasurementAsync()
    {
        if (!_calibrationFlat)
        {
            return;
        }

        var path = _configFilePath;
        var original = _calibrationBackup;
        var existed = _calibrationBackupExisted;

        _calibrationFlat = false;
        _calibrationBackup = null;
        _calibrationBackupExisted = false;
        _previewLastText = null;

        if (path is null)
        {
            return;
        }

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await Task.Run(() =>
            {
                try
                {
                    if (existed && original is not null)
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.WriteAllText(path, original, new UTF8Encoding(false));
                    }
                    else if (File.Exists(path))
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.Delete(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 还原不了就交给下一次试听或保存覆盖，这里不打断。
                    StatusText = "还原 peace.txt 失败：" + ex.Message;
                }
            }).ConfigureAwait(true);
        }
        finally
        {
            _peaceWriteGate.Release();
        }
    }

    /// <summary>
    /// 试听扫频：临时把「左右 + 低音」合并写进 peace.txt，放完再还原。
    ///
    /// 这是唯一会叠加低频的场合。测量期间绝不能叠：设备不一定发得出那个低频，
    /// 叠进去测到的就是另一个系统。所以这里单独开一条写入通道，期间把主窗口的预览挡掉。
    /// </summary>
    /// <param name="main">要试听的主层曲线（通常是刚算出来的那份）。</param>
    /// <param name="bass">要一起叠上去的低音层曲线。</param>
    /// <param name="preAmpDb">左右层的预增益。低音层的提升不参与这个数，和套用时同一条规则。</param>
    public async Task<(bool Ok, string? Message)> BeginSweepPreviewAsync(
        EqPointList main,
        EqPointList bass,
        double preAmpDb)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(bass);

        var path = _configFilePath;
        if (path is null)
        {
            return (false, "没找到 EqualizerAPO 的配置文件，试听叠加不了低频。");
        }

        if (!IsConfigWritable)
        {
            return (false, "EqualizerAPO 的配置目录写不进去，试听叠加不了低频。");
        }

        // 先把还在飞的预览写完，别让它在扫频期间把文件改回去。
        try
        {
            await _previewWriteTask.ConfigureAwait(true);
        }
        catch (Exception)
        {
            // 预览写失败它自己报过；这里只管接着写试听用的这一份。
        }

        _previewTimer.Stop();
        _previewPending = false;

        // 连续点试听时不要重新备份：第一次备份的才是「扫频前的原样」。
        var alreadyActive = _sweepPreviewActive;

        var merged = new EqLayerSet(main, bass).MergedForDisplay(true, Flavors);

        // 预增益只看左右层：低音层提升再多也不压整体音量，和套用时的规则保持一致。
        var preAmp = Math.Round(Math.Clamp(preAmpDb, -20, 20), 1);
        var text = EapoConfig.Build(merged.Points, preAmp, Balance, _deviceLine);

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    var existed = File.Exists(path);
                    var previous = existed ? File.ReadAllText(path) : null;
                    EapoConfig.ClearReadOnly(path);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    return (Previous: previous, Existed: existed, Error: (string?)null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return (Previous: (string?)null, Existed: false, Error: (string?)ex.Message);
                }
            }).ConfigureAwait(true);

            if (result.Error is not null)
            {
                return (false, "试听写入失败：" + result.Error);
            }

            if (!alreadyActive)
            {
                _sweepBackup = result.Previous;
                _sweepBackupExisted = result.Existed;
            }

            _sweepPreviewActive = true;

            // 给引擎一点时间重新加载配置，不然扫频开头听到的还是旧曲线。
            await Task.Delay(250).ConfigureAwait(true);
            return (true, null);
        }
        finally
        {
            _peaceWriteGate.Release();
        }
    }

    /// <summary>试听扫频结束：把 peace.txt 还原成扫频前那一份，并允许主窗口继续预览。</summary>
    public async Task EndSweepPreviewAsync()
    {
        // 从没开始过就什么都不做。少了这个判断，窗口一关就会走到下面
        // 「原本不存在 → 删掉」那一支，把用户正常的 peace.txt 删了。
        if (!_sweepPreviewActive)
        {
            return;
        }

        _sweepPreviewActive = false;

        var path = _configFilePath;
        var original = _sweepBackup;
        var existed = _sweepBackupExisted;
        _sweepBackup = null;
        _sweepBackupExisted = false;

        if (path is null)
        {
            return;
        }

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await Task.Run(() =>
            {
                try
                {
                    if (existed && original is not null)
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.WriteAllText(path, original, new UTF8Encoding(false));
                    }
                    else if (File.Exists(path))
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.Delete(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    StatusText = "还原 peace.txt 失败：" + ex.Message;
                }
            }).ConfigureAwait(true);
        }
        finally
        {
            _peaceWriteGate.Release();
        }
    }

    /// <summary>
    /// 校准只能套到「左右」层：开窗口之前先确保切过去。
    /// 左右↔低音切换不询问保存，所以这里通常直接成功；保留返回值让调用方统一处理
    /// （真切不过去时，比如草稿状态异常，仍然不要开一个最后套不上的窗口）。
    /// </summary>
    public async Task<bool> EnsureMainLayerForCalibrationAsync()
    {
        if (Mode == MainMode.MainLayer)
        {
            return true;
        }

        await RequestModeAsync(MainMode.MainLayer);
        return Mode == MainMode.MainLayer;
    }

    /// <summary>
    /// 把一次校准的结果套到「左右」层；给了 <paramref name="bassPoints"/> 就一起整层替换「低音」层。
    ///
    /// 低频矫正单独放低音层，是因为主层从 100 Hz 起才动推子：设备下潜不够的地方
    /// 主层补不了，也不该把整条主曲线拖低。低音层只在试听扫频时才叠进 peace.txt，
    /// 测量期间不叠（设备不一定发得出那个声音）。
    ///
    /// 预增益只看「左右」层：低频提升不参与，低音炮 / 低音单元的音量由下游自己管。
    /// 代价是低频提得多时合并曲线可能削波，但整体音量不会被一条低频曲线拖低十几 dB。
    /// </summary>
    public async Task<string> ApplyCalibrationAsync(CalibrationPlan plan, EqPointList? bassPoints = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (Mode != MainMode.MainLayer)
        {
            await RequestModeAsync(MainMode.MainLayer);
            if (Mode != MainMode.MainLayer)
            {
                return "还没进到「左右」层：先处理掉未保存的改动，再套一次。";
            }
        }

        FlushQualityNow();

        // 切过预设、或者保存过之后，草稿的编辑会话已经被收掉了（SwitchPreset / 保存 → _mainDraft.End）。
        // 没有会话时 Preview/Commit 都是空操作：套用照样返回成功，其实一个点都没改。
        // 后面那轮就会在「没套 EQ」的状态下测，拟合出来接近平直——看起来就像 EQ 被清空了。
        // 所以先开会话，再验一遍到底落没落地。
        if (!_mainDraft.IsEditing)
        {
            _mainDraft.Begin();
        }

        _mainDraft.Preview(plan.Points);
        _mainDraft.Commit();
        _lastEditDraft = _mainDraft;

        if (!_mainDraft.Current.Equals(plan.Points))
        {
            throw new InvalidOperationException("套用没生效：主窗口不在编辑状态，回主窗口点一下「左右」层再套一次。");
        }

        var replacedBass = false;
        if (bassPoints is not null)
        {
            if (!_bassDraft.IsEditing)
            {
                _bassDraft.Begin();
            }

            _bassDraft.Preview(bassPoints);
            _bassDraft.Commit();

            if (!_bassDraft.Current.Equals(bassPoints))
            {
                throw new InvalidOperationException("低频矫正没写进低音层：回主窗口点一下「低音」层再套一次。");
            }

            // 低音层被替换掉了，叠加开关却还关着的话，低频矫正等于没声音。
            if (!IsBassLayerEnabled)
            {
                IsBassLayerEnabled = true;
            }

            replacedBass = true;
        }

        // 预增益只看左右层。低音层的提升不进这个数：低音炮有自己的音量，
        // 硬按合并峰值压预增益会把中高频一起拖小，听起来就是「校准完整体变小了」。
        PreAmpDb = Math.Round(Math.Clamp(plan.PreAmpDb, EqLimits.MinPreAmpDb, EqLimits.MaxPreAmpDb), 1);

        RaiseDerived();
        SchedulePreview();

        var message = $"已按{TargetCurve.DisplayName(plan.Target)}校准 · 左右 {plan.FilterCount} 段 · "
            + $"预增益 {PreAmpDb:+0.0;-0.0;0.0} dB";

        if (replacedBass)
        {
            var bands = bassPoints!.Points
                .Where(point => !point.IsEndpoint && Math.Abs(point.GainDb) >= 0.005)
                .ToList();
            var range = bands.Count == 0
                ? "没有需要补的"
                : $"提升 {bands.Max(point => point.GainDb):+0.0;-0.0;0.0} / "
                    + $"削减 {bands.Min(point => point.GainDb):+0.0;-0.0;0.0} dB";
            message += $" · 低频 {EqCurveFitter.BassFloorHz:0}–{plan.FloorHz:0} Hz "
                + $"{bands.Count} 段（{range}）已整层替换「低音」层";
        }

        // 预增益只压不提，目标整体下移也含在曲线里：用曲线中频的净变化才算得准，
        // 单看预增益在全削减曲线上会报「不变」，单把两个数相加又会重复计算。
        var overall = plan.OverallGainDb;
        if (overall < -0.05)
        {
            message += $" · 中频整体低 {Math.Abs(overall):0.0} dB（可用系统音量补回来）";
        }

        StatusText = message + " · 正在试听，保存才写回预设";
        return StatusText;
    }

    public bool ConfirmClose()
    {
        if (!IsDirty)
        {
            PersistState();
            return true;
        }

        var choice = ConfirmDirty?.Invoke("还有改动没保存", "关窗口之前要保存吗？") ?? DirtyChoice.Stay;
        switch (choice)
        {
            case DirtyChoice.Save:
                if (!SaveBlocking())
                {
                    return false;
                }

                PersistState();
                return true;

            case DirtyChoice.Discard:
                CancelEdit();
                PersistState();
                return true;

            default:
                return false;
        }
    }

    // ---- 预设切换 ----

    partial void OnSelectedMainPresetChanged(string value)
    {
        // 手动换预设之后就不属于刚才那一组了，语音还原别再用旧组号。
        _lastComboIndex = 0;

        if (_loading || _presetSwitch || _presetReorder || string.IsNullOrEmpty(value) || value == _mainConfigName)
        {
            return;
        }

        SwitchPreset(value, isMain: true);
    }

    partial void OnSelectedBassPresetChanged(string value)
    {
        _lastComboIndex = 0;

        if (_loading || _presetSwitch || _presetReorder || string.IsNullOrEmpty(value) || value == _bassConfigName)
        {
            return;
        }

        SwitchPreset(value, isMain: false);
    }

    private void SwitchPreset(string name, bool isMain)
    {
        if (_configDirectory is null)
        {
            return;
        }

        _presetSwitch = true;
        try
        {
            // 切走之前先把当前层写回旧文件，不丢改动。
            if (isMain)
            {
                // 增益和风味都跟着左右预设走：先把当前这一份记到旧预设名下，再读新预设自己的。
                RememberMainPreAmp();
                _uiState.SetMainFlavors(_mainConfigName, _mainDraft.CurrentFlavors);
                SaveLayer(_mainConfigName, _mainDraft.Current, out _);
                _mainConfigName = name;
                // 自动EQ 预览期间切走：草稿仍然接测得的曲线，图上不跟着变。
                var presetPoints = _calibrationPreview?.Main ?? LoadLayer(name) ?? EqPointList.CreateDefault();
                var presetFlavors = _uiState.TryGetMainFlavors(_mainConfigName, out var storedFlavors)
                    ? storedFlavors
                    : FlavorSet.CreateDefault();
                _mainDraft.End();
                _mainDraft.AcceptSaved(presetPoints, presetFlavors);
                ApplyPresetPreAmp();
            }
            else
            {
                // 低音层没有自己的增益，切它时推子一动不动。
                SaveLayer(_bassConfigName, _bassDraft.Current, out _);
                _bassConfigName = name;
                _bassDraft.End();
                _bassDraft.AcceptSaved(_calibrationPreview?.Bass ?? LoadLayer(name) ?? EqPointList.CreateDefault());
            }

            PersistState();
            RaiseDerived();
            EndPreviewSession();
            SchedulePreview();
            StatusText = (isMain ? "左右" : "低音") + $"层已切换到 {name}";
        }
        finally
        {
            _presetSwitch = false;
        }
    }

    // ---- 自动EQ 预览：新建「左右矫正 / 低音矫正」，曲线钉在主界面 ----

    private sealed class CalibrationPreviewState
    {
        public required EqPointList Main { get; set; }
        public EqPointList? Bass { get; set; }
        public double PreAmpDb { get; set; }
        public string MainBefore { get; init; } = string.Empty;
        public string BassBefore { get; init; } = string.Empty;
        public string MainPreset { get; init; } = string.Empty;
        public string BassPreset { get; init; } = string.Empty;
        public bool MainPresetExisted { get; init; }
        public bool BassPresetExisted { get; init; }
    }

    private CalibrationPreviewState? _calibrationPreview;

    /// <summary>自动EQ 测量中：这段时间主窗口的试听写盘会和测量抢 peace.txt，必须停。</summary>
    private bool _calibrationMeasuring;

    /// <summary>主界面现在是不是在显示自动EQ 的预览曲线。</summary>
    public bool HasCalibrationPreview => _calibrationPreview is not null;

    /// <summary>自动EQ 测一轮开始/结束时调用，期间禁止主界面预览写 peace.txt。</summary>
    public void SetCalibrationMeasuring(bool measuring) => _calibrationMeasuring = measuring;

    /// <summary>
    /// 进入自动EQ 预览：新建「左右矫正 / 低音矫正」两个预设并切过去。
    /// 曲线先给平直，测量结果由 <see cref="UpdateCalibrationPreview"/> 填进来。
    /// </summary>
    public async Task<bool> BeginCalibrationPreviewAsync()
    {
        if (_configDirectory is null)
        {
            return false;
        }

        if (_calibrationPreview is not null)
        {
            return true;
        }

        if (!await EnsureMainLayerForCalibrationAsync().ConfigureAwait(true))
        {
            return false;
        }

        var directory = _configDirectory;
        var mainName = MakeUniquePresetName(directory, "左右矫正" + PeaceConfig.Extension);
        var bassName = MakeUniquePresetName(directory, "低音矫正" + PeaceConfig.Extension);
        var mainExisted = File.Exists(Path.Combine(directory, mainName));
        var bassExisted = File.Exists(Path.Combine(directory, bassName));

        SaveLayer(mainName, EqPointList.CreateDefault(), out _);
        SaveLayer(bassName, EqPointList.CreateDefault(), out _);

        _calibrationPreview = new CalibrationPreviewState
        {
            Main = EqPointList.CreateDefault(),
            Bass = null,
            PreAmpDb = 0,
            MainBefore = _mainConfigName,
            BassBefore = _bassConfigName,
            MainPreset = mainName,
            BassPreset = bassName,
            MainPresetExisted = mainExisted,
            BassPresetExisted = bassExisted,
        };

        SwitchPreset(mainName, isMain: true);
        SwitchPreset(bassName, isMain: false);
        RefreshPresets();
        SyncPresetSelection();
        return true;
    }

    /// <summary>
    /// 自动EQ 每次拟合出结果就推过来：写进主界面草稿、预增益和两个矫正预设。
    ///
    /// 拖动分界线时 fit 会连发很多次，那条路传 <paramref name="commit"/> = false：
    /// 只更新界面、不进撤回栈、不写预设文件（松手那一次才落盘）。否则一次拖动能塞进
    /// 几十条撤销记录，磁盘也被刷几十遍。
    /// </summary>
    public void UpdateCalibrationPreview(CalibrationPlan plan, EqPointList? bass, bool commit = true)
    {
        if (_calibrationPreview is not { } state)
        {
            return;
        }

        state.Main = plan.Points;
        state.Bass = bass;
        state.PreAmpDb = plan.PreAmpDb;

        ApplyPreviewToDrafts(state.Main, state.Bass, commit);
        if (commit)
        {
            PersistPreviewFiles(state);
        }

        SetPreviewPreAmp(state.PreAmpDb);

        RaiseDerived();
        SchedulePreview();
    }

    /// <summary>主界面拖完曲线：把草稿接回预览，再通知自动EQ 按滑条重算。</summary>
    public void NotifyCalibrationEdited()
    {
        if (_calibrationPreview is not { } state)
        {
            return;
        }

        if (Mode == MainMode.BassLayer)
        {
            state.Bass = _bassDraft.Current;
        }
        else
        {
            state.Main = _mainDraft.Current;
        }

        RaiseDerived();
        CalibrationEdited?.Invoke();
    }

    /// <summary>自动EQ 重算完的结果：写回草稿和预览，主界面立刻看到修正后的曲线。</summary>
    public void ApplyEditedCalibration(EqPointList main, EqPointList? bass, double preAmpDb)
    {
        if (_calibrationPreview is not { } state)
        {
            return;
        }

        state.Main = main;
        if (bass is not null)
        {
            state.Bass = bass;
        }

        state.PreAmpDb = preAmpDb;
        ApplyPreviewToDrafts(state.Main, state.Bass);
        PersistPreviewFiles(state);
        SetPreviewPreAmp(preAmpDb);

        RaiseDerived();
        SchedulePreview();
    }

    /// <summary>主界面曲线拖完的通知，订阅者是自动EQ 窗口。</summary>
    public event Action? CalibrationEdited;

    /// <summary>预览期间预增益由算法决定，推子锁住不让拖。</summary>
    public bool IsPreampEnabled => !HasCalibrationPreview && IsApoFound;

    private void ApplyPreviewToDrafts(EqPointList main, EqPointList? bass, bool commit = true)
    {
        if (!_mainDraft.IsEditing)
        {
            _mainDraft.Begin();
        }

        _mainDraft.Preview(main);
        if (commit)
        {
            _mainDraft.Commit();
        }

        if (bass is not null)
        {
            if (!_bassDraft.IsEditing)
            {
                _bassDraft.Begin();
            }

            _bassDraft.Preview(bass);
            if (commit)
            {
                _bassDraft.Commit();
            }
        }
    }

    private void PersistPreviewFiles(CalibrationPreviewState state)
    {
        SaveLayer(state.MainPreset, state.Main, out _);
        if (state.Bass is not null)
        {
            SaveLayer(state.BassPreset, state.Bass, out _);
        }
    }

    private void SetPreviewPreAmp(double preAmpDb)
    {
        _loading = true;
        try
        {
            PreAmpDb = Math.Round(Math.Clamp(preAmpDb, EqLimits.MinPreAmpDb, EqLimits.MaxPreAmpDb), 1);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// 套用自动EQ 结果：写进「当前下拉选中的」配置，覆盖它。
    /// 用户在预览期间切到哪个配置，就覆盖哪个。
    /// </summary>
    public Task<string> ApplyCalibrationPreviewAsync()
    {
        if (_calibrationPreview is not { } state)
        {
            return Task.FromResult("没有自动EQ 结果。");
        }

        var mainTarget = _mainConfigName;
        var bassTarget = _bassConfigName;

        SaveLayer(mainTarget, state.Main, out _);
        if (state.Bass is not null)
        {
            SaveLayer(bassTarget, state.Bass, out _);
        }

        if (!_mainDraft.IsEditing)
        {
            _mainDraft.Begin();
        }

        _mainDraft.Preview(state.Main);
        _mainDraft.Commit();
        _lastEditDraft = _mainDraft;

        if (state.Bass is not null)
        {
            if (!_bassDraft.IsEditing)
            {
                _bassDraft.Begin();
            }

            _bassDraft.Preview(state.Bass);
            _bassDraft.Commit();

            if (!IsBassLayerEnabled)
            {
                IsBassLayerEnabled = true;
            }
        }

        _loading = true;
        try
        {
            PreAmpDb = Math.Round(Math.Clamp(state.PreAmpDb, EqLimits.MinPreAmpDb, EqLimits.MaxPreAmpDb), 1);
            _savedPreAmpDb = PreAmpDb;
            _uiState.SetMainPreAmp(mainTarget, PreAmpDb);
        }
        finally
        {
            _loading = false;
        }

        _calibrationPreview = null;
        PersistState();
        RaiseDerived();
        SchedulePreview();

        var message = $"已覆盖 {mainTarget} · 左右 {state.Main.Count} 点 · 预增益 {PreAmpDb:+0.0;-0.0;0.0} dB";
        if (state.Bass is not null)
        {
            message += $" · 低音 {bassTarget} 已替换";
        }

        return Task.FromResult(message);
    }

    /// <summary>取消自动EQ 预览：切回进入前的两个配置，删掉这次新建的矫正预设。</summary>
    public void CancelCalibrationPreview()
    {
        if (_calibrationPreview is not { } state)
        {
            return;
        }

        _calibrationPreview = null;

        SwitchPreset(state.MainBefore, isMain: true);
        SwitchPreset(state.BassBefore, isMain: false);

        if (!state.MainPresetExisted)
        {
            TryDeletePreset(state.MainPreset);
        }

        if (!state.BassPresetExisted)
        {
            TryDeletePreset(state.BassPreset);
        }

        RefreshPresets();
        SyncPresetSelection();
        RaiseDerived();
        SchedulePreview();
    }

    /// <summary>RefreshPresets 会按「旧选中」恢复下拉，配置名变了之后要把两个下拉拉回当前配置。</summary>
    private void SyncPresetSelection()
    {
        _presetSwitch = true;
        try
        {
            SelectedMainPreset = _mainConfigName;
            SelectedBassPreset = _bassConfigName;
        }
        finally
        {
            _presetSwitch = false;
        }
    }

    private void TryDeletePreset(string name)
    {
        var path = LayerPath(name);
        if (path is null)
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 删不掉就算了，下次预览会换一个新名字。
        }
    }

    // ---- 自动切换：组合列表 ----

    /// <summary>组合列表变了（增删、改预设、改热键、改进程）。热键和进程监听靠它刷新。</summary>
    public event EventHandler? AutoSwitchProfilesChanged;

    /// <summary>
    /// 一组组合套用成功。参数是「谁触发的、第几组」的一句话
    /// （「热键 Ctrl+1 激活第 1 个组合」），通知条直接拿它当副标题。
    /// 判断「软件在不在前台」是界面的事，ViewModel 不碰窗口状态。
    /// </summary>
    public event Action<string>? AutoSwitchApplied;

    /// <summary>组合列表本体，和 <see cref="UiState"/> 里那份是同一个列表。</summary>
    public List<UiAutoSwitch> AutoSwitchProfiles
    {
        get
        {
            _uiState.AutoSwitchProfiles ??= new List<UiAutoSwitch>();
            return _uiState.AutoSwitchProfiles;
        }
    }

    /// <summary>当前左右层真正在用的预设文件名（不一定是下拉里选中的那个）。</summary>
    public string MainPresetName => _mainConfigName;

    /// <summary>
    /// 语音唤醒绑定的组合键（左右|低音|叠加，见 <see cref="UiAutoSwitch.MakeComboKey"/>）。
    /// 全局只有一个；null 表示没开。改了就写盘，并通知监听层重新挂。
    /// </summary>
    public string? VoiceWakeKey
    {
        get => _uiState.VoiceWakeKey;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? null : value;
            if (string.Equals(_uiState.VoiceWakeKey, next, StringComparison.Ordinal))
            {
                return;
            }

            _uiState.VoiceWakeKey = next;
            PersistState();
            AutoSwitchProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>当前低音层真正在用的预设文件名。</summary>
    public string BassPresetName => _bassConfigName;

    /// <summary>这个页面「改了就存」：写盘之后通知热键 / 前台进程监听重新挂。</summary>
    public void SaveAutoSwitchProfiles()
    {
        PersistState();
        AutoSwitchProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 教程最后一步用来讲解的组合：只在本来一组都没有时，用教程那两条临时曲线建一行。
    ///
    /// 这一行只挂在内存里，不调用 <see cref="SaveAutoSwitchProfiles"/>，也不改用户已有的组合；
    /// 教程结束或跳过时由 <see cref="EndTutorialScenario"/> 摘掉。强杀时因为没落过盘，也不会留下。
    /// </summary>
    public UiAutoSwitch? TutorialAutoSwitchDemo { get; private set; }

    public UiAutoSwitch? EnsureTutorialAutoSwitchDemo()
    {
        if (!_tutorialScenario)
        {
            return null;
        }

        if (TutorialAutoSwitchDemo is not null)
        {
            return TutorialAutoSwitchDemo;
        }

        // 用户已经有组合就让他看自己的，不自作主张塞演示数据。
        if (AutoSwitchProfiles.Count > 0)
        {
            return null;
        }

        var demo = new UiAutoSwitch
        {
            MainPreset = _mainConfigName,
            BassPreset = _bassConfigName,
            BassEnabled = IsBassLayerEnabled,
        };

        AutoSwitchProfiles.Add(demo);
        TutorialAutoSwitchDemo = demo;
        return demo;
    }

    /// <summary>把教程临时建的那一组从内存里摘掉；必须在写盘之前调用。</summary>
    public void RemoveTutorialAutoSwitchDemo()
    {
        if (TutorialAutoSwitchDemo is not { } demo)
        {
            return;
        }

        AutoSwitchProfiles.Remove(demo);
        TutorialAutoSwitchDemo = null;
    }

    /// <summary>最近一次按组合切过去时，它在列表里的位置（从 1 开始，0 = 没有）。</summary>
    private int _lastComboIndex;

    /// <summary>最近一次组合切换的触发者（「热键 Ctrl+1」「进程 firefox」）。语音自己的切换不覆盖它。</summary>
    private string? _lastComboSource;

    /// <summary>
    /// 当前生效的组合序号（从 1 开始，0 = 不在任何一组）。
    /// 优先用组合切换时直接记下的号：组合可以只设左右层，切完之后曲线状态跟组合三元组
    /// 对不上，靠比对是认不出来的（语音唤醒记录、语音结束还原都读这里）。
    /// </summary>
    public int ActiveComboIndex => _lastComboIndex > 0 ? _lastComboIndex : CurrentComboIndex();

    /// <summary>当前这组是被谁点亮的（热键 / 进程）；手动改过、从没切过就是 null。</summary>
    public string? ActiveComboSource => _lastComboSource;

    /// <summary>
    /// 把「当前在第几组、谁点亮的」重新登记一遍。
    /// 语音结束还原时用的是临时组合，走不通正常的索引，只能由调用方把记下的值交回来。
    /// </summary>
    public void RememberActiveCombo(int index, string? source)
    {
        _lastComboIndex = index > 0 ? index : 0;
        _lastComboSource = string.IsNullOrWhiteSpace(source) ? null : source;
    }

    /// <summary>
    /// 当前这套曲线对应组合列表里的第几组（从 1 开始）；不在任何一组里返回 0。
    /// 语音唤醒先把它记下来，说完要还原时直接用，不用再去猜是哪一组。
    /// </summary>
    public int CurrentComboIndex()
    {
        var main = _mainConfigName;
        var bass = _bassConfigName;
        var bassOn = IsBassLayerEnabled;

        for (var i = 0; i < AutoSwitchProfiles.Count; i++)
        {
            var item = AutoSwitchProfiles[i];
            if (item is not null
                && string.Equals(item.MainPreset, main, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.BassPreset, bass, StringComparison.OrdinalIgnoreCase)
                && item.BassEnabled == bassOn)
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// 套用一组组合：切左右预设、切低音预设、按组合改叠加开关。
    /// 正在编辑时先落盘 —— 和手动切预设一样，切走不丢改动。
    /// </summary>
    /// <param name="source">
    /// 谁触发的（「热键 Ctrl+1」「进程 Firefox」「语音唤醒」）；手动点行时留空。
    /// 只用来写通知条上那行说明，不参与任何判断。
    /// </param>
    /// <param name="osdText">
    /// 整句通知说明，给了就原样用（语音结束还原时那句要单独措辞）；
    /// 留空则按「来源 + 第几组」自动拼。
    /// </param>
    public bool ApplyAutoSwitch(
        UiAutoSwitch profile,
        out string message,
        string? source = null,
        string? osdText = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (_configDirectory is null)
        {
            message = "找不到 EqualizerAPO 的 config 目录，组合切不过去。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.MainPreset) && string.IsNullOrWhiteSpace(profile.BassPreset))
        {
            message = "这一组左右和低音都没选，没什么可切的。";
            return false;
        }

        // 空的格子表示「这一层不动」：只切选了的那些层。
        var main = string.IsNullOrWhiteSpace(profile.MainPreset) ? null : FindPreset(profile.MainPreset);
        var bass = string.IsNullOrWhiteSpace(profile.BassPreset) ? null : FindPreset(profile.BassPreset);
        if ((!string.IsNullOrWhiteSpace(profile.MainPreset) && main is null)
            || (!string.IsNullOrWhiteSpace(profile.BassPreset) && bass is null))
        {
            message = "这一组里的预设已经被删了，先去「自动切换」把它改回现成的。";
            return false;
        }

        if (IsEditing && !SaveBlocking())
        {
            message = "正在编辑，当前改动没能保存，组合没有切换。";
            return false;
        }

        // 记录这次到底动没动：三层都没变就是「已经在这一组」，不弹通知。
        var changed = false;

        if (main is not null && !string.Equals(_mainConfigName, main, StringComparison.OrdinalIgnoreCase))
        {
            SelectedMainPreset = main;
            changed = true;
        }

        if (bass is not null && !string.Equals(_bassConfigName, bass, StringComparison.OrdinalIgnoreCase))
        {
            SelectedBassPreset = bass;
            changed = true;
        }

        // 低音没选的时候连叠加开关也一起不动：那一层整个不归这一组管。
        if (bass is not null && IsBassLayerEnabled != profile.BassEnabled)
        {
            // 不在编辑里，这一改会自己走 SaveBlocking，把合并结果写进 peace.txt。
            IsBassLayerEnabled = profile.BassEnabled;
            changed = true;
        }

        PersistState();

        // 声音一个字节都没变（重复按同一个热键、来回切回同一组）时只更新状态栏，
        // 屏幕通知不弹：没换东西就别拿一条通知挡视线。
        if (!changed)
        {
            message = "已经是这一组，没有改动。";
            StatusText = message;
            return true;
        }

        var switched = new List<string>();
        if (main is not null)
        {
            switched.Add(main);
        }

        if (bass is not null)
        {
            switched.Add(bass + (profile.BassEnabled ? " · 叠加低音" : " · 不叠加低音"));
        }

        message = "已切到组合：" + string.Join(" / ", switched);
        StatusText = message;

        // 通知条下面那行写「谁触发的、第几组」：热键 / 进程 / 语音唤醒，一眼就知道为什么换了。
        // 语音结束那几种有单独措辞，调用方直接把整句传进来（osdText）。
        var index = AutoSwitchProfiles.IndexOf(profile) + 1;
        _lastComboIndex = index > 0 ? index : 0;

        // 语音唤醒 / 语音结束只是过程，不算「点亮来源」：保留上一次热键或进程的说明，
        // 语音结束还原时才能说清「之前是谁点亮的那一组」。
        if (!string.IsNullOrWhiteSpace(source)
            && !source.StartsWith("语音", StringComparison.Ordinal))
        {
            _lastComboSource = source;
        }

        var text = osdText;
        if (string.IsNullOrEmpty(text))
        {
            var trigger = string.IsNullOrWhiteSpace(source) ? "手动切换" : source;
            text = index > 0 ? $"{trigger} 激活第 {index} 个组合" : $"{trigger} 激活自定义组合";
        }

        AutoSwitchApplied?.Invoke(text);

        return true;
    }

    /// <summary>把组合里存的名字对到现成的预设上；两边都不认就返回 null。</summary>
    private string? FindPreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // 列表里没有、但正是当前在用的那份（可能还没落盘）也算数。
        if (string.Equals(name, _mainConfigName, StringComparison.OrdinalIgnoreCase))
        {
            return _mainConfigName;
        }

        if (string.Equals(name, _bassConfigName, StringComparison.OrdinalIgnoreCase))
        {
            return _bassConfigName;
        }

        return AvailablePresets.FirstOrDefault(item =>
            string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>把当前推子的值记到当前左右预设名下。切预设、保存时调用。</summary>
    private void RememberMainPreAmp() =>
        _uiState.SetMainPreAmp(
            _mainConfigName,
            Math.Clamp(PreAmpDb, EqLimits.MinPreAmpDb, EqLimits.MaxPreAmpDb));

    /// <summary>
    /// 切到新的左右预设后，把推子挪到那份预设记住的增益上。
    ///
    /// 没记过就先沿用当前值，不凭空跳到 0 dB —— 那一下音量会突然变大。
    /// 走 _loading 是为了让属性回写不触发“脏”和一次多余的写盘：切换本身已经算一次落定。
    /// </summary>
    private void ApplyPresetPreAmp()
    {
        var next = _uiState.TryGetMainPreAmp(_mainConfigName, out var stored)
            ? stored
            : PreAmpDb;

        _loading = true;
        _savedPreAmpDb = Math.Clamp(next, EqLimits.MinPreAmpDb, EqLimits.MaxPreAmpDb);
        PreAmpDb = _savedPreAmpDb;
        _loading = false;
    }

    // ---- 主流程 ----

    private async Task RequestModeAsync(MainMode mode)
    {
        // 点当前层的按钮 = 退出全部编辑；退出到「两个都不编辑」才问保存/放弃。
        if (Mode == mode || mode == MainMode.None)
        {
            await LeaveModeAsync();
            return;
        }

        // 左右 ↔ 低音 只是换编辑对象，不弹保存框：
        // 两层各有自己的撤回栈，切过去才能撤回另一层刚套上的改动
        // （自动EQ 套用后要单独撤回低频，就走这条）。保存仍然是两层一起写。
        EnterMode(mode);
    }

    /// <param name="announceFilterWarning">
    /// 拖增益推子顺带进来时传 false：那不是“要编辑曲线”，别拿滤波器提示打断人。
    /// 提示留给真正点「左右」的那次，所以也不占掉 _warnedAboutFilters。
    /// </param>
    // ---- 教程：临时曲线 + 演示操作 ----

    private bool _tutorialScenario;
    private MainMode _tutorialModeBefore;
    private EqPointList? _tutorialMainBefore;
    private EqPointList? _tutorialBassBefore;
    private FlavorSet? _tutorialFlavorsBefore;
    private bool _tutorialBassOnBefore;
    private bool _tutorialGuardBefore;
    private bool _tutorialSpectrumBefore;
    private double _tutorialPreAmpBefore;
    private double _tutorialBalanceBefore;

    /// <summary>进阶课专用的两条临时预设：任何保存都只落到它们身上，不碰用户自己的文件。</summary>
    private const string TutorialMainPreset = "高音曲线.peace";
    private const string TutorialBassPreset = "低音加强.peace";

    private string? _tutorialMainNameBefore;
    private string? _tutorialBassNameBefore;

    /// <summary>本次教程实际用的临时预设名（同名冲突时退到「… 教程.peace」，不碰用户的同名预设）。</summary>
    private string _tutorialMainPreset = TutorialMainPreset;
    private string _tutorialBassPreset = TutorialBassPreset;

    /// <summary>进阶课演示期间为 true：这两层是教程数据，不是用户预设。</summary>
    public bool IsTutorialScenario => _tutorialScenario;

    /// <summary>
    /// 进阶课改成用真实曲线演示：临时把两层换成教程数据
    /// （左右平直、低音把 100 Hz 以下抬起来），并进入左右编辑。
    /// 全程只在草稿里，<see cref="EndTutorialScenario"/> 会把原状态换回来，绝不保存。
    /// </summary>
    public void BeginTutorialScenario()
    {
        if (_tutorialScenario)
        {
            return;
        }

        _tutorialModeBefore = Mode;
        _tutorialMainBefore = _mainDraft.Saved;
        _tutorialBassBefore = _bassDraft.Saved;
        _tutorialFlavorsBefore = _mainDraft.SavedFlavors;
        _tutorialBassOnBefore = IsBassLayerEnabled;
        _tutorialGuardBefore = IsPeakGuardEnabled;
        _tutorialSpectrumBefore = IsSpectrumEnabled;
        _tutorialPreAmpBefore = PreAmpDb;
        _tutorialBalanceBefore = Balance;

        _mainDraft.Cancel();
        _mainDraft.End();
        _bassDraft.Cancel();
        _bassDraft.End();
        Mode = MainMode.None;

        _mainDraft.RefreshSaved(TutorialFlatCurve(), FlavorSet.CreateDefault());
        _bassDraft.RefreshSaved(TutorialBassCurve(), FlavorSet.CreateDefault());

        // 换成两条临时预设再演示：中途保存 / 关闭窗口 / 进程被杀，都只会动这两个文件。
        _tutorialMainNameBefore = _mainConfigName;
        _tutorialBassNameBefore = _bassConfigName;
        _mainConfigName = _tutorialMainPreset = ChooseTutorialPresetName(TutorialMainPreset, TutorialFlatCurve());
        _bassConfigName = _tutorialBassPreset = ChooseTutorialPresetName(TutorialBassPreset, TutorialBassCurve());
        SaveLayer(_tutorialMainPreset, TutorialFlatCurve(), out _);
        SaveLayer(_tutorialBassPreset, TutorialBassCurve(), out _);
        _presetSwitch = true;
        try
        {
            SelectedMainPreset = _tutorialMainPreset;
            SelectedBassPreset = _tutorialBassPreset;
        }
        finally
        {
            _presetSwitch = false;
        }

        RefreshPresets();
        _tutorialScenario = true;
        IsBassLayerEnabled = false;
        IsPeakGuardEnabled = true;
        IsSpectrumEnabled = true;

        // 教程从「没调过」的状态开始：增益 0 dB、左右居中。
        PreAmpDb = 0;
        Balance = 0;
    }

    /// <summary>教程第 2 步：光标点完「左右」之后再真正进入编辑，让人看见曲线变色。</summary>
    public void EnterTutorialEditing()
    {
        if (_tutorialScenario && Mode == MainMode.None)
        {
            EnterMode(MainMode.MainLayer, announceFilterWarning: false);
        }
    }

    /// <summary>教程低音那一步：和进左右一样，等遮罩播完点击再切到低音编辑。</summary>
    public void EnterTutorialBassEditing()
    {
        if (_tutorialScenario && Mode != MainMode.BassLayer)
        {
            EnterMode(MainMode.BassLayer, announceFilterWarning: false);
        }
    }

    /// <summary>
    /// 教程试听：走软件自己的试听通道（把当前草稿写进 peace.txt 再放扫频），
    /// 而不是教程自带的那套播放器，听感和界面操作保持一致。
    /// </summary>
    private EqPointList? _previewFlatBackup;

    public Task<(bool Ok, string? Message)> BeginTutorialPreviewAsync(bool overlayBass, bool flat = false)
    {
        // 放「没改过」那遍时，图上也要换成平直；放完或开始下一遍再换回来。
        if (!_mainDraft.IsEditing)
        {
            _mainDraft.Begin();
        }

        if (flat)
        {
            _previewFlatBackup ??= _mainDraft.Current;
            _mainDraft.Preview(TutorialFlatCurve());
        }
        else if (_previewFlatBackup is { } backup)
        {
            _previewFlatBackup = null;
            _mainDraft.Preview(backup);
        }

        // 图上也要跟着换：不叠低音时只画左右，叠的时候才把低音合进来。
        if (_tutorialScenario && IsBassLayerEnabled != overlayBass)
        {
            IsBassLayerEnabled = overlayBass;
        }

        var main = _mainDraft.Current;
        var bass = overlayBass ? _bassDraft.Current : EqPointList.Empty;
        return BeginSweepPreviewAsync(main, bass, PreAmpDb);
    }

    public Task EndTutorialPreviewAsync()
    {
        if (_previewFlatBackup is { } backup)
        {
            _previewFlatBackup = null;
            _mainDraft.Preview(backup);
        }

        return EndSweepPreviewAsync();
    }

    /// <summary>教程看完低音编辑后退出来，让下一条专门讲开关；不弹保存。</summary>
    public void ExitTutorialEditing()
    {
        if (!_tutorialScenario)
        {
            return;
        }

        // 退出编辑但保留刚才的改动：教程已经演示过「拉低高音」，不能一退就变回平直。
        _mainDraft.AcceptSaved(_mainDraft.Current, _mainDraft.CurrentFlavors);
        _mainDraft.End();
        _bassDraft.AcceptSaved(_bassDraft.Current, _bassDraft.CurrentFlavors);
        _bassDraft.End();
        Mode = MainMode.None;
        RaiseDerived();
        EndPreviewSession();
        SchedulePreview();
    }

    /// <summary>教程演重播时先把左右曲线清回平直，避免叠出好几个控制点。</summary>
    public void ResetTutorialMainCurve()
    {
        if (!_tutorialScenario)
        {
            return;
        }

        if (!_mainDraft.IsEditing)
        {
            _mainDraft.Begin();
        }

        _mainDraft.Preview(TutorialFlatCurve());
        _mainDraft.Commit();
    }

    /// <summary>
    /// 演示没播完就点下一步时，把左右曲线直接落到最终形状
    /// （平直 + 6 kHz 处 −10 dB、Q 0.55），后面的步骤看到的就是调整好的那条。
    /// </summary>
    public void FinishTutorialCurveDemo()
    {
        if (!_tutorialScenario)
        {
            return;
        }

        if (!_mainDraft.IsEditing)
        {
            _mainDraft.Begin();
        }

        var points = TutorialFlatCurve();
        if (points.TryAdd(6000, out var added, out var created))
        {
            points = added.Move(created.Id, 6000, -10).SetQuality(created.Id, 0.55);
        }

        _mainDraft.Preview(points);
        _mainDraft.Commit();
    }

    /// <summary>教程结束 / 跳过 / 关窗口：删掉两条临时预设文件。</summary>
    private void DeleteTutorialPresetFiles()
    {
        if (_configDirectory is null)
        {
            return;
        }

        foreach (var name in new[] { _tutorialMainPreset, _tutorialBassPreset })
        {
            try
            {
                var path = Path.Combine(_configDirectory, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"删除教程临时预设失败：{name} · {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 上次教程被强杀会留下两条临时预设：只有内容仍是教程曲线时才删，
    /// 免得误删用户自己起的同名预设；顺带把状态里指向临时预设的名字拨回来。
    /// </summary>
    private void CleanupStaleTutorialPresets()
    {
        if (_configDirectory is null)
        {
            return;
        }

        var wasMain = string.Equals(_mainConfigName, TutorialMainPreset, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_mainConfigName, TutorialFallbackName(TutorialMainPreset), StringComparison.OrdinalIgnoreCase);
        var wasBass = string.Equals(_bassConfigName, TutorialBassPreset, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_bassConfigName, TutorialFallbackName(TutorialBassPreset), StringComparison.OrdinalIgnoreCase);

        if (wasMain)
        {
            _mainConfigName = "EsportsMain.peace";
        }

        if (wasBass)
        {
            _bassConfigName = "EsportsBass.peace";
        }

        // 状态里还指着临时预设（说明上次是保存后被杀）→ 按名字删；
        // 否则只在内容仍是教程曲线时删，避免误删用户自己起的同名预设。
        var removed = TryRemoveTutorialPreset(TutorialMainPreset, TutorialFlatCurve(), wasMain)
            | TryRemoveTutorialPreset(TutorialFallbackName(TutorialMainPreset), TutorialFlatCurve(), wasMain)
            | TryRemoveTutorialPreset(TutorialBassPreset, TutorialBassCurve(), wasBass)
            | TryRemoveTutorialPreset(TutorialFallbackName(TutorialBassPreset), TutorialBassCurve(), wasBass);
        if (removed)
        {
            RefreshPresets();
        }
    }

    /// <summary>同名预设已经存在且内容不是教程曲线时，退到「… 教程.peace」，绝不覆盖用户文件。</summary>
    private string ChooseTutorialPresetName(string preferred, EqPointList expected)
    {
        if (_configDirectory is null)
        {
            return preferred;
        }

        var path = Path.Combine(_configDirectory, preferred);
        if (!File.Exists(path))
        {
            return preferred;
        }

        var existing = LoadLayer(preferred);
        return existing is not null && SamePoints(existing, expected)
            ? preferred
            : TutorialFallbackName(preferred);
    }

    private static string TutorialFallbackName(string name) =>
        Path.GetFileNameWithoutExtension(name) + " 教程.peace";

    private static bool SamePoints(EqPointList actual, EqPointList expected)
    {
        if (actual.Points.Count != expected.Points.Count)
        {
            return false;
        }

        for (var i = 0; i < expected.Points.Count; i++)
        {
            var a = actual.Points[i];
            var b = expected.Points[i];
            if (Math.Abs(a.FrequencyHz - b.FrequencyHz) > 0.5 || Math.Abs(a.GainDb - b.GainDb) > 0.05)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryRemoveTutorialPreset(string name, EqPointList expected, bool forceByName)
    {
        var path = Path.Combine(_configDirectory!, name);
        if (!File.Exists(path))
        {
            return false;
        }

        if (!forceByName)
        {
            var points = LoadLayer(name);
            if (points is null || !SamePoints(points, expected))
            {
                return false;
            }
        }

        try
        {
            File.Delete(Path.Combine(_configDirectory!, name));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"清理教程临时预设失败：{name} · {ex.Message}");
            return false;
        }
    }

    /// <summary>教程演示勾选「叠加低音修正」：曲线会自己变形。</summary>
    public void EnableTutorialBassOverlay()
    {
        if (_tutorialScenario && !IsBassLayerEnabled)
        {
            IsBassLayerEnabled = true;
        }
    }

    /// <summary>把教程的两层换回用户原来的曲线和风味；原来在编辑态就回到那个编辑态。</summary>
    public void EndTutorialScenario()
    {
        if (!_tutorialScenario)
        {
            return;
        }

        _tutorialScenario = false;

        // 演示组合只活在教程里：先摘掉，后面 PersistState 才不会把它写进配置。
        RemoveTutorialAutoSwitchDemo();

        _mainDraft.Cancel();
        _mainDraft.End();
        _bassDraft.Cancel();
        _bassDraft.End();
        Mode = MainMode.None;

        if (_tutorialMainBefore is { } main)
        {
            _mainDraft.RefreshSaved(main, _tutorialFlavorsBefore ?? FlavorSet.CreateDefault());
        }

        if (_tutorialBassBefore is { } bass)
        {
            _bassDraft.RefreshSaved(bass);
        }

        // 切回用户原本的两条预设，再删掉教程临时文件。
        if (_tutorialMainNameBefore is { } mainName)
        {
            _mainConfigName = mainName;
        }

        if (_tutorialBassNameBefore is { } bassName)
        {
            _bassConfigName = bassName;
        }

        _tutorialMainNameBefore = null;
        _tutorialBassNameBefore = null;
        DeleteTutorialPresetFiles();
        RefreshPresets();
        _presetSwitch = true;
        try
        {
            SelectedMainPreset = _mainConfigName;
            SelectedBassPreset = _bassConfigName;
        }
        finally
        {
            _presetSwitch = false;
        }

        PersistState();

        IsBassLayerEnabled = _tutorialBassOnBefore;
        IsPeakGuardEnabled = _tutorialGuardBefore;
        IsSpectrumEnabled = _tutorialSpectrumBefore;

        // 增益和均衡是教程临时拨到 0 dB / 居中的，退出时原样放回去。
        var loading = _loading;
        _loading = true;
        PreAmpDb = _tutorialPreAmpBefore;
        Balance = _tutorialBalanceBefore;
        _loading = loading;
        _tutorialMainBefore = null;
        _tutorialBassBefore = null;
        _tutorialFlavorsBefore = null;
        RaiseDerived();
        EndPreviewSession();
        SchedulePreview();

        if (_tutorialModeBefore is MainMode.MainLayer or MainMode.BassLayer)
        {
            EnterMode(_tutorialModeBefore, announceFilterWarning: false);
        }
    }

    /// <summary>教程里那条「左右」曲线：全平。</summary>
    private static EqPointList TutorialFlatCurve() => EqPointList.FromPoints(new[]
    {
        new EqPoint(1, 20, 0, 0.7, true),
        new EqPoint(2, 20000, 0, 0.7, true),
    });

    /// <summary>教程低音：把 EsportsBass 那条调好的曲线（5 个点）直接内置，不读运行目录的预设文件。</summary>
    private static EqPointList TutorialBassCurve() => EqPointList.FromPoints(new[]
    {
        new EqPoint(1, 20, 6.6, 0.7, true),
        new EqPoint(2, 54, 6.8, 0.4, false),
        new EqPoint(3, 133, 3.6, 0.81, false),
        new EqPoint(4, 252, -2.4, 0.93, false),
        new EqPoint(5, 20000, 0, 0.7, true),
    });

    // 教程演示只借现有入口，不另写一套改数据的逻辑。
    public int TutorialAddPoint(double frequencyHz) => CreatePoint(frequencyHz);

    public void TutorialPreviewPoint(int id, double frequencyHz, double gainDb) =>
        PreviewPoint(id, frequencyHz, gainDb);

    public void TutorialCommit() => CommitPoint();

    public void TutorialDeletePoint(int id) => DeletePoint(id);

    public void TutorialUndo() => Undo();

    private void EnterMode(MainMode mode, bool announceFilterWarning = true)
    {
        _qualityPending = false;

        if (mode is MainMode.MainLayer or MainMode.BassLayer)
        {
            _lastEditDraft = mode == MainMode.BassLayer ? _bassDraft : _mainDraft;
        }

        if (mode is MainMode.MainLayer or MainMode.BassLayer)
        {
            _mainDraft.Begin();
            _bassDraft.Begin();
        }
        else
        {
            _mainDraft.End();
            _bassDraft.End();
        }

        Mode = mode;
        if (mode == MainMode.MainLayer)
        {
            // 进编辑时风味不预选：下拉框默认空着，手动选了才出控制点。
            // 下拉绑的是 SelectedBand（对象），只清下标它还会按旧对象反推出「脚步」。
            SelectedBand = null;
            SelectedFlavorIndex = -1;
        }
        RaiseDerived();
        StatusText = mode switch
        {
            MainMode.MainLayer => "左右：按住曲线拖最近的推子，曲线始终经过鼠标；空白处按下就在鼠标处建点（Q 按邻居自动定）。滚轮调 Q，右键普通点删除、首尾端点归零。试听里不叠低音层，退出编辑后才按勾选。",
            MainMode.BassLayer => "低音：这条曲线会和左右曲线叠加后一起试听；退出编辑后才按勾选决定是否保留叠加。",
            _ => StatusText,
        };

        // 进编辑态就按本层的试听规则出声：低音层叠上左右，左右层不叠低音。
        // 不重写的话，第一次落点之前听到的还是「按勾选」的旧合并结果。
        if (mode is MainMode.MainLayer or MainMode.BassLayer)
        {
            SchedulePreview();
        }

        if (announceFilterWarning && mode == MainMode.MainLayer && HasNonPeakingFilters && !_warnedAboutFilters)
        {
            _warnedAboutFilters = true;
            Notify?.Invoke(
                "有些滤波器类型存不下来",
                $"当前预设里有 {NonPeakingFilterCount} 个点模型表达不了的滤波器（低通、高通、带通、陷波等）。"
                + "保存时会把这些段统一写成 PK。低架 / 高架不受影响，AutoEQ 带进来的不会丢。");
        }
    }

    /// <returns>true = 脏改动已经处理完并退出；false = 用户选了「留下」，还留在编辑里。</returns>
    private async Task<bool> LeaveModeAsync()
    {
        if (!await ResolveDirtyAsync())
        {
            return false;
        }

        _mainDraft.End();
        _bassDraft.End();
        Mode = MainMode.None;
        RaiseDerived();

        // 退出全部编辑：编辑期间的「按层叠加」作废，回到勾选设置才算落定。
        return await SettleOverlayAfterEditingAsync();
    }

    /// <summary>
    /// 退出全部编辑后把 peace.txt 收回「叠加低音修正」勾选设置。
    /// 试听是照当前层叠的（低音叠左右、左右不叠低音），不重写就会把试听时的叠加留在声音里。
    /// </summary>
    /// <returns>true = 声音已经收回勾选设置（或本来就没写过盘）；false = 重写失败，状态栏已经写了原因。</returns>
    private async Task<bool> SettleOverlayAfterEditingAsync()
    {
        _previewTimer.Stop();
        _previewPending = false;

        if (!_previewActive && !_previewWriting)
        {
            // 编辑期间没写过盘（例如直接退出、改动都已保存或还原），声音已经是勾选设置。
            StatusText = "只读：点亮上方模式按钮才能编辑。";
            return true;
        }

        // 等还在飞的那次试听写完，免得它盖掉下面这次重写。
        try
        {
            await _previewWriteTask.ConfigureAwait(true);
        }
        catch (Exception)
        {
            // 试听写失败已经由它自己报过，这里只管接着把声音收回勾选设置。
        }

        EndPreviewSession();

        if (_configFilePath is null)
        {
            StatusText = "只读：点亮上方模式按钮才能编辑。";
            return true;
        }

        var set = new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved);
        if (!set.TryMerge(IsBassLayerEnabled, Flavors, out var merged, out var mergeError))
        {
            StatusText = mergeError;
            return false;
        }

        var text = EapoConfig.Build(merged.Points, PreAmpDb, Balance, _deviceLine);
        var path = _configFilePath;

        var error = await Task.Run(() =>
        {
            try
            {
                EapoConfig.ClearReadOnly(path);
                File.WriteAllText(path, text, new UTF8Encoding(false));
                return (string?)null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }).ConfigureAwait(true);

        if (error is not null)
        {
            StatusText = $"按勾选重写 {ConfigFileName} 失败：{error}";
            return false;
        }

        // 收回勾选设置之后，Peace 那边也要跟上：写 Mix 预设再推推子。
        // 少这一步就会出现「peace.txt 已经叠回来了，Peace 还停在编辑时的只主层」。
        if (!_uiState.IndependentMode)
        {
            WriteMixPreset(merged.Points);
            _peaceSync.Queue(merged.Points, PreAmpDb);
        }

        _previewLastText = text;
        StatusText = IsBassLayerEnabled
            ? "只读：已退出编辑 · 按勾选叠加低音层。"
            : "只读：已退出编辑 · 按勾选不叠加低音层。";
        return true;
    }

    private async Task<bool> ResolveDirtyAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        var choice = ConfirmDirty?.Invoke(
            "改动还没保存",
            "改动现在只在内存里，保存之后才会作用到声音。") ?? DirtyChoice.Stay;

        switch (choice)
        {
            case DirtyChoice.Save:
                return await SaveCoreAsync();

            case DirtyChoice.Discard:
                CancelEdit();
                return true;

            default:
                return false;
        }
    }

    private async Task<bool> SaveCoreAsync()
    {
        FlushQualityNow();

        if (!BuildSaveRequest(out var request, out var error))
        {
            StatusText = error;
            return false;
        }

        StatusText = "正在写配置…";
        var outcome = await Task.Run(() => ExecuteSave(request)).ConfigureAwait(true);
        if (!outcome.Ok)
        {
            AppendSaveErrorLog(outcome.Message);
            StatusText = outcome.Message;
            return false;
        }

        ApplySaveSuccess(outcome);
        StatusText = outcome.Message;
        return true;
    }

    private bool SaveBlocking()
    {
        FlushQualityNow();

        if (!BuildSaveRequest(out var request, out var error))
        {
            StatusText = error;
            return false;
        }

        StatusText = "正在写配置…";
        var outcome = Task.Run(() => ExecuteSave(request)).GetAwaiter().GetResult();
        if (!outcome.Ok)
        {
            AppendSaveErrorLog(outcome.Message);
            StatusText = outcome.Message;
            return false;
        }

        ApplySaveSuccess(outcome);
        StatusText = outcome.Message;
        return true;
    }

    private bool BuildSaveRequest(out SaveRequest request, out string error)
    {
        request = null!;
        error = string.Empty;

        if (_configDirectory is null || _configFilePath is null)
        {
            error = "找不到 EqualizerAPO 的 config 目录，保存不了。";
            return false;
        }

        var layers = new EqLayerSet(_mainDraft.Current, _bassDraft.Current);
        if (!layers.TryMerge(IsBassLayerEnabled, Flavors, out var merged, out var mergeError))
        {
            error = mergeError;
            return false;
        }

        request = new SaveRequest(
            Path.Combine(_configDirectory, _mainConfigName),
            Path.Combine(_configDirectory, _bassConfigName),
            _configFilePath,
            _mainDraft.Current,
            _bassDraft.Current,
            merged,
            PreAmpDb,
            Balance,
            _deviceLine);

        return true;
    }

    /// <summary>后台线程：两个 .peace 各写一份，合并结果写 peace.txt。</summary>
    private SaveOutcome ExecuteSave(SaveRequest request)
    {
        try
        {
            if (!PeaceConfig.Save(request.MainPath, request.MainPoints.Points, out var mainError))
            {
                return new SaveOutcome(false, DescribeSaveFailure("写主配置失败", request.MainPath, mainError));
            }

            if (!PeaceConfig.Save(request.BassPath, request.BassPoints.Points, out var bassError))
            {
                return new SaveOutcome(false, DescribeSaveFailure("写低音配置失败", request.BassPath, bassError));
            }

            var text = EapoConfig.Build(request.Merged.Points, request.PreAmp, request.Balance, request.DeviceLine);

            if (File.Exists(request.PeacePath))
            {
                try
                {
                    var backup = Path.Combine(UiState.Directory, Path.GetFileName(request.PeacePath) + ".bak");
                    Directory.CreateDirectory(UiState.Directory);
                    File.Copy(request.PeacePath, backup, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 备份失败不阻止保存。
                }
            }

            EapoConfig.ClearReadOnly(request.PeacePath);
            File.WriteAllText(request.PeacePath, text, new UTF8Encoding(false));

            var overflow = request.Merged.Count > EqLimits.PeaceDisplayBands
                ? $" · 超过 {EqLimits.PeaceDisplayBands} 个，Peace 显示不全"
                : string.Empty;

            // 非独立模式：文件已经落到 peace.txt，Peace 如果在跑就把推子同步过去。
            // 独立模式不碰 Peace；推送失败也不影响这次保存。
            if (!_uiState.IndependentMode)
            {
                WriteMixPreset(request.Merged.Points);
                _peaceSync.Queue(request.Merged.Points, request.PreAmp);
            }

            return new SaveOutcome(
                true,
                $"已保存 · 主 {request.MainPoints.Count} 点 / 低音 {request.BassPoints.Count} 点 · 合并 {request.Merged.Count} 个滤波器{overflow}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SaveOutcome(
                false,
                DescribeSaveFailure("写 peace.txt 失败", request.PeacePath, ex.GetType().Name + ": " + ex.Message));
        }
    }

    /// <summary>
    /// 启动时把「谁在跑、是不是管理员、配置目录能不能写」写进一份小报告。
    /// 保存失败而界面上看不出来环境差异时，就靠它。
    /// </summary>
    private void WriteStartupReport()
    {
        try
        {
            Directory.CreateDirectory(UiState.Directory);
            var lines = new List<string>
            {
                $"time: {DateTime.Now:O}",
                $"user: {Environment.UserDomainName}\\{Environment.UserName}",
                $"elevated: {_elevated}",
                $"configDir: {_configDirectory ?? "(not found)"}",
                $"configWritable: {IsConfigWritable}",
                $"mainPreset: {_mainConfigName} -> {ProbeWrite(_configDirectory is null ? null : Path.Combine(_configDirectory, _mainConfigName))}",
                $"bassPreset: {_bassConfigName} -> {ProbeWrite(_configDirectory is null ? null : Path.Combine(_configDirectory, _bassConfigName))}",
                $"peaceTxt: {ProbeWrite(_configFilePath)}",
                $"os: {Environment.OSVersion}",
            };

            File.WriteAllText(Path.Combine(UiState.Directory, "startup-report.txt"), string.Join(Environment.NewLine, lines));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 报告写不出来不影响使用。
        }
    }

    /// <summary>试着打开文件写句柄但不动内容，用来判断「这个进程能不能写它」。</summary>
    private static string ProbeWrite(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "no path";
        }

        if (!File.Exists(path))
        {
            return "missing";
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return "ok";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    /// <summary>保存失败也往磁盘落一份，界面截断了还能翻回来。</summary>
    private static void AppendSaveErrorLog(string message)
    {
        try
        {
            Directory.CreateDirectory(UiState.Directory);
            var path = Path.Combine(UiState.Directory, "save-error.log");
            File.AppendAllText(path, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 日志都写不进去就算了，别为了记录失败再抛一次。
        }
    }

    /// <summary>保存失败时把路径和“该怎么办”一起说出来，别只丢一句“失败”。</summary>
    private static string DescribeSaveFailure(string what, string path, string error)
    {
        var text = $"{what}：{error} · 路径 {path}";

        var denied = error.Contains("UnauthorizedAccess", StringComparison.OrdinalIgnoreCase)
            || error.Contains("denied", StringComparison.OrdinalIgnoreCase)
            || error.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase);

        if (denied)
        {
            text += " · 配置目录需要写权限：关掉程序，右键「以管理员身份运行」再保存";
        }

        return text;
    }

    private void ApplySaveSuccess(SaveOutcome outcome)
    {
        _mainDraft.AcceptSaved(_mainDraft.Current, _mainDraft.CurrentFlavors);
        _bassDraft.AcceptSaved(_bassDraft.Current);
        _savedBalance = Math.Clamp(Balance, -1, 1);
        _savedBassLayerEnabled = IsBassLayerEnabled;
        _savedPreAmpDb = PreAmpDb;
        RememberMainPreAmp();
        _qualityPending = false;
        EndPreviewSession();
        RefreshPresets();
        PersistState();
        RaiseDerived();

        // 保存之后就不在编辑里了（点保存和脏确认里选保存都一样），
        // 声音已经是刚写进去的「按勾选」合并结果，不需要再按本层规则补一次试听。
    }

    private void CancelEdit()
    {
        _previewSuspended = true;
        _previewTimer.Stop();
        _previewPending = false;

        _qualityPending = false;
        _mainDraft.Cancel();
        _bassDraft.Cancel();
        Balance = _savedBalance;
        IsBassLayerEnabled = _savedBassLayerEnabled;
        PreAmpDb = _savedPreAmpDb;
        _ = RestorePreviewAsync();
        RaiseDerived();
    }

    // ---- 文件 ----

    private string? LayerPath(string name) =>
        _configDirectory is null ? null : Path.Combine(_configDirectory, name);

    private EqPointList? LoadLayer(string name)
    {
        var path = LayerPath(name);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return PeaceConfig.Load(path, out _);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private bool SaveLayer(string name, EqPointList points, out string error)
    {
        var path = LayerPath(name);
        if (path is null)
        {
            error = "没有配置目录。";
            return false;
        }

        return PeaceConfig.Save(path, points.Points, out error);
    }

    private EapoConfigData? TryReadConfig()
    {
        if (_configFilePath is null || !File.Exists(_configFilePath))
        {
            return null;
        }

        try
        {
            return EapoConfig.Parse(File.ReadAllText(_configFilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Post(() => StatusText = $"读不了 {ConfigFileName}：{ex.Message}");
            return null;
        }
    }

    private void RefreshPresets()
    {
        if (_configDirectory is null)
        {
            return;
        }

        IReadOnlyList<string> files;
        try
        {
            files = PeaceConfig.ListPresets(_configDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            files = Array.Empty<string>();
        }

        var names = files
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            // Peace 的专用混音配置只给同步用，不该出现在用户的下拉里。
            .Where(name => !string.Equals(name, PeaceSync.MixPresetName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 只保证“这两层现在用的那两个文件”在列表里：它们可能还没落盘，选中时要有个名字。
        // 不再硬塞 EsportsMain/EsportsBass —— 用户把默认预设改了名之后，那两个名字会变成
        // 谁也没在用的幽灵条目，右键删还删不掉。
        foreach (var fallback in new[] { _mainConfigName, _bassConfigName })
        {
            if (!names.Any(n => string.Equals(n, fallback, StringComparison.OrdinalIgnoreCase)))
            {
                names.Add(fallback);
            }
        }

        names = ApplyPresetOrder(names);

        var selectedMain = SelectedMainPreset;
        var selectedBass = SelectedBassPreset;

        AvailablePresets.Clear();
        foreach (var name in names)
        {
            AvailablePresets.Add(name);
        }

        _loading = true;
        SelectedMainPreset = names.FirstOrDefault(n => string.Equals(n, selectedMain, StringComparison.OrdinalIgnoreCase)) ?? names[0];
        SelectedBassPreset = names.FirstOrDefault(n => string.Equals(n, selectedBass, StringComparison.OrdinalIgnoreCase)) ?? names[0];
        _loading = false;
    }

    /// <summary>
    /// 按用户拖出来的顺序排；没记过的名字按文件名的本地化顺序补在后面。
    /// 已经不在磁盘上、也不在用的名字顺手从记忆里清掉，免得越攒越多。
    /// </summary>
    private List<string> ApplyPresetOrder(IEnumerable<string> available)
    {
        var remaining = new List<string>(available);
        var ordered = new List<string>();

        foreach (var wanted in _uiState.PresetOrder)
        {
            var index = remaining.FindIndex(name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                continue;
            }

            ordered.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        remaining.Sort(StringComparer.CurrentCultureIgnoreCase);
        ordered.AddRange(remaining);

        _uiState.PresetOrder = ordered;
        return ordered;
    }

    // ---- 预设文件：右键菜单（重命名 / 复制 / 删除）和拖动排序 ----

    /// <summary>拖动排序：把一条挪到 targetIndex。落盘由 <see cref="PersistPresetOrder"/> 负责。</summary>
    public void MovePreset(string name, int targetIndex)
    {
        var from = AvailablePresets.IndexOf(name);
        if (from < 0)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, AvailablePresets.Count - 1);
        if (from == targetIndex)
        {
            return;
        }

        _presetReorder = true;
        try
        {
            AvailablePresets.Move(from, targetIndex);
        }
        finally
        {
            _presetReorder = false;
        }

        // 挪条目不该改变“哪一层用哪个预设”。万一选择跟着漂了，按记下的文件名拉回来。
        if (!string.Equals(SelectedMainPreset, _mainConfigName, StringComparison.OrdinalIgnoreCase))
        {
            SelectedMainPreset = _mainConfigName;
        }

        if (!string.Equals(SelectedBassPreset, _bassConfigName, StringComparison.OrdinalIgnoreCase))
        {
            SelectedBassPreset = _bassConfigName;
        }
    }

    /// <summary>记住下拉栏现在的顺序，下次启动照这个顺序排。</summary>
    public void PersistPresetOrder()
    {
        _uiState.PresetOrder = AvailablePresets.ToList();
        UiStateStore.Save(_uiState);
    }

    /// <summary>
    /// 重命名预设。被某一层用着的话，内部记的文件名和界面上的选中一起改，
    /// 否则下一次保存会写回那个已经不存在的旧名字。
    /// </summary>
    public void RenamePreset(string name)
    {
        if (_configDirectory is null || PromptText is null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var current = Path.GetFileName(name);
        var typed = PromptText("重命名预设", $"「{current}」的新名字：", current);
        if (typed is null)
        {
            return;
        }

        if (!TryNormalizePresetName(typed, out var target, out var problem))
        {
            Notify?.Invoke("这个名字不能用", problem);
            return;
        }

        if (string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sourcePath = Path.Combine(_configDirectory, current);
        var targetPath = Path.Combine(_configDirectory, target);

        if (File.Exists(targetPath))
        {
            Notify?.Invoke("重名了", $"{target} 已经在配置目录里，换一个名字。");
            return;
        }

        if (File.Exists(sourcePath))
        {
            try
            {
                File.Move(sourcePath, targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusText = DescribeSaveFailure("重命名预设失败", sourcePath, ex.Message);
                return;
            }
        }

        if (string.Equals(current, _mainConfigName, StringComparison.OrdinalIgnoreCase))
        {
            _mainConfigName = target;
            SelectedMainPreset = target;
        }

        if (string.Equals(current, _bassConfigName, StringComparison.OrdinalIgnoreCase))
        {
            _bassConfigName = target;
            SelectedBassPreset = target;
        }

        SyncPresetOrderFromList();
        ReplaceInPresetOrder(current, target);
        RefreshPresets();
        PersistState();
        StatusText = $"预设已重命名：{current} → {target}";
    }

    /// <summary>复制一份预设，新文件插在原文件后面。当前层不动。</summary>
    public void DuplicatePreset(string name)
    {
        if (_configDirectory is null || PromptText is null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var current = Path.GetFileName(name);
        var sourcePath = Path.Combine(_configDirectory, current);

        if (!File.Exists(sourcePath))
        {
            StatusText = $"{current} 还没落盘：先保存一次，再复制。";
            return;
        }

        var typed = PromptText("复制预设", $"把「{current}」复制成：", SuggestCopyName(current));
        if (typed is null)
        {
            return;
        }

        if (!TryNormalizePresetName(typed, out var target, out var problem))
        {
            Notify?.Invoke("这个名字不能用", problem);
            return;
        }

        if (string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var targetPath = Path.Combine(_configDirectory, target);
        if (File.Exists(targetPath))
        {
            Notify?.Invoke("重名了", $"{target} 已经在配置目录里，换一个名字。");
            return;
        }

        try
        {
            File.Copy(sourcePath, targetPath, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = DescribeSaveFailure("复制预设失败", sourcePath, ex.Message);
            return;
        }

        SyncPresetOrderFromList();
        InsertInPresetOrder(current, target);
        RefreshPresets();
        PersistState();
        StatusText = $"已复制预设：{current} → {target}";
    }

    /// <summary>
    /// 新建一份空白预设：曲线从平直开始、没有滤波器，建完就让这一层切过去。
    /// 名字重了自动按「名字 2」往后排，不覆盖已有文件。
    /// </summary>
    public void CreatePreset(bool isMain)
    {
        if (_configDirectory is null)
        {
            StatusText = "没找到 EqualizerAPO 的配置目录，没法新建预设。";
            return;
        }

        if (PromptText is null)
        {
            return;
        }

        var directory = _configDirectory;
        var layer = isMain ? "左右" : "低音";
        var seed = (isMain ? "EsportsMain" : "EsportsBass") + PeaceConfig.Extension;
        var typed = PromptText($"新建{layer}预设", $"新{layer}预设的名字：", MakeUniquePresetName(directory, seed));
        if (typed is null)
        {
            return;
        }

        if (!TryNormalizePresetName(typed, out var name, out var problem))
        {
            Notify?.Invoke("这个名字不能用", problem);
            return;
        }

        var final = MakeUniquePresetName(directory, name);
        var path = Path.Combine(directory, final);
        var points = EqPointList.CreateDefault();

        if (!PeaceConfig.Save(path, points.Points, out var error))
        {
            StatusText = DescribeSaveFailure("新建预设失败", path, error);
            return;
        }

        SyncPresetOrderFromList();
        InsertInPresetOrder(isMain ? _mainConfigName : _bassConfigName, final);
        RefreshPresets();
        PersistState();

        // 建完直接切过去：新建就是为了开始调这一份
        if (isMain)
        {
            SelectedMainPreset = final;
        }
        else
        {
            SelectedBassPreset = final;
        }

        StatusText = $"已新建{layer}预设：{final}";
    }

    /// <summary>
    /// 删除预设文件。正在被某一层用着时先切到别的预设再删；
    /// 但这层还有没保存的改动时不让删 —— 删文件顺手把正在调的东西带走，太不讲道理。
    /// </summary>
    public void DeletePreset(string name)
    {
        if (_configDirectory is null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var current = Path.GetFileName(name);
        var path = Path.Combine(_configDirectory, current);
        var isMain = string.Equals(current, _mainConfigName, StringComparison.OrdinalIgnoreCase);
        var isBass = string.Equals(current, _bassConfigName, StringComparison.OrdinalIgnoreCase);
        var inUse = isMain || isBass;

        if (inUse && IsDirty)
        {
            StatusText = $"「{current}」正在用，而且还有没保存的改动：先保存或取消，再删。";
            return;
        }

        var fallback = AvailablePresets.FirstOrDefault(n => !string.Equals(n, current, StringComparison.OrdinalIgnoreCase));
        if (inUse && fallback is null)
        {
            Notify?.Invoke("至少要留一个", "这是下拉栏里最后一条预设，删掉就没得用了。");
            return;
        }

        var exists = File.Exists(path);
        if (exists && ConfirmAction is { } confirm
            && !confirm("删除预设", $"删除「{current}」？\n文件会从 EqualizerAPO 的配置目录里删掉，删了找不回来。"))
        {
            return;
        }

        // 先切走再删文件：正在编辑的那一层不能指着一条已经删掉的名字。
        if (isMain && fallback is not null)
        {
            SwitchPreset(fallback, isMain: true);
        }

        if (isBass && fallback is not null)
        {
            SwitchPreset(fallback, isMain: false);
        }

        if (exists)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusText = DescribeSaveFailure("删除预设失败", path, ex.Message);
                return;
            }
        }

        SyncPresetOrderFromList();
        RemoveFromPresetOrder(current);
        RefreshPresets();
        PersistState();
        StatusText = inUse
            ? $"预设已删除：{current} · 这一层已切到 {fallback}"
            : $"预设已删除：{current}";
    }

    /// <summary>下拉栏里现在是什么顺序，就以它为准来改顺序表。</summary>
    private void SyncPresetOrderFromList()
    {
        if (AvailablePresets.Count > 0)
        {
            _uiState.PresetOrder = AvailablePresets.ToList();
        }
    }

    private void ReplaceInPresetOrder(string oldName, string newName)
    {
        var index = _uiState.PresetOrder.FindIndex(n => string.Equals(n, oldName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _uiState.PresetOrder[index] = newName;
            return;
        }

        _uiState.PresetOrder.Add(newName);
    }

    private void InsertInPresetOrder(string after, string name)
    {
        var index = _uiState.PresetOrder.FindIndex(n => string.Equals(n, after, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _uiState.PresetOrder.Insert(index + 1, name);
            return;
        }

        _uiState.PresetOrder.Add(name);
    }

    private void RemoveFromPresetOrder(string name) =>
        _uiState.PresetOrder.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>把用户输入收拾成一个能用的文件名：去掉路径、补 .peace、挡住非法字符。</summary>
    private static bool TryNormalizePresetName(string typed, out string name, out string problem)
    {
        name = Path.GetFileName(typed.Trim());
        problem = string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            problem = "名字不能是空的。";
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            problem = "名字里不能有 \\ / : * ? \" < > | 这些字符。";
            return false;
        }

        if (!name.EndsWith(PeaceConfig.Extension, StringComparison.OrdinalIgnoreCase))
        {
            name += PeaceConfig.Extension;
        }

        return true;
    }

    private string SuggestCopyName(string current)
    {
        var directory = _configDirectory!;
        var stem = Path.GetFileNameWithoutExtension(current);
        var candidate = $"{stem} - 副本{PeaceConfig.Extension}";

        for (var index = 2; File.Exists(Path.Combine(directory, candidate)); index++)
        {
            candidate = $"{stem} - 副本 {index}{PeaceConfig.Extension}";
        }

        return candidate;
    }

    /// <summary>重新问一遍：config 目录在不在、入口文件有没有挂上 peace.txt。</summary>
    private void RefreshApoState()
    {
        IsApoFound = _configDirectory is not null;

        var text = _configDirectory is null ? null : ApoSetup.TryRead(_configDirectory);
        IsApoIncluded = text is not null && ApoSetup.IsIncluded(text, ConfigFileName);
    }

    /// <summary>
    /// 把 peace.txt 挂进 EqualizerAPO 的 config.txt。这是显式动作：不自动改用户的入口文件，
    /// 改之前先备份，只追加一行，别的内容原样保留。
    /// </summary>
    [RelayCommand]
    private async Task ConnectApoAsync()
    {
        if (_configDirectory is null)
        {
            StatusText = "找不到 EqualizerAPO 的 config 目录，接不上。";
            return;
        }

        var directory = _configDirectory;
        var includeName = ConfigFileName;
        var excludeName = EapoConfig.FileNameFor(!_uiState.IndependentMode);
        StatusText = "正在接上 EqualizerAPO…";

        var result = await Task
            .Run(() => ApoSetup.SwitchInclude(directory, includeName, excludeName, UiState.Directory))
            .ConfigureAwait(true);

        RefreshApoState();

        if (result.Error is not null)
        {
            StatusText = $"接入失败：{result.Error}"
                + (IsRunningElevated()
                    ? "（已经是管理员，检查 config.txt 是不是被别的程序占着）"
                    : "（关掉程序，右键「以管理员身份运行」再试）");
            return;
        }

        if (result.Changed)
        {
            var backup = result.BackupPath is null
                ? "（原文件没备份成功，但已经改了）"
                : $"（原文件备份到 {result.BackupPath}）";
            StatusText = $"已接入：{ApoSetup.ConfigFileName} 里加了 {ApoSetup.IncludeKeyword} {includeName} {backup}";
            return;
        }

        StatusText = $"本来就接好了：{ApoSetup.ConfigFileName} 里已经有 {ApoSetup.IncludeKeyword} {includeName}";
    }

    /// <summary>
    /// 打开 / 关闭独立模式。这是一次换档：先把当前已保存的结果写到新文件，
    /// 再把 config.txt 的 Include 二选一切过去，最后才改内存里的开关。
    /// 任何一步失败都停在原模式，不写 UiState；返回 null 表示成功，否则是错误原因。
    /// </summary>
    public async Task<string?> SetIndependentModeAsync(bool on)
    {
        if (_uiState.IndependentMode == on)
        {
            return null;
        }

        var directory = _configDirectory;
        var newName = EapoConfig.FileNameFor(on);
        var oldName = EapoConfig.FileNameFor(!on);

        if (directory is not null)
        {
            var layers = new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved);
            if (!layers.TryMerge(IsBassLayerEnabled, Flavors, out var merged, out var mergeError))
            {
                return "切换模式前合并曲线失败：" + mergeError;
            }

            var text = EapoConfig.Build(merged.Points, _savedPreAmpDb, _savedBalance, _deviceLine);
            var newPath = Path.Combine(directory, newName);
            var writeError = await Task.Run(() => WriteTextOrError(newPath, text)).ConfigureAwait(true);
            if (writeError is not null)
            {
                return $"写 {newName} 失败：{writeError}";
            }

            var switched = await Task
                .Run(() => ApoSetup.SwitchInclude(directory, newName, oldName, UiState.Directory))
                .ConfigureAwait(true);

            if (switched.Error is not null)
            {
                return $"改 {ApoSetup.ConfigFileName} 失败：{switched.Error}";
            }
        }

        // 换档完成：文件、入口、内存状态一起切。
        EndPreviewSession();
        _previewLastText = null;
        _uiState.IndependentMode = on;
        _configFilePath = directory is null ? null : Path.Combine(directory, newName);
        UiStateStore.Save(_uiState);
        RefreshApoState();

        OnPropertyChanged(nameof(IndependentMode));
        OnPropertyChanged(nameof(ConfigFileName));
        OnPropertyChanged(nameof(ConfigWarning));
        OnPropertyChanged(nameof(HasConfigWarning));

        if (!on)
        {
            // 回到 peace.txt：Peace 正开着的话，马上把推子对齐一次。
            var layers = new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved);
            if (layers.TryMerge(IsBassLayerEnabled, Flavors, out var merged, out _))
            {
                WriteMixPreset(merged.Points);
                _peaceSync.Queue(merged.Points, _savedPreAmpDb);
            }
        }

        StatusText = on
            ? $"独立模式已打开：以后写 {EapoConfig.IndependentFileName}，Peace 的 {EapoConfig.FileName} 不再被动。"
            : $"独立模式已关闭：以后写 {EapoConfig.FileName}，Peace 在跑时推子会跟着同步。";
        return null;
    }

    /// <summary>
    /// 打开程序就把 Peace 切到混音配置，不用等进编辑模式。
    /// 非独立模式才有这一步；Peace 没开时只把 EsportsMix 落到磁盘，
    /// 等它下次启动扫到，之后的调整会再触发切换。
    /// </summary>
    private void AutoSwitchPeaceToMix()
    {
        PeaceSync.Log($"启动切换：independent={_uiState.IndependentMode} dir={_configDirectory ?? "(null)"}");

        if (_uiState.IndependentMode || _configDirectory is null)
        {
            return;
        }

        var layers = new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved);
        if (!layers.TryMerge(IsBassLayerEnabled, Flavors, out var merged, out var mergeError))
        {
            PeaceSync.Log("启动切换：合并失败 " + mergeError);
            return;
        }

        WriteMixPreset(merged.Points);
        _peaceSync.Queue(merged.Points, _savedPreAmpDb);
    }

    /// <summary>
    /// 把合并结果也写进 Peace 的专用混音预设（EsportsMix），并保证它有配置 Id。
    /// 只在非独立模式写；失败不影响主流程——那份文件只是给 Peace 看的。
    /// </summary>
    private void WriteMixPreset(IReadOnlyList<EqPoint> points)
    {
        if (_uiState.IndependentMode || _configDirectory is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_configDirectory, PeaceSync.MixPresetName);
            if (!PeaceConfig.Save(path, points, out var saveError))
            {
                PeaceSync.Log($"写 {PeaceSync.MixPresetName} 失败：{saveError}");
                return;
            }

            if (!PeaceConfig.EnsureConfigurationId(path, PeaceSync.MixPresetId, out var idError))
            {
                PeaceSync.Log($"给 {PeaceSync.MixPresetName} 写 Id 失败：{idError}");
                return;
            }

            PeaceSync.Log($"已写 {PeaceSync.MixPresetName}：{points.Count} 段");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 混音预设写不进去不该挡住真正要写的 peace.txt。
            PeaceSync.Log("写混音预设异常：" + ex.Message);
        }
    }

    /// <summary>写一份文本；失败返回错误原因，不抛。</summary>
    private static string? WriteTextOrError(string path, string text)
    {
        try
        {
            EapoConfig.ClearReadOnly(path);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    private void ApplyLoaded(EapoConfigData? data, EqPointList? mainLayer, EqPointList? bassLayer)
    {
        _loading = true;

        _deviceLine = data?.DeviceLine ?? "Device: all";

        // peace.txt 是实际生效的那份声音，启动时以它为准；顺手记到当前左右预设名下。
        _savedPreAmpDb = Math.Clamp(data?.PreAmpDb ?? 0, EqLimits.MinPreAmpDb, EqLimits.MaxPreAmpDb);
        PreAmpDb = _savedPreAmpDb;
        _uiState.SetMainPreAmp(_mainConfigName, _savedPreAmpDb);

        // 风味跟着左右预设走：没记过就用默认的九条全平。
        var loadedFlavors = _uiState.TryGetMainFlavors(_mainConfigName, out var storedFlavors)
            ? storedFlavors
            : FlavorSet.CreateDefault();
        NonPeakingFilterCount = data?.NonPeakingFilters ?? 0;
        HasNonPeakingFilters = NonPeakingFilterCount > 0;

        // 预设文件是每一层的权威来源；没有就看本地缓存里有没有真正存过主层；
        // 再没有才用 peace.txt（它可能是合并结果，所以要排在缓存后面）。
        var cached = _uiState.ToLayerSet();
        var cachedMain = _uiState.MainPoints is { Count: > 0 } ? cached?.Main : null;
        var main = mainLayer ?? cachedMain ?? data?.Points ?? EqPointList.CreateDefault();
        var bass = bassLayer ?? cached?.Bass ?? EqPointList.CreateDefault();

        if (!_mainDraft.IsEditing)
        {
            _mainDraft.AcceptSaved(main, loadedFlavors);
        }

        if (!_bassDraft.IsEditing)
        {
            _bassDraft.AcceptSaved(bass);
        }

        _savedBalance = Math.Clamp(_uiState.Balance, -1, 1);
        Balance = _savedBalance;
        _savedBassLayerEnabled = _uiState.BassLayerEnabled;
        IsBassLayerEnabled = _savedBassLayerEnabled;

        RefreshApoState();
        _loading = false;

        // 开关上次是开的：启动时把 DLL 和 protect.txt 补到位（可能换了机器 / 被清掉了）。
        if (IsPeakGuardEnabled)
        {
            ApplyPeakGuard(revertOnFailure: false);
        }

        PersistState();
        RaiseDerived();
        ApplySpectrumSetting();
        StatusText = _configDirectory is null
            ? "找不到 EqualizerAPO 的 config 目录，只能看本地缓存。"
            : !IsApoIncluded
                ? $"已载入 · 左右 {main.Count} 点 · 低音 {bass.Count} 点 · 但 APO 还没加载 {ConfigFileName}，点顶栏「接入 APO」"
                : $"已载入 · 左右 {main.Count} 点（{_mainConfigName}）· 低音 {bass.Count} 点（{_bassConfigName}）";
    }

    private void PersistState()
    {
        _uiState.GainRangeDb = GainRangeDb;
        _uiState.SetLayers(new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved));
        _uiState.SetMainFlavors(_mainConfigName, _mainDraft.SavedFlavors);
        _uiState.Balance = _savedBalance;
        _uiState.BassLayerEnabled = _savedBassLayerEnabled;
        _uiState.SpectrumEnabled = IsSpectrumEnabled;
        _uiState.PeakGuardEnabled = IsPeakGuardEnabled;
        _uiState.MainConfig = _mainConfigName;
        _uiState.BassConfig = _bassConfigName;

        // 教程最后一步的演示组合只活在内存里：写盘时把它从这份快照里滤掉，
        // 否则教程里关窗口、或后台切进程触发保存，都会把它写成用户的配置。
        var demo = TutorialAutoSwitchDemo;
        var profiles = _uiState.AutoSwitchProfiles;
        if (demo is not null && profiles is not null && profiles.Contains(demo))
        {
            _uiState.AutoSwitchProfiles = profiles.Where(item => !ReferenceEquals(item, demo)).ToList();
            try
            {
                UiStateStore.Save(_uiState);
            }
            finally
            {
                _uiState.AutoSwitchProfiles = profiles;
            }

            return;
        }

        UiStateStore.Save(_uiState);
    }

    // ---- 属性变化 ----

    partial void OnPreAmpDbChanged(double value)
    {
        if (_loading)
        {
            return;
        }

        // 只读态拖增益推子 = 开始改这份预设，直接进「左右」编辑。
        // 不进编辑的话，停手 450 ms 后 FlushLiveSave 会把它当「平衡 / 增益」直接落盘，
        // 顺手把 _savedPreAmpDb 一起改掉；IsDirty 归零，保存 / 取消按钮跟着消失，拖完就退不回去了。
        if (Mode == MainMode.None && !_tutorialScenario)
        {
            _liveSaveTimer.Stop();
            EnterMode(MainMode.MainLayer, announceFilterWarning: false);
        }

        RaiseDerived();
        ScheduleSoundUpdate();
    }

    partial void OnModeChanged(MainMode value)
    {
        // 自动风味只在「左右」编辑态里有意义：离开编辑（或切到低音）就收掉，
        // 免得声音停在校准链上、数据却还挂着。
        if (!IsFlavorEditable && IsAutoFlavorMode)
        {
            _autoFlavorAudio = null;
            _autoFlavorDisplay = null;
            ExitAutoFlavorMode();
            _ = RestorePreviewAsync();
        }

        RaiseDerived();
    }

    partial void OnSelectedBandChanged(ToneBand? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.Equals(value.Key, "all", StringComparison.Ordinal))
        {
            SelectedFlavorIndex = AllFlavorsIndex;
            return;
        }

        var index = ToneBands.IndexOf(value.Key);
        if (index >= 0)
        {
            SelectedFlavorIndex = index;
        }

    }

    partial void OnSelectedFlavorIndexChanged(int value)
    {
        if (value == AllFlavorsIndex)
        {
            return;
        }

        if (value < 0 || value >= ToneBands.All.Count)
        {
            return;
        }

        var band = ToneBands.All[value];
        if (!Equals(SelectedBand, band))
        {
            SelectedBand = band;
        }
    }

    partial void OnIsConfigWritableChanged(bool value)
    {
        OnPropertyChanged(nameof(IsConfigReadOnly));
        OnPropertyChanged(nameof(ConfigWarning));
        OnPropertyChanged(nameof(HasConfigWarning));
    }

    partial void OnIsApoFoundChanged(bool value) => RaiseApoWarning();

    partial void OnIsApoIncludedChanged(bool value) => RaiseApoWarning();

    private void RaiseApoWarning()
    {
        OnPropertyChanged(nameof(HasApoWarning));
        OnPropertyChanged(nameof(ApoWarning));
    }

    /// <summary>当前进程是不是管理员。用来把「没提权」和「提了权仍被拒」分开。</summary>
    private static bool IsRunningElevated() => ConfigPermission.IsElevated();

    /// <summary>
    /// 权限窗口修完之后，重新问一遍目录还写不写得了。
    /// 只重测、不重载预设：修 ACL 不该顺带把正在编辑的曲线冲掉。
    /// </summary>
    public void RecheckConfigWritable()
    {
        if (_configDirectory is null)
        {
            return;
        }

        IsConfigWritable = EapoConfig.CanWrite(_configDirectory);
        StatusText = IsConfigWritable
            ? "配置目录现在可写了，保存会正常落盘。"
            : "配置目录还是不可写，看权限窗口里的结论。";
    }

    /// <summary>
    /// 设置窗口的「重新检查连接」：config 目录、Include、可写性都重新问一遍。
    /// 只更新状态，不重载预设，不打断正在编辑的曲线。
    /// </summary>
    public void RecheckApoConnection()
    {
        _configDirectory = EapoConfig.FindConfigDirectory(_uiState.ConfigDirectoryOverride);
        _configFilePath = _configDirectory is null ? null : Path.Combine(_configDirectory, ConfigFileName);
        IsConfigWritable = _configDirectory is not null && EapoConfig.CanWrite(_configDirectory);

        OnPropertyChanged(nameof(ConfigDirectory));
        OnPropertyChanged(nameof(IsConfigReadOnly));
        RefreshApoState();

        StatusText = _configDirectory is null
            ? "还是没找到 EqualizerAPO 的 config 目录。"
            : IsApoIncluded
                ? "已经连上 EqualizerAPO，改动会立即生效。"
                : "找到目录了，但 config.txt 里还没挂 peace.txt。";
    }

    /// <summary>
    /// 设置窗口里手选的配置目录：要求里面有 config.txt（选到安装目录时，自动往下找 config 子目录）。
    /// 存进 ui-state 后立即重查，不用重启。
    /// </summary>
    public bool TrySetConfigDirectory(string? directory, out string? error)
    {
        var candidate = directory?.Trim();
        if (string.IsNullOrEmpty(candidate) || !Directory.Exists(candidate))
        {
            error = "这个文件夹不存在。";
            return false;
        }

        if (!File.Exists(Path.Combine(candidate, ApoSetup.ConfigFileName)))
        {
            var sub = Path.Combine(candidate, "config");
            if (File.Exists(Path.Combine(sub, ApoSetup.ConfigFileName)))
            {
                candidate = sub;
            }
            else
            {
                error = $"这里没有 {ApoSetup.ConfigFileName}，请选 EqualizerAPO 的 config 文件夹。";
                return false;
            }
        }

        _uiState.ConfigDirectoryOverride = candidate;
        _configDirectory = candidate;
        _configFilePath = Path.Combine(candidate, ConfigFileName);
        IsConfigWritable = EapoConfig.CanWrite(candidate);

        OnPropertyChanged(nameof(ConfigDirectory));
        OnPropertyChanged(nameof(IsConfigReadOnly));
        RefreshApoState();
        UiStateStore.Save(_uiState);

        StatusText = $"配置目录已切到 {candidate}。";
        error = null;
        return true;
    }

    /// <summary>开机自启时是否把主界面显示出来；设置窗口的开关写这里。</summary>
    public bool ShowWindowOnStart
    {
        get => _uiState.ShowWindowOnStart;
        set
        {
            _uiState.ShowWindowOnStart = value;
            UiStateStore.Save(_uiState);
        }
    }

    /// <summary>
    /// 组合切换时是否在主屏顶部滑一条曲线通知。设置窗口的开关写这里；
    /// 关掉不影响切换本身，只是不再弹。
    /// </summary>
    public bool ShowSwitchOsd
    {
        get => _uiState.ShowSwitchOsd;
        set
        {
            _uiState.ShowSwitchOsd = value;
            UiStateStore.Save(_uiState);
        }
    }

    /// <summary>
    /// 点关闭按钮时做什么。设置里的单选框和首次关闭的询问都写这里，
    /// 但只有「记住选择」才写盘 —— 这就是它和 <see cref="RememberCloseAction"/> 的区别。
    /// </summary>
    public CloseAction CloseAction
    {
        get => _uiState.CloseAction;
        set => RememberCloseAction(value);
    }

    /// <summary>关闭行为是不是用户记住过的；false 时每次关闭都要问。</summary>
    public bool IsCloseActionRemembered => _uiState.CloseActionRemembered;

    /// <summary>记住下次关闭怎么做。只改选择、不触发任何界面动作。</summary>
    public void RememberCloseAction(CloseAction action)
    {
        _uiState.CloseAction = action;
        _uiState.CloseActionRemembered = true;
        UiStateStore.Save(_uiState);
    }

    partial void OnBalanceChanged(double value)
    {
        if (!_loading)
        {
            OnPropertyChanged(nameof(BalanceSummary));
            ScheduleSoundUpdate();
        }

        RaiseDerived();
    }

    /// <summary>叠加开关一改就保存，不弹询问；但编辑期间只记下勾选，退出编辑才生效。</summary>
    partial void OnIsBassLayerEnabledChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        OnPropertyChanged(nameof(BassLayerSummary));
        RaiseDerived();

        if (IsEditing)
        {
            // 编辑时的叠加方式由当前层决定（编低音叠左右、编左右不叠低音），
            // 勾选只记下来：退出编辑时才按它重写声音、才写回预设。
            StatusText = value
                ? "已勾选叠加 · 退出编辑后才会叠加并保存。"
                : "已取消叠加 · 退出编辑后才会撤销叠加并保存。";
            return;
        }

        if (!IsApoFound)
        {
            return;
        }

        if (SaveBlocking())
        {
            StatusText = value ? "低音层已叠加并保存。" : "低音层已关闭并保存。";
        }
    }

    private void FlushQualityIfIdle()
    {
        if (_qualityPending && DateTime.UtcNow - _qualityAt > TimeSpan.FromMilliseconds(300))
        {
            FlushQualityNow();
        }
    }

    private void FlushQualityNow()
    {
        if (!_qualityPending)
        {
            return;
        }

        _qualityPending = false;
        _lastEditDraft = ActiveDraft;
        ActiveDraft.Commit();
    }

    // ---- 采集、电平、频谱 ----

    /// <summary>
    /// 常驻的回环采集：电平表、频谱、语音唤醒、自动风味都挂在它上面，不另开一路 WASAPI。
    /// </summary>
    public PeaceWpf.Core.Audio.LoopbackCaptureService SpectrumCapture => _spectrumCapture;

    /// <summary>
    /// 采集始终开着：右侧电平表要用它，频谱只是决定要不要算 FFT。
    /// 抓不到就静默留空，不弹窗、不报错。
    /// </summary>
    private void ApplySpectrumSetting()
    {
        if (!_spectrumCapture.Start())
        {
            _spectrumTimer.Stop();
            Spectrum = null;
            PeakDb = -90;
            SpectrumHint = "电平 / 频谱不可用：抓不到系统输出";
            OnPropertyChanged(nameof(WarningsSummary));
            return;
        }

        SpectrumHint = string.Empty;
        OnPropertyChanged(nameof(WarningsSummary));
        if (!_spectrumTimer.IsEnabled)
        {
            _spectrumTimer.Start();
        }

        if (!IsSpectrumEnabled)
        {
            Spectrum = null;
        }
    }

    private void PumpSpectrum()
    {
        PeakDb = _spectrumCapture.PeakDb;

        if (!IsSpectrumEnabled)
        {
            return;
        }

        Spectrum = _spectrumCapture.Snapshot();
    }

    partial void OnIsSpectrumEnabledChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        ApplySpectrumSetting();
        PersistState();
    }

    /// <summary>无延迟防爆音：开关一变就把 DLL 和 protect.txt 落到位。</summary>
    partial void OnIsPeakGuardEnabledChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        ApplyPeakGuard(revertOnFailure: true);
        PersistState();
    }

    /// <summary>
    /// 把开关写进 APO 配置。失败时（用户点的开关）把开关弹回去，免得界面显示开着、声音没变。
    /// 启动时补文件失败只写状态栏，不擅自改掉用户上次的选择。
    /// </summary>
    private void ApplyPeakGuard(bool revertOnFailure)
    {
        var result = PeakGuardSetup.Apply(_configDirectory, IsPeakGuardEnabled);
        if (result.Error is null)
        {
            if (revertOnFailure || IsPeakGuardEnabled)
            {
                StatusText = IsPeakGuardEnabled
                    ? "无延迟防爆音已开：只在快破音时削峰，平时不改变音量。"
                    : "无延迟防爆音已关。";
            }

            return;
        }

        if (revertOnFailure)
        {
            _loading = true;
            IsPeakGuardEnabled = !IsPeakGuardEnabled;
            _loading = false;
        }

        StatusText = "无延迟防爆音没生效：" + result.Error;
    }

    // ---- 平衡：拖完就写（增益不走这条，拖它直接进「左右」编辑）----

    /// <summary>拖动中不写盘；停手 450 ms 由 <see cref="FlushLiveSave"/> 写一次。</summary>
    private void ScheduleLiveSave()
    {
        if (_loading || !IsApoFound)
        {
            return;
        }

        _liveSaveTimer.Stop();
        _liveSaveTimer.Start();
    }

    private void FlushLiveSave()
    {
        _liveSaveTimer.Stop();

        // 编辑态只走试听：这里再落一次盘，会把正在改的增益 / 平衡当成“已保存”，
        // 脏标记和保存 / 取消按钮就没意义了（拖动推子刚带进编辑时正是这个窗口）。
        if (IsEditing)
        {
            return;
        }

        if (Math.Abs(Balance - _savedBalance) < 0.0005 && Math.Abs(PreAmpDb - _savedPreAmpDb) < 0.001)
        {
            return;
        }

        _ = ApplyLiveSettingsAsync();
    }

    // ---- 试听：改动不等保存 ----

    /// <summary>
    /// 改动要不要马上出声：编辑曲线时走试听（可回滚），只读态拖平衡时走即时保存。
    /// 增益不会有只读态：它一变就先把自己带进编辑（见 OnPreAmpDbChanged）。
    /// </summary>
    private void ScheduleSoundUpdate()
    {
        if (IsEditing)
        {
            SchedulePreview();
        }
        else
        {
            ScheduleLiveSave();
        }
    }

    /// <summary>节流 150 ms：手指还在动就等下一次空闲，别把盘刷爆、也别让引擎一直重载。</summary>
    private void SchedulePreview()
    {
        // 试听扫频正在临时叠加低音：这时按「编辑左右不叠低音」的规则写盘，
        // 会把刚叠上去的低频冲掉，扫频听到一半就变味。
        if (_loading || _previewSuspended || _sweepPreviewActive || _calibrationMeasuring || !IsApoFound || !IsConfigWritable || _configFilePath is null)
        {
            return;
        }

        if (_previewTimer.IsEnabled)
        {
            return;
        }

        var since = (DateTime.UtcNow - _previewLastAt).TotalMilliseconds;
        if (since >= PreviewIntervalMs)
        {
            _previewWriteTask = WritePreviewAsync();
            return;
        }

        _previewTimer.Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs - since);
        _previewTimer.Start();
    }

    private void OnPreviewTick()
    {
        _previewTimer.Stop();
        _previewWriteTask = WritePreviewAsync();
    }

    /// <summary>
    /// 把草稿的合并结果写进 peace.txt。EqualizerAPO 监视这个文件，写完就出声——
    /// 「不保存也能听到」就靠这一条通道，没有别的。
    /// </summary>
    private async Task WritePreviewAsync()
    {
        if (_configFilePath is null)
        {
            return;
        }

        if (_previewWriting)
        {
            _previewPending = true;
            return;
        }

        var set = new EqLayerSet(_mainDraft.Current, _bassDraft.Current);
        if (!set.TryMerge(PreviewIncludeBass, WriterFlavors, out var merged, out _))
        {
            return;
        }

        var text = EapoConfig.Build(merged.Points, PreAmpDb, Balance, _deviceLine);
        if (string.Equals(text, _previewLastText, StringComparison.Ordinal))
        {
            return;
        }

        var path = _configFilePath;
        var takeSnapshot = !_previewActive;

        _previewWriting = true;
        _previewPending = false;
        _previewLastAt = DateTime.UtcNow;

        await _peaceWriteGate.WaitAsync().ConfigureAwait(true);
        (string? original, bool existed, string? error) result;
        try
        {
            result = await Task.Run(() =>
            {
                try
                {
                    var exists = File.Exists(path);
                    var previous = takeSnapshot && exists ? File.ReadAllText(path) : null;
                    EapoConfig.ClearReadOnly(path);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    return (previous, exists, (string?)null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return ((string?)null, false, ex.Message);
                }
            }).ConfigureAwait(true);
        }
        finally
        {
            _peaceWriteGate.Release();
        }

        _previewWriting = false;
        var (original, existed, error) = result;

        if (error is not null)
        {
            _previewPending = false;
            StatusText = $"试听写不进 {ConfigFileName}：{error}（改动只在内存里，保存时会再试）";
            return;
        }

        if (_previewSuspended)
        {
            // 正在取消：这次写入作废。如果它是这个会话的第一次写入，就把原文写回去，
            // 否则「取消」会来不及知道有这么一次试听，文件就停在试听内容上。
            if (takeSnapshot && existed && original is not null)
            {
                await Task.Run(async () =>
                {
                    await _peaceWriteGate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.WriteAllText(path, original, new UTF8Encoding(false));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // 还原不了就交给下一次保存覆盖，这里不打断。
                    }
                    finally
                    {
                        _peaceWriteGate.Release();
                    }
                }).ConfigureAwait(true);
            }

            return;
        }

        if (takeSnapshot)
        {
            _previewOriginal = original;
            _previewOriginalExisted = existed;
            _previewActive = true;
        }

        // 试听也同步：Peace 在跑的时候，拖曲线它的推子跟着动。
        if (!_uiState.IndependentMode)
        {
            WriteMixPreset(merged.Points);
            _peaceSync.Queue(merged.Points, PreAmpDb);
        }

        _previewLastText = text;

        if (_previewPending)
        {
            SchedulePreview();
        }
    }

    /// <summary>取消时把 peace.txt 还原成试听前的内容，声音跟着退回去。</summary>
    private async Task RestorePreviewAsync()
    {
        _previewTimer.Stop();
        _previewPending = false;
        _previewSuspended = true;

        var path = _configFilePath;
        var active = _previewActive;
        var original = _previewOriginal;
        var existed = _previewOriginalExisted;

        _previewActive = false;
        _previewOriginal = null;
        _previewOriginalExisted = false;
        _previewLastText = null;

        if (!active || path is null)
        {
            _previewSuspended = false;
            return;
        }

        var error = await Task.Run(async () =>
        {
            await _peaceWriteGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!existed)
                {
                    if (File.Exists(path))
                    {
                        EapoConfig.ClearReadOnly(path);
                        File.Delete(path);
                    }

                    return (string?)null;
                }

                EapoConfig.ClearReadOnly(path);
                File.WriteAllText(path, original ?? string.Empty, new UTF8Encoding(false));
                return (string?)null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ex.Message;
            }
            finally
            {
                _peaceWriteGate.Release();
            }
        }).ConfigureAwait(true);

        _previewSuspended = false;

        if (error is not null)
        {
            StatusText = $"还原 {ConfigFileName} 失败：{error} · 声音可能还停在试听状态";
            return;
        }

        // 取消只把曲线退回上次保存的样子；风味是设置、不在被还原的原文里，
        // 所以按「已保存曲线 + 风味」再写一遍，否则取消之后声音里的风味会跟着消失。
        if (!_mainDraft.SavedFlavors.IsFlat)
        {
            await ApplyLiveSettingsAsync(announce: false);
        }

        // 还原只是把文件写回去，Peace 那边不会自己知道；按勾选状态再对齐一次。
        if (!_uiState.IndependentMode)
        {
            var set = new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved);
            if (set.TryMerge(IsBassLayerEnabled, Flavors, out var merged, out _))
            {
                WriteMixPreset(merged.Points);
                _peaceSync.Queue(merged.Points, PreAmpDb);
            }
        }
    }

    /// <summary>结束试听状态（保存成功、切换预设时用），不动文件。</summary>
    private void EndPreviewSession()
    {
        _previewTimer.Stop();
        _previewPending = false;
        _previewActive = false;
        _previewOriginal = null;
        _previewOriginalExisted = false;
    }

    /// <summary>
    /// 只重写 peace.txt：用“已保存”的那两层点，不动草稿，也不碰 .peace 文件。
    /// 这样拖平衡或退出编辑收尾时，不会把还没保存的曲线改动提前生效。
    /// </summary>
    /// <param name="announce">false 时不动状态栏（取消编辑收尾时用，别盖掉「已放弃改动」）。</param>
    private async Task ApplyLiveSettingsAsync(bool announce = true)
    {
        if (_configDirectory is null || _configFilePath is null)
        {
            return;
        }

        var balance = Math.Clamp(Balance, -1, 1);
        var preAmp = PreAmpDb;
        var set = new EqLayerSet(_mainDraft.Saved, _bassDraft.Saved);
        if (!set.TryMerge(IsBassLayerEnabled, Flavors, out var merged, out var mergeError))
        {
            StatusText = mergeError;
            return;
        }

        var text = EapoConfig.Build(merged.Points, preAmp, balance, _deviceLine);
        var path = _configFilePath;

        var error = await Task.Run(() =>
        {
            try
            {
                BackupPeace(path);
                EapoConfig.ClearReadOnly(path);
                File.WriteAllText(path, text, new UTF8Encoding(false));
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }).ConfigureAwait(true);

        if (error is not null)
        {
            StatusText = DescribeSaveFailure($"写 {ConfigFileName} 失败", path, error);
            return;
        }

        _savedBalance = balance;
        _savedPreAmpDb = preAmp;
        PersistState();

        if (!_uiState.IndependentMode)
        {
            WriteMixPreset(merged.Points);
            _peaceSync.Queue(merged.Points, preAmp);
        }

        RaiseDerived();
        if (announce)
        {
            StatusText = IsDirty
                ? "平衡已生效 · 曲线改动还没保存"
                : "平衡已生效。";
        }
    }

    /// <summary>
    /// 备份只是保险，不是写入的前置条件：备份失败就跳过，绝不因此把「拖滑块没反应」变成静默失败。
    /// 这里曾经让 UnauthorizedAccessException 冒出去，结果整个即时保存都作废——
    /// 一个不重要的 .bak 挡住了真正要写的 peace.txt。
    /// </summary>
    private static void BackupPeace(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var backup = Path.Combine(UiState.Directory, Path.GetFileName(path) + ".bak");
            Directory.CreateDirectory(UiState.Directory);
            File.Copy(path, backup, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 备份不了就算了，下面的写入才是正事。
        }
    }

    /// <summary>窗口关掉时停采集，别把后台线程留在那里。</summary>
    public void Shutdown()
    {
        _spectrumTimer.Stop();
        _liveSaveTimer.Stop();
        _spectrumCapture.Dispose();
        _peaceSync.Stop();
    }

    private void RaiseDraftState()
    {
        OnPropertyChanged(nameof(Points));
        OnPropertyChanged(nameof(ReferencePoints));
        RaiseDerived();

        // 曲线一改就试听：写进 peace.txt，引擎读它，声音立刻变。
        SchedulePreview();
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsMainLayerMode));
        OnPropertyChanged(nameof(IsBassLayerMode));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(IsFlavorEditable));
        OnPropertyChanged(nameof(Flavors));
        OnPropertyChanged(nameof(DisplayFlavors));
        OnPropertyChanged(nameof(ShowManualFlavorGroup));
        OnPropertyChanged(nameof(FlavorHandlesEditable));
        OnPropertyChanged(nameof(IsInstrumentNaming));
        OnPropertyChanged(nameof(IsConfigReadOnly));
        OnPropertyChanged(nameof(ConfigWarning));
        OnPropertyChanged(nameof(HasConfigWarning));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsActiveDraftDirty));
        OnPropertyChanged(nameof(ShowDraftActions));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(PointsSummary));
        OnPropertyChanged(nameof(MergedPointCount));
        OnPropertyChanged(nameof(PeaceOverflowHint));
        OnPropertyChanged(nameof(Points));
        OnPropertyChanged(nameof(ReferencePoints));
        OnPropertyChanged(nameof(GraphPreAmpDb));
        OnPropertyChanged(nameof(IsPreampEnabled));
        OnPropertyChanged(nameof(BalanceSummary));
        OnPropertyChanged(nameof(BassLayerSummary));
        OnPropertyChanged(nameof(NonPeakingHint));
        OnPropertyChanged(nameof(ConfigDirectory));
        OnPropertyChanged(nameof(CurveSummary));
        OnPropertyChanged(nameof(EnvironmentSummary));
        OnPropertyChanged(nameof(WarningsSummary));
    }

    private void Post(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.BeginInvoke(action);
    }

    private sealed record SaveRequest(
        string MainPath,
        string BassPath,
        string PeacePath,
        EqPointList MainPoints,
        EqPointList BassPoints,
        EqPointList Merged,
        double PreAmp,
        double Balance,
        string DeviceLine);

    private sealed record SaveOutcome(bool Ok, string Message);
}
