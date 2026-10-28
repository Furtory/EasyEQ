using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Model;

namespace PeaceWpf;

/// <summary>
/// 全屏教程遮罩：目标可能在主窗口，也可能在教程刚打开的设置 / 预设EQ / 自动EQ 窗口里。
/// 用屏幕坐标找到目标，把周围压暗，用气泡写一两句话，再让一个圆形光标移过去点一下。
///
/// 最后一步可以播两遍扫频：先「现在的声音」，再「示例 EQ」，差别只来自软件里那层示例，
/// 不写 peace.txt，也不动用户正在用的曲线。
/// </summary>
public partial class TutorialOverlay : UserControl
{
    private const double BubbleWidth = 340;
    private const double BubbleGap = 14;
    private const double ScreenMargin = 12;

    private readonly string _courseLabel;
    private readonly IReadOnlyList<TutorialStep> _steps;
    private readonly Func<TutorialStep, Task>? _prepareStep;
    private readonly TutorialSweepPlayer _audio = new();
    private readonly List<DemoPoint> _demoPoints = new();

    private CancellationTokenSource? _audioCts;
    private CancellationTokenSource? _demoCts;
    private bool _audioCurvePreview;
    private bool _graphEditVisible;

    /// <summary>进阶课：曲线图区域不压变暗层，颜色和渐变看得见。</summary>
    public bool KeepGraphClear { get; set; }

    /// <summary>挪真实窗口的矫正起点：参数是（Hz，拖动途中用快速重算）。</summary>
    public Action<double, bool>? CrossoverSet { get; set; }

    /// <summary>真实窗口里黄线的屏幕坐标（设备像素）。</summary>
    public Func<Point>? CursorAtCrossover { get; set; }

    /// <summary>挪真实窗口的「贴合目标 ↔ 保证音量」滑杆：参数是（百分比，快速重算）。</summary>
    public Action<double, bool>? BoostSet { get; set; }

    /// <summary>真实窗口里滑杆拇指的屏幕坐标（设备像素）。</summary>
    public Func<Point>? CursorAtBoost { get; set; }

    /// <summary>演示用的滑杆当前值，滑动时用来算起点。</summary>
    private double? BoostPercent { get; set; }

    private Rect? _graphHole;
    private DemoPlot _demoPlot;
    private int _index = -1;
    private bool _busy;
    private bool _closed;
    private bool _audioPlaying;
    private bool _cursorReady;
    private double _cursorX;
    private double _cursorY;

    private sealed class DemoPoint
    {
        public double Freq;
        public double Gain;
        public double Q;
        public Ellipse Dot = null!;
    }

    private enum DemoMousePart
    {
        None,
        Left,
        Right,
        Wheel,
    }

    private readonly record struct DemoPlot(double Left, double Right, double Top, double Bottom)
    {
        public double Mid => Top + (Bottom - Top) / 2;
    }

