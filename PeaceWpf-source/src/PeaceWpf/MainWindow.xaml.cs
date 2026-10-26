using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PeaceWpf.Controls;
using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Model;
using PeaceWpf.ViewModels;
using Wpf.Ui.Controls;

namespace PeaceWpf;

public partial class MainWindow : FluentWindow
{
    private bool _closeConfirmed;
    private SettingsWindow? _settingsWindow;
    private AutoEqWindow? _autoEqWindow;
    private AutoCalibrateWindow? _calibrateWindow;
    private TutorialOverlay? _tutorialOverlay;
    private Window? _tutorialHostWindow;
    private readonly List<Window> _tutorialOwnedWindows = new();
    private TutorialWindowKind _tutorialCurrentWindow = TutorialWindowKind.Main;

    /// <summary>主界面「扫频」用的播放器，和自动EQ里那个是同一个类。</summary>
    private readonly ChirpPreviewPlayer _sweepPlayer = new();

    /// <summary>主界面扫频计时：ChirpPreviewPlayer 不报播放位置，只能自己掐表。</summary>
    private System.Diagnostics.Stopwatch? _sweepWatch;

    // 本次实际播放的频段和时长：绿线必须跟着它走，不能再写死 20–20k / 5 秒。
    private double _sweepFromHz = ChirpPreviewPlayer.StartHz;
    private double _sweepToHz = ChirpPreviewPlayer.EndHz;
    private double _sweepSeconds = ChirpPreviewPlayer.Seconds;
    private System.Windows.Threading.DispatcherTimer? _sweepTimer;

    /// <summary>扫频放完的还原等待；连点扫频时用来取消上一次。</summary>
    private CancellationTokenSource? _sweepRestoreCts;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;

