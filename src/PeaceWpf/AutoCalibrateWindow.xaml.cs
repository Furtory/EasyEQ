using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Calibration;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;
using PeaceWpf.ViewModels;
using Wpf.Ui.Controls;

namespace PeaceWpf;

/// <summary>
/// 自动EQ 的执行窗口：选麦克风 → 扫频 → 看三条线 → 套到「左右」层。
///
/// 测量期间会把 peace.txt 临时写成平直（由 <see cref="MainViewModel.BeginCalibrationMeasurementAsync"/> 负责），
/// 否则扫频信号会被当前的 EQ 再染一遍，算出来的补偿就叠了两层。
/// 窗口用 ShowDialog 打开：测量的二十多秒里不该有人在主窗口上拖曲线。
/// </summary>
public partial class AutoCalibrateWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly CalibrationTargetKind _target;

    private CancellationTokenSource _cancellation = new();
    private CalibrationPlan? _plan;

    /// <summary>
    /// 当前这份结果是不是已经套到「左右」层了。
    ///
    /// 「继续测量」靠它决定要不要先套一次：没套过就先套，这一轮测到的才是「补过之后」的效果；
    /// 手动点过「套用」的话这里已经是 true，不必再套一遍。
    /// </summary>
    private bool _applied;

    /// <summary>这一轮是否用激进参数（两轮没变化、用户确认后切过来）。</summary>
    private bool _aggressive;

    /// <summary>点过「套用矫正EQ」没有。没点就关窗口，主窗口会当作取消。</summary>
    public bool Applied { get; private set; }

    /// <summary>窗口是不是被收起来了。</summary>
    public bool IsCollapsed { get; private set; }

    /// <summary>点主界面就收起：不移动、不变透明，直接藏起来。</summary>
    public void Collapse()
    {
        if (IsCollapsed)
        {
            return;
        }

        Hide();
        IsCollapsed = true;
    }

    /// <summary>点自动EQ 按钮就按原位置显示回来，并补一段和其他窗口一致的淡入。</summary>
    public void Expand()
    {
        if (!IsCollapsed)
        {
            Activate();
            return;
        }

        Show();
        IsCollapsed = false;
        Activate();
        PlayShowAnimation();
    }

    /// <summary>
    /// 隐藏再显示不会重放系统那套窗口打开动画，这里对内容补一段：
    /// 淡入 + 轻微上移，180 ms，和 FluentWindow 的观感接近。
    /// </summary>
    private void PlayShowAnimation()
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }

        var slide = new TranslateTransform(0, 12);
        root.RenderTransform = slide;
        root.Opacity = 0;

        var duration = new Duration(TimeSpan.FromMilliseconds(180));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        root.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration) { EasingFunction = ease });
        slide.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0, duration) { EasingFunction = ease });
    }

    // ---- 教程：让这个窗口自己演 ----

    /// <summary>
    /// 教程用：装一份写死的实测，窗口按平常那套流程算两层、画三条线。
    ///
    /// 曲线不是遮罩层另画的——走的是同一个 <see cref="EqCurveFitter"/>、同一份 <see cref="DrawPlot"/>，
    /// 只是没有真的扫频。演示态下「开始测量 / 套用」都锁着：这两条线不属于用户的设备。
    /// </summary>
    internal void LoadTutorialMeasurement(SweepMeasurement measurement)
    {
        if (_closed || _busy || measurement.Points.Count == 0)
        {
            return;
        }

        _tutorialDemo = true;
        _baselinePlan = null;
        _measurement = measurement;
        _crossoverHz = EqCurveFitter.CalibrationFloorHz;
        _compensationPercent = 0;
        _boostWired = false;
        BoostSlider.Value = 0;
        _boostWired = true;
        StartButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;
        RefitForCrossover(quick: false);
    }

    /// <summary>教程用：把黄线挪到某个频率并按它重算两层。quick 给拖动途中用。</summary>
    internal void SetTutorialCrossover(double hz, bool quick)
    {
        if (_closed || _busy || !_tutorialDemo)
        {
            return;
        }

        var clamped = Math.Round(Math.Clamp(hz, MinCrossoverHz, MaxCrossoverHz));
        if (Math.Abs(clamped - _crossoverHz) >= 0.5)
        {
            _crossoverHz = clamped;
            RefitForCrossover(quick);
        }

        DrawPlot();
    }

    /// <summary>教程用：把「贴合目标 ↔ 保证音量」滑杆挪到某个百分比并按它重算。quick 给拖动途中用。</summary>
    internal void SetTutorialBoost(double percent, bool quick)
    {
        if (_closed || _busy || !_tutorialDemo)
        {
            return;
        }

        var value = Math.Clamp(Math.Round(percent / 10) * 10, 0, 100);
        _compensationPercent = value;
        _boostWired = false;
        BoostSlider.Value = value;
        _boostWired = true;
        RefitForCrossover(quick);
    }

    /// <summary>教程用：黄线在当前画面里的屏幕坐标（设备像素），遮罩把光标放到那里。</summary>
    internal Point TutorialCrossoverScreenPoint() =>
        Plot.PointToScreen(new Point(PlotX(_crossoverHz), Plot.ActualHeight * 0.42));

    /// <summary>教程用：滑杆拇指的屏幕坐标（设备像素）。</summary>
    internal Point TutorialBoostScreenPoint()
    {
        var travel = Math.Max(1, BoostSlider.ActualWidth - 16);
        var x = 8 + travel * (BoostSlider.Value - BoostSlider.Minimum)
            / Math.Max(1, BoostSlider.Maximum - BoostSlider.Minimum);
        return BoostSlider.PointToScreen(new Point(x, BoostSlider.ActualHeight / 2));
    }

    /// <summary>
    /// 激进倍率：1.0 = 常规（gain 0.9 / 每段 6 dB），大于 1 就是这一轮更大步。
    /// 固定死一档会「激进过头」，第 3 轮起按前两轮的残差比自适应。
    /// </summary>
    private double _aggressiveScale = 1.0;

    /// <summary>每轮测完的残差，用来算下一轮的激进倍率。</summary>
    private readonly List<double> _roundResiduals = [];
    private bool _busy;
    private bool _closed;

    /// <summary>
    /// 这一轮扫频要不要实时预览 EQ 矫正曲线。
    ///
    /// 只有「手上还没有结果」的那一轮（一次测量的第 1 轮，也是唯一测裸设备的一轮）开：
    /// 那时候曲线边测边长，能看出个子丑寅卯。后面几轮是在上一轮曲线上做微调，
    /// 实时预览只会让一条本来已经定住的线跟着抖动，反而看不清——那几轮图上保留上一轮的
    /// 矫正曲线，本轮拟合结果在轮末存下来，下一轮开头整条换上。
    /// </summary>
    private bool _liveEqEnabled = true;

    /// <summary>测量按钮的三态：闲着 / 正在测 / 已请求停止（等本轮跑完）。</summary>
    private enum MeasureState
    {
        Idle,
        Running,
        Stopping,
    }

    private MeasureState _state = MeasureState.Idle;

    /// <summary>软停止请求。可以在本轮跑完之前被撤销。</summary>
    private bool _stopRequested;

    /// <summary>测量过程中已经测出来的点（LevelDb 已按中位数归一）。</summary>
    private MeasurementPoint[] _livePoints = Array.Empty<MeasurementPoint>();

    /// <summary>用已测点临时拟合出来的方案，测完会被正式结果覆盖。</summary>
    private CalibrationPlan? _livePlan;

    /// <summary>当前扫频频率：图上的绿色竖线跟着它走。null 表示没在扫。</summary>
    private double? _currentHz;

    /// <summary>那条绿线。扫频时只改它的坐标，不重画整张图。</summary>
    private Line? _sweepCursor;

    /// <summary>绿线顶上那个「当前频率」标签。</summary>
    private TextBlock? _sweepLabel;

    /// <summary>试听扫频的计时器：ChirpPreviewPlayer 不报播放位置，只能自己掐表。</summary>
    private System.Diagnostics.Stopwatch? _sweepWatch;
    private DispatcherTimer? _sweepTimer;

    /// <summary>
    /// 低频矫正曲线：20 Hz–矫正起点，套用时整层替换「低音」层。
    ///
    /// 它只在试听扫频时叠进 peace.txt；测量期间不叠——设备不一定发得出那个声音。
    /// null 表示这次的低频点不够、算不出来，套用只写主层。
    /// </summary>
    private EqPointList? _bassPlan;

    /// <summary>实时预览那份低频矫正，测完被正式结果替掉。</summary>
    private EqPointList? _liveBassPlan;

    /// <summary>试听结束后把 peace.txt 还原的那次等待；再点一次试听就取消它。</summary>
    private CancellationTokenSource? _sweepRestoreCts;

    /// <summary>这次测量用的推子疏密，实时预览也跟着用同一个。</summary>
    private FitDensity _density = FitDensity.Normal;

    /// <summary>
    /// 预增益补偿百分比（0–100）。前 50% 把被压掉的整体音量补回来，
    /// 后 50% 预增益固定 0、放开曲线提升，峰值超出 0 dBFS 的部分就是允许的削波。
    /// </summary>
    private double _compensationPercent;

    /// <summary>
    /// 这一轮样本不够：可用点不到总数的一半。按钮会变成「重测本轮」，
    /// 刻度条已经自动往「保证音量」挪了 3 dB。
    /// </summary>
    private bool _sampleShort;

    /// <summary>
    /// 教程演示态：窗口里那份「实测」是写死的，曲线照样走真实拟合器和真实画法，
    /// 但「开始测量 / 套用」锁住——这两条线不是用户的设备，套用会写错预设。
    /// </summary>
    private bool _tutorialDemo;

    /// <summary>滑杆防抖：拖动途中每跨 1 dB 都会触发一次，停手后再按最终值重算。</summary>
    private DispatcherTimer? _boostFitTimer;

    /// <summary>OnLoaded 之后才认滑杆的 ValueChanged：InitializeComponent 里设 Value 也会触发一次。</summary>
    private bool _boostWired;

    /// <summary>
    /// 这次测量时喇叭上已经套着的 EQ（用上一轮的结果近似）。没把当前 EQ 拉平时才非 null——
    /// 实测里含着它，拟合前必须先减掉，否则同一个凹陷会被补第二遍。
    /// </summary>
    private CalibrationPlan? _baselinePlan;

    /// <summary>上一次重画的时间。进度每 100 ms 报一次，没必要每次都重建图元。</summary>
    private long _lastLiveDrawAt;

    /// <summary>实时拟合正在后台跑：1 = 忙，跳过这一轮，避免任务越堆越多。</summary>
    private int _liveFitBusy;

    /// <summary>实时拟合的版本号：后台算完时只让最新那一份回到界面。</summary>
    private int _liveFitVersion;

    /// <summary>
    /// 一次点「测量」跑几轮。1 = 点一下只跑一轮（第一轮测裸喇叭，继续轮套用后再测一轮）。
    /// 想让它自己连续收敛，把这个数调大；循环里已经带了收敛判断。
    /// </summary>
    private const int AutoRoundCount = 1;

    /// <summary>
    /// 继续测量时，一轮最多在现有曲线上补多少 dB。
    ///
    /// 原来是 3 dB，两轮也追不平一个 10 dB 的峰；现在放到 6 dB，配合九成增益，
    /// 通常一到两轮就贴住。仍有限幅：一次测偏不会让整条曲线翻过去。
    /// </summary>
    private const double MaxStepDb = 6.0;

    /// <summary>
    /// 继续测量时的增益：补实测误差的九成。
    ///
    /// 原来只补六成，每一轮都留四成，看着就是「第二轮还是起伏很大」。
    /// 九成加每段 6 dB 限幅，误差大时也只走一步，剩下的留给下一次点击。
    /// </summary>
    private const double FeedbackGain = 0.9;

    /// <summary>
    /// 补完这一轮后，最大残差还超过这个值，就建议再扫一轮。
    ///
    /// 判断依据是「离目标还有多远」，不是「这一轮有没有让曲线动」——动是动了，
    /// 但凸起还剩 4–5 dB 的时候，再按常规步长走十轮也追不上。
    /// </summary>
    private const double StalledResidualDb = 2.0;

    /// <summary>
    /// 停滞时用的激进参数：误差全量走一步，每段最多挪 12 dB。
    /// 常规是九成 / 6 dB——保守是为了不振荡，但补不动时保守就等于不动。
    /// </summary>
    private const double AggressiveFeedbackGain = 1.0;
    private const double AggressiveMaxStepDb = 12.0;

    /// <summary>相邻两轮的 EQ 曲线差不到这个值（dB），就算收敛、不再加轮。</summary>
    private const double ConvergedDb = 1.0;

    /// <summary>本轮测量是不是跑到了「拟合完成」这一步。中断、失败都算没跑成。</summary>
    private bool _runSucceeded;

    /// <summary>试听用的五秒连续升频扫频。</summary>
    private readonly ChirpPreviewPlayer _sweepPreview = new();

    /// <summary>EQ 曲线动画：现在画到哪、目标在哪（dB，201 个对数采样点）。</summary>
    private readonly double[] _eqShown = new double[201];

    private readonly double[] _eqTarget = new double[201];

    /// <summary>这个采样点有没有值（实时预览只覆盖到已测频段，后面那些还没轮到）。</summary>
    private readonly bool[] _eqValid = new bool[201];

    /// <summary>每帧往目标靠多少。0.22 大约 0.15–0.2 秒追上，看着是滑过去而不是跳过去。</summary>
    private const double EqEase = 0.22;

    private DispatcherTimer? _eqTimer;
    private DispatcherTimer? _etaTimer;
    private int _totalRounds;
    private int _currentRound;
    private int _roundMs;

    // ---- 矫正起点：图上那条可拖的竖线 ----
    //
    // 分界线以上由「左右」层拟合，以下只由「低音」层反相补齐。
    // 默认 100 Hz，拖动后如果手上有测量结果就立刻按新起点重算两层。

    /// <summary>竖线能拖到的最低频率。再低低音层点太少，反相曲线会抖。</summary>
    private const double MinCrossoverHz = 40;

    /// <summary>竖线能拖到的最高频率。再高主层推子不够覆盖中低频。</summary>
    private const double MaxCrossoverHz = 400;

    private const double CrossoverHitPixels = 8;

    private double _crossoverHz = EqCurveFitter.CalibrationFloorHz;

    /// <summary>最近一次正式测量。拖动分界线后拿它重算，不用重测。</summary>
    private SweepMeasurement? _measurement;

    // ---- 麦克风矫正曲线 ----
    //
    // 测到的是「喇叭 + 麦克风」。导入一条麦克风曲线之后，拟合之前先把麦克风那段减掉，
    // 否则 EQ 会去纠麦克风的偏差，换个麦克风听就不对了。
    // 原始实测（_measurement）一直留着：换曲线、清曲线都从同一份数据重算，不用重扫。

    /// <summary>图上那条细线的颜色，和 XAML 图例里的一致。</summary>
    private static readonly Color MicCalColor = Color.FromRgb(0x6F, 0xD3, 0xC7);

    /// <summary>载入曲线后按钮用的强调色，和「目标」那条蓝线同一支。</summary>
    private static readonly Color MicCalAccent = Color.FromRgb(0x4C, 0xC2, 0xFF);

    /// <summary>导入的麦克风曲线；null 表示不矫正。</summary>
    private MicCalibrationCurve? _micCurve;

    /// <summary>曲线文件路径：按钮提示里显示，也是下次打开窗口自动载入的依据。</summary>
    private string? _micCurvePath;

    private bool _draggingCrossover;

    /// <summary>分界线重算的版本号：拖动时会连发多次，只让最后一次回到界面。</summary>
    private int _crossoverFitVersion;

    /// <summary>
    /// 拖动黄线时实时重算的最小间隔。完整 Q 搜索一次约 0.1 秒，比这更密只是排队，
    /// 排队的旧结果最后都会被版本号丢掉，白烧 CPU 还抢 UI 线程。
    /// </summary>
    private const int LiveRefitIntervalMs = 90;

    /// <summary>拖动重算是否已有一个在跑（0/1，跨线程用 Interlocked 读写）。</summary>
    private int _liveRefitInFlight;

    /// <summary>拖动中又挪了位置：手上这次算完，按最新起点再补一次。</summary>
    private bool _liveRefitPending;

    /// <summary>上一次真正开始拖动重算的时刻，给节流用。</summary>
    private long _lastLiveRefitAt;

    /// <summary>节流到点时触发下一次拖动重算。</summary>
    private DispatcherTimer? _liveRefitTimer;

    /// <summary>
    /// 目标曲线整体压低的量（dB）。第一次拟合算出来之后就钉住，拖动分界线、拖动补偿滑杆都沿用。
    ///
    /// <see cref="EqCurveFitter"/> 会自动按「分界线以上最难达成的一段」压低整条目标：
    /// 设备够不着目标时，压低比硬加增益诚实。但拖动分界线会改变「分界线以上」的范围，
    /// 这个压量跟着重算，整条目标就上下平移——用户看到的就是「拖动时曲线整体在跳」。
    /// 所以只在第一轮测量时算一次，之后的变化交给分界线本身去表达。
    /// </summary>
    private double? _targetOffsetDb;

    public AutoCalibrateWindow(MainViewModel viewModel, CalibrationTargetKind target)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _target = target;

        InitializeComponent();

        // 尺寸钉死之后没有可拉的边框，顶部留一条能按住拖窗口
        WindowDragHelper.Enable(this);

        _eqTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _eqTimer.Tick += OnEqTick;

        _etaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _etaTimer.Tick += (_, _) => UpdateRemainingText();

        Title = $"自动EQ · {TargetCurve.DisplayName(target)}";
        TargetText.Text = "校准到：" + TargetCurve.DisplayName(target);
        TargetHint.Text = TargetCurve.Description(target);

        // 上次导入的麦克风曲线：文件还在就悄悄套上，不在了当作没有。
        RestoreMicCalibration();

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;

        // 主界面拖完曲线：回来按补偿滑杆重算，再刷回那边。
        _viewModel.CalibrationEdited += OnMainEdited;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadDevices();
        LoadOptions();
        BoostSlider.Value = _compensationPercent;
        BoostSlider.ValueChanged += OnBoostChanged;
        _boostWired = true;

        _boostFitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _boostFitTimer.Tick += (_, _) =>
        {
            _boostFitTimer.Stop();
            if (!_busy && _measurement is not null && !_closed)
            {
                RefitForCrossover(quick: false);
            }
        };

        DrawPlot();
    }

    /// <summary>
    /// 一个下拉管三个东西：扫几遍、用哪些频点、用多少段推子。
    ///
    /// 「快速」：61 个频点、16 段推子、扫 1 遍，约 25 秒；
    /// 「标准」：121 点、23 段、扫 2 遍，约 1 分 40；
    /// 「更准」：240 点、28 段（100 Hz–4 kHz 每 1/4 倍频程）、扫 3 遍，约 4 分 40。
    ///
    /// 次数是写死的，不做稳定性自停；「继续测量」固定只扫 1 遍。打开窗口默认「快速」。
    /// </summary>
    private void LoadOptions()
    {
        SpeedBox.ItemsSource = new[] { "快速", "标准", "更准" };
        // 默认「快速」：先花二十多秒看个大概，不满意再用「继续测量」一轮轮往前挪。
        SpeedBox.SelectedIndex = 0;
    }

    /// <summary>
    /// 测量中关窗口：直接掐断这一轮扫频，然后放行关闭。
    ///
    /// 以前这里是 e.Cancel = true，等本轮自然结束才真的关：用户点了关闭却看到窗口还在、
    /// 按钮变成「等待结束」，等于关不掉；再点一次绕过等待把窗口关了，扫频却还在后台响。
    /// 现在关窗口就是硬取消——播放和录音随 token 一起停；测量态由 finally 还原。
    /// 想让这一轮走完，用按钮上的软停止，别用窗口的关闭。
    /// </summary>
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 用 _busy 而不是 _state：测量一起来它就先变 true，比按钮状态早，也不会漏。
        if (!_busy)
        {
            return;
        }

        // 不设 e.Cancel：这一下就要关掉。取消信号让 RunAsync 立刻收手，
        // 而不是等二十多秒的扫频走完。
        _cancellation.Cancel();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _etaTimer?.Stop();
        StopLiveRefit();
        _viewModel.CalibrationEdited -= OnMainEdited;

        // 只发取消，不 Dispose：测量任务可能还捏着这个 token，取消后它会在下一个
        // await 处抛出来。Dispose 交给下一次测量开头，或者随窗口一起回收。
        _cancellation.Cancel();
        _sweepPreview.Dispose();

        // 试听扫频可能还叠着低频：窗口关了也要把 peace.txt 还原成主层。
        _sweepRestoreCts?.Cancel();
        _sweepRestoreCts = null;
        _ = _viewModel.EndSweepPreviewAsync();
    }

    /// <summary>
    /// 主界面手拖完曲线：以拖出来的形状为准，只按补偿滑杆重算预增益，再写回主界面。
    ///
    /// 这里不再截断形状。自动EQ 的结果在拟合时已经按补偿档截过一轮，手调回来的点
    /// 如果在这里再截一次，每拖一次整条曲线就被多压一刀——用户看到的就是「拖一下、
    /// 松手后曲线自己往下掉一截」。截断的职责留在拟合那一步，手调之后形状只听手的。
    /// </summary>
    private void OnMainEdited()
    {
        if (_closed)
        {
            return;
        }

        var edited = _viewModel.MainPoints;
        var (_, preAmp) = EqCurveFitter.ApplyCompensation(edited, _compensationPercent);

        if (_plan is not null)
        {
            _plan = _plan.WithPoints(edited, preAmp);
        }

        _viewModel.ApplyEditedCalibration(edited, _viewModel.BassPoints, preAmp);
        DrawPlot();
    }

    private void LoadDevices()
    {
        var devices = MicrophoneCaptureService.ListDevices();
        DeviceBox.ItemsSource = devices;

        if (devices.Count == 0)
        {
            DeviceBox.IsEnabled = false;
            StartButton.IsEnabled = false;
            return;
        }

        DeviceBox.SelectedIndex = PickDefaultDevice(devices);
    }

    /// <summary>
    /// 挑默认设备：上次用的 → 系统默认录音设备 → 第一个。
    ///
    /// 不能直接用第一个。Steinberg 这类声卡会同时暴露 Voice / Streaming / Input 1/2 三个端点，
    /// 枚举顺序里第一个往往是 Voice，而那个端点没有输入信号（采出来全是 0），
    /// 电平永远是 -90，界面上看着就是「一直静音」。真正的输入在 Input 1/2 上。
    /// </summary>
    private static int PickDefaultDevice(IReadOnlyList<CaptureDevice> devices)
    {
        var saved = IndexOfDevice(devices, CalibrateDeviceMemory.Load());
        if (saved >= 0)
        {
            return saved;
        }

        var systemDefault = IndexOfDevice(devices, MicrophoneCaptureService.DefaultDeviceId());
        return systemDefault >= 0 ? systemDefault : 0;
    }

    private static int IndexOfDevice(IReadOnlyList<CaptureDevice> devices, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return -1;
        }

        for (var i = 0; i < devices.Count; i++)
        {
            if (string.Equals(devices[i].Id, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 一个按钮三种状态：开始测量 → 停止测量 → 等待结束，再点回停止测量。
    ///
    /// 「停止测量」不立刻掐断，只是请求「这一轮跑完就别再开下一轮」。
    /// 只要这一轮还没跑完，再点一次就能撤销——所以按钮一直是亮的。
    /// </summary>
    private async void OnMeasureClick(object sender, RoutedEventArgs e)
    {
        Trace($"click state={_state} planNull={_plan is null} applied={_applied}");

        switch (_state)
        {
            case MeasureState.Running:
                _stopRequested = true;
                // 停止 = 这一轮跑完就收手，剩余次数立刻压成 1 次。
                UpdateRemainingText();
                SetState(MeasureState.Stopping);
                return;

            case MeasureState.Stopping:
                _stopRequested = false;
                UpdateRemainingText();
                SetState(MeasureState.Running);
                return;
        }

        // 已经有结果时这个按钮是「继续测量」：不管什么档位，只再扫一遍。
        // 刚打开窗口第一次才是「开始测量」，按档位跑满轮数（更准 3 轮）。
        var totalRounds = _plan is not null
            ? 1
            : SpeedBox.SelectedIndex switch
            {
                2 => 3,
                1 => 2,
                _ => 1,
            };

        await MeasureLoopAsync(totalRounds);
    }

    /// <summary>
    /// 跑一至几轮测量。第一轮只测一次（测裸喇叭）；「继续测量」会连着跑几轮，
    /// 每一轮都先把上一轮套上去，再测「补过之后还剩多少」，直到两轮结果差不多。
    /// </summary>
    private async Task MeasureLoopAsync(int totalRounds)
    {
        _totalRounds = totalRounds;
        _roundMs = EstimateRoundMs();
        _etaTimer?.Start();

        try
        {
            for (var round = 1; round <= totalRounds; round++)
            {
                _currentRound = round;
                UpdateRemainingText();

                if (_closed)
                {
                    return;
                }

                var before = _plan;

                if (round > 1)
                {
                    await Task.Delay(400);
                }

                if (_closed)
                {
                    return;
                }

                _hasMoreRounds = round < totalRounds;

                await RunMeasurementAsync();

                if (!_runSucceeded || _closed || _stopRequested || _plan is null)
                {
                    return;
                }

                if (round >= totalRounds)
                {
                    return;
                }

                // 更准档的 3 轮是设计的一部分：每轮换一次曲线、再测一遍。
                // 不按残差提前收手——用户要的就是「3 轮跑满、一轮比一轮贴」。
                _ = MaxDiffDb(before ?? _plan, _plan);
            }
        }
        finally
        {
            _etaTimer?.Stop();
            var stopped = _stopRequested;
            _totalRounds = 0;
            _currentRound = 0;
            _stopRequested = false;
            if (!_closed)
            {
                // 停下后还能点「继续测量」再扫最后一次，所以这里显示 1 次而不是清空。
                RemainingText.Text = stopped && _plan is not null
                    ? $"还剩 1 次扫频 · 约 {FormatEta(TimeSpan.FromMilliseconds(_roundMs))}"
                    : string.Empty;
            }
        }
    }

    /// <summary>单轮大致要多久：按当前档位的频点数算，再补上开麦和分析的余量。</summary>
    private int EstimateRoundMs()
    {
        var count = SpeedBox.SelectedIndex switch
        {
            0 => SweepPlan.BuildFastFrequencies().Length,
            2 => SweepPlan.BuildPreciseFrequencies().Length,
            _ => SweepPlan.BuildStandardFrequencies().Length,
        };

        return SweepPlan.TotalMsFor(count) + 1500;
    }

    /// <summary>进度条上方那行：还剩几次扫频、大概还要多久。</summary>
    private void UpdateRemainingText()
    {
        if (_closed || _totalRounds <= 0 || _currentRound <= 0)
        {
            return;
        }

        var stopping = _stopRequested;
        var leftRounds = stopping ? 1 : Math.Max(0, _totalRounds - _currentRound + 1);
        if (leftRounds == 0)
        {
            RemainingText.Text = string.Empty;
            return;
        }

        // 本轮按进度条估，后面几轮按整轮估；已经请求停止就只剩这一轮。
        var progress = Math.Clamp(Progress.Value / 100.0, 0, 1);
        var leftMs = _roundMs * (1 - progress)
            + (stopping ? 0 : _roundMs * (_totalRounds - _currentRound));
        RemainingText.Text = $"还剩 {leftRounds} 次扫频 · 约 {FormatEta(TimeSpan.FromMilliseconds(Math.Max(0, leftMs)))}";
    }

    private static string FormatEta(TimeSpan eta) =>
        eta.TotalSeconds < 60
            ? $"{Math.Max(1, (int)Math.Ceiling(eta.TotalSeconds))} 秒"
            : $"{(int)eta.TotalMinutes} 分 {eta.Seconds} 秒";

    /// <summary>两套方案的实际响应差多少（1/12 倍频程上取最大值）。</summary>
    private static double MaxDiffDb(CalibrationPlan before, CalibrationPlan after)    {
        var left = before.Points.ToSnapshot();
        var right = after.Points.ToSnapshot();
        var worst = 0.0;

        for (var hz = 20.0; hz <= 20000; hz *= Math.Pow(2, 1.0 / 12))
        {
            worst = Math.Max(worst, Math.Abs(EqResponse.GainAt(left, hz) - EqResponse.GainAt(right, hz)));
        }

        return worst;
    }

    /// <summary>推子的增益范围，临时打点用。</summary>
    private static string GainRange(EqPointList points)
    {
        var bands = points.Points.Where(point => !point.IsEndpoint).ToList();
        return bands.Count == 0
            ? "none"
            : $"{bands.Min(point => point.GainDb):0.0}..{bands.Max(point => point.GainDb):0.0}";
    }

    /// <summary>
    /// 实时预览的 0 dB 参考：和正式测量一样取 1 kHz 附近的中位数。
    ///
    /// 以前拿「已测点的中位数」，扫到低频段时那全是 20–100 Hz 的电平，参考点就偏了二十多 dB：
    /// 预览曲线整体平移，拟合跟着带一个常数偏移，看起来像「重新优化了一遍」。
    /// 还没扫到 1 kHz 时才退回全段中位数。
    /// </summary>
    private static double ReferenceLevel(MeasurementPoint[] points)
    {
        var near = points
            .Where(point => point.FrequencyHz is >= 500 and <= 2000)
            .Select(point => point.RawDb)
            .OrderBy(value => value)
            .ToArray();

        if (near.Length > 0)
        {
            return near[near.Length / 2];
        }

        var all = points.Select(point => point.RawDb).OrderBy(value => value).ToArray();
        return all[all.Length / 2];
    }

    /// <summary>
    /// 继续测量这一轮的拟合：在现有曲线上按实测误差小步微调，不再从头重算一遍。
    ///
    /// 实测里含着已经套着的那套，所以「离目标还差多少」是直接看得见的。
    /// 新曲线 = 现在这条 + 九成的误差，每段最多挪 <see cref="MaxStepDb"/> dB：
    /// 误差大时也只走一步，剩下的留给下一次点击——既不会一动不动，也不会因为一次测偏就整条翻过去。
    /// （更早的版本没限幅，第二轮把补偿叠了两遍；再后来只补六成，看着就是「第二轮还是没贴住」。）
    ///
    /// 100 Hz 以下不再追目标，而是每轮最多往回撤 <see cref="MaxStepDb"/> dB，直到归零。
    /// 这样上一版算法留在低频的 +9 dB 也会在一两轮里退掉，预增益跟着回正。
    /// </summary>
    /// <summary>
    /// 离目标还差太远时，建议直接换激进参数再扫一轮。
    ///
    /// 为什么不是自动切：多扫一轮是几十秒到几分钟，用户该有权说不要。
    /// 但也不能只提示不动作——点「是」就直接把这一轮跑起来。
    /// </summary>
    /// <summary>这一轮后面还有没有下一轮。有的话残差大就直接换激进曲线，不弹窗。</summary>
    private bool _hasMoreRounds;

    /// <summary>
    /// 把当前这一轮的曲线换成激进版，给下一轮套。
    /// </summary>
    private void TryRefitAggressive(SweepMeasurement measurement, CalibrationPlan? appliedPlan)
    {
        if (appliedPlan is null)
        {
            return;
        }

        try
        {
            _plan = FitFromFeedback(appliedPlan, measurement, _density);
            DrawPlot();
            Trace($"aggressive refit corr={_plan.CorrectionRange.Min:0.0}..{_plan.CorrectionRange.Max:0.0}");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Trace($"aggressive refit failed: {ex.Message}");
        }
    }

    private void OfferAggressiveRound(
        double residual,
        SweepMeasurement measurement,
        CalibrationPlan? appliedPlan)
    {
        var again = AppDialog.Confirm(
            this,
            "建议再扫一次",
            $"这一轮补完，最大残差还有 {residual:0.0} dB（目标 0 dB）——凸起离目标还很远。\n\n"
            + "再按常规步长走，可能几轮都追不上。点下面之后，会先把这一轮的曲线按更激进的参数重算"
            + $"（误差全量走一步、每段最多挪 {AggressiveMaxStepDb:0} dB），再拿这条更激进的曲线去测下一轮。",
            "再扫一轮",
            "先不动");

        if (!again)
        {
            return;
        }

        _aggressive = true;
        _aggressiveScale = Math.Max(_aggressiveScale, 1.5);

        // 关键：这条曲线是给下一轮套的。现在不重算，下一轮套上去的还是常规那条，
        // 测完再切激进参数就晚了一整轮——用户看到的就是「第三轮还是那么鼓」。
        TryRefitAggressive(measurement, appliedPlan);

        // 这一轮还在 finally 里没复位 _busy，直接调会被开头的守卫挡掉；排到 UI 队列末尾再跑。
        Dispatcher.BeginInvoke(new Action(() => _ = RunMeasurementAsync()));
    }

    private CalibrationPlan FitFromFeedback(
        CalibrationPlan appliedPlan,
        SweepMeasurement measurement,
        FitDensity density) =>
        // 实现在 Core 里，自测能直接验证「两轮之后还剩多少起伏」。
        // 停滞过一次之后换成激进参数：误差全量走、每段允许 12 dB，否则补不动的还是补不动。
        EqCurveFitter.FitFeedback(
            _target, appliedPlan, measurement, density,
            FeedbackGain * _aggressiveScale,
            MaxStepDb * _aggressiveScale,
            _crossoverHz,
            quick: false,
            aggressive: _aggressive,
            compensationPercent: _compensationPercent);

    /// <summary>
    /// 生成低频矫正曲线。低频点不够时返回 null——主层结果照常可用，只是不写低音层。
    ///
    /// 它不走反馈轮次：测量时低音层不叠加，测回来永远是主设备的裸低频，
    /// 反相（或者补到目标）只需按当前实测算一次，没有「再测一轮验证」这回事。
    /// </summary>
    private EqPointList? TryFitBass(SweepMeasurement measurement, bool quick = false) =>
        FitBassSafe(measurement, _target, _baselinePlan?.Points, _crossoverHz, quick, _compensationPercent);

    /// <summary>
    /// 低频拟合的线程安全版本：不读窗口字段，后台线程也能直接调。
    ///
    /// <paramref name="compensationPercent"/> 是左下补偿滑杆：低频测不到时，
    /// 兜底的低频曲线按它决定提升多少（见 <see cref="EqCurveFitter.BassFallbackMinPoints"/>）。
    /// </summary>
    private static EqPointList? FitBassSafe(
        SweepMeasurement measurement,
        CalibrationTargetKind target,
        EqPointList? baseline,
        double ceilingHz,
        bool quick,
        double compensationPercent)
    {
        // 还没扫到分界线就先不画：低频段只测了半截时拟合出来会是个假台阶，
        // 等这一段扫完再显示，图上才和耳朵听到的一致。
        if (measurement.Frequencies.Count == 0
            || measurement.Frequencies[^1] < ceilingHz)
        {
            return null;
        }

        try
        {
            return EqCurveFitter.FitBass(
                target, measurement, baseline, ceilingHz: ceilingHz, quick: quick,
                compensationPercent: compensationPercent);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    // ---- 麦克风矫正曲线：导入、清除、套到实测上 ----

    /// <summary>
    /// 把导入的麦克风曲线套到实测上。没有曲线时原样返回。
    ///
    /// 教程那份实测是写死的样本，不属于用户的设备，不参与矫正——不然演示出来的
    /// 曲线会被人自己的麦克风文件改掉。
    /// </summary>
    private SweepMeasurement Corrected(SweepMeasurement measurement) =>
        _micCurve is null || _tutorialDemo ? measurement : _micCurve.ApplyTo(measurement);

    /// <summary>
    /// 窗口打开时按上次记住的路径悄悄载入。文件被删了、格式坏了都当作「没有曲线」，
    /// 不弹窗打断——上次的选择不该变成这次开窗口的拦路虎。
    /// </summary>
    private void RestoreMicCalibration()
    {
        var path = _viewModel.MicCalibrationFile;
        if (!string.IsNullOrWhiteSpace(path) && !TryLoadMicCalibration(path, out var error))
        {
            Trace($"mic cal restore failed: {error}");
        }

        UpdateMicCalibrationUi();
    }

    /// <summary>读一条曲线进 <see cref="_micCurve"/>。失败时清空，并把原因交给调用方。</summary>
    private bool TryLoadMicCalibration(string path, out string? error)
    {
        try
        {
            _micCurve = MicCalibrationCurve.Load(path);
            _micCurvePath = path;
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            _micCurve = null;
            _micCurvePath = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>按钮和提示跟着状态走：载入了变蓝、提示里写文件名和范围，没载入写怎么用。</summary>
    private void UpdateMicCalibrationUi()
    {
        if (_micCurve is null)
        {
            MicCalibrationButton.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            MicCalibrationButton.ToolTip =
                "把你的测量麦克风的频响曲线导进来，实测会先扣掉麦克风自己的偏差，拟合出来的 EQ 才是喇叭的。\n"
                + "两列文本就行：频率（Hz，可写 1k）、偏差（dB）。导入后按钮变蓝，右键清除。";
            return;
        }

        MicCalibrationButton.Foreground = new SolidColorBrush(MicCalAccent);
        MicCalibrationButton.ToolTip =
            $"正在用：{_micCurve.Name}\n"
            + $"{_micCurve.PointCount} 个点 · {_micCurve.MinFrequencyHz:0}–{_micCurve.MaxFrequencyHz:0} Hz · "
            + $"{_micCurve.MinGainDb:+0.0;-0.0;0.0} ~ {_micCurve.MaxGainDb:+0.0;-0.0;0.0} dB\n"
            + "实测会先扣掉这条偏差（1 kHz 锚点不动）。右键清除。";
    }

    /// <summary>
    /// 曲线变了：手上如果已经有实测，就拿同一份原始数据重算，不重扫。
    /// 没有实测（还没开始测）就只重画，图上那条细线先显示出来。
    /// </summary>
    private void ApplyMicCalibrationToResult()
    {
        // 重算出来的结果还没套到「左右」层：按钮要重新变成可套用。
        _applied = false;

        if (_measurement is null || _busy)
        {
            DrawPlot();
            return;
        }

        RefitForCrossover(quick: false);
    }

    private void OnImportMicCalibration(object sender, RoutedEventArgs e)
    {
        // 测量途中不改：这一轮的点已经按旧曲线算下去了，中途换尺子只会让结果前后不一致。
        if (_busy)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选麦克风矫正曲线",
            Filter = "曲线文本 (*.txt;*.csv;*.cal)|*.txt;*.csv;*.cal|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };

        // 上次那个文件所在的目录就是下次的起点，省得每次都从「文档」翻回来。
        if (!string.IsNullOrWhiteSpace(_micCurvePath))
        {
            var directory = System.IO.Path.GetDirectoryName(_micCurvePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!TryLoadMicCalibration(dialog.FileName, out var error))
        {
            UpdateMicCalibrationUi();
            AppDialog.Info(this, "这条曲线读不了", error ?? "文件读不出来，也可能格式不对。");
            return;
        }

        _viewModel.SetMicCalibrationFile(_micCurvePath);
        UpdateMicCalibrationUi();
        Trace($"mic cal loaded {_micCurvePath} points={_micCurve!.PointCount}");
        ApplyMicCalibrationToResult();
    }

    private void OnClearMicCalibration(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_micCurve is null || _busy)
        {
            return;
        }

        _micCurve = null;
        _micCurvePath = null;
        _viewModel.SetMicCalibrationFile(null);
        UpdateMicCalibrationUi();
        Trace("mic cal cleared");
        ApplyMicCalibrationToResult();
    }

    /// <summary>低频层里真正会写进后端的段数（不算端点、不算 0 dB）。</summary>
    private static int BassBandCount(EqPointList points) =>
        points.Points.Count(point => !point.IsEndpoint && Math.Abs(point.GainDb) >= 0.005);

    /// <summary>
    /// 分界线变了：用最近一次实测按新起点重算主层和低音层，不重测。
    /// 重算出来的还没套到主窗口，所以要把「已套用」清掉、重新允许套用。
    ///
    /// <paramref name="live"/> = true 是拖动途中的实时跟随：同一时刻只跑一个（见
    /// <see cref="PumpLiveRefit"/>），结果照常画，但不写预设文件、不进撤回栈；
    /// 松手时再用 <paramref name="live"/> = false 落一次盘。
    ///
    /// 拖动途中始终用完整搜索（quick = false），和松手那一次算法一致——
    /// 以前拖动跑的是 quick（不摆位、只解一套 Q、不逐段收尾），和松手的完整结果交替出现，
    /// 曲线就在「粗糙」和「圆滑」之间来回闪。
    /// </summary>
    /// <returns>true 表示确实起了一次后台拟合。</returns>
    private bool RefitForCrossover(bool quick, bool live = false)
    {
        if (_measurement is null || _busy)
        {
            return false;
        }

        var version = Interlocked.Increment(ref _crossoverFitVersion);
        var measurement = Corrected(_measurement);
        var target = _target;
        var density = _density;
        var baseline = _baselinePlan?.Points;
        var crossover = _crossoverHz;
        var maxBoost = _compensationPercent;
        // 目标整体压量：首轮算出来后钉住，别再按新起点重算（否则整条曲线会上下平移）。
        var targetOffset = _targetOffsetDb;

        // 教程演示按「只削不增」跑：提升上限收紧到 0，凹陷处留空、曲线不会翻到 0 dB 以上。
        // 真实测量仍用程序默认的 9 dB 上限。
        var boostLimit = _tutorialDemo ? 0 : EqCurveFitter.MaxBoostDb;

        _ = Task.Run(() =>
        {
            CalibrationPlan? plan;
            EqPointList? bass;
            try
            {
                plan = EqCurveFitter.Fit(
                    target, measurement, targetOffset, density, baseline,
                    movableBands: !quick, floorHz: crossover, quick: quick,
                    maxBoostDb: boostLimit, compensationPercent: maxBoost);
                bass = FitBassSafe(measurement, target, baseline, crossover, quick, maxBoost);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                Trace($"crossover refit failed at {crossover:0} Hz: {ex.Message}");
                if (live)
                {
                    Dispatcher.InvokeAsync(() => EndLiveRefit());
                }

                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (!_closed && version == Volatile.Read(ref _crossoverFitVersion))
                {
                    _plan = plan;
                    // 首轮把目标压量钉住；之后重算（拖分界线、拖滑杆）都沿用它，整条曲线不再上下跳。
                    _targetOffsetDb = plan.TargetOffsetDb;
                    _bassPlan = bass;
                    // 拖动重算的结果也不能被还在飞的实时预览盖住。
                    Interlocked.Increment(ref _liveFitVersion);
                    _livePlan = null;
                    _liveBassPlan = null;
                    _applied = false;
                    ApplyButton.IsEnabled = plan.FilterCount > 0 && !_tutorialDemo;
                    DrawPlot();

                    // 拖分界线、拖补偿滑杆、样本不足重算都走这里：主界面的曲线和预增益跟着动。
                    // 拖动中的中间结果不落盘、不进撤回栈，省掉每次 0.1 秒都写三份文件的抖动。
                    _viewModel.UpdateCalibrationPreview(plan, bass, commit: !live);
                }

                // 只给实时任务收尾：非实时重算不该碰这把单飞锁（可能正有一次实时在跑）。
                // 不管这次结果用没用到（可能已经被更新的请求顶掉），都要放开锁，
                // 并按黄线的最新位置排下一次。
                if (live)
                {
                    EndLiveRefit();
                }
            });
        });

        return true;
    }

    /// <summary>黄线或补偿滑杆正在被拖：这段时间走实时重算。</summary>
    private bool IsLiveDragging => _draggingCrossover || _boostDragging;

    /// <summary>
    /// 拖动途中排一次重算。黄线先动，曲线随后跟上。
    /// </summary>
    private void QueueLiveRefit()
    {
        _liveRefitPending = true;
        PumpLiveRefit();
    }

    /// <summary>松手 / 关窗口：不再排新的拖动重算。</summary>
    private void StopLiveRefit()
    {
        _liveRefitPending = false;
        _liveRefitTimer?.Stop();
    }

    /// <summary>
    /// 拖动重算的调度器。只在 UI 线程调用。
    ///
    /// 同一时刻只跑一个完整搜索（约 0.1 秒）：叠着跑会占满线程池，鼠标反而更涩。
    /// 跑完时如果黄线又挪了（<see cref="_liveRefitPending"/>），立刻按最新起点补一次；
    /// 间隔不到 <see cref="LiveRefitIntervalMs"/> 就交给计时器等一小会儿。
    /// </summary>
    private void PumpLiveRefit()
    {
        if (!_liveRefitPending)
        {
            return;
        }

        if (!IsLiveDragging || _closed || _busy || _measurement is null)
        {
            _liveRefitPending = false;
            _liveRefitTimer?.Stop();
            return;
        }

        if (Volatile.Read(ref _liveRefitInFlight) != 0)
        {
            return;
        }

        var now = Environment.TickCount64;
        var wait = LiveRefitIntervalMs - (now - _lastLiveRefitAt);
        if (wait > 0)
        {
            _liveRefitTimer ??= new DispatcherTimer();
            _liveRefitTimer.Stop();
            _liveRefitTimer.Interval = TimeSpan.FromMilliseconds(wait);
            _liveRefitTimer.Tick -= OnLiveRefitTick;
            _liveRefitTimer.Tick += OnLiveRefitTick;
            _liveRefitTimer.Start();
            return;
        }

        _liveRefitTimer?.Stop();
        _liveRefitPending = false;
        _lastLiveRefitAt = now;
        Interlocked.Exchange(ref _liveRefitInFlight, 1);
        if (!RefitForCrossover(quick: false, live: true))
        {
            Interlocked.Exchange(ref _liveRefitInFlight, 0);
        }
    }

    private void OnLiveRefitTick(object? sender, EventArgs e)
    {
        _liveRefitTimer?.Stop();
        PumpLiveRefit();
    }

    /// <summary>一次拖动重算收尾（UI 线程）：放开单飞锁，若还有待办的再排一次。</summary>
    private void EndLiveRefit()
    {
        Interlocked.Exchange(ref _liveRefitInFlight, 0);
        PumpLiveRefit();
    }

    /// <summary>频率 → 画布横坐标，和 <see cref="DrawPlot"/> 里的对数轴一致。</summary>
    private double PlotX(double hz)
    {
        var plotWidth = Plot.ActualWidth - AxisLeft - AxisRight;
        return AxisLeft + Math.Log10(Math.Clamp(hz, 20, 20000) / 20) / Math.Log10(1000.0) * plotWidth;
    }

    /// <summary>画布横坐标 → 频率。</summary>
    /// <summary>dB → 画布纵坐标，和 DrawPlot 里的纵轴一致。</summary>
    private double PlotY(double db)
    {
        var plotHeight = Plot.ActualHeight - AxisTop - AxisBottom;
        return AxisTop + (_maxDb - Math.Clamp(db, _minDb, _maxDb)) / (_maxDb - _minDb) * plotHeight;
    }

    private double FrequencyAt(double x)
    {
        var plotWidth = Plot.ActualWidth - AxisLeft - AxisRight;
        if (plotWidth <= 0)
        {
            return _crossoverHz;
        }

        var t = Math.Clamp((x - AxisLeft) / plotWidth, 0, 1);
        return 20 * Math.Pow(1000, t);
    }

    private void OnPlotMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (Math.Abs(e.GetPosition(Plot).X - PlotX(_crossoverHz)) > CrossoverHitPixels)
        {
            return;
        }

        _draggingCrossover = true;
        Plot.CaptureMouse();
        Plot.Cursor = Cursors.SizeWE;
        e.Handled = true;
    }

    private void OnPlotMouseMove(object sender, MouseEventArgs e)
    {
        var x = e.GetPosition(Plot).X;
        if (!_draggingCrossover)
        {
            Plot.Cursor = Math.Abs(x - PlotX(_crossoverHz)) <= CrossoverHitPixels
                ? Cursors.SizeWE
                : Cursors.Arrow;
            return;
        }

        var hz = Math.Round(Math.Clamp(FrequencyAt(x), MinCrossoverHz, MaxCrossoverHz));
        if (Math.Abs(hz - _crossoverHz) < 0.5)
        {
            return;
        }

        _crossoverHz = hz;

        // 拖动途中实时重算：黄线先动，曲线随后跟上。
        //
        // 用的是同一个完整搜索（不是以前的 quick 轻量版），所以中途和松手后的形状一致，
        // 不会「一会儿平滑一会儿锯齿」。同一时刻只跑一个（PumpLiveRefit），
        // 算完若黄线又挪了，就按最新位置再补一次；中间结果不写盘、不进撤回栈。
        QueueLiveRefit();
        DrawPlot();
        e.Handled = true;
    }

    private void OnPlotMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_draggingCrossover)
        {
            return;
        }

        _draggingCrossover = false;
        StopLiveRefit();
        Plot.ReleaseMouseCapture();
        Plot.Cursor = Cursors.Arrow;
        // 松手：用同一个起点跑最后一次，这一次才落盘（写预设、进撤回栈）。
        RefitForCrossover(quick: false);
        DrawPlot();
        e.Handled = true;
    }

    private void OnPlotMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_draggingCrossover)
        {
            Plot.Cursor = Cursors.Arrow;
        }
    }

    /// <summary>上一次「生效」的档位。取消换档时用它把下拉退回去。</summary>
    private int _speedIndex;

    /// <summary>正在把下拉退回去，别让 SelectionChanged 再走一遍。</summary>
    private bool _revertingSpeed;

    /// <summary>
    /// 换档：手上有结果就先问一句，确认才清空。
    ///
    /// 档位同时决定「扫哪些频点」和「推子有多密」。留着旧结果再切档，「继续测量」会拿旧方案
    /// 去套、用新密度重新拟合，图上和摘要变成两套口径，还容易让人以为这轮是在验证上一轮。
    /// 所以换档就是重新开始——但结果可能是十几分钟测出来的，不能一点就没了，先问。
    ///
    /// 取消时把下拉退回原档；测量过程中下拉是禁用的，不会走到这里。
    /// </summary>
    /// <summary>
    /// 刻度条变了：更新提升上限。手上有实测就按新上限把当前曲线重算一遍，
    /// 不然刻度条要等下一轮测量才生效。
    /// </summary>
    private void OnBoostChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_boostWired)
        {
            return;
        }

        _compensationPercent = Math.Clamp(Math.Round(e.NewValue), 0, 100);
        if (_busy || _measurement is null)
        {
            return;
        }

        // 拖着滑杆：和拖黄线共用同一个实时调度器（同一时刻只跑一个完整搜索）。
        // 点一下 / 键盘改值没有拖动过程，仍然防抖到停手后按最终值重算。
        if (_boostDragging)
        {
            _boostFitTimer?.Stop();
            QueueLiveRefit();
            return;
        }

        _boostFitTimer?.Stop();
        _boostFitTimer?.Start();
    }

    /// <summary>滑杆正在拖：数值气泡跟着鼠标走。</summary>
    private bool _boostDragging;

    private void OnBoostDragStart(object sender, MouseButtonEventArgs e)
    {
        _boostDragging = true;
        // 拖动前按下那一下可能已经排了防抖重算：实时路径接管，别让它插一脚。
        _boostFitTimer?.Stop();
        ShowBoostTip(e.GetPosition(BoostSlider));
    }

    private void OnBoostDragMove(object sender, MouseEventArgs e)
    {
        if (_boostDragging)
        {
            ShowBoostTip(e.GetPosition(BoostSlider));
        }
    }

    private void OnBoostDragUp(object sender, MouseButtonEventArgs e) => EndBoostDrag();

    private void OnBoostDragLeave(object sender, MouseEventArgs e)
    {
        // 拖动时鼠标可能短暂滑出杆体（左键还按着），这时不收起；
        // 松开或真正离开才收起。
        if (e.LeftButton == MouseButtonState.Released)
        {
            EndBoostDrag();
        }
    }

    /// <summary>
    /// 滑杆松手：停掉实时重算，按最终值完整算一次——这一次才写预设、进撤回栈。
    /// 拖动中的中间结果只更新界面，和拖黄线那边的收尾一致。
    /// </summary>
    private void EndBoostDrag()
    {
        if (!_boostDragging)
        {
            HideBoostTip();
            return;
        }

        HideBoostTip();
        StopLiveRefit();
        RefitForCrossover(quick: false);
    }

    /// <summary>气泡放在鼠标上方：坐标相对滑杆，DPI 缩放不会偏。</summary>
    private void ShowBoostTip(Point position)
    {
        BoostTipText.Text = $"{Math.Round(BoostSlider.Value):0}%";
        BoostTip.HorizontalOffset = position.X - 18;
        BoostTip.VerticalOffset = position.Y - 38;
        BoostTip.IsOpen = true;
    }

    private void HideBoostTip()
    {
        _boostDragging = false;
        BoostTip.IsOpen = false;
    }

    private void OnSpeedChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_revertingSpeed || _busy)
        {
            return;
        }

        var selected = Math.Clamp(SpeedBox.SelectedIndex, 0, 2);

        // 手上没结果：换档只是换参数，直接记下就行。
        if (_plan is null && _livePlan is null && _livePoints.Length == 0)
        {
            _speedIndex = selected;
            _bassPlan = null;
            _liveBassPlan = null;
            _measurement = null;
            _targetOffsetDb = null;
            _sampleShort = false;
            return;
        }

        // 用项目自己的对话框，不用系统 MessageBox：深色主题里那个是白底灰框，跟窗口对不上。
        // 「先不换」是默认按钮，回车 / Esc 都不会误清。
        var confirmed = AppDialog.Confirm(
            this,
            "换档会清空结果",
            "换档会清空这一轮的测量结果和曲线。\n"
            + "已经套用的 EQ（左右和低音两层）不会自动还原——那是主窗口里未保存的改动，可以在那里撤回。\n\n"
            + "要换档并清空吗？",
            "换档并清空",
            "先不换");

        if (!confirmed)
        {
            _revertingSpeed = true;
            SpeedBox.SelectedIndex = _speedIndex;
            _revertingSpeed = false;
            return;
        }

        _speedIndex = selected;
        _plan = null;
        _bassPlan = null;
        _livePlan = null;
        _liveBassPlan = null;
        _measurement = null;
        _targetOffsetDb = null;
        _livePoints = Array.Empty<MeasurementPoint>();
        _applied = false;
        _sampleShort = false;
        Array.Clear(_eqValid);
        ApplyButton.IsEnabled = false;
        SetState(MeasureState.Idle);
        DrawPlot();
    }

    private void SetState(MeasureState state)
    {
        _state = state;

        // 测量中不许换档：换了也只会影响下一轮，徒增困惑。
        SpeedBox.IsEnabled = state == MeasureState.Idle;

        // 提升上限同理：本轮拟合用的是开始测量时捕获的值，测量中拖动只会改下一轮，
        // 看着就是「拖了这轮没反应」，所以测量期间直接禁用。
        BoostSlider.IsEnabled = state == MeasureState.Idle;

        // 麦克风曲线也只在空闲时能换：测量中途换「尺子」，这一轮先后的点就对不上了。
        MicCalibrationButton.IsEnabled = state == MeasureState.Idle;
        StartButton.Content = state switch
        {
            MeasureState.Running => "停止测量",
            MeasureState.Stopping => "等待结束",

            // 手里有结果就是「继续」：再点一次会先把这套套上去，然后立刻再测一轮。
            // 这一轮样本不够时改成「重测本轮」，刻度条已经自动往保证音量挪过 3 dB。
            _ => _plan is null
                ? "开始测量"
                : _sampleShort ? "重测本轮" : "继续测量",
        };

        if (PreviewButton is not null)
        {
            PreviewButton.IsEnabled = state == MeasureState.Idle;
        }
    }

    /// <summary>
    /// 试听：放五秒连续升频的扫频。
    ///
    /// 和测量不同，试听**会**把低音层叠进去：测量时叠低频，设备发不出那个声音就会算错；
    /// 试听是给人耳听的，叠上才能判断低频到底补过头没有。
    ///
    /// 手上有结果时试听的是「这份结果」：主层用刚算出来的，低音层用刚生成的低频矫正，
    /// 所以套用之前就能听见套用之后的样子。没有结果时退回主窗口当前的两层。
    /// </summary>
    private async void OnPreviewSweep(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        // 连点试听：先取消上一次「放完还原」的等待，并把它已经叠上去的低频还原掉，
        // 再重新备份、重新叠。否则第二次会拿「叠过低频的文件」当原样，还原不回去。
        var hadPendingRestore = _sweepRestoreCts is not null;
        _sweepRestoreCts?.Cancel();
        _sweepRestoreCts = null;
        if (hadPendingRestore)
        {
            await _viewModel.EndSweepPreviewAsync();
        }

        var layered = _plan is not null || _bassPlan is not null;
        var main = _plan?.Points ?? _viewModel.MainPoints;
        var bass = _bassPlan ?? _viewModel.BassPoints;

        if (layered)
        {
            // 预增益只用左右层那份：低频提升不参与，和套用时同一条规则。
            var preAmp = _plan?.PreAmpDb ?? _viewModel.PreAmpDb;
            var (ok, _) = await _viewModel.BeginSweepPreviewAsync(main, bass, preAmp);
            if (!ok)
            {
                return;
            }
        }

        if (!_sweepPreview.Play())
        {
            if (layered)
            {
                await _viewModel.EndSweepPreviewAsync();
            }

            return;
        }

        StartSweepCursor();

        if (layered)
        {
            var cts = new CancellationTokenSource();
            _sweepRestoreCts = cts;
            _ = RestoreSweepAfterAsync(cts);
        }
    }

    /// <summary>
    /// 试听扫频的绿线：ChirpPreviewPlayer 只说明「五秒、20→20k 指数扫频」，不报播放位置，
    /// 所以自己掐表，按 f(t) = f0·(f1/f0)^(t/T) 反推当前频率。
    /// </summary>
    private void StartSweepCursor()
    {
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

        var t = Math.Clamp(_sweepWatch.Elapsed.TotalSeconds / ChirpPreviewPlayer.Seconds, 0, 1);
        _currentHz = ChirpPreviewPlayer.StartHz
            * Math.Pow(ChirpPreviewPlayer.EndHz / ChirpPreviewPlayer.StartHz, t);
        DrawSweepCursor();

        if (t >= 1)
        {
            StopSweepCursor();
        }
    }

    private void StopSweepCursor()
    {
        _sweepTimer?.Stop();
        _sweepWatch = null;
        _currentHz = null;
        DrawSweepCursor();
    }

    /// <summary>扫频放完（再多留 0.6 秒余量）就把 peace.txt 还原成主层，别让低频一直叠着。</summary>
    private async Task RestoreSweepAfterAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(ChirpPreviewPlayer.Seconds + 0.6), cts.Token);
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

        if (_closed)
        {
            return;
        }

        await _viewModel.EndSweepPreviewAsync();
    }

    /// <summary>
    /// 测量途中的实时曲线：每收到一批已测点就更新，顺手用这些点临时拟合一条 EQ 预览。
    ///
    /// 归一化拿「已测点的中位数」当 0 dB——绝对电平没有意义，看的是形状。
    /// 每 500 ms 才重画一次，拟合放后台、只解一套 Q：进度 100 ms 一报，
    /// 每次都跑完整 Q 搜索会把 UI 线程拖死，进度条就卡住了。
    /// </summary>
    private void UpdateLive(IReadOnlyList<MeasurementPoint>? partial)
    {
        if (partial is null || partial.Count == 0)
        {
            return;
        }

        var points = partial.ToArray();
        var reference = ReferenceLevel(points);

        for (var i = 0; i < points.Length; i++)
        {
            points[i] = points[i] with { LevelDb = points[i].RawDb - reference };
        }

        // 麦克风偏差在这里就扣掉：图上这条灰线从长出来的第一秒起就该是「喇叭的样子」，
        // 不能等测完换了正式结果才突然变一次。
        if (_micCurve is not null && !_tutorialDemo)
        {
            var corrected = _micCurve.ApplyTo(new SweepMeasurement(
                points, reference, 48000, points.Select(point => point.FrequencyHz).ToArray()));
            points = corrected.Points.ToArray();
        }

        _livePoints = points;

        var now = Environment.TickCount64;
        if (now - _lastLiveDrawAt < 500)
        {
            return;
        }

        _lastLiveDrawAt = now;

        // 同一时间只放一个后台拟合；算得比 500 ms 慢时，中间那些轮直接跳过。
        // 第 2 轮起不再实时拟合：图上要一直挂着上一轮的矫正曲线，本轮结果等轮末整条换上。
        if (_liveEqEnabled && points.Length >= 6 && Interlocked.CompareExchange(ref _liveFitBusy, 1, 0) == 0)
        {
            var version = Interlocked.Increment(ref _liveFitVersion);
            var frequencies = points.Select(point => point.FrequencyHz).ToArray();
            var measurement = new SweepMeasurement(points, reference, 48000, frequencies);
            var target = _target;
            var density = _density;
            var baseline = _baselinePlan?.Points;
            var crossover = _crossoverHz;
            var applied = _applied && _plan is not null;
            var previous = _plan;
            var firstLive = _livePlan is null;
            var maxBoost = _compensationPercent;

            _ = Task.Run(() =>
            {
                CalibrationPlan? live;
                try
                {
                    live = applied && previous is not null
                        ? EqCurveFitter.FitFeedback(
                            target, previous, measurement, density, FeedbackGain, MaxStepDb,
                            crossover, quick: true, compensationPercent: maxBoost)
                        : EqCurveFitter.Fit(
                            target, measurement, null, density, baseline,
                            movableBands: false, floorHz: crossover, quick: true, compensationPercent: maxBoost);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    return;
                }
                finally
                {
                    Interlocked.Exchange(ref _liveFitBusy, 0);
                }

                Dispatcher.InvokeAsync(() =>
                {
                    if (_closed || version != Volatile.Read(ref _liveFitVersion))
                    {
                        return;
                    }

                    _livePlan = live;

                    // 低频曲线等测完再生成；实时预览只画主层，省掉一半计算。
                    _liveBassPlan = null;

                    if (firstLive)
                    {
                        Trace($"live first baseline={baseline is not null} points={points.Length} "
                            + $"corr={live.CorrectionRange.Min:0.0}..{live.CorrectionRange.Max:0.0}");
                    }

                    DrawPlot();
                });
            });
        }

        DrawPlot();
    }

    private async Task RunMeasurementAsync()
    {
        if (_busy)
        {
            return;
        }

        if (DeviceBox.Items.Count == 0)
        {
            return;
        }

        // 先换一张新的取消票再进 _busy：关窗口只认 _busy，而它一变 true，
        // OnClosing 取消的就必须是这一次测量的 token，不能是上一轮剩下的。
        _cancellation.Cancel();
        _cancellation.Dispose();
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;

        _busy = true;
        _stopRequested = false;
        _runSucceeded = false;

        // 测量期间主窗口的试听/预览写盘会和拉平抢 peace.txt，先挡住。
        _viewModel.SetCalibrationMeasuring(true);

        // 上一轮的结果一直留着：测的这二十多秒里，图上画的还是它，测完才被新结果替掉。
        // 继续测之前先把它套上去——这一轮测到的才是「补过之后还剩多少」，而不是又测一遍原样。
        var previous = _plan;
        Trace($"start previousNull={previous is null} applied={_applied}");
        if (previous is not null && !_applied)
        {
            try
            {
                // 连低音层一起套：这条结果套过一次，低频矫正就该跟着在；
                // 测量本身仍然不会叠它（下面 BeginCalibrationMeasurementKeepEqAsync 只写主层）。
                await _viewModel.ApplyCalibrationAsync(previous, _bassPlan);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Trace($"apply before measure failed: {ex.Message}");
                _busy = false;
                SetState(MeasureState.Idle);
                return;
            }

            // 套用期间窗口可能已经被关掉了：别再往下开测量。
            token.ThrowIfCancellationRequested();

            if (_viewModel.Mode != MainMode.MainLayer)
            {
                _busy = false;
                SetState(MeasureState.Idle);
                return;
            }

            _applied = true;
            ApplyButton.IsEnabled = false;
            Trace($"applied mode={_viewModel.Mode}");
        }

        _livePoints = Array.Empty<MeasurementPoint>();
        _livePlan = null;
        _liveBassPlan = null;
        // 作废上一轮还在飞的实时预览：别让它在新一轮开始后才把旧曲线画回来。
        Interlocked.Increment(ref _liveFitVersion);
        _lastLiveDrawAt = 0;
        _baselinePlan = null;
        ApplyButton.IsEnabled = false;
        SetState(MeasureState.Running);
        Progress.Value = 0;
        DrawPlot();

        var deviceId = (DeviceBox.SelectedItem as CaptureDevice)?.Id;
        CalibrateDeviceMemory.Save(deviceId);

        var mode = Math.Clamp(SpeedBox.SelectedIndex, 0, 2);

        // 每轮只扫一遍。轮数由 MeasureLoopAsync 控制：更准 3 轮 = 套 3 次曲线、测 3 遍。
        // 以前这里按档位扫 2–3 遍，那是「同一套 EQ 下重复取平均」，中间不换曲线，
        // 所以用户看到的就是「第三轮还是那么鼓」。
        var maxRepeats = 1;

        // 固定次数模式下这个值不参与判断，只是 RunAdaptiveAsync 的签名还要它。
        const double stabilityDb = 1.0;

        // 基础频点表：快速 61 点、标准 121 点、更准 240 点。标准不再是默认的 61 点。
        var baseFrequencies = mode switch
        {
            0 => SweepPlan.BuildFastFrequencies(),
            2 => SweepPlan.BuildPreciseFrequencies(),
            _ => SweepPlan.BuildStandardFrequencies(),
        };

        IReadOnlyList<double> frequencies = baseFrequencies;
        if (previous is not null)
        {
            // 第二轮起按上一轮的起伏加密：坑和峰附近多测几刀，平的地方照旧。
            // 只有快速档（比拟合网格还疏）值得补点；标准和更准本来就比网格密。
            var extra = baseFrequencies.Length <= SweepPlan.Frequencies.Count ? 16 : 0;
            var adaptive = SweepPlan.BuildAdaptiveFrequencies(previous.Measured, extra, baseFrequencies);
            if (adaptive.Length > baseFrequencies.Length)
            {
                frequencies = adaptive;
            }
        }

        var density = mode switch
        {
            0 => FitDensity.Coarse,
            2 => FitDensity.Dense,
            _ => FitDensity.Normal,
        };

        _density = density;

        var progress = new Progress<SweepProgress>(item =>
        {
            if (_closed)
            {
                return;
            }

            Progress.Value = Math.Clamp(item.Fraction * 100, 0, 100);
            UpdateLive(item.PartialPoints);
            _currentHz = item.CurrentHz;
            DrawSweepCursor();

            // null 是「这一刻没有电平」——还没开始送数据，或者已经录完了。
            // 这和「设备在送数据但全是 0」是两件事，不能都写成「静音」。
            if (item.LevelDb is double level)
            {
                LevelText.Text = level <= -89 ? "静音" : $"{level:0} dB";
            }
            else
            {
                LevelText.Text = "—";
            }
        });

        var flattened = false;

        // 手上还没有结果 = 这次是第 1 轮（测裸设备）：开实时预览。
        // 「继续测量」、第 2/3 轮、重测本轮都不开——那几轮图上保留上一轮的矫正曲线，
        // 本轮拟合结果在轮末存下来，下一轮开头整条换上。
        _liveEqEnabled = previous is null;

        try
        {
            // 第一次测量才拉平：测裸喇叭，从零算。
            // 第二次起保留主层 EQ，但显式只写主层——低频矫正不能进测量，
            // 设备不一定发得出那个声音，叠进去测到的就不是主设备本身了。
            var flat = previous is null
                ? await _viewModel.BeginCalibrationMeasurementAsync()
                : await _viewModel.BeginCalibrationMeasurementKeepEqAsync();

            flattened = flat.Flat;

            // 拉平了，实测是裸喇叭；没拉平，实测里含着上一轮那份 EQ，拟合前得先减掉。
            _baselinePlan = flattened ? null : previous;

            // 拉平这几百毫秒里窗口可能已经被关掉：别再把扫频放出来。
            token.ThrowIfCancellationRequested();

            var rawMeasurement = await SweepMeasureService.RunAdaptiveAsync(
                deviceId, stabilityDb, maxRepeats, progress, token, () => _stopRequested, frequencies,
                fixedRepeats: true);
            if (_closed)
            {
                return;
            }

            // 原始实测留一份：换麦克风曲线、拖分界线都按它重算，不用重新扫频。
            // 往下的拟合用的是扣掉麦克风偏差那一份——图上「实测」就该是喇叭本身的样子。
            _measurement = rawMeasurement;
            var measurement = Corrected(rawMeasurement);

            // 新测量、新目标：目标压量重新算一次，之后拖动只在这一次的值上。
            _targetOffsetDb = null;

            // 完整 Q 搜索放后台：要解几遍方程再逐段收尾，放 UI 线程会假死好几秒，
            // 进度条看起来就是「卡在 100%」。
            var crossover = _crossoverHz;
            var baselineForFit = _baselinePlan?.Points;
            var useFeedback = _applied && previous is not null;
            var appliedPlan = previous;
            var maxBoost = _compensationPercent;

            var fitted = await Task.Run(() =>
            {
                var plan = useFeedback
                    ? EqCurveFitter.FitFeedback(
                        _target, appliedPlan!, measurement, density, FeedbackGain, MaxStepDb, crossover,
                        compensationPercent: maxBoost)
                    : EqCurveFitter.Fit(
                        _target, measurement, null, density, baselineForFit,
                        movableBands: true, floorHz: crossover, compensationPercent: maxBoost);

                // 低频矫正按同一份实测单独算：这段不追反馈轮次，因为测量时低音层不叠加，
                // 测回来永远是主设备的裸低频，反相（或补到目标）只有一个答案。
                var bass = FitBassSafe(measurement, _target, baselineForFit, crossover, quick: false, maxBoost);
                return (Plan: plan, Bass: bass);
            }).ConfigureAwait(true);

            if (_closed)
            {
                return;
            }

            _plan = fitted.Plan;
            // 这一轮的目标压量就此钉住：后面拖分界线只重算形状，不再让整条曲线上下平移。
            _targetOffsetDb = _plan.TargetOffsetDb;
            _bassPlan = fitted.Bass;

            // 推给主界面：曲线和预增益立刻钉在那边，测量期间主窗口的预览写盘已被挡掉。
            _viewModel.UpdateCalibrationPreview(_plan, _bassPlan);

            // 可用点不到一半：这一轮样本不够。按钮变「重测本轮」，
            // 刻度条自动往「保证音量」挪 3 dB，下一轮多提一点弱频段。
            _sampleShort = measurement.UsableCount * 2 < measurement.Points.Count;
            if (_sampleShort)
            {
                _compensationPercent = Math.Min(100, _compensationPercent + 10);
                _boostWired = false;
                BoostSlider.Value = _compensationPercent;
                _boostWired = true;
            }

            // 新结果到手，旧的那份就不算「已套用」了；实时预览那份也退场，别盖住正式结果。
            // 版本号也要往前走一步：还在后台跑的实时拟合不能再画上来。
            Interlocked.Increment(ref _liveFitVersion);
            _livePlan = null;
            _liveBassPlan = null;
            _applied = false;
            _runSucceeded = true;
            // 残差最大的三个频点：评估「第一轮为什么补不够」时，这个比总量更有用——
            // 是整段欠补，还是几个窄坑/窄峰卡在格点之间。
            var worst = _plan.ResidualDb
                .Select((db, index) => (
                    Hz: index < measurement.Points.Count ? measurement.Points[index].FrequencyHz : 0,
                    Db: Math.Abs(db)))
                .Where(item => item.Hz >= EqCurveFitter.CalibrationFloorHz)
                .OrderByDescending(item => item.Db)
                .Take(3)
                .Select(item => $"{item.Hz:0}Hz:{item.Db:0.0}");

            Trace($"fit done filters={_plan.FilterCount} baseline={_baselinePlan is not null} "
                + $"aggressive={_aggressive} flattened={flattened} "
                + $"engine={measurement.EngineName ?? "-"} usable={measurement.UsableCount}/{measurement.Points.Count} "
                + $"dirty={measurement.DirtyCount} "
                + $"gains={GainRange(_plan.Points)} corr={_plan.CorrectionRange.Min:0.0}..{_plan.CorrectionRange.Max:0.0} "
                + $"worst={string.Join(",", worst)}");

            // 扫完了，绿线退场；结果图不带游标。
            _currentHz = null;

            Progress.Value = 100;
            DrawPlot();
            ApplyButton.IsEnabled = _plan.FilterCount > 0;

            // 反馈轮补完还离目标很远，就建议再扫一轮。
            // 判据不用 previous（那是窗口内状态，换档或重开窗口就没了，_plan 会变 null），
            // 用「这一轮有没有拉平」：拉平轮测的是裸设备，残差大是正常的；反馈轮残差大才是补不动。
            if (!flattened)
            {
                // 记下这一轮离目标还差多少；下一轮的激进倍率靠它和上一轮的比值算。
                var residual = _plan.MaxResidualDb;
                _roundResiduals.Add(residual);
                Trace($"round residual={residual:0.0}");

                // 倍率 = 1 + clamp(上一轮残差 / 这一轮残差, 0, 1)。
                // 这一轮变差了（残差更大）→ 比值小 → 倍率往 1 退；还在改善 → 保持上一档。
                if (_roundResiduals.Count >= 2)
                {
                    var before = _roundResiduals[^2];
                    var cur = _roundResiduals[^1];
                    if (before > 0 && cur > 0)
                    {
                        _aggressiveScale = 1 + Math.Clamp(before / cur, 0, 1);
                        Trace($"aggressive scale={_aggressiveScale:0.00} (before={before:0.0} cur={cur:0.0})");
                    }
                }

                if (!_aggressive)
                {
                    if (_hasMoreRounds)
                    {
                        // 多轮里这一轮是「给下一轮套的」：不按残差挑，直接切激进。
                        // 用户要的是「第 2、3 轮每轮都变」，残差小了就停手等于第 3 轮白跑。
                        _aggressive = true;
                        _aggressiveScale = 2.0;
                        TryRefitAggressive(measurement, previous);
                        Trace("auto aggressive for next round");
                    }
                    else if (residual > StalledResidualDb)
                    {
                        OfferAggressiveRound(residual, measurement, previous);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 停止是常态，不弹提示。
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace($"measure failed: {ex.Message}");
            if (!_closed)
            {
                Progress.Value = 0;

                // 这一轮没测成，手上还留着上一轮的结果：把它恢复成「可用」的样子。
                // 不然按钮会一直停在测量中的灰色，图上也只剩上一轮的线，看着像程序坏了。
                if (_plan is not null)
                {
                    ApplyButton.IsEnabled = _plan.FilterCount > 0;
                }

                StartButton.Content = _plan is null ? "开始测量" : "继续测量";
            }
        }
        finally
        {
            if (flattened)
            {
                await _viewModel.EndCalibrationMeasurementAsync();
            }

            _busy = false;
            _viewModel.SetCalibrationMeasuring(false);

            // 测量期间主窗口的预览写盘被挡着，结果出来后放开并推一次，APO 那边才真的听到。
            if (_plan is not null && !_closed)
            {
                _viewModel.UpdateCalibrationPreview(_plan, _bassPlan);
            }

            // 样本不足时这条曲线是按旧上限算的，按自动 +3 之后的上限重算一次；
            // 必须等 _busy 落下，RefitForCrossover 开头有这个守卫。
            if (_sampleShort && _measurement is not null && !_closed)
            {
                RefitForCrossover(quick: false);
            }
            if (!_closed)
            {
                // 半截预览退场：中断或失败时，图上回到「手上那份结果」，不是一堆没测完的点。
                _livePlan = null;
                SetState(MeasureState.Idle);
                DrawPlot();
            }
        }
    }

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (_plan is null || _busy)
        {
            return;
        }

        ApplyButton.IsEnabled = false;
        try
        {
            // 套用 = 覆盖主界面当前选中的配置；预览会话到此结束，关窗口不会再撤销。
            await _viewModel.ApplyCalibrationPreviewAsync();
            _applied = true;
            Applied = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace($"apply failed: {ex.Message}");
            ApplyButton.IsEnabled = true;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 临时诊断：把关键状态追加到 %TEMP%\autocal-trace.log。
    /// 只在排查「继续测量后曲线被清空」时用，查完删掉。
    /// </summary>
    private static void Trace(string message)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autocal-trace.log");
            System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // 诊断用，写不进去就算了。
        }
    }

    // ---- 画图 ----

    private const double DefaultMinDb = -15;
    private const double DefaultMaxDb = 15;

    /// <summary>主界面编辑模式那条曲线的蓝，左右 EQ 和目标都用它。</summary>
    private static readonly Color EqBlue = Color.FromRgb(0x4C, 0xC2, 0xFF);

    /// <summary>主界面低音编辑模式的紫，低频矫正在没削波时用它。</summary>
    private static readonly Color BassPurple = Color.FromRgb(0xA7, 0x8B, 0xFA);

    /// <summary>削波到这一档就全红。</summary>
    private static readonly Color ClipRed = Color.FromRgb(0xFF, 0x4D, 0x4D);

    private const double ClipRedAtDb = 6.0;

    /// <summary>低频本来就要提得多，红色到 12 dB 才满。</summary>
    private const double BassClipRedAtDb = 12.0;

    /// <summary>按削波量把基色往红拉：0 dB 保持原色，到 redAtDb 及以上全红。</summary>
    private static Color ClipTint(Color baseColor, double clipDb, double redAtDb)
    {
        var t = Math.Clamp(clipDb / redAtDb, 0, 1);
        if (t <= 0)
        {
            return baseColor;
        }

        return Color.FromRgb(
            (byte)Math.Round(baseColor.R + (ClipRed.R - baseColor.R) * t),
            (byte)Math.Round(baseColor.G + (ClipRed.G - baseColor.G) * t),
            (byte)Math.Round(baseColor.B + (ClipRed.B - baseColor.B) * t));
    }

    /// <summary>某个频率上的削波量：取最近的测量点，不顺滑，反正颜色看不出那么细。</summary>
    private static double ClipAt(CalibrationPlan? plan, double hz)
    {
        if (plan is null || plan.ClipDb.Count == 0)
        {
            return 0;
        }

        var best = 0.0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < plan.Measured.Count && i < plan.ClipDb.Count; i++)
        {
            var distance = Math.Abs(Math.Log(plan.Measured[i].FrequencyHz / hz));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = plan.ClipDb[i];
            }
        }

        return best;
    }

    /// <summary>逐段画曲线：每段的颜色按两端削波量的平均值取，同色段不合并也够用。</summary>
    private void AddSegmentedCurve(
        IReadOnlyList<(double Hz, double Db)> samples,
        Color baseColor,
        Func<double, double> clipAt,
        double thickness,
        double opacity,
        double redAtDb = ClipRedAtDb)
    {
        for (var i = 1; i < samples.Count; i++)
        {
            var clip = (clipAt(samples[i - 1].Hz) + clipAt(samples[i].Hz)) / 2;
            Plot.Children.Add(new Line
            {
                X1 = PlotX(samples[i - 1].Hz),
                Y1 = PlotY(samples[i - 1].Db),
                X2 = PlotX(samples[i].Hz),
                Y2 = PlotY(samples[i].Db),
                Stroke = new SolidColorBrush(ClipTint(baseColor, clip, redAtDb)),
                StrokeThickness = thickness,
                Opacity = opacity,
                IsHitTestVisible = false,
            });
        }
    }

    /// <summary>
    /// 当前纵轴范围。默认 ±15，实测掉得更低时自动往下撑。
    ///
    /// 低频（20–80 Hz）在大多数音箱+麦克风组合上只有 −20 dB 以下。固定 ±15 的画法会把
    /// 那一段直接裁到图外，看起来就像「低频没画」——其实画了，只是跑出可视区了。
    /// </summary>
    private double _minDb = DefaultMinDb;
    private double _maxDb = DefaultMaxDb;

    private const double AxisLeft = 46;
    private const double AxisRight = 14;
    private const double AxisTop = 12;
    private const double AxisBottom = 26;

    private void OnPlotSizeChanged(object sender, SizeChangedEventArgs e) => DrawPlot();

    /// <summary>
    /// 按实测决定纵轴范围：默认 ±15 dB；实测掉得更低就往下撑（最多 −30），
    /// 让 20–80 Hz 那段虽然在 −20 dB，也照常画在图里。
    /// 只扩视觉范围，不影响任何拟合数字。
    /// </summary>
    private void UpdatePlotRange(CalibrationPlan? plan)
    {
        var min = DefaultMinDb;
        var max = DefaultMaxDb;

        var points = (IReadOnlyList<MeasurementPoint>?)plan?.Measured ?? _livePoints;
        foreach (var point in points)
        {
            if (!double.IsFinite(point.LevelDb))
            {
                continue;
            }

            min = Math.Min(min, point.LevelDb);
            max = Math.Max(max, point.LevelDb);
        }

        // 低频矫正画在低音层，可能到 +18 dB；不算进来的话绿线会被裁到框外。
        var bassPlan = _liveBassPlan ?? _bassPlan;
        if (bassPlan is not null)
        {
            var bassSnapshot = bassPlan.ToSnapshot();
            for (var hz = EqCurveFitter.BassFloorHz; hz <= _crossoverHz; hz *= Math.Pow(2, 1.0 / 12))
            {
                var gain = EqResponse.GainAt(bassSnapshot, hz);
                min = Math.Min(min, gain);
                max = Math.Max(max, gain);
            }
        }

        // 预增益线是水平基准，得保证它始终落在可视范围内。
        if (plan?.PreAmpDb is double preAmp)
        {
            min = Math.Min(min, preAmp);
            max = Math.Max(max, preAmp);
        }

        // 5 dB 一档、上下各留一点余量；至少 30 dB 跨度，曲线不会被压扁。
        min = Math.Max(-30, Math.Floor((min - 2) / 5) * 5);
        max = Math.Min(30, Math.Ceiling((max + 2) / 5) * 5);

        if (max - min < 30)
        {
            max = Math.Min(30, min + 30);
            if (max - min < 30)
            {
                min = max - 30;
            }
        }

        _minDb = min;
        _maxDb = max;
    }

    /// <summary>
    /// 当前扫频频率那条绿色竖线。只动它的两个 X，不重画整张图——扫频时 100 ms 报一次，
    /// 每次都重画 Canvas 会把 UI 线程拖死，进度条就卡住了。
    /// </summary>
    private void DrawSweepCursor()
    {
        var width = Plot.ActualWidth;
        var height = Plot.ActualHeight;
        if (width < 160 || height < 100)
        {
            return;
        }

        if (_currentHz is not double hz)
        {
            if (_sweepCursor is not null)
            {
                Plot.Children.Remove(_sweepCursor);
                _sweepCursor = null;
            }

            if (_sweepLabel is not null)
            {
                Plot.Children.Remove(_sweepLabel);
                _sweepLabel = null;
            }

            return;
        }

        if (_sweepCursor is null || !Plot.Children.Contains(_sweepCursor))
        {
            _sweepCursor = new Line
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x5A, 0xD6, 0x7A)),
                StrokeThickness = 1.4,
                IsHitTestVisible = false,
            };
            Plot.Children.Add(_sweepCursor);

            // 曲线是在网格之后才画的，绿线必须在它们上面，不然被盖住就看不见了。
            System.Windows.Controls.Panel.SetZIndex(_sweepCursor, 1000);
            Trace($"sweep cursor at {hz:0} Hz");
        }

        if (_sweepLabel is null || !Plot.Children.Contains(_sweepLabel))
        {
            _sweepLabel = new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0xD6, 0x7A)),
                IsHitTestVisible = false,
            };
            Plot.Children.Add(_sweepLabel);
            System.Windows.Controls.Panel.SetZIndex(_sweepLabel, 1001);
        }

        var plotWidth = width - AxisLeft - AxisRight;
        var plotHeight = height - AxisTop - AxisBottom;
        var x = AxisLeft + Math.Log10(Math.Clamp(hz, 20, 20000) / 20) / Math.Log10(1000.0) * plotWidth;

        _sweepCursor.X1 = x;
        _sweepCursor.X2 = x;
        _sweepCursor.Y1 = AxisTop;
        _sweepCursor.Y2 = AxisTop + plotHeight;

        _sweepLabel.Text = $"{hz:0} Hz";
        System.Windows.Controls.Canvas.SetLeft(_sweepLabel, Math.Min(x + 4, width - 62));
        System.Windows.Controls.Canvas.SetTop(_sweepLabel, AxisTop + 2);
    }

    private void DrawPlot()
    {
        Plot.Children.Clear();
        _sweepCursor = null;

        var width = Plot.ActualWidth;
        var height = Plot.ActualHeight;
        if (width < 160 || height < 100)
        {
            return;
        }

        var plotWidth = width - AxisLeft - AxisRight;
        var plotHeight = height - AxisTop - AxisBottom;

        // 实测掉到 −20 dB 以下时把纵轴往下放，低频曲线才不会被裁到图外。
        var plan = _livePlan ?? _plan;
        UpdatePlotRange(plan);

        double X(double hz) =>
            AxisLeft + Math.Log10(Math.Clamp(hz, 20, 20000) / 20) / Math.Log10(1000.0) * plotWidth;

        double Y(double db) =>
            AxisTop + (_maxDb - Math.Clamp(db, _minDb, _maxDb)) / (_maxDb - _minDb) * plotHeight;

        // 实测灰线：只把「可用」的点连成曲线，用单调三次插值（穿过每个点、峰谷不过冲）画圆滑；
        // 判脏 / 信噪比不够的点不进曲线，单独画成空心圈——意思是「这里测到了，但不可信」。
        //
        // judge=false 是测量途中的临时点：那批点还没算纯度（实时走的是宽带 RMS），
        // 这时候判脏没有意义，直接连起来就行，颜色浅一点，和正式结果区分开。
        //
        // 矫正起点以下的低频点用 EqCurveFitter.TrustedLowMask：信噪比门槛更严，
        // 孤立在噪声里的一两个点也不采信——和拟合实际用的是同一批点，图不会说假话。
        void DrawMeasured(IReadOnlyList<MeasurementPoint> points, bool judge, bool provisional = false, bool dim = false)
        {
            if (points.Count == 0)
            {
                return;
            }

            var lowMask = EqCurveFitter.TrustedLowMask(points, _crossoverHz);
            bool TrustedAt(int index)
            {
                var point = points[index];
                return point.IsUsable || (point.FrequencyHz < _crossoverHz && lowMask[index]);
            }

            var usable = new List<(double Hz, double Db)>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (double.IsFinite(point.LevelDb) && (!judge || TrustedAt(i)))
                {
                    usable.Add((point.FrequencyHz, point.LevelDb));
                }
            }

            var curve = SmoothCurve.Build(usable);
            if (curve is not null)
            {
                var line = new Polyline
                {
                    Stroke = new SolidColorBrush(provisional
                        ? Color.FromRgb(0xB4, 0xB4, 0xB4)
                        : dim
                            ? Color.FromRgb(0x5C, 0x5C, 0x5C)
                            : Color.FromRgb(0x90, 0x90, 0x90)),
                    StrokeThickness = 1.4,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };

                foreach (var (hz, db) in curve.Sample(480))
                {
                    line.Points.Add(new System.Windows.Point(X(hz), Y(db)));
                }

                Plot.Children.Add(line);
            }

            if (!judge)
            {
                return;
            }

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (TrustedAt(i) || !double.IsFinite(point.LevelDb))
                {
                    continue;
                }

                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Stroke = new SolidColorBrush(dim
                        ? Color.FromRgb(0x4A, 0x4A, 0x4A)
                        : Color.FromRgb(0x78, 0x78, 0x78)),
                    StrokeThickness = 1.1,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false,
                };

                // 用全限定名：文件里有 Wpf.Ui.Controls，直接 using System.Windows.Controls 会和它撞名。
                System.Windows.Controls.Canvas.SetLeft(dot, X(point.FrequencyHz) - 2.5);
                System.Windows.Controls.Canvas.SetTop(dot, Y(point.LevelDb) - 2.5);
                Plot.Children.Add(dot);
            }
        }

        DrawGrid(X, Y, plotWidth, plotHeight);

        // 预增益：整条曲线整体下移的基准，红色虚线。先画它、再画曲线——
        // 曲线会盖在上面，不会挡住左右和低音曲线。
        if (plan is not null && Math.Abs(plan.PreAmpDb) >= 0.05)
        {
            var preAmpY = Y(plan.PreAmpDb);
            Plot.Children.Add(new Line
            {
                X1 = AxisLeft,
                X2 = AxisLeft + plotWidth,
                Y1 = preAmpY,
                Y2 = preAmpY,
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5A)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 4 },
                IsHitTestVisible = false,
            });
        }

        DrawSweepCursor();

        // 矫正起点：分界线以上由左右层拟合，以下交给低音层反相。
        // 画在网格之上、曲线之下，用户拖它的时候不会被别的线挡住。
        var crossoverX = X(_crossoverHz);
        var crossoverLine = new Line
        {
            X1 = crossoverX,
            X2 = crossoverX,
            Y1 = AxisTop,
            Y2 = AxisTop + plotHeight,
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x66)),
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 5, 3 },
        };
        Plot.Children.Add(crossoverLine);

        var crossoverLabel = new System.Windows.Controls.TextBlock
        {
            Text = $"矫正起点 {_crossoverHz:0} Hz",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x66)),
        };
        System.Windows.Controls.Canvas.SetLeft(crossoverLabel, Math.Min(crossoverX + 5, width - 105));
        System.Windows.Controls.Canvas.SetTop(crossoverLabel, AxisTop + 2);
        Plot.Children.Add(crossoverLabel);

        // 目标曲线
        var target = new Polyline
        {
            Stroke = new SolidColorBrush(EqBlue),
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 4, 3 },
        };
        for (var i = 0; i <= 200; i++)
        {
            var hz = 20 * Math.Pow(1000, i / 200.0);
            target.Points.Add(new System.Windows.Point(X(hz), Y(TargetCurve.GainAt(_target, hz))));
        }

        Plot.Children.Add(target);

        // 麦克风曲线：细虚线，和实测用同一把 dB 尺子——它扣掉多少，图上就画多少。
        // 画在实测之前，让灰线压在上面：这条是「尺子的刻度」，不是主角。
        if (_micCurve is not null)
        {
            var mic = new Polyline
            {
                Stroke = new SolidColorBrush(MicCalColor),
                StrokeThickness = 1.1,
                StrokeDashArray = new DoubleCollection { 2, 3 },
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false,
            };

            foreach (var (hz, db) in _micCurve.Sample())
            {
                mic.Points.Add(new System.Windows.Point(X(hz), Y(db)));
            }

            if (mic.Points.Count > 0)
            {
                Plot.Children.Add(mic);
            }
        }

        // 测量途中先画实时那份（边测边长），测完再换成正式结果；
        // 两个都没有才落到「还没凑够点」的那条分支——手上有上一轮的结果时，那二十多秒里图上画的就是它。
        Trace($"draw planNull={plan is null} live={_livePlan is not null} points={_livePoints.Length} "
            + $"range={_minDb:0}..{_maxDb:0} meas={plan?.Measured.Count ?? -1} "
            + $"measMin={(plan is null || plan.Measured.Count == 0 ? 0 : plan.Measured.Min(p => p.LevelDb)):0.0} "
            + $"measMax={(plan is null || plan.Measured.Count == 0 ? 0 : plan.Measured.Max(p => p.LevelDb)):0.0}");
        if (plan is null)
        {
            // 还没凑够能拟合的点，先把已经测到的画出来——不然头几秒什么都看不到。
            if (_livePoints.Length > 0)
            {
                DrawMeasured(_livePoints, judge: false);
            }

            return;
        }

        // 实测。下一轮真的开扫之后，上一轮这条压暗（旧线退后、新线才看得清）；
        // 轮与轮之间是亮的——那几十秒正是用来看上一轮结果的。
        DrawMeasured(plan.Measured, judge: true, dim: _busy && !_liveEqEnabled);

        // 测量途中，图上还挂着上一轮的结果时（「继续测量」），光画 plan 就是一动不动二十多秒。
        // 把这一轮已经测到的点用浅灰叠上去，边测边长。
        // 第 1 轮不用叠：那时候 plan 本身就是实时那份，再叠一遍等于同一根线画两次。
        if (_busy && !_liveEqEnabled && _livePoints.Length > 0)
        {
            DrawMeasured(_livePoints, judge: false, provisional: true);
        }

        var snapshot = plan.Points.ToSnapshot();

        var eqSamples = new List<(double Hz, double Db)>();

        var limit = _livePlan is not null && _livePoints.Length > 0
            ? _livePoints[^1].FrequencyHz
            : 20000.0;

        var needsAnimation = false;
        for (var i = 0; i <= 200; i++)
        {
            var hz = 20 * Math.Pow(1000, i / 200.0);
            if (hz > limit)
            {
                break;
            }

            _eqTarget[i] = EqResponse.GainAt(snapshot, hz);

            if (!_eqValid[i])
            {
                // 新露出来的那一段直接落在目标上：从别处滑过来会像「凭空长出一截」。
                _eqShown[i] = _eqTarget[i];
                _eqValid[i] = true;
            }
            else if (Math.Abs(_eqTarget[i] - _eqShown[i]) > 0.02)
            {
                // 测量途中不追补间：这二十多秒里实时曲线每 0.5 秒就把整幅画布拆了重建一次，
                // 再叠一个 30 fps 的补间，等于每秒重画三十遍。重到掉帧时，屏幕上就会留下
                // 上一帧的线——也就是「残影」。测的时候直接落在目标上，稳。
                if (_busy)
                {
                    _eqShown[i] = _eqTarget[i];
                }
                else
                {
                    needsAnimation = true;
                }
            }

            eqSamples.Add((hz, _eqShown[i]));
        }

        // 左右曲线：主界面编辑蓝，被削掉的提升越多越红。
        AddSegmentedCurve(
            eqSamples, EqBlue, hz => ClipAt(plan, hz), 1.8, _livePlan is null ? 1.0 : 0.55);

        // 低音层的低频矫正：紫色，只覆盖 20 Hz–分界线。它不在主层的推子范围里，
        // 画出来是为了套用之前就能看见低频要补多少、哪里补得凶；叠上主层预增益后
        // 超出 0 dBFS 的部分同样染红。
        var bassPlan = _liveBassPlan ?? _bassPlan;
        if (bassPlan is not null)
        {
            var bassSnapshot = bassPlan.ToSnapshot();
            var bassSamples = new List<(double Hz, double Db)>();

            for (var hz = EqCurveFitter.BassFloorHz; hz <= _crossoverHz; hz *= Math.Pow(2, 1.0 / 24))
            {
                bassSamples.Add((hz, EqResponse.GainAt(bassSnapshot, hz)));
            }

            // 补最后一个分界点：循环按 1/24 倍频程走，未必正好落在上限上。
            bassSamples.Add((_crossoverHz, EqResponse.GainAt(bassSnapshot, _crossoverHz)));

            var mainPreAmp = plan?.PreAmpDb ?? 0;
            AddSegmentedCurve(
                bassSamples, BassPurple,
                hz => Math.Max(0, EqResponse.GainAt(bassSnapshot, hz) + mainPreAmp),
                1.4, _liveBassPlan is null ? 1.0 : 0.7, BassClipRedAtDb);
        }

        if (needsAnimation)
        {
            _eqTimer?.Start();
        }
    }

    /// <summary>
    /// 把 EQ 曲线一帧一帧地拉向新目标。每 33 ms 走 22%，大约 0.2 秒追上——
    /// 实时预览每 300 ms 换一次拟合结果，直接换会一跳一跳的，滑过去才像同一条线在变。
    /// </summary>
    private void OnEqTick(object? sender, EventArgs e)
    {
        var settled = true;

        for (var i = 0; i <= 200; i++)
        {
            if (!_eqValid[i])
            {
                continue;
            }

            var delta = _eqTarget[i] - _eqShown[i];
            if (Math.Abs(delta) <= 0.02)
            {
                _eqShown[i] = _eqTarget[i];
                continue;
            }

            _eqShown[i] += delta * EqEase;
            settled = false;
        }

        if (settled)
        {
            _eqTimer?.Stop();
        }

        DrawPlot();
    }

    private void DrawGrid(Func<double, double> x, Func<double, double> y, double plotWidth, double plotHeight)
    {
        var gridBrush = new SolidColorBrush(Color.FromArgb(0x28, 0x80, 0x80, 0x80));
        var axisBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x80, 0x80, 0x80));
        var labelBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xC8, 0xC8, 0xC8));

        for (var db = (int)_minDb; db <= (int)_maxDb; db += 5)
        {
            var line = new Line
            {
                X1 = AxisLeft,
                X2 = AxisLeft + plotWidth,
                Y1 = y(db),
                Y2 = y(db),
                Stroke = db == 0 ? axisBrush : gridBrush,
                StrokeThickness = db == 0 ? 1.2 : 1,
            };
            Plot.Children.Add(line);

            var label = new System.Windows.Controls.TextBlock
            {
                Text = db > 0 ? $"+{db}" : db.ToString(),
                FontSize = 10,
                Foreground = labelBrush,
            };
            System.Windows.Controls.Canvas.SetLeft(label, 6);
            System.Windows.Controls.Canvas.SetTop(label, y(db) - 7);
            Plot.Children.Add(label);
        }

        foreach (var (hz, text) in new (double, string)[] { (20, "20"), (100, "100"), (1000, "1k"), (10000, "10k"), (20000, "20k") })
        {
            var line = new Line
            {
                X1 = x(hz),
                X2 = x(hz),
                Y1 = AxisTop,
                Y2 = AxisTop + plotHeight,
                Stroke = gridBrush,
                StrokeThickness = 1,
            };
            Plot.Children.Add(line);

            var label = new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = labelBrush,
            };
            System.Windows.Controls.Canvas.SetLeft(label, x(hz) - 8);
            System.Windows.Controls.Canvas.SetTop(label, AxisTop + plotHeight + 6);
            Plot.Children.Add(label);
        }


    }
}