    public TutorialOverlay(
        IReadOnlyList<TutorialStep> steps,
        string courseLabel,
        Func<TutorialStep, Task>? prepareStep = null)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _courseLabel = courseLabel ?? throw new ArgumentNullException(nameof(courseLabel));
        _prepareStep = prepareStep;

        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 主窗口被关掉、或遮罩被移除时，别让扫频还在后台响。
        _closed = true;
        StopAudio();
        StopCurveDemo();
        HideGraphEditGlow();
        _audio.Dispose();
    }

    /// <summary>教程结束或跳过时触发；主窗口负责把这个遮罩从可视树里拿掉。</summary>
    public event EventHandler? Closed;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        _ = ShowStepAsync(0);
    }

    private async Task ShowStepAsync(int index)
    {
        if (_closed || _busy || index < 0 || index >= _steps.Count)
        {
            return;
        }

        _busy = true;
        StopAudio();
        StopCurveDemo();
        NextButton.IsEnabled = false;
        AudioButton.IsEnabled = false;
        DemoButton.IsEnabled = false;
        _index = index;

        var step = _steps[index];
        var hasDemo = step.CurveDemo || step.FlavorDemo || step.CrossoverDemo || step.BoostDemo || step.RealCurveDemo;
        StepLabel.Text = _courseLabel + $" · {index + 1}/{_steps.Count}";
        TitleText.Text = step.Title;
        BodyText.Text = step.Body;
        AudioStatus.Visibility = Visibility.Collapsed;
        AudioStatus.Text = string.Empty;
        DemoStatus.Visibility = hasDemo ? Visibility.Visible : Visibility.Collapsed;
        DemoStatus.Text = step.FlavorDemo
            ? "看演示：选目标 → 拖动 → 右键还原。"
            : step.CrossoverDemo
                ? "看演示：黄线就是矫正起点，左右拖它。"
                : step.BoostDemo
                    ? "看演示：这条滑杆换的是补得多准、留多少音量。"
                    : step.CurveDemo
                        ? "看曲线：先建点，再拖、滚轮、右键。"
                        : string.Empty;
        AudioButton.Visibility = step.AudioDemo ? Visibility.Visible : Visibility.Collapsed;
        AudioButton.Content = "播放对比";
        DemoButton.Visibility = hasDemo ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = index == _steps.Count - 1 ? "完成" : "下一步";

        if (_prepareStep is not null)
        {
            await _prepareStep(step);
            if (_closed)
            {
                return;
            }
        }

        var target = GetTargetRect(step.TargetId);
        HideGraphEditGlow();
        _graphHole = KeepGraphClear && TryGetTargetRect("EqGraph", out var graphRect) ? graphRect : null;
        UpdateSpotlight(target);
        PositionBubble(step, target);
        SetBubbleOpacity(0);

        try
        {
            var audioStep = step.AudioCurveDemo || step.AudioDemo;
            // 试听步骤改成把光标停在「扫频」按钮上，所以这里不再因为音频步骤隐藏光标。
            var noCursor = step.AudioCurveDemo
                || step.TargetId == "AutoEqPreviewGraph";
            CursorDot.Visibility = noCursor ? Visibility.Collapsed : Visibility.Visible;
            if (noCursor)
            {
                DemoMouse.Visibility = Visibility.Collapsed;
            }

            if (step.FlavorDemo)
            {
                FadeBubbleInAfter(650);
                StartFlavorDemo();
            }
            else if (step.CurveDemo)
            {
                FadeBubbleInAfter(650);
                StartCurveDemo(target);
            }
            else if (step.CrossoverDemo)
            {
                FadeBubbleInAfter(650);
                StartCrossoverDemo(target);
            }
            else if (step.BoostDemo)
            {
                FadeBubbleInAfter(650);
                StartBoostDemo(target);
            }
            else if (step.RealCurveDemo)
            {
                FadeBubbleInAfter(650);
                StartRealCurveDemo(target);
            }
            else if (step.PreampDemo)
            {
                FadeBubbleInAfter(650);
                await RunPreampDemoAsync(target);
            }
            else if (!noCursor)
            {
                await MoveCursorAsync(target);
                if (_closed)
                {
                    return;
                }

                var modeStep = step.TargetId is "ModeMain" or "ModeBass";
                await PulseAsync(
                    endScale: modeStep ? 3.2 : 1.8,
                    durationMs: modeStep ? 650 : 420);

                if (modeStep)
                {
                    ShowGraphEditGlow(animate: true);
                    ShowModeIconGlow(step.TargetId, target);
                }

                if (step.BassCurvePreview)
                {
                    ShowBassCurvePreview();
                }

                // 光标到位了，再把这一步的说明浮出来。
                await FadeBubbleAsync(1, 240);
            }
            else
            {
                await FadeBubbleAsync(1, 240);
            }
        }
        finally
        {
            if (!_closed)
            {
                NextButton.IsEnabled = true;
                AudioButton.IsEnabled = step.AudioDemo;
                DemoButton.IsEnabled = hasDemo;
            }

            _busy = false;
        }
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_busy || _closed)
        {
            return;
        }

        if (_index >= _steps.Count - 1)
        {
            CloseTutorial();
            return;
        }

        _ = NextStepAsync(_index + 1);
    }

    /// <summary>点「下一步」：旧文字先渐隐，等光标走到新目标、内容就位后再渐显。</summary>
    private async Task NextStepAsync(int next)
    {
        await FadeBubbleAsync(0, 140);
        await ShowStepAsync(next);
    }

    private void OnSkip(object sender, RoutedEventArgs e) => CloseTutorial();

    private void OnReplayDemo(object sender, RoutedEventArgs e)
    {
        if (_index < 0 || _index >= _steps.Count)
        {
            return;
        }

        var step = _steps[_index];
        if (step.FlavorDemo)
        {
            StartFlavorDemo();
        }
        else if (step.CurveDemo)
        {
            StartCurveDemo(GetTargetRect(step.TargetId));
        }
        else if (step.RealCurveDemo)
        {
            StartRealCurveDemo(GetTargetRect(step.TargetId));
        }
        else if (step.CrossoverDemo)
        {
            StartCrossoverDemo(GetTargetRect(step.TargetId));
        }
        else if (step.BoostDemo)
        {
            StartBoostDemo(GetTargetRect(step.TargetId));
        }
    }

    private void StartCurveDemo(Rect target)
    {
        if (_closed)
        {
            return;
        }

        StopCurveDemo();
        ResetCurveDemo(target);
        var cts = new CancellationTokenSource();
        _demoCts = cts;
        _ = RunCurveDemoAsync(cts.Token);
    }

    /// <summary>
    /// 风味步：先在主界面那个下拉框下面展开一份模拟列表，选完再把曲线铺到图表上演「拖起来 → 右键还原」。
    /// 全程只画在遮罩上，不动真实风味，也不写 peace.txt。
    /// </summary>
    private void StartFlavorDemo()
    {
        if (_closed)
        {
            return;
        }

        StopCurveDemo();
        CurveDemoLayer.Visibility = Visibility.Visible;
        // 找不到下拉就不演「点它」那一段，免得遮罩指向一片空白。
        _ = TryGetTargetRect("FlavorCombo", out var combo);
        var cts = new CancellationTokenSource();
        _demoCts = cts;
        _ = RunFlavorDemoAsync(combo, cts.Token);
    }

    /// <summary>三拍：① 在下拉里选一种声音；② 把它的手柄拖起来；③ 右键还原成 0 dB。</summary>
    private async Task RunFlavorDemoAsync(Rect combo, CancellationToken token)
    {
        const string demoKey = "step";
        try
        {
            if (combo.IsEmpty)
            {
                // 没有下拉可点（比如没进编辑态）：只说明用法，不画假的点击。
                DemoStatus.Text = "风味下拉要先进入「左右」或「低音」编辑才会出现。";
                await Task.Delay(1200, token);
            }
            else
            {
                // 第一拍：下拉就在主界面那个位置展开，而不是一上来在图区铺曲线。
                DemoStatus.Text = "① 点开下拉：选你要调的那种声音";
                SetDemoMousePart(DemoMousePart.None);
                DemoMouse.Visibility = Visibility.Visible;
                DemoPanel.Visibility = Visibility.Collapsed;

                var comboCenter = new Point(combo.Left + combo.Width / 2, combo.Top + combo.Height / 2);
                SetDemoCursor(comboCenter);
                await Task.Delay(300, token);
                SetDemoMousePart(DemoMousePart.Left);
                SetDemoBadge("① 点开下拉", comboCenter);
                await Task.Delay(500, token);
                await PulseAsync();
                await Task.Delay(200, token);

                // 下拉在底部工具条上：列表往下会出屏，所以往上展开，和真实弹出方向一致。
                var items = ShowFlavorDropdown(combo);
                DemoBadge.Visibility = Visibility.Collapsed;
                DemoStatus.Text = "① 选：脚步";
                if (items.TryGetValue(demoKey, out var itemRect))
                {
                    await MoveCursorAsync(itemRect);
                    await Task.Delay(120, token);
                    await PulseAsync();
                    SetDemoBadge(
                        "① 选：脚步",
                        new Point(itemRect.Left + itemRect.Width / 2, itemRect.Top + itemRect.Height / 2));
                    await Task.Delay(500, token);
                }

                HideFlavorDropdown();
                DemoBadge.Visibility = Visibility.Collapsed;
                await Task.Delay(350, token);
            }

            // 第二拍：选完了才铺曲线——这条曲线属于刚才选中的那种声音。
            ResetCurveDemo(GetTargetRect("EqGraph"));
            DemoPanelTitle.Text = "模拟：脚步 2.5 kHz +6 dB";
            DemoPanelTitle.HorizontalAlignment = HorizontalAlignment.Right;
            DemoPanelTitle.VerticalAlignment = VerticalAlignment.Top;
            DemoPanelTitle.Margin = new Thickness(0, 6, 10, 0);
            DemoCurvePath.Stroke = Theme.TonePalette.BrushFor(demoKey);

            DemoStatus.Text = "② 上下拖：增强或减弱它";
            SetDemoMousePart(DemoMousePart.Left);
            SetDemoCursor(DemoPointAt(2500, 0));
            await Task.Delay(350, token);
            var point = AddDemoPoint(2500, 0, 1.0);
            await DragDemoPointAsync(point, 2500, 6, 1100, "② 拖起来 = 增强", token);
            await Task.Delay(700, token);

            // 右键原地生效：光标不移动，曲线直接复位成平直，不做拖回去的过渡。
            DemoStatus.Text = "③ 右键：还原成 0 dB";
            SetDemoMousePart(DemoMousePart.Right);
            SetDemoBadge("③ 右键 = 还原", DemoPointAt(2500, 6));
            await PulseAsync(new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)));
            RemoveDemoPoint(point);
            DemoBadge.Visibility = Visibility.Collapsed;
            await Task.Delay(600, token);

            DemoStatus.Text = "还原后它不再显示，也不再影响声音；演示结束，不改你的曲线";
            DemoMouse.Visibility = Visibility.Collapsed;
            SetDemoMousePart(DemoMousePart.None);
        }
        catch (OperationCanceledException)
        {
            // 用户切下一步、跳过或关教程。
        }
    }

    /// <summary>
    /// 照抄真实下拉框的位置、宽度、外观，在它正下方展开一份模拟列表；
    /// 返回每一项在遮罩里的矩形，用来把光标移过去「点」它。
    /// </summary>
    private Dictionary<string, Rect> ShowFlavorDropdown(Rect combo)
    {
        var rects = new Dictionary<string, Rect>();
        DemoDropdownItems.Children.Clear();

        var bands = PeaceWpf.Core.Model.ToneBands.All;
        const double itemHeight = 22;
        foreach (var band in bands)
        {
            var isSelected = band.Key == "step";
            DemoDropdownItems.Children.Add(new Border
            {
                Height = itemHeight,
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(4),
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(0x33, 0x4C, 0xC2, 0xFF))
                    : Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = band.Label,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Theme.TonePalette.BrushFor(band.Key),
                },
            });
        }

        // 高度按项数直接算，不依赖布局测量：第一次显示时 DesiredSize 还可能是 0，
        // 用它判断方向会永远“放得下”，列表就掉到屏幕外了。
        var listHeight = bands.Count * itemHeight + 6;
        DemoDropdown.Width = Math.Max(150, combo.Width);
        DemoDropdown.Height = listHeight;
        DemoDropdown.Visibility = Visibility.Visible;

        // 真实下拉框就是一律向上弹的，模拟列表跟着向上，两边位置才对得上。
        var top = Math.Max(ScreenMargin, combo.Top - 2 - listHeight);
        Canvas.SetLeft(DemoDropdown, combo.Left);
        Canvas.SetTop(DemoDropdown, top);
        DemoDropdown.UpdateLayout();

        for (var i = 0; i < DemoDropdownItems.Children.Count && i < bands.Count; i++)
        {
            if (DemoDropdownItems.Children[i] is not FrameworkElement element
                || element.ActualWidth <= 0)
            {
                continue;
            }

            var origin = element.TransformToAncestor(this).Transform(new Point(0, 0));
            rects[bands[i].Key] = new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
        }

        return rects;
    }

    private void HideFlavorDropdown()
    {
        if (DemoDropdown is null)
        {
            return;
        }

        DemoDropdown.Visibility = Visibility.Collapsed;
        DemoDropdownItems.Children.Clear();
    }

    private void StopCurveDemo()
    {
        if (_realCurveDemoActive)
        {
            // 演示没播完就离开：直接把曲线落到最终形状，别留半截。
            _realCurveDemoActive = false;
            CurveFinishDemo?.Invoke();
        }

        _audioCurvePreview = false;
        BoostPercent = null;
        var cts = _demoCts;
        _demoCts = null;
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (CurveDemoLayer is null)
        {
            return;
        }

        CurveDemoLayer.Visibility = Visibility.Collapsed;
        DemoPanel.Visibility = Visibility.Collapsed;
        DemoMouse.Visibility = Visibility.Collapsed;
        DemoKeyCap.Visibility = Visibility.Collapsed;
        DemoBadge.Visibility = Visibility.Collapsed;
        HideFlavorDropdown();
        DemoDots.Children.Clear();
        _demoPoints.Clear();
        DemoCurvePath.Data = null;
        DemoFlatLine.Data = null;
        DemoMeasuredPath.Data = null;
        DemoBassPath.Data = null;
        DemoBassPath.Visibility = Visibility.Collapsed;
        DemoCrossover.Visibility = Visibility.Collapsed;
        DemoCrossoverLabel.Visibility = Visibility.Collapsed;
    }

    private void ShowAudioCurvePreview(bool shaped)
    {
        if (_index < 0 || !_steps[_index].AudioCurveDemo)
        {
            return;
        }

        if (!_audioCurvePreview)
        {
            StopCurveDemo();
            ResetCurveDemo(GetTargetRect("EqGraph"));
            CursorDot.Visibility = Visibility.Collapsed;
            DemoMouse.Visibility = Visibility.Collapsed;
            DemoKeyCap.Visibility = Visibility.Collapsed;
            DemoBadge.Visibility = Visibility.Collapsed;
            DemoStatus.Visibility = Visibility.Collapsed;
            _audioCurvePreview = true;
        }

        var bass = _steps[_index].BassBoostDemo;
        var layer = _steps[_index].LayerCompareDemo;
        var edit = _steps[_index].EditCurveDemo;
        DemoPanelTitle.Text = layer
            ? (shaped ? "左右 + 低音增强" : "左右曲线")
            : edit
                ? (shaped ? "编辑后的曲线" : "平直曲线")
                : bass
                    ? (shaped ? "低音增强曲线" : "没叠加：平直")
                    : (shaped ? "示例 EQ 曲线" : "当前声音：平直");
        DemoPanelTitle.HorizontalAlignment = HorizontalAlignment.Right;
        DemoPanelTitle.VerticalAlignment = VerticalAlignment.Bottom;
        DemoPanelTitle.Margin = new Thickness(0, 0, 10, 8);
        DemoDots.Children.Clear();
        _demoPoints.Clear();
        if (bass)
        {
            if (shaped)
            {
                AddDemoPoint(55, 6, 0.7);
                AddDemoPoint(120, 3, 0.8);
            }
            else
            {
                UpdateDemoCurve();
            }
        }
        else if (shaped || layer)
        {
            AddDemoPoint(110, 6, 0.7);
            AddDemoPoint(10000, -5, 1.0);
            if (layer && shaped)
            {
                AddDemoPoint(55, 4, 0.9);
            }
        }
        else
        {
            UpdateDemoCurve();
        }

        DemoCurvePath.Opacity = 1;
    }

    private void HideAudioCurvePreview()
    {
        if (!_audioCurvePreview)
        {
            return;
        }

        _audioCurvePreview = false;
        StopCurveDemo();
        CursorDot.Visibility = Visibility.Visible;
    }

    private void ShowGraphEditGlow(bool animate)
    {
        var rect = GetTargetRect("EqGraph");
        Canvas.SetLeft(GraphEditGlow, rect.Left);
        Canvas.SetTop(GraphEditGlow, rect.Top);
        GraphEditGlow.Width = Math.Max(1, rect.Width);
        GraphEditGlow.Height = Math.Max(1, rect.Height);
        GraphEditGlow.Visibility = Visibility.Visible;

        if (animate && !_graphEditVisible)
        {
            GraphEditGlow.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)));
        }
        else
        {
            GraphEditGlow.BeginAnimation(OpacityProperty, null);
            GraphEditGlow.Opacity = 1;
        }

        _graphEditVisible = true;
    }

    private void ShowModeIconGlow(string targetId, Rect target)
    {
        ModeIconText.Text = targetId == "ModeBass" ? "低音" : "左右";
        Canvas.SetLeft(ModeIconGlow, target.Left);
        Canvas.SetTop(ModeIconGlow, target.Top);
        ModeIconGlow.Width = Math.Max(1, target.Width);
        ModeIconGlow.Height = Math.Max(1, target.Height);
        ModeIconGlow.Visibility = Visibility.Visible;
        ModeIconGlow.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
    }

    private void HideGraphEditGlow()
    {
        _graphEditVisible = false;
        GraphEditGlow.BeginAnimation(OpacityProperty, null);
        GraphEditGlow.Opacity = 0;
        GraphEditGlow.Visibility = Visibility.Collapsed;

        ModeIconGlow.BeginAnimation(OpacityProperty, null);
        ModeIconGlow.Opacity = 0;
        ModeIconGlow.Visibility = Visibility.Collapsed;
    }

    private void ShowBassCurvePreview()
    {
        StopCurveDemo();
        ResetCurveDemo(GetTargetRect("EqGraph"));
        DemoPanelTitle.Text = "低音增强曲线";
        DemoPanelTitle.HorizontalAlignment = HorizontalAlignment.Right;
        DemoPanelTitle.VerticalAlignment = VerticalAlignment.Top;
        DemoPanelTitle.Margin = new Thickness(0, 6, 10, 0);
        DemoMouse.Visibility = Visibility.Collapsed;
        DemoKeyCap.Visibility = Visibility.Collapsed;
        DemoBadge.Visibility = Visibility.Collapsed;
        DemoStatus.Visibility = Visibility.Collapsed;
        DemoDots.Children.Clear();
        _demoPoints.Clear();
        AddDemoPoint(55, 6, 0.7);
        AddDemoPoint(120, 3, 0.8);
        DemoCurvePath.Opacity = 1;
        CursorDot.Visibility = Visibility.Visible;
        SetCursorImmediate(GetTargetRect("ModeBass"));
    }

    // ---- 进阶课：直接在真实曲线控件上演（数据由主窗口临时换成教程那套） ----

    /// <summary>频率/增益 → 屏幕坐标，由主窗口接到 EqGraphView。</summary>
    public Func<double, double, Point>? CurvePointAt { get; set; }

    public Func<double, int>? CurveAddPoint { get; set; }

    public Action<int, double, double>? CurveMovePoint { get; set; }

    public Action? CurveCommitPoint { get; set; }

    public Action<int>? CurveDeletePoint { get; set; }

    public Action? CurveUndo { get; set; }

    /// <summary>试听直接走主程序的扫频；第一参数叠低音，第二参数只放低频段。</summary>
    public Func<bool, bool, Task<bool>>? AppPreviewAsync { get; set; }

    /// <summary>放一遍「没改过」的平直曲线，用来做修改前后对比。</summary>
    public Func<Task<bool>>? AppPreviewFlatAsync { get; set; }

    public Action? AppStopPreview { get; set; }

    private async Task RunAppPreviewAsync(
        bool compare,
        bool flatFirst,
        bool lowOnly,
        CancellationToken token)
    {
        try
        {
            if (AppPreviewAsync is null)
            {
                return;
            }

            if (flatFirst && AppPreviewFlatAsync is not null)
            {
                AudioStatus.Text = "正在播：没改过的平直曲线";
                await AppPreviewFlatAsync();
                await Task.Delay(3200, token);
                AppStopPreview?.Invoke();
                await Task.Delay(150, token);
            }

            AudioStatus.Text = lowOnly ? "正在播：低频段" : "正在播：高频段";
            await AppPreviewAsync(false, lowOnly);
            await Task.Delay(3200, token);
            AppStopPreview?.Invoke();
            if (!compare)
            {
                AudioStatus.Text = "对比结束。再点一次可以重听。";
                return;
            }

            await Task.Delay(150, token);
            AudioStatus.Text = lowOnly ? "正在播：低频段 + 低音层" : "正在播：左右 + 低音层";
            await AppPreviewAsync(true, lowOnly);
            await Task.Delay(3200, token);
            AppStopPreview?.Invoke();
            AudioStatus.Text = "对比结束。再点一次可以重听。";
        }
        catch (OperationCanceledException)
        {
            // 用户按了停止、切了下一步或关了教程。
        }
    }

    /// <summary>总音量那一步：光标沿竖直推子上下拖，曲线跟着整体涨落，最后回到 0 dB。</summary>
    private async Task RunPreampDemoAsync(Rect target)
    {
        if (CurveSetPreAmp is null)
        {
            return;
        }

        var x = target.Left + target.Width / 2;
        var top = target.Top + 16;
        var bottom = target.Bottom - 16;
        if (bottom <= top)
        {
            return;
        }

        var middle = (top + bottom) / 2;
        var lower = top + (bottom - top) * 0.66;

        CurveSetPreAmp(0);
        SetDemoCursor(new Point(x, middle));
        SetDemoMousePart(DemoMousePart.Left);
        SetDemoBadge("按住往下拖：整条曲线一起变低", BadgeAt());
        await Task.Delay(350);
        await DragPreampAsync(x, middle, lower, top, bottom, 800);

        SetDemoBadge("往上推：整条曲线一起变高", BadgeAt());
        await Task.Delay(350);
        await DragPreampAsync(x, lower, middle, top, bottom, 800);

        SetDemoMousePart(DemoMousePart.None);
        DemoBadge.Visibility = Visibility.Collapsed;
    }

    private async Task DragPreampAsync(
        double x,
        double fromY,
        double toY,
        double top,
        double bottom,
        int ms)
    {
        const int steps = 24;
        for (var i = 1; i <= steps; i++)
        {
            var y = fromY + (toY - fromY) * i / steps;
            SetDemoCursor(new Point(x, y));

            // 推子范围 −20…+20 dB，上面是正、下面是负；0 dB 在正中。
            var db = 20 - 40 * (y - top) / (bottom - top);
            CurveSetPreAmp?.Invoke(Math.Round(db, 1));
            await Task.Delay(Math.Max(12, ms / steps));
        }
    }

    private void StartRealCurveDemo(Rect target)
    {
        if (_closed || CurvePointAt is null)
        {
            return;
        }

        StopCurveDemo();
        _realCurveDemoActive = true;
        CurveDemoLayer.Visibility = Visibility.Visible;
        DemoPanel.Visibility = Visibility.Collapsed;
        DemoMouse.Visibility = Visibility.Visible;
        DemoKeyCap.Visibility = Visibility.Collapsed;
        DemoBadge.Visibility = Visibility.Collapsed;
        DemoDots.Children.Clear();
        CursorDot.Visibility = Visibility.Visible;
        SetDemoMousePart(DemoMousePart.Left);

        var cts = new CancellationTokenSource();
        _demoCts = cts;
        _ = RunRealCurveDemoAsync(cts.Token);
    }

    public Action<int, double>? CurveChangeQuality { get; set; }

    public Action? CurveResetToFlat { get; set; }

    /// <summary>演示被中途打断时，把曲线直接落到最终形状。</summary>
    public Action? CurveFinishDemo { get; set; }

    /// <summary>总音量演示用：把预增益设成这个值，曲线跟着整体平移。</summary>
    public Action<double>? CurveSetPreAmp { get; set; }

    private bool _realCurveDemoActive;

    private async Task RunRealCurveDemoAsync(CancellationToken token)
    {
        try
        {
            // 重播先清回平直，不然每点一次就多叠一个控制点。
            CurveResetToFlat?.Invoke();
            await Task.Delay(250, token);

            // ① 左键点空白：3 kHz 建一个点
            await MoveToGraphAsync(3000, 0, token);
            SetDemoBadge("左键点空白：建一个控制点", BadgeAt());
            SetDemoMousePart(DemoMousePart.Left);
            await Task.Delay(700, token);
            await PulseAsync();
            var id = CurveAddPoint?.Invoke(3000) ?? -1;
            CurveCommitPoint?.Invoke();
            await Task.Delay(900, token);
            if (id < 0)
            {
                return;
            }

            // ② 上下拖：拉低高音的增益
            SetDemoBadge("上下拖：调增益（把高频拉低）", BadgeAt());
            await DragGraphAsync(3000, 0, 3000, -10, 1400, id, token);
            await Task.Delay(900, token);

            // ③ 左右拖：改频率
            SetDemoBadge("左右拖：调频率", BadgeAt());
            await DragGraphAsync(3000, -10, 6000, -10, 1400, id, token);
            await Task.Delay(900, token);

            // ④ 滚轮：改 Q（宽窄）
            SetDemoBadge("滚轮：调 Q，改这一段的宽窄", BadgeAt());
            SetDemoMousePart(DemoMousePart.Wheel);
            // 新点的 Q 是按邻居自动定的；3 kHz 空地上落在自适应下限，演示就从这里往窄里压。
            await WheelQualityAsync(id, EqLimits.MinAutoQuality, 0.55, token);
            await Task.Delay(900, token);

            // ⑤ 右键删掉这个点
            SetDemoBadge("右键：删掉这个点", BadgeAt());
            SetDemoMousePart(DemoMousePart.Right);
            await PulseAsync();
            CurveDeletePoint?.Invoke(id);
            await Task.Delay(900, token);

            // ⑥ Ctrl+Z 撤回删除：收起鼠标图标，在原位换成一枚键帽
            DemoBadge.Visibility = Visibility.Collapsed;
            SetDemoMousePart(DemoMousePart.None);
            DemoMouse.Visibility = Visibility.Collapsed;
            SetDemoKeyCap(true);
            await Task.Delay(700, token);
            CurveUndo?.Invoke();
            await Task.Delay(900, token);
            SetDemoKeyCap(false);
        }
        catch (OperationCanceledException)
        {
            // 切下一步、跳过或关教程。
        }
    }

    private Point BadgeAt() => new(_cursorX + 96, _cursorY + 34);

    private async Task WheelQualityAsync(int id, double fromQ, double toQ, CancellationToken token)
    {
        const int steps = 20;
        for (var i = 0; i <= steps; i++)
        {
            token.ThrowIfCancellationRequested();
            var t = i / (double)steps;
            // Q 按对数走，滚一格感觉更均匀。
            var q = Math.Exp(Math.Log(fromQ) + (Math.Log(toQ) - Math.Log(fromQ)) * t);
            CurveChangeQuality?.Invoke(id, q);
            await Task.Delay(50, token);
        }

        CurveCommitPoint?.Invoke();
    }

    private async Task MoveToGraphAsync(double frequencyHz, double gainDb, CancellationToken token)
    {
        if (CurvePointAt is null)
        {
            return;
        }

        SetDemoCursor(PointFromScreen(CurvePointAt(frequencyHz, gainDb)));
        await Task.Delay(24, token);
    }

    private async Task DragGraphAsync(
        double fromHz,
        double fromDb,
        double toHz,
        double toDb,
        int durationMs,
        int id,
        CancellationToken token)
    {
        var frames = Math.Max(1, durationMs / 16);
        for (var i = 0; i <= frames; i++)
        {
            token.ThrowIfCancellationRequested();
            var t = i / (double)frames;
            var eased = t * t * (3 - 2 * t);
            var hz = fromHz + (toHz - fromHz) * eased;
            var db = fromDb + (toDb - fromDb) * eased;
            CurveMovePoint?.Invoke(id, hz, db);
            if (CurvePointAt is not null)
            {
                SetDemoCursor(PointFromScreen(CurvePointAt(hz, db)));
            }

            await Task.Delay(16, token);
        }

        CurveCommitPoint?.Invoke();
        await Task.Delay(200, token);
    }

    /// <summary>
    /// 自动EQ 那一步：不在这里画曲线，直接驱动真实窗口那条黄线。
    /// 三条线由窗口自己的拟合器和画法算出来，遮罩只负责把光标放到黄线上，左右拖几趟。
    /// </summary>
    private void StartCrossoverDemo(Rect target)
    {
        if (_closed)
        {
            return;
        }

        StopCurveDemo();
        CursorDot.Visibility = Visibility.Visible;
        DemoMouse.Visibility = Visibility.Visible;

        var cts = new CancellationTokenSource();
        _demoCts = cts;
        _ = RunCrossoverDemoAsync(cts.Token);
    }

    private async Task RunCrossoverDemoAsync(CancellationToken token)
    {
        try
        {
            const double start = 120;
            const double far = 260;
            const double back = 150;

            DemoStatus.Text = "黄色虚线是矫正起点：按住它左右拖";
            SetCrossover(start);
            SetDemoMousePart(DemoMousePart.Left);
            await Task.Delay(600, token);
            await PulseAsync();
            await Task.Delay(200, token);

            DemoStatus.Text = "往右拖：起点以上交给「左右」层";
            await DragCrossoverAsync(start, far, 1200, token);
            await Task.Delay(450, token);

            DemoStatus.Text = "往左拖：更多低频交回低音层";
            await DragCrossoverAsync(far, back, 900, token);
            await Task.Delay(450, token);

            DemoStatus.Text = "松手就按新起点重算，不用重测";
            SetDemoMousePart(DemoMousePart.None);
            DemoMouse.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // 切下一步、跳过或关教程。
        }
    }

    private async Task DragCrossoverAsync(double fromHz, double toHz, int durationMs, CancellationToken token)
    {
        // 窗口那边拖动时本来就按 120 ms 节流重算，这里跟它一个节奏，别把重算排成队。
        var frames = Math.Max(1, durationMs / 120);
        for (var i = 0; i <= frames; i++)
        {
            token.ThrowIfCancellationRequested();
            var t = i / (double)frames;
            var eased = t * t * (3 - 2 * t);
            SetCrossover(fromHz + (toHz - fromHz) * eased);
            await Task.Delay(90, token);
        }
    }

    /// <summary>把矫正起点交给真实窗口，并把光标贴到它挪过去之后的位置上。</summary>
    private void SetCrossover(double floorHz)
    {
        if (CrossoverSet is null)
        {
            return;
        }

        CrossoverSet(floorHz, true);
        if (CursorAtCrossover?.Invoke() is { } screen && screen.X > 0)
        {
            SetDemoCursor(PointFromScreen(screen));
        }
    }

    /// <summary>
    /// 左下角「贴合目标 ↔ 保证音量」：把真实滑杆从一头拖到另一头，看曲线怎么换。
    /// </summary>
    private void StartBoostDemo(Rect target)
    {
        if (_closed || BoostSet is null)
        {
            return;
        }

        StopCurveDemo();
        CursorDot.Visibility = Visibility.Visible;
        DemoMouse.Visibility = Visibility.Visible;
        SetDemoMousePart(DemoMousePart.Left);

        var cts = new CancellationTokenSource();
        _demoCts = cts;
        _ = RunBoostDemoAsync(cts.Token);
    }

    private async Task RunBoostDemoAsync(CancellationToken token)
    {
        try
        {
            DemoStatus.Text = "最左：贴合目标，提升全给，整体靠预增益压下来";
            await SlideBoostAsync(0, token);
            await Task.Delay(900, token);

            DemoStatus.Text = "中间：只切不补，音量最满、不削波";
            await SlideBoostAsync(50, token);
            await Task.Delay(900, token);

            DemoStatus.Text = "往右：提升放回来了，超过 0 dBFS 的会被削掉";
            await SlideBoostAsync(100, token);
            DemoStatus.Text = "到头了；再往左就退回只切不补";
            SetDemoMousePart(DemoMousePart.None);
            DemoMouse.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // 切下一步、跳过或关教程。
        }
    }

    /// <summary>一路挪到目标百分比：途中用快速重算，到最后一步再跑一次完整的。</summary>
    private async Task SlideBoostAsync(double toPercent, CancellationToken token)
    {
        var from = BoostPercent ?? 0;
        var steps = Math.Max(1, (int)Math.Round(Math.Abs(toPercent - from) / 10));
        for (var i = 1; i <= steps; i++)
        {
            token.ThrowIfCancellationRequested();
            var eased = (double)i / steps;
            SetBoost(from + (toPercent - from) * eased, quick: i < steps);
            await Task.Delay(160, token);
        }

        await Task.Delay(220, token);
    }

    private void SetBoost(double percent, bool quick)
    {
        if (BoostSet is null)
        {
            return;
        }

        BoostSet(percent, quick);
        BoostPercent = percent;
        if (CursorAtBoost?.Invoke() is { } screen)
        {
            SetDemoCursor(PointFromScreen(screen));
        }
    }

    private void ResetCurveDemo(Rect target)
    {
        _demoPlot = MakeDemoPlot(target);
        DemoPanelTitle.Text = "演示曲线";
        DemoPanelTitle.HorizontalAlignment = HorizontalAlignment.Left;
        DemoPanelTitle.VerticalAlignment = VerticalAlignment.Top;
        DemoPanelTitle.Margin = new Thickness(8, 6, 0, 0);
        CurveDemoLayer.Visibility = Visibility.Visible;
        DemoPanel.Visibility = Visibility.Visible;
        Canvas.SetLeft(DemoPanel, _demoPlot.Left - 10);
        Canvas.SetTop(DemoPanel, _demoPlot.Top - 8);
        DemoPanel.Width = Math.Max(1, _demoPlot.Right - _demoPlot.Left + 20);
        DemoPanel.Height = Math.Max(1, _demoPlot.Bottom - _demoPlot.Top + 16);
        DemoKeyCap.Visibility = Visibility.Collapsed;
        DemoDots.Children.Clear();
        _demoPoints.Clear();
        DemoBadge.Visibility = Visibility.Collapsed;
        DemoBadgeText.Text = string.Empty;

        DemoGrid.Children.Clear();
        var gridBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
        foreach (var freq in new[] { 20.0, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 })
        {
            var x = DemoPointAt(freq, 0).X;
            DemoGrid.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = _demoPlot.Top,
                Y2 = _demoPlot.Bottom,
                Stroke = gridBrush,
                StrokeThickness = 1,
            });
        }

        for (var gain = -15; gain <= 15; gain += 3)
        {
            var y = DemoPointAt(20, gain).Y;
            DemoGrid.Children.Add(new Line
            {
                X1 = _demoPlot.Left,
                X2 = _demoPlot.Right,
                Y1 = y,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
            });
        }

        DemoFlatLine.Data = new LineGeometry(
            DemoPointAt(20, 0),
            DemoPointAt(20000, 0));
        DemoFlatLine.Stroke = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
        DemoMeasuredPath.Data = null;
        DemoCurvePath.Stroke = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
        DemoCurvePath.Data = BuildDemoCurve();
        DemoCurvePath.Opacity = 0.5;

        DemoStatus.Text = "左键点空白：新建一个点";
        CursorDot.Visibility = Visibility.Visible;
        DemoMouse.Visibility = Visibility.Visible;
        SetDemoMousePart(DemoMousePart.Left);
        _cursorReady = true;
        SetDemoCursor(DemoPointAt(80, 0));
    }

    private DemoPlot MakeDemoPlot(Rect target) =>
        new(
            target.Left + 46,
            target.Right - 16,
            target.Top + 14,
            target.Bottom - 34);

    private Point DemoPointAt(double freq, double gain)
    {
        var width = Math.Max(1, _demoPlot.Right - _demoPlot.Left);
        var height = Math.Max(1, _demoPlot.Bottom - _demoPlot.Top);
        var x = _demoPlot.Left
            + Math.Log10(freq / 20) / Math.Log10(1000) * width;
        var y = _demoPlot.Mid - gain / 30 * height;
        return new Point(x, y);
    }

    private double DemoGainAt(double freq)
    {
        if (_demoPoints.Count == 0)
        {
            return 0;
        }

        var total = 0.0;
        foreach (var point in _demoPoints)
        {
            var sigma = 0.42 / Math.Max(0.55, point.Q);
            var dx = Math.Log10(freq / point.Freq);
            total += point.Gain * Math.Exp(-0.5 * dx * dx / (sigma * sigma));
        }

        return Math.Clamp(total, -15, 15);
    }

    private Geometry BuildDemoCurve()
    {
        const int samples = 180;
        var points = new PointCollection();
        for (var i = 0; i < samples; i++)
        {
            var t = i / (double)(samples - 1);
            var freq = 20 * Math.Pow(1000, t);
            points.Add(DemoPointAt(freq, DemoGainAt(freq)));
        }

        var figure = new PathFigure { StartPoint = points[0] };
        figure.Segments.Add(new PolyLineSegment(points, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private Geometry BuildDemoCurve(Func<double, double> gainAt, double minHz = 20, double maxHz = 20000)
    {
        const int samples = 180;
        var points = new PointCollection();
        for (var i = 0; i < samples; i++)
        {
            var t = i / (double)(samples - 1);
            var freq = minHz * Math.Pow(maxHz / minHz, t);
            points.Add(DemoPointAt(freq, Math.Clamp(gainAt(freq), -14, 14)));
        }

        var figure = new PathFigure { StartPoint = points[0] };
        figure.Segments.Add(new PolyLineSegment(points, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private DemoPoint AddDemoPoint(double freq, double gain, double q)
    {
        var dot = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            StrokeThickness = 3,
            IsHitTestVisible = false,
        };

        var point = new DemoPoint { Freq = freq, Gain = gain, Q = q, Dot = dot };
        DemoDots.Children.Add(dot);
        _demoPoints.Add(point);
        UpdateDemoPoint(point);
        UpdateDemoCurve();
        return point;
    }

    private void RemoveDemoPoint(DemoPoint point)
    {
        DemoDots.Children.Remove(point.Dot);
        _demoPoints.Remove(point);
        UpdateDemoCurve();
    }

    private void UpdateDemoPoint(DemoPoint point)
    {
        var at = DemoPointAt(point.Freq, point.Gain);
        Canvas.SetLeft(point.Dot, at.X - point.Dot.Width / 2);
        Canvas.SetTop(point.Dot, at.Y - point.Dot.Height / 2);
    }

    private void UpdateDemoCurve()
    {
        DemoCurvePath.Data = BuildDemoCurve();
        DemoCurvePath.Opacity = _demoPoints.Count == 0 ? 0.5 : 1;
    }

    private void SetDemoCursor(Point point) =>
        SetCursorImmediate(point.X - CursorDot.Width / 2, point.Y - CursorDot.Height / 2);

    private void SetDemoMousePart(DemoMousePart part)
    {
        var idle = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        var idleBorder = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
        MouseLeftButton.Background = idle;
        MouseLeftButton.BorderBrush = idleBorder;
        MouseRightButton.Background = idle;
        MouseRightButton.BorderBrush = idleBorder;
        MouseWheelMark.Background = idleBorder;
        MouseWheelMark.Width = 4;

        switch (part)
        {
            case DemoMousePart.Left:
                MouseLeftButton.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
                MouseLeftButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0xD8, 0xFF));
                break;
            case DemoMousePart.Right:
                MouseRightButton.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                MouseRightButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0xB3));
                break;
            case DemoMousePart.Wheel:
                MouseWheelMark.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x57));
                MouseWheelMark.Width = 5;
                break;
        }
    }

    private void SetDemoKeyCap(bool visible)
    {
        DemoKeyCap.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
        {
            PositionDemoKeyCap();
        }
    }

    private void PositionDemoKeyCap()
    {
        DemoKeyCap.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = DemoKeyCap.DesiredSize.Width;
        var height = DemoKeyCap.DesiredSize.Height;

        // 键帽替换鼠标图标：左边缘对齐图标原来的左边缘，垂直居中。
        var left = _cursorX + CursorDot.Width + 10;
        var top = _cursorY + CursorDot.Height / 2 - height / 2;
        Canvas.SetLeft(DemoKeyCap, left);
        Canvas.SetTop(DemoKeyCap, top);
    }

    private string _badgeText = string.Empty;

    private void SetDemoBadge(string text, Point at)
    {
        _badgeText = text;
        DemoBadgeText.Text = text;
        DemoBadge.Visibility = Visibility.Visible;
        PositionDemoBadge();
    }

    /// <summary>徽标跟着光标走：左边缘贴在鼠标图标右边，垂直居中对齐图标。</summary>
    private void PositionDemoBadge()
    {
        DemoBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = DemoBadge.DesiredSize.Width;
        var height = DemoBadge.DesiredSize.Height;

        // 鼠标图标画在光标右侧；徽标再从图标右边起，不挡图标。
        var mouseWidth = DemoMouse.ActualWidth > 0 ? DemoMouse.ActualWidth : 34;
        var left = _cursorX + CursorDot.Width + 10 + mouseWidth + 8;
        var top = _cursorY + CursorDot.Height / 2 - height / 2;

        var x = Math.Clamp(left, ScreenMargin, Math.Max(ScreenMargin, ActualWidth - width - ScreenMargin));
        var y = Math.Clamp(top, ScreenMargin, Math.Max(ScreenMargin, ActualHeight - height - ScreenMargin));
        Canvas.SetLeft(DemoBadge, x);
        Canvas.SetTop(DemoBadge, y);
    }

    private async Task DragDemoPointAsync(
        DemoPoint point,
        double targetFreq,
        double targetGain,
        int durationMs,
        string badge,
        CancellationToken token)
    {
        var startFreq = point.Freq;
        var startGain = point.Gain;
        var frames = Math.Max(1, durationMs / 16);

        for (var i = 0; i <= frames; i++)
        {
            token.ThrowIfCancellationRequested();
            var t = i / (double)frames;
            var eased = t * t * (3 - 2 * t);
            point.Freq = startFreq + (targetFreq - startFreq) * eased;
            point.Gain = startGain + (targetGain - startGain) * eased;
            UpdateDemoPoint(point);
            UpdateDemoCurve();

            var at = DemoPointAt(point.Freq, point.Gain);
            SetDemoCursor(at);
            SetDemoBadge(badge, at);
            await Task.Delay(16, token);
        }
    }

    private async Task WheelDemoAsync(
        DemoPoint point,
        double targetQ,
        int durationMs,
        string badge,
        CancellationToken token)
    {
        var startQ = point.Q;
        var frames = Math.Max(1, durationMs / 16);

        for (var i = 0; i <= frames; i++)
        {
            token.ThrowIfCancellationRequested();
            var t = i / (double)frames;
            var eased = t * t * (3 - 2 * t);
            point.Q = startQ + (targetQ - startQ) * eased;
            UpdateDemoCurve();

            var at = DemoPointAt(point.Freq, point.Gain);
            SetDemoCursor(at);
            SetDemoBadge(badge, at);
            await Task.Delay(16, token);
        }
    }

    private async Task RunCurveDemoAsync(CancellationToken token)
    {
        try
        {
            var lowStart = DemoPointAt(80, 0);
            SetDemoMousePart(DemoMousePart.Left);
            SetDemoCursor(lowStart);
            SetDemoBadge("左键：建点", lowStart);
            await Task.Delay(650, token);
            await PulseAsync();
            await Task.Delay(420, token);

            var low = AddDemoPoint(80, 0, 1.4);
            DemoBadge.Visibility = Visibility.Collapsed;

            DemoStatus.Text = "上下拖：改增益";
            await DragDemoPointAsync(low, 80, 6, 1200, "上下拖 = 改增益", token);
            await Task.Delay(500, token);

            DemoStatus.Text = "左右拖：改频率";
            await DragDemoPointAsync(low, 110, 6, 1100, "左右拖 = 改频率", token);
            await Task.Delay(600, token);

            SetDemoMousePart(DemoMousePart.Wheel);
            DemoStatus.Text = "滚轮：改 Q，让低频更宽";
            await WheelDemoAsync(low, 0.7, 1200, "滚轮 = 改 Q", token);
            await Task.Delay(650, token);

            SetDemoMousePart(DemoMousePart.Left);
            DemoStatus.Text = "高音也压一点";
            var highStart = DemoPointAt(9000, 0);
            SetDemoCursor(highStart);
            SetDemoBadge("左键：建点", highStart);
            await Task.Delay(450, token);
            await PulseAsync();
            await Task.Delay(350, token);

            var high = AddDemoPoint(9000, 0, 1.0);
            await DragDemoPointAsync(high, 10000, -5, 1300, "上下 + 左右：一起改", token);
            await Task.Delay(700, token);

            DemoStatus.Text = "右键：删掉刚才拉高的低音点";
            var lowFreq = low.Freq;
            var lowGain = low.Gain;
            var lowQ = low.Q;
            SetDemoMousePart(DemoMousePart.Right);
            SetDemoCursor(DemoPointAt(lowFreq, lowGain));
            SetDemoBadge("右键：删点", DemoPointAt(lowFreq, lowGain));
            await Task.Delay(500, token);
            await PulseAsync(new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)));
            RemoveDemoPoint(low);
            DemoBadge.Visibility = Visibility.Collapsed;
            await Task.Delay(900, token);

            DemoStatus.Text = "Ctrl+Z：把删掉的低音点撤回来";
            DemoMouse.Visibility = Visibility.Collapsed;
            SetDemoKeyCap(true);
            SetDemoMousePart(DemoMousePart.None);
            SetDemoCursor(DemoPointAt(lowFreq, lowGain));
            await Task.Delay(600, token);
            var restored = AddDemoPoint(lowFreq, lowGain, lowQ);
            restored.Dot.Opacity = 0;
            restored.Dot.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450)));
            await Task.Delay(700, token);
            SetDemoKeyCap(false);
            DemoBadge.Visibility = Visibility.Collapsed;
            await Task.Delay(500, token);

            DemoMouse.Visibility = Visibility.Visible;
            DemoStatus.Text = "拖右侧「增益」推子：整条曲线一起上下平移，形状不变";
            DemoBadge.Visibility = Visibility.Collapsed;
            await Task.Delay(900, token);

            DemoStatus.Text = "改完记得按保存；演示结束，不改你的曲线";
            SetDemoMousePart(DemoMousePart.None);
            DemoMouse.Visibility = Visibility.Collapsed;
            SetDemoCursor(DemoPointAt(restored.Freq, restored.Gain));
        }
        catch (OperationCanceledException)
        {
            // 用户切下一步、跳过或关教程。
        }
    }

    private void OnAudio(object sender, RoutedEventArgs e)
    {
        if (_audioPlaying)
        {
            StopAudio();
            AudioStatus.Text = "已停止。再点一次可以重听。";
            return;
        }

        _ = PlayComparisonAsync();
    }

    private async Task PlayComparisonAsync()
    {
        if (_closed || _busy)
        {
            return;
        }

        StopAudio();
        _audioPlaying = true;
        AudioButton.Content = "停止";
        AudioButton.IsEnabled = true;
        AudioStatus.Visibility = Visibility.Visible;

        var cts = new CancellationTokenSource();
        _audioCts = cts;

        try
        {
            if (AppPreviewAsync is not null)
            {
                await RunAppPreviewAsync(
                    _steps[_index].LayerCompareDemo,
                    _steps[_index].EditCurveDemo,
                    _steps[_index].LowSweepDemo,
                    cts.Token);
                return;
            }

            var bass = _steps[_index].BassBoostDemo;
            var layer = _steps[_index].LayerCompareDemo;
            var edit = _steps[_index].EditCurveDemo;
            var firstMode = layer ? TutorialDemoEq.Shaped : TutorialDemoEq.None;
            var secondMode = layer
                ? TutorialDemoEq.ShapedWithBass
                : bass
                    ? TutorialDemoEq.BassBoost
                    : TutorialDemoEq.Shaped;
            var firstStatus = layer
                ? "正在播：左右曲线"
                : edit
                    ? "正在播：平直曲线"
                    : bass
                        ? "正在播：没叠加低音层"
                        : "正在播：现在的声音";
            var secondStatus = layer
                ? "正在播：左右 + 低音层"
                : edit
                    ? "正在播：编辑后的曲线"
                    : bass
                        ? "正在播：叠加低音层（低频增强）"
                        : "正在播：示例 EQ（低频更厚、高频收一点）";

            AudioStatus.Text = firstStatus;
            ShowAudioCurvePreview(shaped: false);
            if (!_audio.Play(firstMode))
            {
                HideAudioCurvePreview();
                AudioStatus.Text = "放不出来：" + (_audio.LastError ?? "没有可用的输出设备");
                return;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(TutorialSweepPlayer.Seconds + 0.35),
                cts.Token);

            AudioStatus.Text = secondStatus;
            ShowAudioCurvePreview(shaped: true);
            if (!_audio.Play(secondMode))
            {
                HideAudioCurvePreview();
                AudioStatus.Text = "放不出来：" + (_audio.LastError ?? "没有可用的输出设备");
                return;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(TutorialSweepPlayer.Seconds + 0.35),
                cts.Token);

            AudioStatus.Text = "对比结束。再点一次可以重听。";
        }
        catch (OperationCanceledException)
        {
            // 用户按了停止、切了下一步或关了教程。
        }
        finally
        {
            if (ReferenceEquals(_audioCts, cts))
            {
                _audioCts = null;
                _audioPlaying = false;
                _audio.Stop();
                AudioButton.Content = "播放对比";
                cts.Dispose();
            }
        }
    }

    private void StopAudio()
    {
        HideAudioCurvePreview();
        var cts = _audioCts;
        _audioCts = null;
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _audio.Stop();
        _audioPlaying = false;
        if (AudioButton is not null)
        {
            AudioButton.Content = "播放对比";
        }
    }

    private void CloseTutorial()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        StopAudio();
        StopCurveDemo();
        _audio.Dispose();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseTutorial();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Right or Key.Space && !_busy && NextButton.IsEnabled)
        {
            OnNext(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_closed || _busy || _index < 0 || _index >= _steps.Count)
        {
            return;
        }

        var step = _steps[_index];
        var target = GetTargetRect(step.TargetId);
        UpdateSpotlight(target);
        PositionBubble(step, target);
        if (step.FlavorDemo)
        {
            StartFlavorDemo();
            return;
        }

        if (step.CurveDemo)
        {
            StartCurveDemo(target);
            return;
        }

        if (step.RealCurveDemo)
        {
            StartRealCurveDemo(target);
            return;
        }

        if (step.CrossoverDemo)
        {
            StartCrossoverDemo(target);
            return;
        }

        SetCursorImmediate(target);
    }

    private Rect GetTargetRect(string automationId) =>
        TryGetTargetRect(automationId, out var bounds) ? bounds : FallbackRect();

    /// <summary>
    /// 找得到、又真的显示在屏幕上时才算数。找不到时不要拿备用位置去假装「点」它，
    /// 否则遮罩指的地方和真实控件对不上。
    /// </summary>
    private bool TryGetTargetRect(string automationId, out Rect bounds)
    {
        bounds = Rect.Empty;
        var element = FindTarget(automationId);
        if (element is null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            var topLeft = PointFromScreen(element.PointToScreen(new Point(0, 0)));
            var bottomRight = PointFromScreen(
                element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight)));
            var rect = new Rect(topLeft, bottomRight);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            bounds = rect;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 目标可能在主窗口，也可能在教程刚打开的设置 / 预设EQ / 自动EQ 窗口里。
    /// 遮罩是覆盖整个屏幕的独立窗口，所以用屏幕坐标找，不依赖某一个窗口的可视树。
    /// </summary>
    private FrameworkElement? FindTarget(string automationId)
    {
        var own = Window.GetWindow(this);
        foreach (Window window in Application.Current.Windows)
        {
            if (ReferenceEquals(window, own) || !window.IsVisible)
            {
                continue;
            }

            var found = FindByAutomationId(window, automationId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>目标所在窗口的可见范围（换算成遮罩坐标）。气泡夹在它里面，才不会越过窗口边缘。</summary>
    private bool TryGetWindowBounds(Rect target, out Rect bounds)
    {
        bounds = default;
        var own = Window.GetWindow(this);
        var center = new Point(target.Left + target.Width / 2, target.Top + target.Height / 2);
        foreach (Window window in Application.Current.Windows)
        {
            if (ReferenceEquals(window, own) || !window.IsVisible
                || window.ActualWidth <= 0 || window.ActualHeight <= 0)
            {
                continue;
            }

            try
            {
                var topLeft = PointFromScreen(window.PointToScreen(new Point(0, 0)));
                var bottomRight = PointFromScreen(
                    window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight)));
                var rect = new Rect(topLeft, bottomRight);
                if (rect.Width <= 0 || rect.Height <= 0 || !rect.Contains(center))
                {
                    continue;
                }

                bounds = rect;
                return true;
            }
            catch (InvalidOperationException)
            {
                // 窗口正在创建 / 已关闭：跳过它。
            }
        }

        return false;
    }

    /// <summary>目标所在那块屏幕的可用区域（换算成遮罩坐标）。多显示器时不能按整个虚拟屏算。</summary>
    private bool TryGetMonitorBounds(Rect target, out Rect bounds)
    {
        bounds = default;
        try
        {
            var center = PointToScreen(new Point(target.Left + target.Width / 2, target.Top + target.Height / 2));
            var monitor = MonitorFromPoint(
                new NativePoint { X = (int)center.X, Y = (int)center.Y },
                MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return false;
            }

            var topLeft = PointFromScreen(new Point(info.Work.Left, info.Work.Top));
            var bottomRight = PointFromScreen(new Point(info.Work.Right, info.Work.Bottom));
            bounds = new Rect(topLeft, bottomRight);
            return bounds.Width > 0 && bounds.Height > 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private const int MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private Rect FallbackRect() =>        new(
            Math.Max(ScreenMargin, ActualWidth / 2 - 90),
            Math.Max(ScreenMargin, ActualHeight / 2 - 40),
            180,
            80);

    private static FrameworkElement? FindByAutomationId(DependencyObject root, string automationId)
    {
        if (root is FrameworkElement element
            && string.Equals(AutomationProperties.GetAutomationId(element), automationId, StringComparison.Ordinal))
        {
            return element;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindByAutomationId(VisualTreeHelper.GetChild(root, i), automationId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void UpdateSpotlight(Rect target)
    {
        var width = Math.Max(ActualWidth, 1);
        var height = Math.Max(ActualHeight, 1);
        var full = new RectangleGeometry(new Rect(0, 0, width, height));

        if (target.Width <= 0 || target.Height <= 0)
        {
            DimPath.Data = full;
            Spotlight.Visibility = Visibility.Collapsed;
            LeaderLine.Visibility = Visibility.Collapsed;
            return;
        }

        var hole = new RectangleGeometry(target, 9, 9);
        DimPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);

        // 进阶课：曲线图整块也要保持清晰，不然变暗层会把真实曲线的颜色和渐变盖掉。
        if (_graphHole is { Width: > 0, Height: > 0 } graph)
        {
            DimPath.Data = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                DimPath.Data,
                new RectangleGeometry(graph, 9, 9));
        }

        Canvas.SetLeft(Spotlight, target.Left - 4);
        Canvas.SetTop(Spotlight, target.Top - 4);
        Spotlight.Width = target.Width + 8;
        Spotlight.Height = target.Height + 8;
        Spotlight.Visibility = Visibility.Visible;
    }

    /// <summary>气泡淡入 / 淡出：换步骤时先退掉旧文字，等光标到位再浮出新内容。
    /// 连线和气泡同步：同一段透明度动画，连线不会先孤零零地挂出来。</summary>
    private Task FadeBubbleAsync(double target, int milliseconds)
    {
        if (_closed)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource();
        var animation = new DoubleAnimation(Bubble.Opacity, target, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };

        animation.Completed += (_, _) =>
        {
            SetBubbleOpacity(target);
            completion.TrySetResult();
        };

        Bubble.BeginAnimation(OpacityProperty, animation);
        LeaderLine.BeginAnimation(OpacityProperty, animation);
        return completion.Task;
    }

    /// <summary>气泡和连线是一体的：设透明度就一起设，别只改其中一个。</summary>
    private void SetBubbleOpacity(double value)
    {
        Bubble.BeginAnimation(OpacityProperty, null);
        LeaderLine.BeginAnimation(OpacityProperty, null);
        Bubble.Opacity = value;
        LeaderLine.Opacity = value;
    }

    /// <summary>演示类步骤：光标已经开始走，等一小会儿再把说明浮出来。</summary>
    private void FadeBubbleInAfter(int delayMs)
    {
        _ = Task.Delay(delayMs).ContinueWith(
            _ => Dispatcher.InvokeAsync(() =>
            {
                if (!_closed)
                {
                    _ = FadeBubbleAsync(1, 260);
                }
            }),
            TaskScheduler.Default);
    }

    private void PositionBubble(TutorialStep step, Rect target)
    {
        Bubble.Visibility = Visibility.Visible;
        Bubble.Measure(new Size(BubbleWidth, double.PositiveInfinity));
        var bubbleHeight = Math.Max(120, Bubble.DesiredSize.Height);
        var position = GetBubblePosition(target, bubbleHeight, step.Placement);

        Canvas.SetLeft(Bubble, position.X);
        Canvas.SetTop(Bubble, position.Y);

        UpdateLeaderLine(step.Placement, target, position, bubbleHeight);

        // 换步骤时马上会被 SetBubbleOpacity(0) 压回去；这里设成 1 是为了
        // 遮罩尺寸变化重排气泡时，连线和气泡一起保持原样可见。
        SetBubbleOpacity(1);
    }

    private Point GetBubblePosition(Rect target, double bubbleHeight, TutorialBubblePlacement placement)
    {
        var x = target.Left + target.Width / 2 - BubbleWidth / 2;
        var y = target.Bottom + BubbleGap;
        var mode = placement;

        if (mode == TutorialBubblePlacement.Auto)
        {
            mode = target.Bottom + BubbleGap + bubbleHeight <= ActualHeight - ScreenMargin
                ? TutorialBubblePlacement.Bottom
                : target.Top - BubbleGap - bubbleHeight >= ScreenMargin
                    ? TutorialBubblePlacement.Top
                    : target.Right + BubbleGap + BubbleWidth <= ActualWidth - ScreenMargin
                        ? TutorialBubblePlacement.Right
                        : TutorialBubblePlacement.Left;
        }

        switch (mode)
        {
            case TutorialBubblePlacement.Top:
                y = target.Top - BubbleGap - bubbleHeight;
                break;
            case TutorialBubblePlacement.Left:
                x = target.Left - BubbleGap - BubbleWidth;
                y = target.Top + target.Height / 2 - bubbleHeight / 2;
                break;
            case TutorialBubblePlacement.Right:
                x = target.Right + BubbleGap;
                y = target.Top;
                break;
            case TutorialBubblePlacement.TopLeft:
                x = target.Left + 18;
                y = target.Top + 18;
                break;
            case TutorialBubblePlacement.TopRight:
                x = target.Right - BubbleWidth - 18;
                y = target.Top + 18;
                break;
        }

        // 只夹在气泡所在窗口（取不到窗口就退到那块屏幕）的可见范围里：
        // 遮罩铺的是整个虚拟屏，按它来夹的话，气泡会越过窗口下边缘。
        var minX = ScreenMargin;
        var minY = ScreenMargin;
        var maxX = Math.Max(minX, ActualWidth - BubbleWidth - ScreenMargin);
        var maxY = Math.Max(minY, ActualHeight - bubbleHeight - ScreenMargin);
        if (TryGetWindowBounds(target, out var frame))
        {
            minX = frame.Left + ScreenMargin;
            minY = frame.Top + ScreenMargin;
            maxX = Math.Max(minX, frame.Right - BubbleWidth - ScreenMargin);
            maxY = Math.Max(minY, frame.Bottom - bubbleHeight - ScreenMargin);
        }
        else if (TryGetMonitorBounds(target, out var monitor))
        {
            minX = monitor.Left + ScreenMargin;
            minY = monitor.Top + ScreenMargin;
            maxX = Math.Max(minX, monitor.Right - BubbleWidth - ScreenMargin);
            maxY = Math.Max(minY, monitor.Bottom - bubbleHeight - ScreenMargin);
        }

        x = Math.Clamp(x, minX, maxX);
        y = Math.Clamp(y, minY, maxY);
        return new Point(x, y);
    }

    private void UpdateLeaderLine(TutorialBubblePlacement placement, Rect target, Point bubble, double bubbleHeight)
    {
        if (placement is TutorialBubblePlacement.TopLeft or TutorialBubblePlacement.TopRight)
        {
            // 气泡直接压在目标上方，再画一条线只会穿过气泡。
            LeaderLine.Visibility = Visibility.Collapsed;
            return;
        }

        double startX;
        double startY;
        double endX;
        double endY;

        switch (placement)
        {
            case TutorialBubblePlacement.Top:
                startX = bubble.X + BubbleWidth / 2;
                startY = bubble.Y + bubbleHeight;
                endX = target.Left + target.Width / 2;
                endY = target.Top;
                break;
            case TutorialBubblePlacement.Left:
                startX = bubble.X + BubbleWidth;
                startY = bubble.Y + bubbleHeight / 2;
                endX = target.Left;
                endY = target.Top + target.Height / 2;
                break;
            case TutorialBubblePlacement.Right:
                startX = bubble.X;
                startY = bubble.Y + bubbleHeight / 2;
                endX = target.Right;
                endY = target.Top + target.Height / 2;
                break;
            case TutorialBubblePlacement.TopLeft:
                startX = bubble.X + BubbleWidth / 2;
                startY = bubble.Y + bubbleHeight;
                endX = target.Left + target.Width / 2;
                endY = target.Top;
                break;
            default:
                startX = bubble.X + BubbleWidth / 2;
                startY = bubble.Y;
                endX = target.Left + target.Width / 2;
                endY = target.Bottom;
                break;
        }

        LeaderLine.X1 = startX;
        LeaderLine.Y1 = startY;
        LeaderLine.X2 = endX;
        LeaderLine.Y2 = endY;
        LeaderLine.Visibility = Visibility.Visible;
    }

    private async Task MoveCursorAsync(Rect target)
    {
        CursorDot.Visibility = Visibility.Visible;
        var targetX = target.Left + target.Width / 2 - CursorDot.Width / 2;
        var targetY = target.Top + target.Height / 2 - CursorDot.Height / 2;

        if (!_cursorReady)
        {
            _cursorX = Math.Max(ScreenMargin, ActualWidth - 80);
            _cursorY = Math.Max(ScreenMargin, ActualHeight - 80);
            _cursorReady = true;
            SetCursorImmediate(_cursorX, _cursorY);
        }

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
        var duration = TimeSpan.FromMilliseconds(520);

        CursorTransform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(_cursorX, targetX, duration) { EasingFunction = ease });
        CursorTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(_cursorY, targetY, duration) { EasingFunction = ease });

        await Task.Delay(duration + TimeSpan.FromMilliseconds(40));
        _cursorX = targetX;
        _cursorY = targetY;
        SetCursorImmediate(_cursorX, _cursorY);
    }

    private void SetCursorImmediate(Point point) => SetCursorImmediate(point.X, point.Y);

    private void SetCursorImmediate(Rect target) =>
        SetCursorImmediate(
            target.Left + target.Width / 2 - CursorDot.Width / 2,
            target.Top + target.Height / 2 - CursorDot.Height / 2);

    private void SetCursorImmediate(double x, double y)
    {
        CursorTransform.BeginAnimation(TranslateTransform.XProperty, null);
        CursorTransform.BeginAnimation(TranslateTransform.YProperty, null);
        CursorTransform.X = x;
        CursorTransform.Y = y;
        _cursorX = x;
        _cursorY = y;

        if (DemoBadge is { Visibility: Visibility.Visible } && _badgeText.Length > 0)
        {
            PositionDemoBadge();
        }

        if (DemoMouse is not null && DemoMouse.Visibility == Visibility.Visible)
        {
            DemoMouseTransform.X = x + CursorDot.Width + 10;
            DemoMouseTransform.Y = y + CursorDot.Height / 2 - DemoMouse.Height / 2;
        }

        if (DemoKeyCap is not null && DemoKeyCap.Visibility == Visibility.Visible)        {
            PositionDemoKeyCap();
        }
    }

    private async Task PulseAsync(
        Brush? stroke = null,
        double endScale = 1.8,
        int durationMs = 420)
    {
        var previous = ClickPulse.Stroke;
        if (stroke is not null)
        {
            ClickPulse.Stroke = stroke;
        }

        ClickPulse.Visibility = Visibility.Visible;
        ClickPulse.Opacity = 0.9;
        PulseScale.ScaleX = 0.45;
        PulseScale.ScaleY = 0.45;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(durationMs);

        ClickPulse.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0.9, 0, duration));
        PulseScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.45, endScale, duration) { EasingFunction = ease });
        PulseScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.45, endScale, duration) { EasingFunction = ease });

        await Task.Delay(duration + TimeSpan.FromMilliseconds(20));

        ClickPulse.BeginAnimation(OpacityProperty, null);
        PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ClickPulse.Opacity = 0;
        PulseScale.ScaleX = 0.45;
        PulseScale.ScaleY = 0.45;
        ClickPulse.Visibility = Visibility.Collapsed;
        ClickPulse.Stroke = previous;
    }
}
