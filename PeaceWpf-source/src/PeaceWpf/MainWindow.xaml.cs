using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PeaceWpf.Controls;
using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Model;
using PeaceWpf.Services;
using PeaceWpf.ViewModels;
using Wpf.Ui.Controls;

namespace PeaceWpf;

public partial class MainWindow : FluentWindow
{
    private bool _closeConfirmed;

    /// <summary>托盘菜单点了「退出软件」：不再问关闭行为，直接走保存确认后退出。</summary>
    private bool _exitRequested;

    private TrayIconService? _tray;

    /// <summary>气泡只在第一次收进托盘时弹，别每次关都烦人。</summary>
    private bool _trayHintShown;

    private SettingsWindow? _settingsWindow;
    private AutoEqWindow? _autoEqWindow;
    private AutoCalibrateWindow? _calibrateWindow;
    private AutoSwitchWindow? _autoSwitchWindow;

    /// <summary>全局热键 + 前台进程监听；主窗口一建出来就挂着，收进托盘也不停。</summary>
    private AutoSwitchService? _autoSwitchService;
    private TutorialOverlay? _tutorialOverlay;

    /// <summary>教程里那个自动EQ 窗口：演示要直接挪它的黄线和滑杆。</summary>
    private AutoCalibrateWindow? _tutorialCalibrate;
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
        PreviewMouseDown += OnMainPreviewMouseDown;