        // 顶栏和底栏是定宽内容，窗口缩过头两簇就会互相压掉。
        // 最小宽度不写死，改由内容自然尺寸算出来（见 UpdateContentMinSize）。
        Loaded += OnLoaded;
        SizeChanged += OnContentSizeChanged;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.ConfirmDirty = AskDirty;
        viewModel.PromptText = AskText;
        viewModel.ConfirmAction = AskConfirm;
        viewModel.Notify = (title, message) => AppDialog.Info(this, title, message);
    }

    private DirtyChoice AskDirty(string title, string message)
    {
        var index = AppDialog.Choose(
            this,
            title,
            message + "\n\n保存：把改动写回预设\n放弃改动：回到上次保存的状态\n继续编辑：什么都不做，接着调",
            "保存",
            "放弃改动",
            "继续编辑");

        return index switch
        {
            0 => DirtyChoice.Save,
            1 => DirtyChoice.Discard,
            _ => DirtyChoice.Stay,
        };
    }

    /// <summary>要一行字：重命名 / 复制预设用。取消返回 null。</summary>
    private string? AskText(string title, string message, string initial)
    {
        var prompt = new TextPromptWindow(title, message, initial) { Owner = this };
        return prompt.ShowDialog() == true ? prompt.Value : null;
    }

    /// <summary>破坏性操作前的确认。默认落在“否”，回车不会顺手把文件删掉。</summary>
    private bool AskConfirm(string title, string message) =>
        AppDialog.Confirm(this, title, message, "确定", "取消");

    private void OnCreatePointRequested(object? sender, PointCreateEventArgs e)
    {
        e.CreatedPointId = ViewModel?.CreatePoint(e.FrequencyHz) ?? -1;
    }

    private void OnPointMovePreview(object? sender, PointMoveEventArgs e)
    {
        ViewModel?.PreviewPoint(e.Id, e.FrequencyHz, e.GainDb);
    }

    private void OnPointMoveCommitted(object? sender, EventArgs e)
    {
        ViewModel?.CommitPoint();
    }

    private void OnPointDeleteRequested(object? sender, PointEventArgs e)
    {
        ViewModel?.DeletePoint(e.Id);
    }

    private void OnQualityChanged(object? sender, PointQualityEventArgs e)
    {
        ViewModel?.ChangePointQuality(e.Id, e.Quality);
    }

    // ---- 风味（独立叠加层）----

    private void OnFlavorSelected(object? sender, FlavorIndexEventArgs e)
    {
        ViewModel?.SelectFlavor(e.Index);
    }

    private void OnFlavorFrequencyPreview(object? sender, FlavorMoveEventArgs e)
    {
        ViewModel?.PreviewFlavor(e.Index, e.FrequencyHz, e.GainDb);
    }

    private void OnFlavorFrequencyCommitted(object? sender, FlavorIndexEventArgs e)
    {
        ViewModel?.CommitFlavor(e.Index);
    }

    private void OnFlavorQualityChanged(object? sender, FlavorQualityEventArgs e)
    {
        ViewModel?.ChangeFlavorQuality(e.Index, e.Quality);
    }

    private void OnFlavorReset(object? sender, FlavorIndexEventArgs e)
    {
        ViewModel?.ResetFlavor(e.Index);
    }

    // ---- 标题栏 ----

    private void OnTitleBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            OnMaximize(sender, e);
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 鼠标已经松开时 DragMove 会抛，忽略。
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    /// <summary>右键均衡滑条：不弹菜单，直接把平衡拉回正中间（带滑动动画）。</summary>
    private void OnBalanceReset(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ResetSliderWithAnimation(sender as Slider, value =>
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.Balance = value;
            }
        });

        e.Handled = true;
    }

    /// <summary>右键增益推子：同样动画回到 0 dB。</summary>
    private void OnPreampReset(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ResetSliderWithAnimation(sender as Slider, value =>
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.PreAmpDb = value;
            }
        });

        e.Handled = true;
    }

    /// <summary>
    /// 让滑块的拇指先滑到 0，再把值写回 ViewModel：
    /// 动画期间不写绑定，避免拖一路触发一串配置刷新；到位后一次落定。
    /// </summary>
    private static void ResetSliderWithAnimation(Slider? slider, System.Action<double> apply)
    {
        if (slider is null)
        {
            return;
        }

        var from = slider.Value;
        if (Math.Abs(from) < 0.0005)
        {
            apply(0);
            return;
        }

        var animation = new System.Windows.Media.Animation.DoubleAnimation(
            from,
            0,
            TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
            },
        };

        animation.Completed += (_, _) =>
        {
            slider.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
            slider.Value = 0;
            apply(0);
        };

        slider.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
    }

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(viewModel) { Owner = this };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    /// <summary>先问基础还是进阶，选完铺全屏气泡；走到功能按钮时打开对应窗口继续引导。</summary>
    private void OnOpenTutorial(object sender, RoutedEventArgs e)
    {
        if (_tutorialOverlay is not null)
        {
            return;
        }

        var picker = new TutorialChoiceWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.Chosen is not { } course)
        {
            return;
        }

        StartTutorial(course);
    }

    private void StartTutorial(TutorialCourse course)
    {
        if (_tutorialOverlay is not null || ViewModel is null)
        {
            return;
        }

        var steps = TutorialSteps.For(course);
        if (course == TutorialCourse.Advanced)
        {
            // 进阶课用真实曲线演示：临时换成教程那两层（左右平直 / 低音 100 Hz 以下增强）。
            ViewModel.BeginTutorialScenario();
        }

        var overlay = new TutorialOverlay(
            steps,
            course == TutorialCourse.Basic ? "基础课" : "进阶课",
            PrepareTutorialStepAsync);

        overlay.KeepGraphClear = course == TutorialCourse.Advanced;
        overlay.CurvePointAt = (hz, db) => Graph.PointOnGraph(hz, db);        overlay.CurveAddPoint = hz => ViewModel.TutorialAddPoint(hz);
        overlay.CurveMovePoint = (id, hz, db) => ViewModel.TutorialPreviewPoint(id, hz, db);
        overlay.CurveCommitPoint = ViewModel.TutorialCommit;
        overlay.CurveDeletePoint = id => ViewModel.TutorialDeletePoint(id);
        overlay.CurveUndo = ViewModel.TutorialUndo;
        overlay.CurveChangeQuality = (id, quality) => ViewModel?.ChangePointQuality(id, quality);
        overlay.CurveResetToFlat = () => ViewModel?.ResetTutorialMainCurve();
        overlay.CurveFinishDemo = () => ViewModel?.FinishTutorialCurveDemo();
        overlay.CurveSetPreAmp = value =>
        {
            if (ViewModel is { } vm)
            {
                vm.PreAmpDb = value;
            }
        };
        overlay.AppPreviewAsync = async (bass, lowOnly) =>
        {
            if (ViewModel is not { } vm)
            {
                return false;
            }

            var (ok, _) = await vm.BeginTutorialPreviewAsync(bass);
            if (!ok)
            {
                return false;
            }

            // 真正出声的是这个播放器：之前只写了 peace.txt，没启动它，所以试听没声音。
            var from = lowOnly ? 20 : 1000;
            var to = lowOnly ? 300 : 20000;
            if (!StartSweepRange(from, to))
            {
                await vm.EndTutorialPreviewAsync();
                return false;
            }

            StartSweepCursor();
            return true;
        };
        overlay.AppPreviewFlatAsync = async () =>
        {
            if (ViewModel is not { } vm)
            {
                return false;
            }

            var (ok, _) = await vm.BeginTutorialPreviewAsync(false, flat: true);
            if (!ok)
            {
                return false;
            }

            if (!StartSweepRange(1000, 20000))
            {
                await vm.EndTutorialPreviewAsync();
                return false;
            }

            StartSweepCursor();
            return true;
        };
        overlay.AppStopPreview = () =>
        {
            _sweepPlayer.Stop();
            if (ViewModel is { } vm)
            {
                _ = vm.EndTutorialPreviewAsync();
            }
        };

        var host = new Window
        {
            Title = "教程 · 引导",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Topmost = true,
            Owner = this,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight,
            Content = overlay,
        };

        _tutorialOverlay = overlay;
        _tutorialHostWindow = host;
        _tutorialCurrentWindow = TutorialWindowKind.Main;

        overlay.Closed += (_, _) => host.Close();
        host.Closed += (_, _) => CleanupTutorial();

        host.Show();
        overlay.Focus();
    }

    /// <summary>
    /// 每一步换窗口时先把上一个教程窗口收掉，再打开目标窗口并等它布局完。
    /// 窗口用 ShowActivated=false，焦点始终留在全屏遮罩上，下一步/跳过不会被抢走。
    /// </summary>
    /// <summary>等遮罩把「点击」动画播完，再进编辑态，让蓝色的框和曲线变色出现在同一拍。</summary>
    private async Task EnableTutorialEditingAfterPauseAsync(MainViewModel viewModel)
    {
        await Task.Delay(1100);
        if (_tutorialOverlay is not null)
        {
            viewModel.EnterTutorialEditing();
        }
    }

    /// <summary>低音那一步：等遮罩点完再切层，蓝色/紫色和虚线的互换才看得见。</summary>
    private async Task EnableTutorialBassAfterPauseAsync(MainViewModel viewModel)
    {
        await Task.Delay(1100);
        if (_tutorialOverlay is not null)
        {
            viewModel.EnterTutorialBassEditing();
        }
    }

    /// <summary>开关那一步：等遮罩点完「叠加低音修正」，再真勾上，让曲线自己变形。</summary>
    private async Task EnableTutorialBassOverlayAfterPauseAsync(MainViewModel viewModel)
    {
        await Task.Delay(1100);
        if (_tutorialOverlay is not null)
        {
            viewModel.EnableTutorialBassOverlay();
        }
    }

    private async Task PrepareTutorialStepAsync(TutorialStep step)
    {
        if (step.TargetId == "ModeMain" && ViewModel is { IsTutorialScenario: true } tutorialVm)
        {
            // 教程第 2 步：先让遮罩播完「点击」的动画和蓝框提示，再真正进入编辑，让人看见变色。
            _ = EnableTutorialEditingAfterPauseAsync(tutorialVm);
        }
        else if (step.TargetId == "ModeBass" && ViewModel is { IsTutorialScenario: true } bassVm)
        {
            // 低音编辑同理：先播点击，再切过去，让人看见左右线淡出、低音线淡入。
            _ = EnableTutorialBassAfterPauseAsync(bassVm);
        }
        else if (step.TargetId == "BassLayerSwitch" && ViewModel is { IsTutorialScenario: true } switchVm)
        {
            // 先退出低音编辑，回到「左右 + 叠加」的显示；再等遮罩点完，真正勾上开关看曲线变形。
            switchVm.ExitTutorialEditing();
            _ = EnableTutorialBassOverlayAfterPauseAsync(switchVm);
        }
        // 风味下拉只在编辑态出现：这一步要指它，就得先真的进编辑，
        // 否则遮罩找不到控件，只能退回屏幕中央的备用位置。
        if (step.FlavorDemo && ViewModel is { IsEditable: false } tutorialViewModel)
        {
            tutorialViewModel.ToggleMainLayerCommand.Execute(null);
            await Task.Delay(180);
        }

        if (step.Window == _tutorialCurrentWindow)
        {
            return;
        }

        CloseTutorialWindows();
        _tutorialCurrentWindow = step.Window;

        if (step.Window == TutorialWindowKind.Main)
        {
            _tutorialHostWindow?.Activate();
            return;
        }

        var window = OpenTutorialWindow(step.Window);
        if (window is not null)
        {
            await Task.Delay(160);
            _tutorialHostWindow?.Activate();
        }
    }

    private Window? OpenTutorialWindow(TutorialWindowKind kind)
    {
        if (ViewModel is not { } viewModel)
        {
            return null;
        }

        switch (kind)
        {
            case TutorialWindowKind.Settings:
                if (_settingsWindow is { IsVisible: true })
                {
                    return _settingsWindow;
                }

                var settings = new SettingsWindow(viewModel) { Owner = this, ShowActivated = false };
                _settingsWindow = settings;
                settings.Closed += (_, _) => _settingsWindow = null;
                settings.Show();
                _tutorialOwnedWindows.Add(settings);
                return settings;

            case TutorialWindowKind.PresetEq:
                if (_autoEqWindow is { IsVisible: true })
                {
                    return _autoEqWindow;
                }

                var autoEq = new AutoEqWindow(
                    viewModel.ApplyAutoEqAsync,
                    viewModel.CreateCurveFromAutoEqAsync)
                {
                    Owner = this,
                    ShowActivated = false,
                };
                _autoEqWindow = autoEq;
                autoEq.Closed += (_, _) => _autoEqWindow = null;
                autoEq.Show();
                _tutorialOwnedWindows.Add(autoEq);
                return autoEq;

            case TutorialWindowKind.CalibrationTarget:
                var targetPicker = new CalibrationTargetWindow(interactive: false)
                {
                    Owner = this,
                    ShowActivated = false,
                };
                targetPicker.Closed += (_, _) => _tutorialOwnedWindows.Remove(targetPicker);
                targetPicker.Show();
                _tutorialOwnedWindows.Add(targetPicker);
                return targetPicker;

            case TutorialWindowKind.AutoCalibrate:
                var calibrate = new AutoCalibrateWindow(
                    viewModel,
                    PeaceWpf.Core.Calibration.CalibrationTargetKind.Flat)
                {
                    Owner = this,
                    ShowActivated = false,
                };
                calibrate.Closed += (_, _) => _tutorialOwnedWindows.Remove(calibrate);
                calibrate.Show();
                _tutorialOwnedWindows.Add(calibrate);
                return calibrate;

            default:
                return null;
        }
    }

    private void CloseTutorialWindows()
    {
        foreach (var window in _tutorialOwnedWindows.ToArray())
        {
            if (window.IsVisible)
            {
                window.Close();
            }
        }

        _tutorialOwnedWindows.Clear();
    }

    private void CleanupTutorial()
    {
        // 先把教程临时换上去的两层还回去，再关窗口。
        ViewModel?.EndTutorialScenario();
        CloseTutorialWindows();
        _tutorialOverlay = null;
        _tutorialHostWindow = null;
        _tutorialCurrentWindow = TutorialWindowKind.Main;
        if (IsVisible)
        {
            Activate();
        }
    }

    /// <summary>打开 AutoEQ 选择窗口。非模态：挑型号的时候主窗口的曲线还能看。</summary>
    private void OnOpenAutoEq(object sender, RoutedEventArgs e) => OpenAutoEq();

    private void OpenAutoEq()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (_autoEqWindow is { IsVisible: true })
        {
            _autoEqWindow.Activate();
            return;
        }

        _autoEqWindow = new AutoEqWindow(
            viewModel.ApplyAutoEqAsync,
            viewModel.CreateCurveFromAutoEqAsync) { Owner = this };
        _autoEqWindow.Closed += (_, _) => _autoEqWindow = null;
        _autoEqWindow.Show();
    }

    /// <summary>
    /// 打开自动EQ：先问校准到哪条曲线，再开扫频窗口。
    /// 模态打开——测量的二十多秒里主窗口的曲线不该被人动，不然 peace.txt 会被试听改写，
    /// 临时拉平的状态就断了。
    /// </summary>
    private void OnOpenAutoCalibrate(object sender, RoutedEventArgs e) => OpenAutoCalibrate();

    /// <summary>
    /// 主界面「扫频」：放一遍五秒扫频，听现在这套 EQ。
    ///
    /// 和自动EQ里的「试听扫频」走同一条路：临时把「左右 + 低音」合并写进 peace.txt，
    /// 放完再还原。低频层平时不参与主窗口的试听，只有这里会叠上去。
    /// </summary>
    private async void OnPlaySweep(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        // 连点：先取消上一次「放完还原」，并把它已经叠上去的低频还原掉，
        // 再重新备份、重新叠。否则第二次会拿叠过低频的文件当原样。
        var hadPendingRestore = _sweepRestoreCts is not null;
        _sweepRestoreCts?.Cancel();
        _sweepRestoreCts = null;
        if (hadPendingRestore)
        {
            await viewModel.EndSweepPreviewAsync();
        }

        // 听不听低音层，跟「叠加低音修正」那个勾选走：关着就只放左右层。
        var bass = viewModel.IsBassLayerEnabled ? viewModel.BassPoints : EqPointList.Empty;
        var (ok, message) = await viewModel.BeginSweepPreviewAsync(
            viewModel.MainPoints,
            bass,
            viewModel.PreAmpDb);
        if (!ok)
        {
            viewModel.StatusText = message ?? "试听叠加低频失败。";
            return;
        }

        if (!StartSweepRange(ChirpPreviewPlayer.StartHz, ChirpPreviewPlayer.EndHz))
        {
            viewModel.StatusText = "扫频放不出来：" + (_sweepPlayer.LastError ?? "没有可用的输出设备");
            await viewModel.EndSweepPreviewAsync();
            return;
        }

        viewModel.StatusText = viewModel.IsBassLayerEnabled
            ? "正在放 5 秒扫频：左右 + 低音层，20 Hz 一路升到 20 kHz。"
            : "正在放 5 秒扫频：只有左右层，20 Hz 一路升到 20 kHz。";

        StartSweepCursor();

        var cts = new CancellationTokenSource();
        _sweepRestoreCts = cts;
        _ = RestoreSweepAfterAsync(viewModel, cts);
    }

    /// <summary>
    /// 主界面扫频的绿线：ChirpPreviewPlayer 只说明「五秒、20→20k 指数扫频」，不报播放位置，
    /// 所以自己掐表，按 f(t) = f0·(f1/f0)^(t/T) 反推。
    /// </summary>
    /// <summary>按本次实际播放的频段启动绿线：时长也按同一段算，别再用整段 5 秒。</summary>
    private bool StartSweepRange(double fromHz, double toHz)
    {
        _sweepFromHz = fromHz;
        _sweepToHz = toHz;
        var octaves = Math.Log(toHz / fromHz, 2);
        _sweepSeconds = Math.Clamp(ChirpPreviewPlayer.Seconds * octaves / 9.97, 1.2, ChirpPreviewPlayer.Seconds);
        return _sweepPlayer.Play(fromHz, toHz);
    }

    private void StartSweepCursor()    {
        _sweepWatch = System.Diagnostics.Stopwatch.StartNew();
        _sweepTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _sweepTimer.Tick -= OnSweepCursorTick;
        _sweepTimer.Tick += OnSweepCursorTick;
        _sweepTimer.Start();
    }

    private void OnSweepCursorTick(object? sender, EventArgs e)
    {
        if (_sweepWatch is null)
        {
            StopSweepCursor();
            return;
        }

        var t = Math.Clamp(_sweepWatch.Elapsed.TotalSeconds / Math.Max(0.1, _sweepSeconds), 0, 1);
        Graph.SweepHz = _sweepFromHz * Math.Pow(_sweepToHz / _sweepFromHz, t);

        if (t >= 1)
        {
            StopSweepCursor();
        }
    }

    private void StopSweepCursor()
    {
        _sweepTimer?.Stop();
        _sweepWatch = null;
        Graph.SweepHz = null;
    }

    /// <summary>扫频放完（多留 0.6 秒余量）就把 peace.txt 还原，别让低频一直叠着。</summary>
    private async Task RestoreSweepAfterAsync(MainViewModel viewModel, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_sweepSeconds + 0.6), cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_sweepRestoreCts, cts))
            {
                _sweepRestoreCts = null;
            }

            cts.Dispose();
        }

        await viewModel.EndSweepPreviewAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _sweepRestoreCts?.Cancel();
        _sweepRestoreCts = null;
        _sweepPlayer.Dispose();

        // 扫频可能还叠着低频：窗口关了也要把 peace.txt 还原成原样。
        if (ViewModel is { } viewModel)
        {
            _ = viewModel.EndSweepPreviewAsync();
        }
    }

    private void OpenAutoCalibrate()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (_calibrateWindow is { IsVisible: true })
        {
            _calibrateWindow.Activate();
            return;
        }

        _ = OpenAutoCalibrateAsync(viewModel);
    }

    private async Task OpenAutoCalibrateAsync(MainViewModel viewModel)
    {
        if (!await viewModel.EnsureMainLayerForCalibrationAsync())
        {
            AppDialog.Info(
                this,
                "自动EQ",
                "校准要套到「左右」层。当前还有没处理的改动，先保存或放弃，再点一次「自动EQ」。");
            return;
        }

        var picker = new CalibrationTargetWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.Chosen is not { } target)
        {
            return;
        }

        var window = new AutoCalibrateWindow(viewModel, target) { Owner = this };
        _calibrateWindow = window;
        window.Closed += (_, _) => _calibrateWindow = null;
        window.ShowDialog();
    }

    // ---- 预设下拉：右侧抓手的拖动排序 + 右键菜单 ----

    /// <summary>抓手只占每行最右边这么宽；点左边的文字仍然是普通选择，不会误拖。</summary>
    private const double PresetGripWidth = 30;

    private System.Windows.Controls.ComboBox? _presetDragCombo;
    private string? _presetDragItem;
    private bool _presetDragMoved;
    private string? _presetMenuTarget;

    // 就地菜单：画在弹层自己的可视树里，这样下拉不会被收起来。
    private System.Windows.Controls.Decorator? _presetMenuHost;
    private UIElement? _presetMenuOriginal;
    private System.Windows.Controls.Grid? _presetMenuWrapper;

    /// <summary>在抓手那一条上按下：开始拖，但不让下拉选中东西。</summary>
    private void OnPresetGripMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _presetDragCombo = null;
        _presetDragItem = null;
        _presetDragMoved = false;

        var item = sender as System.Windows.Controls.ComboBoxItem
            ?? FindAncestor<System.Windows.Controls.ComboBoxItem>(e.OriginalSource as DependencyObject);

        if (item?.Content is not string name || !IsPresetGripHit(item, e.GetPosition(item)))
        {
            return;
        }

        _presetDragCombo = OwningCombo(item);
        _presetDragItem = name;

        // 拦下这次按下：不拦的话 ComboBoxItem 会顺手选中并合上下拉，就没地方拖了。
        e.Handled = true;
    }

    /// <summary>拖着走：指针压到哪一行，就把手里这条挪到哪一行。</summary>
    private void OnPresetMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_presetDragCombo is null || _presetDragItem is null)
        {
            return;
        }

        if (!_presetDragCombo.IsDropDownOpen || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            EndPresetDrag(persist: false);
            return;
        }

        var over = FindAncestor<System.Windows.Controls.ComboBoxItem>(System.Windows.Input.Mouse.DirectlyOver as DependencyObject);
        if (over?.Content is not string target || string.Equals(target, _presetDragItem, StringComparison.Ordinal))
        {
            return;
        }

        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var to = viewModel.AvailablePresets.IndexOf(target);
        if (to < 0)
        {
            return;
        }

        viewModel.MovePreset(_presetDragItem, to);
        _presetDragMoved = true;
    }

    private void OnPresetMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_presetDragCombo is null)
        {
            return;
        }

        EndPresetDrag(persist: true);
        e.Handled = true;
    }

    private void OnPresetDropDownClosed(object? sender, EventArgs e)
    {
        ClosePresetMenu();
        EndPresetDrag(persist: true);
    }

    private void EndPresetDrag(bool persist)
    {
        var moved = _presetDragMoved;
        _presetDragCombo = null;
        _presetDragItem = null;
        _presetDragMoved = false;

        if (!persist || !moved || ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.PersistPresetOrder();
        viewModel.StatusText = "预设顺序已记下";
    }

    /// <summary>条目上按下右键：记住是哪一条，等松开再弹菜单。</summary>
    private void OnPresetItemRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _presetMenuTarget = null;

        var item = sender as System.Windows.Controls.ComboBoxItem
            ?? FindAncestor<System.Windows.Controls.ComboBoxItem>(System.Windows.Input.Mouse.DirectlyOver as DependencyObject);

        if (item?.Content is not string name)
        {
            return;
        }

        _presetMenuTarget = name;
        e.Handled = true;
    }

    /// <summary>
    /// 条目上松开右键：下拉保持着，就在旁边弹菜单。列表不收起 —— 关掉菜单还能接着点。
    /// 菜单是独立窗口，会短暂抢走鼠标捕获；捕获取回来之后下拉仍在原处。
    /// </summary>
    private void OnPresetItemRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var name = _presetMenuTarget;
        _presetMenuTarget = null;

        if (name is null || OwningCombo(sender as DependencyObject) is not { } combo)
        {
            return;
        }

        e.Handled = true;
        ShowPresetMenu(combo, name);
    }

    // 下拉收着的时候右键 ComboBox 本身，走 XAML 里挂的 ContextMenu 这三项。
    private void OnPresetMenuRename(object sender, RoutedEventArgs e) =>
        RunPresetMenu(sender, e, (viewModel, name) => viewModel.RenamePreset(name));

    private void OnPresetMenuCopy(object sender, RoutedEventArgs e) =>
        RunPresetMenu(sender, e, (viewModel, name) => viewModel.DuplicatePreset(name));

    private void OnPresetMenuDelete(object sender, RoutedEventArgs e) =>
        RunPresetMenu(sender, e, (viewModel, name) => viewModel.DeletePreset(name));

    // 预设下拉右边的「+」：新建一份空白预设，建完直接切过去
    private void OnCreateMainPreset(object sender, RoutedEventArgs e) => ViewModel?.CreatePreset(isMain: true);

    private void OnCreateBassPreset(object sender, RoutedEventArgs e) => ViewModel?.CreatePreset(isMain: false);

    private void RunPresetMenu(object sender, RoutedEventArgs e, Action<MainViewModel, string> action)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (sender is System.Windows.Controls.MenuItem { Parent: System.Windows.Controls.ContextMenu menu }
            && menu.PlacementTarget is System.Windows.Controls.ComboBox { SelectedItem: string name })
        {
            action(viewModel, name);
        }
    }

    /// <summary>
    /// 在下拉里就地弹出三项菜单。
    ///
    /// 不能用 ContextMenu：它是独立窗口，一打开就要拿走鼠标捕获，ComboBox 的下拉会当场收起来。
    /// 所以菜单画进弹层自己的可视树 —— 同一个窗口、同一份捕获，后面的列表原样待在那儿。
    /// </summary>
    private void ShowPresetMenu(System.Windows.Controls.ComboBox combo, string name)
    {
        ClosePresetMenu();

        var popup = PresetPopupOf(combo);
        var host = popup?.Child as System.Windows.Controls.Decorator;
        var original = host?.Child;

        if (popup is null || host is null || original is null)
        {
            return;   // 模板结构认不出来：宁可不弹，也不把下拉收掉
        }

        var point = System.Windows.Input.Mouse.GetPosition(host);
        var panel = BuildPresetMenuPanel(name);
        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        // 透明底是为了让点击落在空白处时由画布接住：只收菜单，不动后面的列表。
        var canvas = new System.Windows.Controls.Canvas
        {
            Background = System.Windows.Media.Brushes.Transparent,
        };

        System.Windows.Controls.Canvas.SetLeft(panel, ClampInside(point.X, host.ActualWidth, panel.DesiredSize.Width));
        System.Windows.Controls.Canvas.SetTop(panel, ClampInside(point.Y, host.ActualHeight, panel.DesiredSize.Height));
        canvas.Children.Add(panel);

        canvas.PreviewMouseDown += (_, args) =>
        {
            if (!IsInside(args.OriginalSource as DependencyObject, panel))
            {
                ClosePresetMenu();
            }
        };

        host.Child = null;
        var wrapper = new System.Windows.Controls.Grid();
        wrapper.Children.Add(original);
        wrapper.Children.Add(canvas);
        host.Child = wrapper;

        _presetMenuHost = host;
        _presetMenuOriginal = original;
        _presetMenuWrapper = wrapper;
    }

    private static bool IsInside(DependencyObject? node, DependencyObject container)
    {
        for (var current = node; current is not null; current = FindParent(current))
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? FindParent(DependencyObject node) =>
        node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    private static double ClampInside(double wanted, double available, double size) =>
        Math.Max(2, Math.Min(wanted, available - size - 2));

    private System.Windows.Controls.Border BuildPresetMenuPanel(string name)
    {
        var rows = new System.Windows.Controls.StackPanel();
        rows.Children.Add(CreatePresetMenuRow("重命名…", () => ViewModel?.RenamePreset(name)));
        rows.Children.Add(CreatePresetMenuRow("复制", () => ViewModel?.DuplicatePreset(name)));
        rows.Children.Add(new System.Windows.Controls.Separator { Margin = new Thickness(6, 3, 6, 3) });
        rows.Children.Add(CreatePresetMenuRow("删除…", () => ViewModel?.DeletePreset(name)));

        return new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2B, 0x2B, 0x2B)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x50, 0x50, 0x50)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            Child = rows,
        };
    }

    private System.Windows.Controls.Button CreatePresetMenuRow(string header, Action action)
    {
        var row = new System.Windows.Controls.Button
        {
            Content = header,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 5, 30, 5),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        row.Click += (_, _) =>
        {
            ClosePresetMenu();
            action();
        };

        return row;
    }

    /// <summary>把就地菜单收掉，弹层原来那个孩子放回去。</summary>
    private void ClosePresetMenu()
    {
        if (_presetMenuHost is { } host && _presetMenuOriginal is { } original)
        {
            _presetMenuWrapper?.Children.Clear();
            host.Child = null;
            host.Child = original;
        }

        _presetMenuHost = null;
        _presetMenuOriginal = null;
        _presetMenuWrapper = null;
    }

    /// <summary>弹层就挂在 ComboBox 的模板里：先按名字找，模板改了名就往下搜。</summary>
    private static System.Windows.Controls.Primitives.Popup? PresetPopupOf(System.Windows.Controls.ComboBox combo)
    {
        combo.ApplyTemplate();

        if (combo.Template?.FindName("PART_Popup", combo) is System.Windows.Controls.Primitives.Popup named)
        {
            return named;
        }

        return FindDescendant<System.Windows.Controls.Primitives.Popup>(combo, popup => popup.IsOpen)
            ?? FindDescendant<System.Windows.Controls.Primitives.Popup>(combo, _ => true);
    }

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool> accept)
        where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match && accept(match))
            {
                return match;
            }

            if (FindDescendant(child, accept) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    /// <summary>这条预设属于哪个下拉：条目往上找 ComboBox，已经从树上摘下来的就反过来找。</summary>
    private static System.Windows.Controls.ComboBox? OwningCombo(DependencyObject? node)
    {
        if (node is System.Windows.Controls.ComboBoxItem item
            && System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(item) is System.Windows.Controls.ComboBox owner)
        {
            return owner;
        }

        return FindAncestor<System.Windows.Controls.ComboBox>(node);
    }

    private static bool IsPresetGripHit(FrameworkElement item, Point positionInItem) =>
        item.ActualWidth > 0 && positionInItem.X >= item.ActualWidth - PresetGripWidth;

    private static T? FindAncestor<T>(DependencyObject? start)
        where T : DependencyObject
    {
        var node = start;
        while (node is not null)
        {
            if (node is T match)
            {
                return match;
            }

            node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateContentMinSize();
        ApplyInitialNineSixteenSize();
    }

    private bool _initialSizeApplied;

    /// <summary>
    /// 首次显示时把窗口定成 16:9，且只比最小尺寸大一点点。
    /// 最小宽度是按顶栏内容量出来的，所以必须等它算完再定尺寸，
    /// 否则开窗时窗口会先被最小值撑一下、再跳到目标大小。
    /// </summary>
    private void ApplyInitialNineSixteenSize()
    {
        if (_initialSizeApplied || MinWidth <= 0 || MinHeight <= 0)
        {
            return;
        }

        _initialSizeApplied = true;

        // 比最小宽度富余 4%（至少多 24），看得出窗口还有拖大的余地
        var width = Math.Max(MinWidth * 1.04, MinWidth + 24);
        var height = width * 9.0 / 16.0;

        // 16:9 算出来的高度压不过最小高度时，反过来按高度推宽度
        if (height < MinHeight)
        {
            height = MinHeight;
            width = height * 16.0 / 9.0;
        }

        // 小屏上别开出去
        width = Math.Min(width, SystemParameters.VirtualScreenWidth);
        height = Math.Min(height, SystemParameters.VirtualScreenHeight);

        Width = width;
        Height = height;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // WPF 在这套 WindowChrome 下没把 MinWidth 交给系统，鼠标一拖就能把窗口压到比内容还窄，
        // 右边那排按钮被盖掉。这里在窗口消息层自己拦（见 OnWindowMessage）。
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(OnWindowMessage);
        }
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmSize = 0x0005;
        const int WmSizing = 0x0214;

        // 拖动边框时系统先问「建议多大」：这里直接把下限钉住，窗口就不会先缩下去再弹回来。
        // wParam 是被拖的那条边：1 左、2 右、3 上、4 左上、5 右上、6 下、7 左下、8 右下。
        if (msg == WmSizing && lParam != IntPtr.Zero)
        {
            var edge = (int)wParam;
            var rect = Marshal.PtrToStructure<NativeRect>(lParam);
            var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var minWidth = (int)Math.Ceiling(MinWidth * scale);
            var minHeight = (int)Math.Ceiling(MinHeight * scale);

            if (rect.Right - rect.Left < minWidth)
            {
                // 从左边拖就固定右边界，从右边拖就固定左边界
                if (edge is 1 or 4 or 7)
                {
                    rect.Left = rect.Right - minWidth;
                }
                else
                {
                    rect.Right = rect.Left + minWidth;
                }
            }

            if (rect.Bottom - rect.Top < minHeight)
            {
                if (edge is 3 or 4 or 5)
                {
                    rect.Top = rect.Bottom - minHeight;
                }
                else
                {
                    rect.Bottom = rect.Top + minHeight;
                }
            }

            Marshal.StructureToPtr(rect, lParam, false);
            handled = true;
            return IntPtr.Zero;
        }

        // 不是拖动来的尺寸变化（程序调用、Aero Snap）：等这一轮处理完再纠正。
        // 不能在窗口过程里直接改尺寸，会递归。
        if (msg == WmSize)
        {
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(EnsureAboveMinSize));
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    /// <summary>Win32 的 RECT / POINT 都是 int，不是 WPF 的 double 点。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    /// <summary>
    /// 窗口尺寸变化时补算一次：换 DPI、改系统字号、顶栏可见项增减都会让内容的自然宽度变，
    /// 最小宽度不跟着走的话，下一次缩小还是会压掉按钮。
    /// </summary>
    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateContentMinSize();
        EnsureAboveMinSize();
    }

    /// <summary>
    /// 兜底：万一系统那边被绕过（程序化改尺寸、Aero Snap、拖到另一个缩放的屏幕），
    /// 窗口已经小于最小值了，当场拉回来，别让顶栏按钮露到窗口外面。
    /// 这里必须读真实的窗口矩形：窗口被外部改小之后，WPF 的 ActualWidth 不会跟着变，
    /// 拿它去比会一直以为尺寸没变小。
    /// </summary>
    private void EnsureAboveMinSize()
    {
        // 最小化时窗口尺寸是 0；最大化时尺寸由屏幕决定，都不该在这里改
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect))
        {
            return;
        }

        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var minWidth = (int)Math.Ceiling(MinWidth * scale);
        var minHeight = (int)Math.Ceiling(MinHeight * scale);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;

        if (width >= minWidth && height >= minHeight)
        {
            return;
        }

        SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            Math.Max(width, minWidth),
            Math.Max(height, minHeight),
            SwpNoMove | SwpNoZOrder | SwpNoActivate);
    }

    /// <summary>
    /// 按内容自然尺寸算出窗口能被拖到多小，代替 XAML 里写死的 MinWidth。
    /// 顶栏两簇（左边模式/预设/开关，右边扫频/预设EQ/自动EQ）在 Auto + 星号两列里，
    /// 窗口不够宽时星号列被压到 0，右边那几个按钮就被盖掉——所以最小宽度必须等于两簇的自然宽度之和。
    /// 以后往顶栏加按钮、改字号、换缩放，这里会自己跟上，不用回来手调数字。
    /// </summary>
    private void UpdateContentMinSize()
    {
        // 顶栏和底栏的 Grid/Border 左右各 16 的页边距
        const double HorizontalMargin = 32;

        // 两簇缩到极限时也留一点空，免得按钮和按钮贴在一起
        const double ClusterGap = 24;

        // 最小化或还没上屏时布局停着，量出来是 0；拿这种值去改最小值，窗口就能缩到很小
        if (!IsLoaded || !IsVisible || WindowState == WindowState.Minimized)
        {
            return;
        }

        var title = MeasureNatural(TitleBarRow);
        var topLeft = MeasureNatural(TopBarLeft);
        var topRight = MeasureNatural(TopBarRight);
        var footer = MeasureNatural(BalanceBar);

        // 任何一块量不出来就这一轮不算，别拿缺了数的结果去改最小值
        if (title.Width <= 0 || topLeft.Width <= 0 || topRight.Width <= 0 || footer.Width <= 0)
        {
            return;
        }

        var width = Math.Max(
            title.Width + HorizontalMargin,
            Math.Max(
                topLeft.Width + topRight.Width + ClusterGap + HorizontalMargin,
                footer.Width + HorizontalMargin));

        // 只在明显变化时才赋值：SizeChanged 里反复赋同一个值会让窗口不停重排
        if (double.IsFinite(width) && width > 0 && Math.Abs(MinWidth - width) > 0.5)
        {
            MinWidth = width;
        }
    }

    /// <summary>
    /// 按「想多宽就多宽」量一次自然尺寸，量完把布局标脏，让它回到正常约束。
    /// 不这么做 DesiredSize 会停在这组无限约束上，界面会按无约束尺寸排一次。
    /// </summary>
    private static Size MeasureNatural(FrameworkElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = element.DesiredSize;
        element.InvalidateMeasure();
        return size;
    }

    protected override void OnClosing(CancelEventArgs e)    {
        base.OnClosing(e);

        if (_closeConfirmed)
        {
            return;
        }

        if (ViewModel is { } viewModel && !viewModel.ConfirmClose())
        {
            e.Cancel = true;
            return;
        }

        _closeConfirmed = true;
        ViewModel?.Shutdown();

        try
        {
            var state = UiStateStore.Load();
            state.WindowWidth = Width;
            state.WindowHeight = Height;
            UiStateStore.Save(state);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存窗口状态失败：{ex.Message}");
        }
    }
}
