using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

    /// <summary>
    /// 激进倍率：1.0 = 常规（gain 0.9 / 每段 6 dB），大于 1 就是这一轮更大步。
    /// 固定死一档会「激进过头」，第 3 轮起按前两轮的残差比自适应。
    /// </summary>
    private double _aggressiveScale = 1.0;

    /// <summary>每轮测完的残差，用来算下一轮的激进倍率。</summary>
    private readonly List<double> _roundResiduals = [];
    private bool _busy;
    private bool _closed;

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

    /// <summary>连续多少次进度报告里电平都是 -90；到这个数就提示换设备（每 100 ms 一次，1.5 秒）。</summary>
    private const int SilentReportThreshold = 15;

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

    private int _silentReports;

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

    private bool _draggingCrossover;

    /// <summary>拖动时重算的节流时间：Q 搜索一轮几十毫秒，每次都算会拖手。</summary>
    private long _lastCrossoverFitAt;

    /// <summary>分界线重算的版本号：拖动时会连发多次，只让最后一次回到界面。</summary>
    private int _crossoverFitVersion;

    public AutoCalibrateWindow(MainViewModel viewModel, CalibrationTargetKind target)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _target = target;

        InitializeComponent();

        // 尺寸钉死之后没有可拉的边框，顶部留一条能按住拖窗口
        WindowDragHelper.Enable(this);

        _eqTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _eqTimer.Tick += OnEqTick;

        Title = $"自动EQ · {TargetCurve.DisplayName(target)}";
        TargetText.Text = "校准到：" + TargetCurve.DisplayName(target);
        TargetHint.Text = TargetCurve.Description(target);

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadDevices();
        LoadOptions();
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

        // 只发取消，不 Dispose：测量任务可能还捏着这个 token，取消后它会在下一个
        // await 处抛出来。Dispose 交给下一次测量开头，或者随窗口一起回收。
        _cancellation.Cancel();
        _sweepPreview.Dispose();

        // 试听扫频可能还叠着低频：窗口关了也要把 peace.txt 还原成主层。
        _sweepRestoreCts?.Cancel();
        _sweepRestoreCts = null;
        _ = _viewModel.EndSweepPreviewAsync();
    }

    private void LoadDevices()
    {
        var devices = MicrophoneCaptureService.ListDevices();
        DeviceBox.ItemsSource = devices;

        if (devices.Count == 0)
        {
            DeviceBox.IsEnabled = false;
            StartButton.IsEnabled = false;
            StatusText.Text = "系统里没有可用的录音设备。插上麦克风，或者在 Windows 的声音设置里启用一个输入设备。";
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
                SetState(MeasureState.Stopping);
                StatusText.Text = "正在停止…本轮扫频会走完，再点一下可以撤销。";
                return;

            case MeasureState.Stopping:
                _stopRequested = false;
                SetState(MeasureState.Running);
                StatusText.Text = "继续测量…";
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
        for (var round = 1; round <= totalRounds; round++)
        {
            if (_closed)
            {
                return;
            }

            var before = _plan;
            if (round > 1)
            {
                StatusText.Text = $"第 {round}/{totalRounds} 轮：接着测「补过之后」还剩多少…";
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
            UpdateResultSummary(measurement);
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
            aggressive: _aggressive);

    /// <summary>
    /// 生成低频矫正曲线。低频点不够时返回 null——主层结果照常可用，只是不写低音层。
    ///
    /// 它不走反馈轮次：测量时低音层不叠加，测回来永远是主设备的裸低频，
    /// 反相（或者补到目标）只需按当前实测算一次，没有「再测一轮验证」这回事。
    /// </summary>
    private EqPointList? TryFitBass(SweepMeasurement measurement, bool quick = false) =>
        FitBassSafe(measurement, _target, _baselinePlan?.Points, _crossoverHz, quick);

    /// <summary>
    /// 低频拟合的线程安全版本：不读窗口字段，后台线程也能直接调。
    /// </summary>
    private static EqPointList? FitBassSafe(
        SweepMeasurement measurement,
        CalibrationTargetKind target,
        EqPointList? baseline,
        double ceilingHz,
        bool quick)
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
                target, measurement, baseline, ceilingHz: ceilingHz, quick: quick);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>低频层里真正会写进后端的段数（不算端点、不算 0 dB）。</summary>
    private static int BassBandCount(EqPointList points) =>
        points.Points.Count(point => !point.IsEndpoint && Math.Abs(point.GainDb) >= 0.005);

    /// <summary>把这次结果的一句话摘要写进右上角；拖动分界线后也走这里。</summary>
    private void UpdateResultSummary(SweepMeasurement measurement)
    {
        if (_plan is null)
        {
            SummaryText.Text = string.Empty;
            return;
        }

        // 段中心是自己挪的，还是挪了也不更贴所以留在格点上——两种都合理，得说清楚是哪种。
        var gridFrequencies = EqCurveFitter.BuildFrequencies(_density, _crossoverHz);
        var shiftedBands = _plan.Points.Points
            .Where(point => !point.IsEndpoint)
            .Count(point => gridFrequencies.All(grid => Math.Abs(grid - point.FrequencyHz) > 0.05));

        var placementNote = shiftedBands > 0
            ? $" · {shiftedBands} 段按实测挪出了格点"
            : " · 段频率留在格点上（挪了不更贴）";

        var bassNote = _bassPlan is null
            ? " · 低频曲线：这次点不够，没生成"
            : $" · 低频 {EqCurveFitter.BassFloorHz:0}–{_crossoverHz:0} Hz "
                + $"{BassBandCount(_bassPlan)} 段";

        SummaryText.Text = $"{_plan.Summary} · 可用频点 {measurement.UsableCount}/{measurement.Points.Count}{placementNote}{bassNote}";
    }

    /// <summary>
    /// 分界线变了：用最近一次实测按新起点重算主层和低音层，不重测。
    /// 重算出来的还没套到主窗口，所以要把「已套用」清掉、重新允许套用。
    /// quick=true 给拖动途中用（只解一套 Q），松手时用 false 跑完整搜索。
    /// </summary>
    private void RefitForCrossover(bool quick)
    {
        if (_measurement is null || _busy)
        {
            return;
        }

        var version = Interlocked.Increment(ref _crossoverFitVersion);
        var measurement = _measurement;
        var target = _target;
        var density = _density;
        var baseline = _baselinePlan?.Points;
        var crossover = _crossoverHz;

        _ = Task.Run(() =>
        {
            CalibrationPlan? plan;
            EqPointList? bass;
            try
            {
                plan = EqCurveFitter.Fit(
                    target, measurement, null, density, baseline,
                    movableBands: !quick, floorHz: crossover, quick: quick);
                bass = FitBassSafe(measurement, target, baseline, crossover, quick);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (_closed || version != Volatile.Read(ref _crossoverFitVersion))
                    {
                        return;
                    }

                    StatusText.Text = $"矫正起点 {crossover:0} Hz：这份实测在这个起点上点不够，先保留上一次曲线。";
                });
                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (_closed || version != Volatile.Read(ref _crossoverFitVersion))
                {
                    return;
                }

                _plan = plan;
                _bassPlan = bass;
                // 拖动重算的结果也不能被还在飞的实时预览盖住。
                Interlocked.Increment(ref _liveFitVersion);
                _livePlan = null;
                _liveBassPlan = null;
                _applied = false;
                UpdateResultSummary(measurement);
                ApplyButton.IsEnabled = plan.FilterCount > 0;
                StatusText.Text = $"矫正起点改成 {crossover:0} Hz，已按同一份实测重算两层。"
                    + "点「套用矫正EQ」才会写回主窗口。";
                DrawPlot();
            });
        });
    }

    /// <summary>频率 → 画布横坐标，和 <see cref="DrawPlot"/> 里的对数轴一致。</summary>
    private double PlotX(double hz)
    {
        var plotWidth = Plot.ActualWidth - AxisLeft - AxisRight;
        return AxisLeft + Math.Log10(Math.Clamp(hz, 20, 20000) / 20) / Math.Log10(1000.0) * plotWidth;
    }

    /// <summary>画布横坐标 → 频率。</summary>
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

        // 拖动途中只解一套 Q；松手时再跑完整搜索。
        var now = Environment.TickCount64;
        if (now - _lastCrossoverFitAt >= 120)
        {
            _lastCrossoverFitAt = now;
            RefitForCrossover(quick: true);
        }

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
        Plot.ReleaseMouseCapture();
        Plot.Cursor = Cursors.Arrow;
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
        _livePoints = Array.Empty<MeasurementPoint>();
        _applied = false;
        Array.Clear(_eqValid);
        SummaryText.Text = string.Empty;
        StatusText.Text = "换档了，之前那份结果已经撤掉。点「开始测量」按新档重测。";
        ApplyButton.IsEnabled = false;
        SetState(MeasureState.Idle);
        DrawPlot();
    }

    private void SetState(MeasureState state)
    {
        _state = state;

        // 测量中不许换档：换了也只会影响下一轮，徒增困惑。
        SpeedBox.IsEnabled = state == MeasureState.Idle;
        StartButton.Content = state switch
        {
            MeasureState.Running => "停止测量",
            MeasureState.Stopping => "等待结束",

            // 手里有结果就是「继续」：再点一次会先把这套套上去，然后立刻再测一轮。
            _ => _plan is null ? "开始测量" : "继续测量",
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
            var (ok, message) = await _viewModel.BeginSweepPreviewAsync(main, bass, preAmp);
            if (!ok)
            {
                StatusText.Text = message ?? "试听叠加低频失败。";
                return;
            }
        }

        if (!_sweepPreview.Play())
        {
            StatusText.Text = "试听放不出来：" + (_sweepPreview.LastError ?? "没有可用的输出设备");
            if (layered)
            {
                await _viewModel.EndSweepPreviewAsync();
            }

            return;
        }

        StatusText.Text = _bassPlan is not null
            ? $"正在放 5 秒扫频：叠上了 {EqCurveFitter.BassFloorHz:0}–{_crossoverHz:0} Hz 的低频矫正。"
                + "只有试听叠低频，测量时不叠。"
            : "正在放 5 秒扫频：20 Hz 一路扫到 20 kHz，听到的就是现在这套 EQ。";

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

        _livePoints = points;

        var now = Environment.TickCount64;
        if (now - _lastLiveDrawAt < 500)
        {
            return;
        }

        _lastLiveDrawAt = now;

        // 同一时间只放一个后台拟合；算得比 500 ms 慢时，中间那些轮直接跳过。
        if (points.Length >= 6 && Interlocked.CompareExchange(ref _liveFitBusy, 1, 0) == 0)
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

            _ = Task.Run(() =>
            {
                CalibrationPlan? live;
                try
                {
                    live = applied && previous is not null
                        ? EqCurveFitter.FitFeedback(
                            target, previous, measurement, density, FeedbackGain, MaxStepDb,
                            crossover, quick: true)
                        : EqCurveFitter.Fit(
                            target, measurement, null, density, baseline,
                            movableBands: false, floorHz: crossover, quick: true);
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
            StatusText.Text = "没有选到麦克风。";
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

        // 上一轮的结果一直留着：测的这二十多秒里，图上画的还是它，测完才被新结果替掉。
        // 继续测之前先把它套上去——这一轮测到的才是「补过之后还剩多少」，而不是又测一遍原样。
        var previous = _plan;
        Trace($"start previousNull={previous is null} applied={_applied}");
        if (previous is not null && !_applied)
        {
            StatusText.Text = "先把上一轮的结果套到「左右」层…";
            try
            {
                // 连低音层一起套：这条结果套过一次，低频矫正就该跟着在；
                // 测量本身仍然不会叠它（下面 BeginCalibrationMeasurementKeepEqAsync 只写主层）。
                await _viewModel.ApplyCalibrationAsync(previous, _bassPlan);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _busy = false;
                SetState(MeasureState.Idle);
                StatusText.Text = "套用失败，这一轮没开始：" + ex.Message;
                return;
            }

            // 套用期间窗口可能已经被关掉了：别再往下开测量。
            token.ThrowIfCancellationRequested();

            if (_viewModel.Mode != MainMode.MainLayer)
            {
                _busy = false;
                SetState(MeasureState.Idle);
                StatusText.Text = "还没进到「左右」层：主窗口里还有没保存的改动，先处理掉，再点「继续测量」。";
                return;
            }

            _applied = true;
            ApplyButton.IsEnabled = false;
            Trace($"applied mode={_viewModel.Mode}");
        }

        // 这一轮测的是不是「上一套已经生效」之后的样子。用在最后的提示语里。
        var continuingWithEq = previous is not null && _applied;

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
        _silentReports = 0;

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
            StatusText.Text = item.Stage;
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
                return;
            }

            // 扫频已经走起来了还一直是 -90：这个端点根本没接信号，调音量没用，得换设备。
            if (item.Fraction > 0.05 && level <= -89)
            {
                _silentReports++;
            }
            else
            {
                _silentReports = 0;
            }

            if (_silentReports >= SilentReportThreshold)
            {
                StatusText.Text = item.Stage + " · 这个端点一直没信号（-90 dB），换上面的麦克风试试";
            }
        });

        var flattened = false;
        try
        {
            // 第一次测量才拉平：测裸喇叭，从零算。
            // 第二次起保留主层 EQ，但显式只写主层——低频矫正不能进测量，
            // 设备不一定发得出那个声音，叠进去测到的就不是主设备本身了。
            var flat = previous is null
                ? await _viewModel.BeginCalibrationMeasurementAsync()
                : await _viewModel.BeginCalibrationMeasurementKeepEqAsync();

            flattened = flat.Flat;
            if (!flat.Flat && flat.Message is not null)
            {
                StatusText.Text = flat.Message;
            }

            // 拉平了，实测是裸喇叭；没拉平，实测里含着上一轮那份 EQ，拟合前得先减掉。
            _baselinePlan = flattened ? null : previous;

            // 拉平这几百毫秒里窗口可能已经被关掉：别再把扫频放出来。
            token.ThrowIfCancellationRequested();

            var measurement = await SweepMeasureService.RunAdaptiveAsync(
                deviceId, stabilityDb, maxRepeats, progress, token, () => _stopRequested, frequencies,
                fixedRepeats: true);
            if (_closed)
            {
                return;
            }

            // 留着这份实测：拖动分界线时按同一个起点重算，不用重新扫频。
            _measurement = measurement;

            // 完整 Q 搜索放后台：要解几遍方程再逐段收尾，放 UI 线程会假死好几秒，
            // 进度条看起来就是「卡在 100%」。
            StatusText.Text = "测完了，正在拟合…";

            var crossover = _crossoverHz;
            var baselineForFit = _baselinePlan?.Points;
            var useFeedback = _applied && previous is not null;
            var appliedPlan = previous;

            var fitted = await Task.Run(() =>
            {
                var plan = useFeedback
                    ? EqCurveFitter.FitFeedback(
                        _target, appliedPlan!, measurement, density, FeedbackGain, MaxStepDb, crossover)
                    : EqCurveFitter.Fit(
                        _target, measurement, null, density, baselineForFit,
                        movableBands: true, floorHz: crossover);

                // 低频矫正按同一份实测单独算：这段不追反馈轮次，因为测量时低音层不叠加，
                // 测回来永远是主设备的裸低频，反相（或补到目标）只有一个答案。
                var bass = FitBassSafe(measurement, _target, baselineForFit, crossover, quick: false);
                return (Plan: plan, Bass: bass);
            }).ConfigureAwait(true);

            if (_closed)
            {
                return;
            }

            _plan = fitted.Plan;
            _bassPlan = fitted.Bass;

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
                + $"gains={GainRange(_plan.Points)} corr={_plan.CorrectionRange.Min:0.0}..{_plan.CorrectionRange.Max:0.0} "
                + $"worst={string.Join(",", worst)}");

            UpdateResultSummary(measurement);

            string status;
            if (flattened)
            {
                status = "测完了。看图确认一下形状；想再准一点就点「继续测量」——先把这套套上去，再测一遍。";
            }
            else if (continuingWithEq)
            {
                status = "测完了：这一轮是在上一套 EQ 已经生效的情况下测的，拟合时已经把它的贡献扣掉了。"
                    + "要再细一点就再点一次「继续测量」。";
            }
            else
            {
                status = "测完了，但这次没有把当前 EQ 拉平，结果里包含现有曲线 —— 套用会叠上去。";
            }

            if (_plan.TargetOffsetDb < -0.05)
            {
                status += $" 设备够不着原始目标，目标已整体压低 {-_plan.TargetOffsetDb:0.0} dB。";
            }

            if (_bassPlan is not null)
            {
                status += $" 绿色是低音层的低频矫正（{BassBandCount(_bassPlan)} 段），"
                    + "只在「试听扫频」时叠加；测量时不叠，因为设备不一定发得出。";
            }

            // 扫完了，绿线退场；结果图不带游标。
            _currentHz = null;

            StatusText.Text = status;
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
                        StatusText.Text += " 下一轮换激进参数。";
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
            if (!_closed)
            {
                StatusText.Text = "已停止。";
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!_closed)
            {
                StatusText.Text = "测量失败：" + ex.Message;
                Progress.Value = 0;
            }
        }
        finally
        {
            if (flattened)
            {
                await _viewModel.EndCalibrationMeasurementAsync();
            }

            _busy = false;
            _stopRequested = false;
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
            // 主层 + 低音层一起写：低频曲线整层替换「低音」层，叠加开关会自动打开。
            var message = await _viewModel.ApplyCalibrationAsync(_plan, _bassPlan);
            _applied = true;
            StatusText.Text = message;
            SummaryText.Text = message;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText.Text = "套用失败：" + ex.Message;
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

        DrawGrid(X, Y, plotWidth, plotHeight);
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
            Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0x9B, 0xFF)),
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 4, 3 },
        };
        for (var i = 0; i <= 200; i++)
        {
            var hz = 20 * Math.Pow(1000, i / 200.0);
            target.Points.Add(new System.Windows.Point(X(hz), Y(TargetCurve.GainAt(_target, hz))));
        }

        Plot.Children.Add(target);

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
                var raw = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
                    StrokeThickness = 1.4,
                };
                foreach (var point in _livePoints)
                {
                    raw.Points.Add(new System.Windows.Point(X(point.FrequencyHz), Y(point.LevelDb)));
                }

                Plot.Children.Add(raw);
            }

            return;
        }

        // 实测
        var measured = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
            StrokeThickness = 1.4,
        };
        foreach (var point in plan.Measured)
        {
            measured.Points.Add(new System.Windows.Point(X(point.FrequencyHz), Y(point.LevelDb)));
        }

        Plot.Children.Add(measured);

        var snapshot = plan.Points.ToSnapshot();

        var eqLine = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xA9, 0x4D)),
            StrokeThickness = 1.8,
            Opacity = _livePlan is null ? 1.0 : 0.55,
        };

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
                needsAnimation = true;
            }

            eqLine.Points.Add(new System.Windows.Point(X(hz), Y(_eqShown[i])));
        }

        Plot.Children.Add(eqLine);

        // 低音层的低频矫正：绿色虚线，只覆盖 20 Hz–分界线。它不在主层的推子范围里，
        // 画出来是为了套用之前就能看见低频要补多少、哪里补得凶。
        var bassPlan = _liveBassPlan ?? _bassPlan;
        if (bassPlan is not null)
        {
            var bassSnapshot = bassPlan.ToSnapshot();
            var bassLine = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x66, 0xD9, 0x7A)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Opacity = _liveBassPlan is null ? 1.0 : 0.7,
            };

            for (var hz = EqCurveFitter.BassFloorHz; hz <= _crossoverHz; hz *= Math.Pow(2, 1.0 / 24))
            {
                bassLine.Points.Add(new System.Windows.Point(X(hz), Y(EqResponse.GainAt(bassSnapshot, hz))));
            }

            // 补最后一个分界点：循环按 1/24 倍频程走，未必正好落在上限上。
            bassLine.Points.Add(new System.Windows.Point(
                X(_crossoverHz),
                Y(EqResponse.GainAt(bassSnapshot, _crossoverHz))));

            Plot.Children.Add(bassLine);
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