        // 托盘图标从启动就挂着：窗口收进去以后，用户还得靠它回来。
        // 它建不起来（资源坏了 / WinForms 初始化失败）也不能拦住整个程序启动；
        // 那种情况下关闭行为会退回「退出软件」，省得窗口藏起来却找不回来。
        try
        {
            _tray = new TrayIconService("电竞调音台");
            _tray.OpenRequested += (_, _) => ShowFromTray();
            _tray.SettingsRequested += (_, _) =>
            {
                ShowFromTray();
                OnOpenSettings(this, new RoutedEventArgs());
            };
            _tray.ExitRequested += (_, _) => ExitFromTray();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"托盘图标创建失败：{ex.Message}");
            _tray = null;
        }
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
        e.CreatedPointId = ViewModel?.CreatePoint(e.FrequencyHz, e.GainDb) ?? -1;
        ViewModel?.NotifyCalibrationEdited();
    }

    private void OnPointMovePreview(object? sender, PointMoveEventArgs e)
    {
        ViewModel?.PreviewPoint(e.Id, e.FrequencyHz, e.GainDb);
    }

    private void OnPointMoveCommitted(object? sender, EventArgs e)
    {
        ViewModel?.CommitPoint();
        ViewModel?.NotifyCalibrationEdited();
    }

    private void OnPointDeleteRequested(object? sender, PointEventArgs e)
    {
        ViewModel?.DeletePoint(e.Id);
        ViewModel?.NotifyCalibrationEdited();
    }

    private void OnPointResetRequested(object? sender, PointEventArgs e)
    {
        ViewModel?.ResetPoint(e.Id);
        ViewModel?.NotifyCalibrationEdited();
    }

    private void OnQualityChanged(object? sender, PointQualityEventArgs e)
    {
        ViewModel?.ChangePointQuality(e.Id, e.Quality);
        ViewModel?.NotifyCalibrationEdited();
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

        // 教程要演曲线：先把之前测出来的绿线清掉，否则旧测量痕迹会叠在演示曲线上。
        ClearSweepCurves();

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

        // 自动EQ 那两步不画假曲线：直接挪真实窗口里的黄线和滑杆，曲线由它自己算。
        overlay.CrossoverSet = (hz, quick) => _tutorialCalibrate?.SetTutorialCrossover(hz, quick);
        overlay.CursorAtCrossover = () => _tutorialCalibrate?.TutorialCrossoverScreenPoint() ?? default;
        overlay.BoostSet = (percent, quick) => _tutorialCalibrate?.SetTutorialBoost(percent, quick);
        overlay.CursorAtBoost = () => _tutorialCalibrate?.TutorialBoostScreenPoint() ?? default;
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
            if (!StartSweepRange(from, to, TutorialSweepSeconds))
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

            if (!StartSweepRange(1000, 20000, TutorialSweepSeconds))
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

    /// <summary>
    /// 进阶课最后一步：气泡先指「自动切换」按钮，约一秒后把组合窗口打开。
    /// 组合本来为空时会把教程那两条临时曲线做成一行演示（只在内存里），
    /// 窗口里只讲一行＝一套声音，热键和进程绑定不替用户录。
    /// </summary>
    private async Task OpenTutorialAutoSwitchAfterPauseAsync(MainViewModel viewModel)
    {
        await Task.Delay(900);
        if (_tutorialOverlay is null)
        {
            return;
        }

        var demo = viewModel.EnsureTutorialAutoSwitchDemo();

        AutoSwitchWindow window;
        if (_autoSwitchWindow is { IsVisible: true } existing)
        {
            // 用户自己开着的窗口直接复用，讲完也不替他关。
            window = existing;
        }
        else
        {
            window = new AutoSwitchWindow(viewModel) { Owner = this, ShowActivated = false };
            window.Closed += (_, _) => _tutorialOwnedWindows.Remove(window);
            _tutorialOwnedWindows.Add(window);
            window.Show();
        }

        // 新建窗口构造时已经带上演示行；复用旧窗口则在这里补上。重复调用无副作用。
        if (demo is not null)
        {
            window.AddTutorialRow(demo);
        }

        await Task.Delay(160);
        _tutorialHostWindow?.Activate();
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

        // 最后一步教「自动切换」：气泡停在主窗口按钮上，等点击动画播完再打开组合窗口。
        if (step.TargetId == "AutoSwitchButton" && ViewModel is { IsTutorialScenario: true } autoSwitchVm)
        {
            _ = OpenTutorialAutoSwitchAfterPauseAsync(autoSwitchVm);
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

            // 自动EQ 窗口一出现就装上演示实测：后面的曲线步骤直接在这个真窗口里演，
            // 黄线和滑杆都由窗口自己的拟合器重算，遮罩不再另画一套。
            if (window is AutoCalibrateWindow calibrate)
            {
                _tutorialCalibrate = calibrate;
                calibrate.LoadTutorialMeasurement(TutorialCalibrationDemo.BuildMeasurement());
            }

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
        // 复用用户窗口时，演示行是后来补进去的，先从窗口上摘掉；再让视图模型把数据摘掉。
        if (ViewModel?.TutorialAutoSwitchDemo is { } demo)
        {
            _autoSwitchWindow?.RemoveTutorialRow(demo);
        }

        // 先把教程临时换上去的两层还回去，再关窗口。
        ViewModel?.EndTutorialScenario();
        _tutorialCalibrate = null;
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
    /// 打开自动切换：非模态。改组合的时候主界面还能看曲线怎么跟着换，
    /// 热键和进程绑定也由后台服务盯着。
    /// </summary>
    private void OnOpenAutoSwitch(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (_autoSwitchWindow is { IsVisible: true })
        {
            _autoSwitchWindow.Activate();
            return;
        }

        _autoSwitchWindow = new AutoSwitchWindow(viewModel, _autoSwitchService) { Owner = this };
        _autoSwitchWindow.Closed += (_, _) => _autoSwitchWindow = null;
        _autoSwitchWindow.Show();
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
    // ---- 主界面连续扫频的实测曲线 ----

    /// <summary>最多留 5 条，最后一条最新。</summary>
    private readonly System.Collections.Generic.List<
        System.Collections.Generic.IReadOnlyList<(double Hz, double Db)>> _sweepCurves = new();

    /// <summary>正在扫频。为真时右键是「停止」，不是「清空」。</summary>
    private bool _sweeping;

    /// <summary>本次扫频的取消票：右键停止就取消它。</summary>
    private System.Threading.CancellationTokenSource? _sweepCts;

    /// <summary>扫频刚结束的保护期（TickCount64）：这之前右键不清空，防误删。</summary>
    private long _sweepGuardUntil;

    /// <summary>中键合并出来的那条；null 表示还没合并过。</summary>
    private System.Collections.Generic.IReadOnlyList<(double Hz, double Db)>? _sweepAverage;

    private const int MaxSweepCurves = 5;

    /// <summary>
    /// 扫频放完再收尾：停麦、取出样本、反卷积成一条曲线。
    /// 反卷积不怕延迟（延迟只改相位），所以不需要和播放对齐。
    /// </summary>
    private async Task FinishSweepAsync(
        MicrophoneCaptureService mic,
        double fromHz,
        double toHz,
        double seconds,
        CancellationToken token)
    {
        try
        {
            await RunSweepAnalysisAsync(mic, fromHz, toHz, seconds, token);
        }
        finally
        {
            // 扫频结束（成功、失败、被停都算）：右键恢复成「清空」，
            // 但先留 0.5 秒保护——手还按在键上，很容易把刚测出来的那条删掉。
            _sweeping = false;
            _sweepGuardUntil = Environment.TickCount64 + 500;

            // 关键：重画一次，只画 _sweepCurves。
            // 中途停掉时，最后一次画的是「已存曲线 + 正在长的临时线」，
            // 不重画的话那半截临时线会一直留在图上，看着像测完了。
            // 正常结束时这一句是无害的：成品已经加进 _sweepCurves 了。
            Graph.SetSweepCurves(_sweepCurves, _sweepAverage);
        }
    }

    private async Task RunSweepAnalysisAsync(
        MicrophoneCaptureService mic,
        double fromHz,
        double toHz,
        double seconds,
        CancellationToken token)
    {
        var rate = mic.SampleRate;

        // 扫频期间先画一条临时线：t 秒时的频率是已知的，切最近 50 ms 量个电平就是一点。
        // 声卡延迟会让高频端略微错位，所以扫完用反卷积那条覆盖它——先长出来，再落到准确位置。
        var live = new System.Collections.Generic.List<(double Hz, double Db)>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var window = Math.Max(1, rate * 50 / 1000);
        double? liveRef = null;
        var liveRefLocked = false;

        while (clock.Elapsed.TotalSeconds < seconds + 0.3)
        {
            await Task.Delay(50);

            // 右键停止：录音丢掉，不留半截曲线。
            if (token.IsCancellationRequested)
            {
                mic.Stop();
                mic.Dispose();
                return;
            }

            var t = clock.Elapsed.TotalSeconds;
            if (t >= seconds)
            {
                break;
            }

            var end = mic.SampleCount;
            if (end < window)
            {
                continue;
            }

            // 扣掉已知的声卡延迟：现在听到的是「延迟之前」那一刻的频率。
            var heard = Math.Max(0, t - SweepLatencyMemory.Seconds);
            var hz = fromHz * Math.Pow(toHz / fromHz, heard / seconds);

            // 窗口至少装 3 个周期：20 Hz 的 3 个周期是 150 ms，固定 50 ms 只有 1 个周期，
            // RMS 会忽高忽低。上限 300 ms，免得一个窗横跨太多频点。
            var spanMs = Math.Clamp(3000.0 / hz, 50, 300);
            var span = Math.Max(1, (int)(rate * spanMs / 1000));
            if (end < span)
            {
                continue;
            }

            var level = mic.WindowRmsDb(end - span, span);

            // 参考：扫到 1 kHz 之前用目前为止的最大值（曲线因此永远在 0 dB 以下，不会顶到图外），
            // 过了 1 kHz 钉在 1 kHz 那一点上。**存原始电平，画的时候才减参考**——
            // 这样参考一改，整条曲线立刻统一重算，不会留下台阶。
            live.Add((hz, level));
            if (!liveRefLocked && hz >= 1000)
            {
                liveRef = level;
                liveRefLocked = true;
            }
            else if (!liveRefLocked)
            {
                liveRef = Math.Max(liveRef ?? level, level);
            }

            var reference0 = liveRef ?? level;
            var shown = new System.Collections.Generic.List<(double Hz, double Db)>(live.Count);
            foreach (var (pointHz, pointDb) in live)
            {
                shown.Add((pointHz, pointDb - reference0));
            }

            Graph.SetSweepCurves(WithLive(shown), null);
        }

        // 扫频本身 + 一点尾巴，等混响散掉。
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        mic.Stop();
        var samples = mic.TakeSamples();
        mic.Dispose();

        if (samples.Length == 0)
        {
            return;
        }

        var reference = ChirpPreviewPlayer.Build(fromHz, toHz, seconds);
        var (curve, delayMs) = await Task.Run(() =>
        {
            var analyzed = ChirpSweepAnalyzer.Analyze(
                samples, rate, reference, out var measured, ChirpSweepAnalyzer.PerOctaveFor(seconds));
            return (analyzed, measured);
        });

        if (curve is null || curve.Count < 8)
        {
            return;
        }

        // 每次扫频都更新延迟：换了声卡，下一次扫频就自动对上，不用手动清。
        SweepLatencyMemory.Seconds = delayMs / 1000.0;

        _sweepCurves.Add(curve);
        while (_sweepCurves.Count > MaxSweepCurves)
        {
            _sweepCurves.RemoveAt(0);
        }

        // 新的一条进来，之前那次合并就过时了。
        _sweepAverage = null;
        Graph.SetSweepCurves(_sweepCurves, _sweepAverage);
    }

    /// <summary>中键：把已有的曲线按功率平均成一条。驻波换位置会移动、平均后互相抵消，
    /// 设备本身的毛病不动、会留下——这就是拿它排除驻波的根据。</summary>
    private void OnSweepMiddleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        e.Handled = true;
        _sweepAverage = ChirpSweepAnalyzer.Merge(_sweepCurves);
        Graph.SetSweepCurves(_sweepCurves, _sweepAverage);
    }

    /// <summary>右键扫频按钮：扫频中是「停止」，扫完了是「清空」，刚扫完的 0.5 秒内什么都不做。</summary>
    private void OnSweepClear(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        // 扫频中：停掉本次录音，别顺手把之前测的几条也删了。
        // 光取消分析只能让曲线不再长——声音还在放、绿线还在跑，那是「停了一半」。
        if (_sweeping)
        {
            _sweepCts?.Cancel();
            _sweepPlayer.Stop();
            StopSweepCursor();

            // 立刻恢复右键语义：这半秒内再点一次，走下面的保护分支，不会误清。
            _sweeping = false;
            _sweepGuardUntil = Environment.TickCount64 + 500;
            return;
        }

        // 刚扫完的半秒：手多半还按在右键上，当成误触。
        if (Environment.TickCount64 < _sweepGuardUntil)
        {
            return;
        }

        _sweepCurves.Clear();
        _sweepAverage = null;
        Graph.SetSweepCurves(_sweepCurves, _sweepAverage);
    }

    /// <summary>
    /// 把扫频测出来的绿线全部清掉（进教程时用，免得旧测量痕迹混进演示曲线）。
    /// 正在测的那一条也要停：只清列表的话，它分析完还会再往图上加回来。
    /// </summary>
    private void ClearSweepCurves()
    {
        if (_sweeping)
        {
            _sweepCts?.Cancel();
            _sweepPlayer.Stop();
            StopSweepCursor();
            _sweeping = false;
            _sweepGuardUntil = Environment.TickCount64 + 500;
        }

        _sweepCurves.Clear();
        _sweepAverage = null;
        Graph.SetSweepCurves(_sweepCurves, _sweepAverage);
    }

    /// <summary>扫频时长（秒）：输入框和步进都能改，范围 2–60。</summary>
    /// <summary>平滑档位：关 / 最低 / 稍低 / 重点 / 稍高 / 最高，按窗口半径（点）定义。</summary>
    private static readonly int[] SweepSmoothWindows = [0, 3, 5, 9, 15, 25];

    private static readonly string[] SweepSmoothNames = ["关", "最低", "稍低", "中度", "稍高", "最高"];

    private int _sweepSmoothIndex;

    /// <summary>当前扫频时长对应的每倍频程点数：越长越密（48 → 384）。</summary>
    private int SweepPerOctave => ChirpSweepAnalyzer.PerOctaveFor(_sweepSecondsSetting);

    /// <summary>把当前档位和当前分辨率换算成平滑半径交给画图控件。</summary>
    private void ApplySweepSmoothing()
    {
        Graph.SweepSmoothingPoints = SweepSmoothWindows[_sweepSmoothIndex];
        SweepSmoothButton.Content = "平滑 " + SweepSmoothNames[_sweepSmoothIndex];
        Graph.InvalidateVisual();
    }

    private void OnSweepSmoothNext(object sender, RoutedEventArgs e)
    {
        _sweepSmoothIndex = (_sweepSmoothIndex + 1) % SweepSmoothWindows.Length;
        ApplySweepSmoothing();
    }

    private double _sweepSecondsSetting = ChirpPreviewPlayer.Seconds;

    private void OnSweepSecondsUp(object sender, RoutedEventArgs e) =>
        SetSweepSeconds(ParseSweepSeconds() + 1);

    private void OnSweepSecondsDown(object sender, RoutedEventArgs e) =>
        SetSweepSeconds(ParseSweepSeconds() - 1);

    private void OnSweepSecondsCommit(object sender, RoutedEventArgs e) =>
        SetSweepSeconds(ParseSweepSeconds());

    private void OnSweepSecondsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        SetSweepSeconds(ParseSweepSeconds());
        e.Handled = true;
    }

    private double ParseSweepSeconds() =>
        double.TryParse(SweepSecondsBox.Text, out var value) ? value : _sweepSecondsSetting;

    private void SetSweepSeconds(double value)
    {
        var clamped = Math.Clamp(Math.Round(value), 2, 99);
        SweepSecondsBox.Text = clamped.ToString("0");

        if (Math.Abs(clamped - _sweepSecondsSetting) < 0.01)
        {
            return;
        }

        _sweepSecondsSetting = clamped;

        // 时长一变，每倍频程点数就变，旧曲线和新曲线不是同一个分辨率，留着没法叠着看。
        _sweepCurves.Clear();
        _sweepAverage = null;
        Graph.SetSweepCurves(_sweepCurves, _sweepAverage);

        // 平滑半径是按点算的，分辨率变了要重新按当前时长换算。
        ApplySweepSmoothing();
    }

    /// <summary>已存的曲线 + 正在长的这条（临时那条排最后，也就是最新、最亮）。</summary>
    private System.Collections.Generic.List<
        System.Collections.Generic.IReadOnlyList<(double Hz, double Db)>> WithLive(
        System.Collections.Generic.List<(double Hz, double Db)> live)
    {
        var shown = new System.Collections.Generic.List<
            System.Collections.Generic.IReadOnlyList<(double Hz, double Db)>>(_sweepCurves);
        if (live.Count >= 2)
        {
            shown.Add(live);
        }

        return shown;
    }

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

        // 扫频的同时录音：麦克风用自动EQ记住的那只，主界面自己没有设备选择。
        MicrophoneCaptureService? mic = new MicrophoneCaptureService();
        if (!mic.Start(CalibrateDeviceMemory.Load()))
        {
            mic.Dispose();
            mic = null;
        }
        else
        {
            // 丢掉开设备的那一下杂音。反卷积不怕延迟，少这一小段不影响曲线。
            mic.TakeSamples();
        }

        if (!StartSweepRange(ChirpPreviewPlayer.StartHz, ChirpPreviewPlayer.EndHz))
        {
            viewModel.StatusText = "扫频放不出来：" + (_sweepPlayer.LastError ?? "没有可用的输出设备");
            mic?.Dispose();
            await viewModel.EndSweepPreviewAsync();
            return;
        }

        viewModel.StatusText = viewModel.IsBassLayerEnabled
            ? "正在放一段扫频：左右 + 低音层，20 Hz 一路升到 20 kHz。"
            : "正在放一段扫频：只有左右层，20 Hz 一路升到 20 kHz。";

        StartSweepCursor();

        _sweeping = mic is not null;
        if (mic is not null)
        {
            _sweepCts?.Dispose();
            _sweepCts = new System.Threading.CancellationTokenSource();
            _ = FinishSweepAsync(mic, _sweepFromHz, _sweepToHz, _sweepSeconds, _sweepCts.Token);
        }

        var cts = new CancellationTokenSource();
        _sweepRestoreCts = cts;
        _ = RestoreSweepAfterAsync(viewModel, cts);
    }

    /// <summary>
    /// 教程试听的整段时长先按 5 秒折算（暂时值）。
    /// 教程每遍只等 3.2 秒就停：按用户设置默认的 10 秒折算，1k–20k 要 4.3 秒、
    /// 20–300 Hz 要 3.9 秒，都会播到一半被掐断；5 秒折算下来约 2.2 / 2.0 秒，能完整放完。
    /// 只影响教程这两处对比，不动「秒」输入框里的用户设置。
    /// </summary>
    private const double TutorialSweepSeconds = 5;

    /// <summary>
    /// 主界面扫频的绿线：ChirpPreviewPlayer 只说明「五秒、20→20k 指数扫频」，不报播放位置，
    /// 所以自己掐表，按 f(t) = f0·(f1/f0)^(t/T) 反推。
    /// 时长按本次实际播放的频段折算，别再用整段 5 秒。
    /// </summary>
    private bool StartSweepRange(double fromHz, double toHz, double? totalSeconds = null)
    {
        _sweepFromHz = fromHz;
        _sweepToHz = toHz;
        var octaves = Math.Log(toHz / fromHz, 2);
        var total = totalSeconds ?? _sweepSecondsSetting;
        _sweepSeconds = Math.Clamp(total * octaves / 9.97, 1.2, total);
        return _sweepPlayer.Play(fromHz, toHz, _sweepSeconds);
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
        _autoSwitchService?.Dispose();
        _autoSwitchService = null;

        _sweepRestoreCts?.Cancel();
        _sweepRestoreCts = null;
        _sweepPlayer.Dispose();

        // 托盘图标必须跟着窗口一起收，不然进程退了图标还赖在托盘里。
        _tray?.Dispose();
        _tray = null;

        // 扫频可能还叠着低频：窗口关了也要把 peace.txt 还原成原样。
        if (ViewModel is { } viewModel)
        {
            _ = viewModel.EndSweepPreviewAsync();
        }

        // 主窗口一关就是退出软件：有别的窗口开着（设置、教程）也一起收掉，
        // 免得留下了它们，进程以「没有主窗口」的样子挂在后台退不干净。
        System.Windows.Application.Current?.Shutdown();
    }

    private void OnMainPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 点主界面就把自动EQ 收成按钮上的一个小点；点按钮本身不收，交给 Click 呼出。
        if (AutoCalibrateButton.IsMouseOver)
        {
            return;
        }

        if (_calibrateWindow is { IsCollapsed: false, IsVisible: true } window)
        {
            window.Collapse();
        }
    }

    private void OpenAutoCalibrate()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (_calibrateWindow is { } existing)
        {
            if (existing.IsCollapsed)
            {
                existing.Expand();
                return;
            }

            if (existing.IsVisible)
            {
                existing.Activate();
                return;
            }
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

        // 进入预览：新建「左右矫正 / 低音矫正」两个预设并切过去，主界面从现在起钉住自动EQ 的曲线。
        if (!await viewModel.BeginCalibrationPreviewAsync())
        {
            AppDialog.Info(
                this,
                "自动EQ",
                "校准要套到「左右」层。当前还有没处理的改动，先保存或放弃，再点一次「自动EQ」。");
            return;
        }

        var window = new AutoCalibrateWindow(viewModel, target) { Owner = this };
        _calibrateWindow = window;
        window.Closed += (_, _) =>
        {
            _calibrateWindow = null;
            // 没点套用就关窗口 = 取消：切回进入前的配置，删掉这次新建的矫正预设。
            if (!window.Applied)
            {
                viewModel.CancelCalibrationPreview();
            }
        };
        window.Show();
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

        // 窗口尺寸定下来之后再量起点，LOGO 才是真正的正中。
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(PlayStartupAnimation));
    }

    private bool _startupAnimationPlayed;

    /// <summary>
    /// 启动动画：全黑遮罩上 LOGO 在正中、软件名在下；停一下，软件名先淡出，
    /// LOGO 缩小飞向标题栏左上角，最后遮罩淡出，所有控件一起露出来。
    /// 开机自启最小化启动时不播（窗口根本看不见，播了只是浪费）。
    /// </summary>
    private void PlayStartupAnimation()
    {
        if (_startupAnimationPlayed)
        {
            return;
        }

        _startupAnimationPlayed = true;

        if (!IsLoaded || WindowState == WindowState.Minimized || SplashLayer.Visibility != Visibility.Visible)
        {
            SplashLayer.Visibility = Visibility.Collapsed;
            return;
        }

        var logoSize = SplashLogo.ActualWidth;
        var targetSize = TitleLogo.ActualWidth;

        // 遮罩层因为什么原因没布局出来，就直接跳过，别让界面永远黑着。
        if (logoSize <= 0 || targetSize <= 0)
        {
            SplashLayer.Visibility = Visibility.Collapsed;
            return;
        }

        // 起点、终点都按真实布局量：改窗口大小或换 DPI 都不会错位。
        var startCenter = SplashLogo.TranslatePoint(new Point(logoSize / 2, logoSize / 2), this);
        var targetCenter = TitleLogo.TranslatePoint(new Point(targetSize / 2, targetSize / 2), this);
        var ratio = targetSize / logoSize;

        var fadeIn = TimeSpan.FromMilliseconds(240);
        var hold = TimeSpan.FromMilliseconds(620);       // LOGO 停在正中的时间
        var fly = TimeSpan.FromMilliseconds(660);
        var reveal = TimeSpan.FromMilliseconds(360);

        // 1) 从全黑里浮出来
        SplashLogo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, fadeIn)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
        SplashTitle.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, fadeIn)
        {
            BeginTime = TimeSpan.FromMilliseconds(90),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });

        // 2) 软件名在起飞前收掉，不然缩到角上会糊成一团
        SplashTitle.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
        {
            BeginTime = hold - TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        });

        // 3) LOGO 缩小 + 平移：外圈快、末段收得住，看着像"落位"而不是"滑过去"
        var arriveEase = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var dx = targetCenter.X - startCenter.X;
        var dy = targetCenter.Y - startCenter.Y;

        DoubleAnimation Fly(double from, double to) => new(from, to, fly)
        {
            BeginTime = hold,
            EasingFunction = arriveEase,
        };

        SplashScale.BeginAnimation(ScaleTransform.ScaleXProperty, Fly(1, ratio));
        SplashScale.BeginAnimation(ScaleTransform.ScaleYProperty, Fly(1, ratio));
        SplashShift.BeginAnimation(TranslateTransform.XProperty, Fly(0, dx));
        SplashShift.BeginAnimation(TranslateTransform.YProperty, Fly(0, dy));

        // 4) 遮罩淡出，控件整体显现；LOGO 这时已经和标题栏里那枚小标重合
        var fadeOut = new DoubleAnimation(1, 0, reveal)
        {
            BeginTime = hold + fly,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        fadeOut.Completed += (_, _) => SplashLayer.Visibility = Visibility.Collapsed;
        SplashLayer.BeginAnimation(OpacityProperty, fadeOut);
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

        // 热键和进程监听挂在窗口句柄上：主窗口收进托盘时句柄还在，热键照样有效。
        if (ViewModel is { } viewModel)
        {
            _autoSwitchService = new AutoSwitchService(viewModel, this);
            _autoSwitchService.Start();
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

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (_closeConfirmed)
        {
            return;
        }

        // 托盘菜单点的「退出软件」：关闭行为已经表过态，不用再问，只确认没保存的改动。
        if (_exitRequested)
        {
            if (!ConfirmCloseBeforeExit())
            {
                e.Cancel = true;
                _exitRequested = false;
                return;
            }

            _closeConfirmed = true;
            FinishClose();
            return;
        }

        var action = ResolveCloseAction();
        if (action is null)
        {
            // 询问里按了 Esc：这次不关了，程序原样留着。
            e.Cancel = true;
            return;
        }

        // 收进托盘不是关闭：改动、采集、同步都留在内存里，回来还能接着用。
        // 但托盘图标没建起来的时候不能藏窗口 —— 那样就找不回来了，只能当成退出。
        if (action == CloseAction.MinimizeToTray && _tray is not null)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        if (!ConfirmCloseBeforeExit())
        {
            e.Cancel = true;
            return;
        }

        _closeConfirmed = true;
        FinishClose();
    }

    /// <summary>
    /// 这次关闭该做什么：记住过就直接用，没记住就问一次。
    /// 返回 null 表示用户取消了这次关闭。
    /// </summary>
    private CloseAction? ResolveCloseAction()
    {
        if (ViewModel is not { } viewModel)
        {
            return CloseAction.MinimizeToTray;
        }

        if (viewModel.IsCloseActionRemembered)
        {
            return viewModel.CloseAction;
        }

        var choice = CloseBehaviorWindow.Ask(this, viewModel.CloseAction);
        if (choice is null)
        {
            return null;
        }

        // 只有「记住选择」才写盘；「仅这一次」什么都不改，下次照问。
        if (choice.Value.Remember)
        {
            viewModel.RememberCloseAction(choice.Value.Action);
        }

        return choice.Value.Action;
    }

    /// <summary>真正退出前的保存确认；返回 false 表示用户选择继续编辑。</summary>
    private bool ConfirmCloseBeforeExit() =>
        ViewModel is not { } viewModel || viewModel.ConfirmClose();

    /// <summary>退出软件：停采集、存窗口尺寸、摘托盘图标。</summary>
    private void FinishClose()
    {
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

        _tray?.Dispose();
        _tray = null;
    }

    /// <summary>
    /// 把窗口收进托盘。用 Hide 而不是 Minimize：任务栏和 Alt+Tab 里都不再留它，
    /// 需要时从托盘叫回来。
    /// </summary>
    private void HideToTray()
    {
        Hide();

        if (!_trayHintShown)
        {
            _trayHintShown = true;
            _tray?.ShowHint("还在后台运行", "窗口已收进托盘。左键点图标打开，右键有菜单。");
        }
    }

    /// <summary>从托盘回到桌面。托盘左键、托盘菜单、第二个实例激活都走这里。</summary>
    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>托盘菜单里的「退出软件」。走正常关闭流程，没保存的改动仍会问。</summary>
    private void ExitFromTray()
    {
        if (_closeConfirmed)
        {
            return;
        }

        _exitRequested = true;
        Close();
    }

    /// <summary>
    /// 系统注销 / 关机：别弹询问、也别收托盘，直接放行。
    /// 否则关机会被一个「要不要最小化到托盘」的问句卡住。
    /// </summary>
    public void AllowSessionEnding()
    {
        _closeConfirmed = true;
    }
}
