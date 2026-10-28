using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;
using PeaceWpf.Theme;

namespace PeaceWpf.Controls;

/// <summary>
/// 左键点在空白处：请求在落点新建一个点 —— 横向给频率，纵向给增益。
/// 视图收到 CreatedPointId 后直接进入拖动。
/// </summary>
public sealed class PointCreateEventArgs : EventArgs
{
    public PointCreateEventArgs(double frequencyHz, double gainDb)
    {
        FrequencyHz = frequencyHz;
        GainDb = gainDb;
    }

    public double FrequencyHz { get; }

    /// <summary>落点的增益，dB。已经扣掉预增益，可以直接当点的增益用。</summary>
    public double GainDb { get; }

    /// <summary>由 ViewModel 填回新建点的 Id；-1 表示没建成（点太多或位置不合适）。</summary>
    public int CreatedPointId { get; set; } = -1;
}

/// <summary>点被拖动：频率与增益一起给，视图不替 ViewModel 做夹取。</summary>
public sealed class PointMoveEventArgs : EventArgs
{
    public PointMoveEventArgs(int id, double frequencyHz, double gainDb)
    {
        Id = id;
        FrequencyHz = frequencyHz;
        GainDb = gainDb;
    }

    public int Id { get; }

    public double FrequencyHz { get; }

    public double GainDb { get; }
}

/// <summary>只带点 Id 的事件。</summary>
public sealed class PointEventArgs : EventArgs
{
    public PointEventArgs(int id)
    {
        Id = id;
    }

    public int Id { get; }
}

/// <summary>滚轮改 Q。</summary>
public sealed class PointQualityEventArgs : EventArgs
{
    public PointQualityEventArgs(int id, double quality)
    {
        Id = id;
        Quality = quality;
    }

    public int Id { get; }

    public double Quality { get; }
}

/// <summary>只带风味下标的点击事件（点中某条风味的手柄）。</summary>
public sealed class FlavorIndexEventArgs : EventArgs
{
    public FlavorIndexEventArgs(int index)
    {
        Index = index;
    }

    /// <summary>风味下标，顺序同 <see cref="ToneBands.All"/>。</summary>
    public int Index { get; }
}

/// <summary>拖动风味手柄：频率和增益一起给，横向改频率、纵向改增益。</summary>
public sealed class FlavorMoveEventArgs : EventArgs
{
    public FlavorMoveEventArgs(int index, double frequencyHz, double gainDb)
    {
        Index = index;
        FrequencyHz = frequencyHz;
        GainDb = gainDb;
    }

    public int Index { get; }

    public double FrequencyHz { get; }

    public double GainDb { get; }
}

/// <summary>滚轮改某条风味的 Q。</summary>
public sealed class FlavorQualityEventArgs : EventArgs
{
    public FlavorQualityEventArgs(int index, double quality)
    {
        Index = index;
        Quality = quality;
    }

    public int Index { get; }

    public double Quality { get; }
}

/// <summary>
/// 自绘频响曲线。曲线本身就是编辑器：
/// 左键按住曲线抓频率最近的推子、空白处按下建点并拖动、左键按住点拖动（左右改频率、上下改增益）、
/// 滚轮改 Q、右键删点（首尾端点右键归零）、悬停高亮并显示数值。
///
/// 控件只负责“画”和“把鼠标意图翻译成事件”，夹取规则、撤回、脏标记都在 Core 里。
/// 只读态（<see cref="IsEditable"/> 为假）不响应任何鼠标操作，曲线整体变暗。
/// </summary>
public sealed class EqGraphView : FrameworkElement
{
    public const double MinHz = EqPointList.MinFrequencyHz;
    public const double MaxHz = EqPointList.MaxFrequencyHz;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(EqPointList),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GainRangeProperty = DependencyProperty.Register(
        nameof(GainRange),
        typeof(double),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(15.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable),
        typeof(bool),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ReferencePointsProperty = DependencyProperty.Register(
        nameof(ReferencePoints),
        typeof(EqPointList),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid),
        typeof(bool),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 预增益（dB）：整条曲线一起上下平移的量，等于 APO 配置里那行 Preamp。
    ///
    /// 它加在每个采样点、每个手柄和命中判定上 —— 图和声音看到的是同一件事：
    /// 拖它，曲线和点一起平移，形状不变。这和「给每段各加 0.2 dB」是两回事，
    /// 后者只会把峰拔高、峰间留在原地。
    /// </summary>
    public static readonly DependencyProperty PreAmpDbProperty = DependencyProperty.Register(
        nameof(PreAmpDb),
        typeof(double),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 实时频谱：按对数频率等分的 dB 值。null 表示不画（没开、抓不到音频，或者还没攒够一帧数据）。
    /// </summary>
    public static readonly DependencyProperty SpectrumProperty = DependencyProperty.Register(
        nameof(Spectrum),
        typeof(float[]),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 风味层：八条固定滤波器。画成一条单独的曲线（叠加量），
    /// 全平时什么都不画。它不进 <see cref="Points"/>，所以点编辑碰不到它。
    /// </summary>
    public static readonly DependencyProperty FlavorsProperty = DependencyProperty.Register(
        nameof(Flavors),
        typeof(FlavorSet),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>八条风味的颜色，顺序同 <see cref="ToneBands.All"/>；由 <c>TonePalette</c> 提供。</summary>
    public static readonly DependencyProperty FlavorBrushesProperty = DependencyProperty.Register(
        nameof(FlavorBrushes),
        typeof(IReadOnlyList<Brush>),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>当前选中的风味下标；-1 表示没选。</summary>
    public static readonly DependencyProperty SelectedFlavorIndexProperty = DependencyProperty.Register(
        nameof(SelectedFlavorIndex),
        typeof(int),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>风味手柄能不能拖。只在「左右」编辑态为真；图标本身一直画。</summary>
    public static readonly DependencyProperty FlavorHandlesInteractiveProperty = DependencyProperty.Register(
        nameof(FlavorHandlesInteractive),
        typeof(bool),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => ((EqGraphView)d).OnInteractiveChanged((bool)e.NewValue)));

    /// <summary>编辑的是低音层：正在编辑的曲线用紫色，左右用蓝色。</summary>
    public static readonly DependencyProperty BassEditingProperty = DependencyProperty.Register(
        nameof(BassEditing),
        typeof(bool),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((EqGraphView)d).AnimateTint(layerSwitch: true)));

    public bool BassEditing
    {
        get => (bool)GetValue(BassEditingProperty);
        set => SetValue(BassEditingProperty, value);
    }

    /// <summary>
    /// 「叠加低音修正」那个勾选。只影响进编辑那一刻的换形起点：
    /// 没叠加时图上原本没有低音这条线，进低音编辑只能从 0 dB 平线长出来，
    /// 不能把屏幕上的左右曲线揉成低音的。
    /// </summary>
    public static readonly DependencyProperty BassOverlayEnabledProperty = DependencyProperty.Register(
        nameof(BassOverlayEnabled),
        typeof(bool),
        typeof(EqGraphView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool BassOverlayEnabled
    {
        get => (bool)GetValue(BassOverlayEnabledProperty);
        set => SetValue(BassOverlayEnabledProperty, value);
    }

    private static readonly Color BassAccent = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color WhiteAccent = Color.FromRgb(0xF2, 0xF5, 0xF8);

    /// <summary>
    /// 自带光标：悬停在能抓的地方是张开的手，按住时换成握住的手。
    /// 系统光标里没有这两种，只能从资源里取；资源丢了就退回系统手形，不影响操作。
    /// </summary>
    private static Cursor? _openHand;
    private static Cursor? _grabHand;

    private static Cursor OpenHand => _openHand ??= LoadCursorAsset("cursor-open.cur");

    private static Cursor GrabHand => _grabHand ??= LoadCursorAsset("cursor-grab.cur");

    private static Cursor LoadCursorAsset(string fileName)
    {
        try
        {
            var info = Application.GetResourceStream(
                new Uri($"pack://application:,,,/assets/{fileName}", UriKind.Absolute));
            return info is null ? Cursors.Hand : new Cursor(info.Stream);
        }
        catch (Exception)
        {
            return Cursors.Hand;
        }
    }

    private double _tintBlend = 1;
    private Color _tintFrom = WhiteAccent;
    private Color _tintLast = WhiteAccent;
    private bool _tintFadeIn;
    private DispatcherTimer? _tintTimer;

    private double[]? _curveTarget;
    private double[] _curveFrom = Array.Empty<double>();
    private double _curveBlend = 1;
    private DispatcherTimer? _curveTimer;

    /// <summary>
    /// 进编辑那一刻屏幕上的旧曲线（只读态的采样）。下一帧换成这层的形状之前先记下它，
    /// 让「合并 → 左右 / 合并 → 低音」是一次变形，而不是一次跳变。
    /// </summary>
    private double[]? _enterMorphFrom;
    private bool _enterMorph;

    /// <summary>退出编辑那一刻屏幕上的曲线；只读态第一帧拿它当起点长回合并曲线。</summary>
    private double[]? _exitMorphFrom;

    /// <summary>「按住曲线」的判定半径：按下点离画出来的曲线不超过这么多像素，就算抓在曲线上。</summary>
    private const double CurvePickPixels = 8;
    /// <summary>
    /// 抓推子的横向范围：鼠标左右这么宽的一段频率里有推子才抓，一个都没有就在落点新建。
    ///
    /// 用倍频程而不是像素 —— 频率轴是 20 Hz–20 kHz 的一条对数轴，五分之一倍频程在任何窗口宽度下
    /// 都只占轴的 2%，4K 的大窗口和 1080p 的小窗口手感一致；写死 32 px 会在大窗口里几乎抓不到、
    /// 小窗口里又太黏。要调手感就改这一个数。
    /// </summary>
    private const double SnapOctaves = 0.2;

    /// <summary>
    /// 最近一次画出来的主曲线采样（dB）。命中判定直接查它，不必再算一遍频响；
    /// 进编辑换形时也拿它当起点，接的是屏幕上真正看到的那条线。
    /// </summary>
    private double[]? _curveSamples;
    private Rect _curveSamplesPlot;

    /// <summary>开关叠加低音这类整条曲线换形：按采样点插值，形状自己长过去。</summary>
    private void OnCurveTick(object? sender, EventArgs e)
    {
        _curveBlend = Math.Min(1, _curveBlend + 0.08);
        if (_curveBlend >= 1)
        {
            _curveTimer?.Stop();
        }

        InvalidateVisual();
    }

    private static bool SameSamples(double[] a, double[] b)
    {
        for (var i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 0.02)
            {
                return false;
            }
        }

        return true;
    }

    private Color TintTarget => !FlavorHandlesInteractive ? WhiteAccent : BassEditing ? BassAccent : Accent;

    /// <summary>
    /// 进出编辑：颜色从当前色渐变到目标色；
    /// 左右/低音互换：新那条线从低透明度淡入，把底下的灰色虚线盖住。
    /// </summary>
    private void AnimateTint(bool layerSwitch)
    {
        _tintFrom = _tintLast;
        _tintBlend = 0;
        _tintFadeIn = layerSwitch && FlavorHandlesInteractive;
        if (_tintTimer is null)
        {
            _tintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _tintTimer.Tick += OnTintTick;
        }

        _tintTimer.Start();
    }

    /// <summary>
    /// 进编辑：先把上一帧画出来的曲线留一份，下一帧在 <see cref="DrawCurve"/> 里拿它做起点。
    /// 起点用「画出来的」而不是「算出来的」，只读态自己正在形变时也不会接不上。
    /// 退出同理：留的是刚编辑完的形状，而不是进编辑之前那条旧曲线。
    /// </summary>
    private void OnInteractiveChanged(bool interactive)
    {
        if (interactive && _curveSamples is { Length: > 1 } samples)
        {
            _enterMorphFrom = (double[])samples.Clone();
            _enterMorph = true;
            _exitMorphFrom = null;
        }
        else if (!interactive)
        {
            _enterMorphFrom = null;
            _enterMorph = false;
            _exitMorphFrom = _curveSamples is { Length: > 1 } last ? (double[])last.Clone() : null;
        }

        AnimateTint(layerSwitch: false);
    }

    /// <summary>让曲线从 <see cref="_curveFrom"/> 插到当前形状；16 ms 一帧，约 0.2 秒走完。</summary>
    private void StartCurveMorph()
    {
        _curveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _curveTimer.Tick -= OnCurveTick;
        _curveTimer.Tick += OnCurveTick;
        _curveTimer.Start();
    }

    /// <summary>换形期间人动手了：直接把曲线交给当前数据，不再让动画拖后腿。</summary>
    private void EndCurveMorph()
    {
        _enterMorph = false;
        _enterMorphFrom = null;
        if (_curveBlend < 1)
        {
            _curveBlend = 1;
            _curveTimer?.Stop();
        }
    }

    private void OnTintTick(object? sender, EventArgs e)
    {
        _tintBlend = Math.Min(1, _tintBlend + 0.07);
        if (_tintBlend >= 1)
        {
            _tintLast = TintTarget;
            _tintTimer?.Stop();
        }

        InvalidateVisual();
    }

    private static Color LerpColor(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    private Pen CurrentCurvePen()
    {
        if (!FlavorHandlesInteractive)
        {
            return ReadOnlyCurvePen;
        }

        var color = LerpColor(_tintFrom, TintTarget, Math.Clamp(_tintBlend, 0, 1));
        var opacity = _tintFadeIn ? 0.15 + 0.85 * _tintBlend : 1;
        return Freeze(new Pen(new SolidColorBrush(color) { Opacity = opacity }, 2));
    }

    private static readonly Color Accent = Color.FromRgb(0x4C, 0xC2, 0xFF);

    private static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)));
    private static readonly Pen GridPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(34, 128, 128, 128)), 1));
    private static readonly Pen ZeroPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(96, 128, 128, 128)), 1));
    private static readonly Pen CurvePen = Freeze(new Pen(new SolidColorBrush(Accent), 2));

    /// <summary>没有进入编辑时曲线走白色：一眼能看出「现在只是看，不是改」。</summary>
    private static readonly Pen ReadOnlyCurvePen =
        Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0xF2, 0xF5, 0xF8)), 2));

    /// <summary>当前层曲线的描边：先把线画粗一圈暗色，风味从底下穿过时也不会把它吃掉。</summary>
    private static readonly Pen CurveOutlinePen =
        Freeze(new Pen(new SolidColorBrush(Color.FromArgb(220, 8, 12, 18)), 4.5));
    private static readonly Pen ReferencePen = CreateReferencePen();
    private static readonly Brush HandleBrush = Freeze(new SolidColorBrush(Accent));
    private static readonly Brush EndpointBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF5, 0xF8)));
    private static readonly Pen HandlePen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(220, 8, 12, 18)), 1.2));
    private static readonly Brush HaloBrush = Freeze(new SolidColorBrush(Color.FromArgb(48, 0x4C, 0xC2, 0xFF)));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromArgb(168, 128, 128, 128)));
    private static readonly Brush PillBrush = Freeze(new SolidColorBrush(Color.FromArgb(235, 18, 22, 28)));
    private static readonly Pen PillPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(70, 0x4C, 0xC2, 0xFF)), 1));
    private static readonly Brush SpectrumFill = Freeze(new SolidColorBrush(Color.FromArgb(44, 0x4C, 0xC2, 0xFF)));
    private static readonly Pen SpectrumPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(132, 0x4C, 0xC2, 0xFF)), 1));
    private static readonly Typeface Face = new("Segoe UI");

    /// <summary>频谱那一块占曲线区多高：贴着底部的三成。</summary>
    private const double SpectrumHeightRatio = 0.32;

    /// <summary>频谱图的下边界。和自测里那个 -90 dB 的底噪不一样：
    /// 画图用不着把底噪也铺出来，-72 已经够平了。</summary>
    private const double SpectrumFloorDb = -72;

    /// <summary>风味图标的字号。</summary>
    private const double FlavorIconSize = 13;

    /// <summary>风味图标离控制点多远：抬在上、削在下。</summary>
    private const double FlavorIconOffset = 16;

    private int _dragId = -1;
    private int _hoverId = -1;

    /// <summary>
    /// 当前这次拖动是不是“抓住曲线”：是的话每次移动都要反推增益，让曲线经过鼠标，
    /// 而不是把推子圆心直接摆在光标上。拖手柄不走这条，那时用户抓的是推子本身。
    /// </summary>
    private bool _dragThroughCurve;

    /// <summary>正在拖的风味下标；-1 表示没在拖。</summary>
    private int _flavorDragIndex = -1;

    /// <summary>鼠标停在哪个风味手柄上；-1 表示没有。</summary>
    private int _flavorHoverIndex = -1;

    private Rect _plot;

    /// <summary>网格与刻度文字画一次就冻住，尺寸、量程或 DPI 变了才重建。</summary>
    private DrawingGroup? _gridCache;
    private Size _gridCachePlotSize;
    private double _gridCacheRange = double.NaN;
    private double _gridCacheDpi = double.NaN;

    public event EventHandler<PointCreateEventArgs>? CreatePointRequested;

    /// <summary>拖动过程中的实时预览。不压撤回栈。</summary>
    public event EventHandler<PointMoveEventArgs>? PointMovePreview;

    /// <summary>一次拖动结束（松手）。ViewModel 在这里压一步撤回。</summary>
    public event EventHandler? PointMoveCommitted;

    public event EventHandler<PointEventArgs>? PointDeleteRequested;

    /// <summary>
    /// 右键首尾两个端点：它们删不掉，右键在它们身上只做“增益归零”这一件事。
    /// </summary>
    public event EventHandler<PointEventArgs>? PointResetRequested;

    public event EventHandler<PointQualityEventArgs>? QualityChanged;

    /// <summary>点中了某条风味的手柄：下拉要跟着切到它。</summary>
    public event EventHandler<FlavorIndexEventArgs>? FlavorSelected;

    /// <summary>拖动风味手柄时实时给频率（只给 X，增益不跟着鼠标走）。</summary>
    public event EventHandler<FlavorMoveEventArgs>? FlavorFrequencyPreview;

    /// <summary>风味手柄松开：ViewModel 在这一步落盘。</summary>
    public event EventHandler<FlavorIndexEventArgs>? FlavorFrequencyCommitted;

    /// <summary>滚轮改某条风味的 Q。</summary>
    public event EventHandler<FlavorQualityEventArgs>? FlavorQualityChanged;

    /// <summary>右键某条风味：把增益归零（不是删除）。</summary>
    public event EventHandler<FlavorIndexEventArgs>? FlavorResetRequested;

    public EqPointList? Points
    {
        get => (EqPointList?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double GainRange
    {
        get => (double)GetValue(GainRangeProperty);
        set => SetValue(GainRangeProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>整条曲线的预增益（dB）。</summary>
    public double PreAmpDb
    {
        get => (double)GetValue(PreAmpDbProperty);
        set => SetValue(PreAmpDbProperty, value);
    }

    /// <summary>另一层的曲线，画成灰色虚线作参考。为空时不画。</summary>
    public EqPointList? ReferencePoints
    {
        get => (EqPointList?)GetValue(ReferencePointsProperty);
        set => SetValue(ReferencePointsProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>系统输出的实时频谱，贴着曲线底部画。为 null 时什么都不画。</summary>
    public float[]? Spectrum
    {
        get => (float[]?)GetValue(SpectrumProperty);
        set => SetValue(SpectrumProperty, value);
    }

    /// <summary>风味层：八条固定滤波器。全平时不画。</summary>
    public FlavorSet? Flavors
    {
        get => (FlavorSet?)GetValue(FlavorsProperty);
        set => SetValue(FlavorsProperty, value);
    }

    /// <summary>八条风味的颜色；长度不够时手柄退回主题色。</summary>
    public IReadOnlyList<Brush>? FlavorBrushes
    {
        get => (IReadOnlyList<Brush>?)GetValue(FlavorBrushesProperty);
        set => SetValue(FlavorBrushesProperty, value);
    }

    /// <summary>选中的风味下标，画成带光晕的那一个。</summary>
    public int SelectedFlavorIndex
    {
        get => (int)GetValue(SelectedFlavorIndexProperty);
        set => SetValue(SelectedFlavorIndexProperty, value);
    }

    /// <summary>风味手柄能不能拖。图标任何时候都画，只读态只是不能拖。</summary>
    public bool FlavorHandlesInteractive
    {
        get => (bool)GetValue(FlavorHandlesInteractiveProperty);
        set => SetValue(FlavorHandlesInteractiveProperty, value);
    }

    private double? _sweepHz;

    /// <summary>当前扫频频率：绿色竖线跟着它走，顶上标着频率。null 表示没在扫。</summary>
    public double? SweepHz
    {
        get => _sweepHz;
        set
        {
            if (_sweepHz == value)
            {
                return;
            }

            _sweepHz = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// 当前扫频频率那条绿线和它的标签。画在一切曲线之后，谁也盖不住。
    /// </summary>
    private void DrawSweepCursor(DrawingContext dc)
    {
        if (_sweepHz is not double hz || _plot.Width <= 0 || _plot.Height <= 0)
        {
            return;
        }

        var brush = new SolidColorBrush(Color.FromRgb(0x5A, 0xD6, 0x7A));
        brush.Freeze();
        var pen = new Pen(brush, 1.4);
        pen.Freeze();

        var x = FreqToX(hz);
        dc.DrawLine(pen, new Point(x, _plot.Top), new Point(x, _plot.Bottom));

        var text = new FormattedText(
            $"{hz:0} Hz",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            10,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var left = Math.Clamp(x + 4, _plot.Left + 2, _plot.Right - text.Width - 2);
        dc.DrawText(text, new Point(left, _plot.Top + 2));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 80 || height < 80)
        {
            return;
        }

        _plot = new Rect(46, 14, width - 62, height - 48);
        dc.DrawRoundedRectangle(BackgroundBrush, null, _plot, 8, 8);

        if (ShowGrid)
        {
            DrawGrid(dc);
        }

        DrawSpectrum(dc);

        var points = Points;
        if (points is null || points.IsEmpty)
        {
            DrawCenteredHint(dc, "还没有读到参数");
            return;
        }

        // 绘制顺序 = 叠放顺序：参考虚线最底，风味往上，左右曲线压在最上面不被遮挡，
        // 最后才是可拖的手柄。
        DrawReferenceCurve(dc, ReferencePoints);
        DrawFlavorCurve(dc);
        DrawSweepCurves(dc);
        DrawCurve(dc, points);
        DrawHandles(dc, points);
        DrawFlavorHandles(dc);

        var labelId = _dragId >= 0 ? _dragId : _hoverId;
        if (labelId >= 0 && points.Find(labelId) is { } labelPoint)
        {
            DrawLabel(dc, labelPoint);
            DrawGuideLine(dc, labelPoint);
        }

        DrawSweepCursor(dc);
    }

    // ---- 连续扫频实测：最多留 5 条，越新越亮 ----

    /// <summary>主界面扫频测到的曲线，按时间先后排：最后一条最新。</summary>
    private readonly List<IReadOnlyList<(double Hz, double Db)>> _sweepCurves = new();

    /// <summary>中键合并出来的那条（多位置功率平均）。</summary>
    private IReadOnlyList<(double Hz, double Db)>? _sweepAverage;

    /// <summary>
    /// 扫频曲线的平滑半径（点）。0 = 不平滑（默认）。
    /// 用点数而不是倍频程：扫频时间一变、每倍频程点数就变，
    /// 按点数算才能保证同一个档位的手感不变。
    /// 只影响画出来的样子，数据本身不动：合并、判断驻波都按原始值算。
    /// </summary>
    public int SweepSmoothingPoints { get; set; }

    // 几何缓存：尺寸、DPI、平滑档位、数据版本任一变化才重算。
    // 拖窗口时每次 OnRender 都重算「平滑 + 分列」是白烧 CPU——20 秒档 3827 点、25 点半径、5 条线，
    // 一帧就是几十万次乘加，拖动窗口会明显发涩。
    private readonly List<Geometry?> _sweepGeometries = new();
    private Geometry? _sweepAverageGeometry;
    private (double W, double H, double Dpi, int Smooth, int Version) _sweepGeometryKey;
    private bool _sweepGeometryReady;
    private int _sweepVersion;

    /// <summary>
    /// 换一组实测曲线。<paramref name="average"/> 为 null 表示还没合并过。
    /// </summary>
    public void SetSweepCurves(
        IReadOnlyList<IReadOnlyList<(double Hz, double Db)>> curves,
        IReadOnlyList<(double Hz, double Db)>? average)
    {
        _sweepCurves.Clear();
        _sweepCurves.AddRange(curves);
        _sweepAverage = average;
        _sweepVersion++;
        _sweepGeometryReady = false;
        InvalidateVisual();
    }

    /// <summary>
    /// 画绿色实测曲线。旧的压暗、新的提亮：一眼能看出「这条是上一次，那条是这一次」，
    /// 而叠着看的时候又能看出哪一段是稳的、哪一段在动——驻波就是这么看出来的。
    /// </summary>
    private void DrawSweepCurves(DrawingContext dc)
    {
        if (_sweepCurves.Count == 0 && _sweepAverage is null)
        {
            return;
        }

        // 尺寸或 DPI 变了要重算：列是按物理像素分的，窗口一拉宽，每列包含的点就变了。
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var key = (ActualWidth, ActualHeight, dpi, SweepSmoothingPoints, _sweepVersion);
        if (!_sweepGeometryReady || !key.Equals(_sweepGeometryKey))
        {
            _sweepGeometries.Clear();
            foreach (var curve in _sweepCurves)
            {
                _sweepGeometries.Add(curve.Count < 2 ? null : BuildCurve(Shape(curve)));
            }

            _sweepAverageGeometry = _sweepAverage is { Count: > 1 } average ? BuildCurve(Shape(average)) : null;
            _sweepGeometryKey = key;
            _sweepGeometryReady = true;
        }

        var count = _sweepGeometries.Count;
        for (var i = 0; i < count; i++)
        {
            if (_sweepGeometries[i] is not { } geometry)
            {
                continue;
            }

            // 最旧一档压到 0.22，最新一档 1.0。
            var t = count == 1 ? 1.0 : i / (double)(count - 1);
            var alpha = (byte)Math.Round((0.22 + 0.78 * t) * 255);
            dc.DrawGeometry(null, new Pen(SweepBrush(alpha), 1.2), geometry);
        }

        if (_sweepAverageGeometry is { } merged)
        {
            // 合并那条用更亮更粗的绿：它就是「几个位置都还在」的东西。
            dc.DrawGeometry(null, new Pen(SweepBrush(255, bright: true), 2.0), merged);
        }
    }

    /// <summary>按当前平滑档位处理一下再画；关档原样返回，数据不动。</summary>
    private IReadOnlyList<(double Hz, double Db)> Shape(IReadOnlyList<(double Hz, double Db)> curve) =>
        SweepSmoothingPoints > 0
            ? PeaceWpf.Core.Audio.ChirpSweepAnalyzer.SmoothByPoints(curve, SweepSmoothingPoints)
            : curve;

    private static Brush SweepBrush(byte alpha, bool bright = false)
    {
        var brush = new SolidColorBrush(Color.FromArgb(
            alpha,
            bright ? (byte)0xB4 : (byte)0x5A,
            bright ? (byte)0xFF : (byte)0xD6,
            bright ? (byte)0xC8 : (byte)0x7A));
        brush.Freeze();
        return brush;
    }

    private StreamGeometry BuildCurve(IReadOnlyList<(double Hz, double Db)> curve)
    {
        // 每列**物理像素**里的点合并成一个（取中位数）。一条扫频曲线有两三千个点、画布只有几百 px，
        // 每列挤着好几个点，逐点连出来就是一根竖着抖的毛线。
        //
        // 按设备像素而不是 DIP 分列：150% 缩放下一个 DIP 是 1.5 个设备像素，
        // 按 DIP 分会把三分之一的分辨率白白丢掉，看起来就是「列很粗」。
        //
        // 这里只改显示、不改数据：峰和坑还在原来那一列，只是同列的点不再互相拉扯。
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale <= 0)
        {
            scale = 1;
        }

        var buckets = new Dictionary<int, List<double>>();
        var columns = new List<int>();

        foreach (var (hz, db) in curve)
        {
            var x = (int)Math.Round(FreqToX(hz) * scale);
            if (!buckets.TryGetValue(x, out var values))
            {
                values = new List<double>(4);
                buckets[x] = values;
                columns.Add(x);
            }

            values.Add(db);
        }

        columns.Sort();

        var geometry = new StreamGeometry();
        if (columns.Count == 0)
        {
            geometry.Freeze();
            return geometry;
        }

        using (var ctx = geometry.Open())
        {
            var first = columns[0];
            ctx.BeginFigure(new Point(first / scale, DbToY(MedianOf(buckets[first]))), false, false);
            for (var i = 1; i < columns.Count; i++)
            {
                var x = columns[i];
                ctx.LineTo(new Point(x / scale, DbToY(MedianOf(buckets[x]))), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static double MedianOf(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2;
    }

    /// <summary>
    /// 网格和刻度文字不随频谱变，只在尺寸、量程或 DPI 变时重建。
    /// 这一块每帧要排十来个文本，实测占 OnRender 的一半上下；缓存之后每帧只剩一次 DrawDrawing。
    /// </summary>
    private void DrawGrid(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var range = EffectiveRange;

        if (_gridCache is null
            || _gridCachePlotSize != _plot.Size
            || _gridCacheRange != range
            || _gridCacheDpi != dpi)
        {
            _gridCache = BuildGridCache(dpi, range);
            _gridCachePlotSize = _plot.Size;
            _gridCacheRange = range;
            _gridCacheDpi = dpi;
        }

        dc.DrawDrawing(_gridCache);
    }

    private DrawingGroup BuildGridCache(double dpi, double range)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            double[] frequencies = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };

            foreach (var hz in frequencies)
            {
                var x = FreqToX(hz);
                dc.DrawLine(GridPen, new Point(x, _plot.Top), new Point(x, _plot.Bottom));

                var label = hz >= 1000 ? $"{hz / 1000:0.#}k" : $"{hz:0}";
                var text = Text(label, 10, TextBrush, dpi);
                dc.DrawText(text, new Point(x - text.Width / 2, _plot.Bottom + 4));
            }

            // 以 0 为基准向上下铺刻度：这样无论预增益把范围撑成多少，0 dB 那一条总是在，
            // 用的还是下面这条既有的 ZeroPen 分支，不另外加线。
            var step = range <= 6 ? 1.5 : (range <= 15 ? 3 : 6);
            var steps = (int)Math.Floor(range / step);
            for (var k = -steps; k <= steps; k++)
            {
                var db = k * step;
                var y = DbToY(db);
                dc.DrawLine(Math.Abs(db) < 0.001 ? ZeroPen : GridPen, new Point(_plot.Left, y), new Point(_plot.Right, y));

                var label = Math.Abs(db) < 0.001 ? "0 dB" : $"{db:+0;-0}";
                var text = Text(label, 10, TextBrush, dpi);
                dc.DrawText(text, new Point(4, y - text.Height / 2));
            }
        }

        group.Freeze();
        return group;
    }

    /// <summary>
    /// 底部三成的实时频谱：一条带子对应一段对数频率，画成半透明面积 + 一条亮线。
    /// 它只是背景，不吃鼠标事件，也不影响点编辑。
    /// </summary>
    private void DrawSpectrum(DrawingContext dc)
    {
        var spectrum = Spectrum;
        if (spectrum is null || spectrum.Length < 2 || _plot.Width <= 2 || _plot.Height <= 2)
        {
            return;
        }

        var strip = new Rect(
            _plot.Left,
            _plot.Bottom - (_plot.Height * SpectrumHeightRatio),
            _plot.Width,
            _plot.Height * SpectrumHeightRatio);

        var fill = new StreamGeometry();
        using (var ctx = fill.Open())
        {
            ctx.BeginFigure(new Point(_plot.Left, strip.Bottom), true, true);
            ctx.LineTo(SpectrumPoint(0, spectrum, strip), true, false);
            TraceSpectrum(ctx, spectrum, strip);
            ctx.LineTo(new Point(_plot.Right, strip.Bottom), true, false);
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(SpectrumPoint(0, spectrum, strip), false, false);
            TraceSpectrum(ctx, spectrum, strip);
        }

        fill.Freeze();
        line.Freeze();

        dc.DrawGeometry(SpectrumFill, null, fill);
        dc.DrawGeometry(null, SpectrumPen, line);
    }

    private Point SpectrumPoint(int index, float[] spectrum, Rect strip) =>
        new(BandToX(index, spectrum.Length), SpectrumToY(spectrum[index], strip));

    private double BandToX(int index, int count) =>
        _plot.Left + (_plot.Width * index / (count - 1.0));

    private static double SpectrumToY(float db, Rect strip)
    {
        var t = Math.Clamp((Math.Clamp(db, SpectrumFloorDb, 0) - SpectrumFloorDb) / -SpectrumFloorDb, 0, 1);
        return strip.Bottom - (strip.Height * t);
    }

    /// <summary>
    /// 相邻两条带子之间画成一段平滑曲线，不再是折线。
    ///
    /// 用 Catmull-Rom 转三次贝塞尔拿斜率，控制点的纵坐标再夹进两端之间：
    /// 曲线因此是连续的，但永远不会在峰旁边鼓出一个比两侧都高的包。
    /// 圆滑归圆滑，数据里没有的起伏也不许画出来。
    /// </summary>
    private void TraceSpectrum(StreamGeometryContext ctx, float[] spectrum, Rect strip)
    {
        var count = spectrum.Length;
        for (var i = 0; i < count - 1; i++)
        {
            var previous = SpectrumPoint(Math.Max(i - 1, 0), spectrum, strip);
            var start = SpectrumPoint(i, spectrum, strip);
            var end = SpectrumPoint(i + 1, spectrum, strip);
            var next = SpectrumPoint(Math.Min(i + 2, count - 1), spectrum, strip);

            var first = new Point(
                start.X + ((end.X - previous.X) / 6),
                Between(start.Y + ((end.Y - previous.Y) / 6), start.Y, end.Y));
            var second = new Point(
                end.X - ((next.X - start.X) / 6),
                Between(end.Y - ((next.Y - start.Y) / 6), start.Y, end.Y));

            ctx.BezierTo(first, second, end, true, false);
        }
    }

    private static double Between(double value, double a, double b) =>
        Math.Clamp(value, Math.Min(a, b), Math.Max(a, b));

    private void DrawCurve(DrawingContext dc, EqPointList points)
    {
        var snapshot = points.ToSnapshot();
        var samples = (int)Math.Clamp(_plot.Width, 160, 900);
        var values = EqResponse.Curve(snapshot, samples, MinHz, MaxHz);
        if (values.Length < 2)
        {
            return;
        }

        // 进编辑那一刻：先把只读态画着的曲线留住当起点，这一帧开始长成这层自己的形状。
        // 没叠低音时进低音编辑，图上原本没有这条线 —— 起点是 0 dB 平线，不是把左右曲线揉过去。
        if (_enterMorph && FlavorHandlesInteractive)
        {
            _enterMorph = false;
            var from = BassEditing && !BassOverlayEnabled ? new double[values.Length] : _enterMorphFrom;
            if (from is not null && from.Length == values.Length)
            {
                _curveFrom = from;
                _curveBlend = 0;
                StartCurveMorph();
            }

            _enterMorphFrom = null;
        }

        // 只读态下曲线换了形状（退出编辑、勾/取消「叠加低音修正」）：从上一份采样插值过去。
        // 退出编辑的起点优先用刚编辑完的那条；之后各帧仍旧和 _curveTarget 比。
        var draw = values;
        if (!FlavorHandlesInteractive)
        {
            var previous = _exitMorphFrom ?? _curveTarget;
            _exitMorphFrom = null;
            if (previous is { } prev
                && prev.Length == values.Length
                && _curveBlend >= 1
                && !SameSamples(prev, values))
            {
                _curveFrom = prev;
                _curveBlend = 0;
                StartCurveMorph();
            }
        }

        if (_curveBlend < 1 && _curveFrom.Length == values.Length)
        {
            draw = new double[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                draw[i] = _curveFrom[i] + (values[i] - _curveFrom[i]) * _curveBlend;
            }
        }

        if (!FlavorHandlesInteractive)
        {
            _curveTarget = values;
        }

        // 屏幕上画的是 draw，留一份给「按住曲线」的命中判定，省得每次鼠标移动重算频响。
        _curveSamples = draw;
        _curveSamplesPlot = _plot;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(_plot.Left, DbToY(draw[0] + PreAmpDb)), false, false);
            for (var i = 1; i < draw.Length; i++)
            {
                var x = _plot.Left + _plot.Width * i / (draw.Length - 1);
                ctx.LineTo(new Point(x, DbToY(draw[i] + PreAmpDb)), true, false);
            }
        }

        geometry.Freeze();

        // 当前层曲线永远压在最上面：先垫一圈暗色描边，再画实色，风味从下面穿过也不会盖住它。
        // 只读态换成白色，编辑态才用强调色。
        dc.DrawGeometry(null, CurveOutlinePen, geometry);
        dc.DrawGeometry(null, CurrentCurvePen(), geometry);
    }

    private void DrawReferenceCurve(DrawingContext dc, EqPointList? reference)
    {
        if (reference is null || reference.IsEmpty)
        {
            return;
        }

        var snapshot = reference.ToSnapshot();
        var samples = (int)Math.Clamp(_plot.Width, 160, 900);
        var values = EqResponse.Curve(snapshot, samples, MinHz, MaxHz);
        if (values.Length < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(_plot.Left, DbToY(values[0] + PreAmpDb)), false, false);
            for (var i = 1; i < values.Length; i++)
            {
                var x = _plot.Left + _plot.Width * i / (values.Length - 1);
                ctx.LineTo(new Point(x, DbToY(values[i] + PreAmpDb)), true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, ReferencePen, geometry);
    }

    private void DrawHandles(DrawingContext dc, EqPointList points)
    {
        foreach (var point in points.Points)
        {
            var center = new Point(FreqToX(point.FrequencyHz), DbToY(point.GainDb + PreAmpDb));
            var active = point.Id == _dragId || point.Id == _hoverId;
            var radius = point.IsEndpoint ? 5.0 : (active ? 7.0 : 5.0);
            var brush = point.IsEndpoint ? EndpointBrush : HandleBrush;

            if (!IsEditable)
            {
                brush = EndpointBrush;
            }

            if (active && IsEditable)
            {
                dc.DrawEllipse(HaloBrush, null, center, radius + 5, radius + 5);
            }

            if (point.IsEndpoint)
            {
                dc.DrawRectangle(brush, HandlePen, new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
            }
            else
            {
                dc.DrawEllipse(brush, HandlePen, center, radius, radius);
            }
        }
    }

    /// <summary>
    /// 每条风味各画一条自己的曲线，颜色和下拉里的名称一致；选中的那条画粗。
    /// 0 dB 的不画——那是一条压在 0 dB 上的平线，画出来只会挡住别人。
    /// </summary>
    private void DrawFlavorCurve(DrawingContext dc)
    {
        if (Flavors is not { } flavors)
        {
            return;
        }

        var samples = (int)Math.Clamp(_plot.Width, 160, 900);
        for (var i = 0; i < flavors.Bands.Count; i++)
        {
            var band = flavors.Bands[i];
            if (Math.Abs(band.GainDb) < 0.005)
            {
                continue;
            }

            var snapshot = new PeaceSnapshot(
                new[] { new SliderBand(i, band.FrequencyHz, band.GainDb, band.Quality, band.FilterType, true) },
                0,
                1);
            var values = EqResponse.Curve(snapshot, samples, MinHz, MaxHz);
            if (values.Length < 2)
            {
                continue;
            }

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(_plot.Left, DbToY(values[0] + PreAmpDb)), false, false);
                for (var step = 1; step < values.Length; step++)
                {
                    var x = _plot.Left + _plot.Width * step / (values.Length - 1);
                    ctx.LineTo(new Point(x, DbToY(values[step] + PreAmpDb)), true, false);
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, TonePalette.CurvePenFor(band.Key, i == SelectedFlavorIndex), geometry);
        }
    }

    /// <summary>
    /// 风味手柄：位置是“设定值”（增益 + 预增益），不是叠加曲线在那点的值。
    /// 0 dB 的也画，半透明——不然用户不知道该从哪儿开始拖。
    /// </summary>
    private void DrawFlavorHandles(DrawingContext dc)
    {
        if (Flavors is not { } flavors)
        {
            return;
        }

        // 下拉里还没选目标：一个风味手柄都不画，选了才出现。
        if (SelectedFlavorIndex < 0)
        {
            return;
        }

        var brushes = FlavorBrushes;
        for (var i = 0; i < flavors.Bands.Count; i++)
        {
            var band = flavors.Bands[i];
            var selected = i == SelectedFlavorIndex;

            // 0 dB 的一律不画——它没有在改变声音。例外只给「左右」编辑态下拉里正选着的那一条：
            // 不画出来就没东西可拖，风味就永远起不来了；退出编辑后这个例外不再生效。
            if (Math.Abs(band.GainDb) < 0.005 && !(selected && FlavorHandlesInteractive))
            {
                continue;
            }

            var brush = brushes is not null && i < brushes.Count ? brushes[i] : HandleBrush;
            var center = new Point(FreqToX(band.FrequencyHz), DbToY(band.GainDb + PreAmpDb));
            var active = selected || i == _flavorDragIndex || i == _flavorHoverIndex;

            if (active)
            {
                dc.PushOpacity(0.28);
                dc.DrawEllipse(brush, null, center, 12, 12);
                dc.Pop();
            }

            dc.DrawEllipse(brush, HandlePen, center, 5.5, 5.5);
            DrawFlavorIcon(dc, band, brush, center);
        }
    }

    /// <summary>
    /// 风味的图标：抬的时候在控制点上方，削的时候在下方，跟着那个点走。
    /// 贴到坐标轴边缘时往回收一点，不让它跑出绘图区。
    /// </summary>
    private void DrawFlavorIcon(DrawingContext dc, FlavorBand band, Brush brush, Point center)
    {
        var anchor = FlavorIconAnchor(band, center);
        FlavorIcons.Draw(dc, band.Key, anchor, FlavorIconSize, brush);
    }

    /// <summary>图标中心：增益 ≥ 0 在上，< 0 在下，离控制点一个固定距离。</summary>
    private Point FlavorIconAnchor(FlavorBand band, Point center)
    {
        var y = band.GainDb >= 0 ? center.Y - FlavorIconOffset : center.Y + FlavorIconOffset;
        y = Math.Clamp(y, _plot.Top + FlavorIconSize / 2, _plot.Bottom - FlavorIconSize / 2);
        return new Point(center.X, y);
    }

    /// <summary>命中风味手柄（圆点或它的图标），返回下标；没有返回 -1。</summary>
    private int HitTestFlavor(Point position, double tolerance)
    {
        if (!FlavorHandlesInteractive || Flavors is not { } flavors)
        {
            return -1;
        }

        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < flavors.Bands.Count; i++)
        {
            var band = flavors.Bands[i];

            // 和绘制保持一致：0 dB 又没被选中的不参与命中。
            if (Math.Abs(band.GainDb) < 0.005 && i != SelectedFlavorIndex)
            {
                continue;
            }

            var center = new Point(FreqToX(band.FrequencyHz), DbToY(band.GainDb + PreAmpDb));
            var icon = FlavorIconAnchor(band, center);
            var distance = Math.Min((center - position).Length, (icon - position).Length);
            if (distance <= tolerance && distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private void DrawGuideLine(DrawingContext dc, EqPoint point)
    {
        var x = FreqToX(point.FrequencyHz);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(70, 0x4C, 0xC2, 0xFF)), 1)
        {
            DashStyle = new DashStyle(new[] { 3.0, 3.0 }, 0),
        };
        pen.Freeze();
        dc.DrawLine(pen, new Point(x, _plot.Top), new Point(x, _plot.Bottom));
    }

    private void DrawLabel(DrawingContext dc, EqPoint point)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = Text(EqFormat.Describe(point.FrequencyHz, point.GainDb, point.Quality), 11, Brushes.White, dpi);

        var x = FreqToX(point.FrequencyHz);
        var y = DbToY(point.GainDb + PreAmpDb);
        var width = text.Width + 16;
        var height = text.Height + 8;

        var left = Math.Clamp(x + 12, _plot.Left + 2, _plot.Right - width - 2);
        var top = Math.Clamp(y - height - 10, _plot.Top + 2, _plot.Bottom - height - 2);
        var rect = new Rect(left, top, width, height);

        dc.DrawRoundedRectangle(PillBrush, PillPen, rect, 6, 6);
        dc.DrawText(text, new Point(rect.Left + 8, rect.Top + 4));
    }

    private void DrawCenteredHint(DrawingContext dc, string message)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = Text(message, 13, TextBrush, dpi);
        dc.DrawText(text, new Point(_plot.Left + (_plot.Width - text.Width) / 2, _plot.Top + _plot.Height / 2 - 10));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!IsEditable || _plot.Width <= 0)
        {
            return;
        }

        // 一落手就是跟手编辑：进场动画到此收尾，别让 0.2 秒的插值吃掉操作手感。
        EndCurveMorph();

        var position = e.GetPosition(this);
        if (!_plot.Contains(position))
        {
            return;
        }

        // 风味手柄优先：它画在最上面，点中了就只认它，不能顺手建一个 EQ 点。
        var flavorHit = HitTestFlavor(position, 13);
        if (flavorHit >= 0)
        {
            _flavorDragIndex = flavorHit;
            _flavorHoverIndex = flavorHit;
            FlavorSelected?.Invoke(this, new FlavorIndexEventArgs(flavorHit));
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var hit = HitTest(position, 13);
        if (hit is not null)
        {
            _dragId = hit.Id;
            _hoverId = hit.Id;
            _dragThroughCurve = false;
            Cursor = GrabHand;
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var frequency = XToFreq(position.X);
        var gainDb = YToDb(position.Y) - PreAmpDb;

        // 压在曲线上就当成“抓住曲线”：最近的那颗推子跟手走。
        // 抓取距离是频率轴的一段比例（见 SnapOctaves），跟窗口宽度无关。
        var grabbed = CurvePick(position, frequency);
        if (grabbed >= 0)
        {
            _dragId = grabbed;
            _hoverId = grabbed;
            _dragThroughCurve = true;
            Cursor = GrabHand;
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // 曲线上没有可抓的推子，或者压根没按在曲线上：在落点新建一个，建成就直接接管拖动。
        // 按在曲线上建点时也反推一次：新点一出生就压在鼠标上，而不是把曲线顶高一截。
        var onCurve = IsNearCurve(position);
        if (onCurve && Points is { } existing)
        {
            gainDb = EqResponse.GainForNewPointAt(existing, frequency, gainDb);
        }

        var args = new PointCreateEventArgs(frequency, gainDb);
        CreatePointRequested?.Invoke(this, args);
        if (args.CreatedPointId >= 0)
        {
            _dragId = args.CreatedPointId;
            _hoverId = args.CreatedPointId;
            _dragThroughCurve = onCurve;
            Cursor = GrabHand;
            CaptureMouse();
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);

        // 风味两个方向都跟手：横向频率、纵向增益（夹取交给 FlavorSet）。
        if (_flavorDragIndex >= 0)
        {
            FlavorFrequencyPreview?.Invoke(
                this,
                new FlavorMoveEventArgs(_flavorDragIndex, XToFreq(position.X), YToDb(position.Y) - PreAmpDb));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_dragId >= 0)
        {
            // 手柄画在「增益 + 预增益」的位置上，拖回来时要扣掉预增益，点才跟手。
            var frequency = XToFreq(position.X);
            var gainDb = YToDb(position.Y) - PreAmpDb;

            // 抓住曲线拖：曲线要经过鼠标，所以增益从曲线反推，不是照抄鼠标高度。
            // 别的推子在这一点的贡献要先扣掉，否则曲线会跟鼠标差那么一点。
            if (_dragThroughCurve && Points is { } points)
            {
                gainDb = EqResponse.GainForCurveThrough(points, _dragId, frequency, gainDb);
            }

            PointMovePreview?.Invoke(this, new PointMoveEventArgs(_dragId, frequency, gainDb));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var inPlot = IsEditable && _plot.Contains(position);
        var flavorHover = inPlot ? HitTestFlavor(position, 14) : -1;
        var hover = flavorHover >= 0 || !inPlot ? null : HitTest(position, 14);
        var hoverId = hover?.Id ?? -1;

        // 光标就是操作说明：压在手柄或曲线上都是“抓”，按下去就能拖；
        // 空白处是十字，按下去是建点。曲线上不要求附近有推子 —— 没有就是新建，同样是“按在线上拖”。
        var onCurve = hoverId < 0 && inPlot && IsNearCurve(position);
        if (flavorHover != _flavorHoverIndex || hoverId != _hoverId)
        {
            _flavorHoverIndex = flavorHover;
            _hoverId = hoverId;
            InvalidateVisual();
        }

        Cursor = !IsEditable
            ? Cursors.Arrow
            : flavorHover >= 0
                ? Cursors.SizeWE
                : hoverId >= 0 || onCurve
                    ? OpenHand
                    : (_plot.Contains(position) ? Cursors.Cross : Cursors.Arrow);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_flavorDragIndex >= 0)
        {
            var index = _flavorDragIndex;
            _flavorDragIndex = -1;
            ReleaseMouseCapture();
            FlavorFrequencyCommitted?.Invoke(this, new FlavorIndexEventArgs(index));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_dragId < 0)
        {
            return;
        }

        _dragId = -1;
        _dragThroughCurve = false;
        ReleaseMouseCapture();

        // 松手后鼠标还在原地：手底下是曲线就还原成张开的手（还能再抓），否则回到十字（可以建点）。
        Cursor = IsNearCurve(e.GetPosition(this)) ? OpenHand : Cursors.Cross;
        PointMoveCommitted?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        if (!IsEditable || _plot.Width <= 0)
        {
            return;
        }

        // 风味不能删：右键在这里是「把增益归零」，也不能落到下面的删点逻辑。
        var flavorHit = HitTestFlavor(e.GetPosition(this), 13);
        if (flavorHit >= 0)
        {
            FlavorResetRequested?.Invoke(this, new FlavorIndexEventArgs(flavorHit));
            e.Handled = true;
            return;
        }

        var hit = HitTest(e.GetPosition(this), 13);
        if (hit is null)
        {
            return;
        }

        // 首尾端点是频轴的地基，删不掉；右键在它们身上改成“增益归零”。
        if (hit.IsDeletable)
        {
            PointDeleteRequested?.Invoke(this, new PointEventArgs(hit.Id));
        }
        else
        {
            PointResetRequested?.Invoke(this, new PointEventArgs(hit.Id));
        }

        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!IsEditable || _plot.Width <= 0)
        {
            return;
        }

        var position = e.GetPosition(this);
        var steps = e.Delta / 120.0;

        // 风味的 Q 先判：手柄画在最上面，滚轮落在它身上就不该改到底下 EQ 点的 Q。
        var flavorHit = HitTestFlavor(position, 14);
        if (flavorHit >= 0 && Flavors is { } flavors)
        {
            FlavorQualityChanged?.Invoke(
                this,
                new FlavorQualityEventArgs(flavorHit, flavors.Bands[flavorHit].Quality * Math.Pow(1.15, steps)));
            e.Handled = true;
            return;
        }

        var hit = HitTest(position, 14);
        if (hit is null)
        {
            return;
        }

        QualityChanged?.Invoke(this, new PointQualityEventArgs(hit.Id, hit.Quality * Math.Pow(1.15, steps)));
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_dragId < 0 && _flavorDragIndex < 0 && (_hoverId >= 0 || _flavorHoverIndex >= 0))
        {
            _hoverId = -1;
            _flavorHoverIndex = -1;
            InvalidateVisual();
        }
    }

    private EqPoint? HitTest(Point position, double tolerance)
    {
        var points = Points;
        if (points is null)
        {
            return null;
        }

        EqPoint? best = null;
        var bestDistance = double.MaxValue;

        foreach (var point in points.Points)
        {
            var center = new Point(FreqToX(point.FrequencyHz), DbToY(point.GainDb + PreAmpDb));
            var distance = (center - position).Length;
            if (distance <= tolerance && distance < bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        return best;
    }

    /// <summary>
    /// 按下点算不算抓住了某颗推子。两件事一起判：纵向贴着画出来的那条曲线，
    /// 横向在鼠标左右 <see cref="SnapOctaves"/> 个倍频程内有推子。返回 -1 表示没抓住，调用方就在落点新建一个。
    /// </summary>
    private int CurvePick(Point position, double frequencyHz)
    {
        if (!IsNearCurve(position) || Points is not { } points)
        {
            return -1;
        }

        return points.NearestWithin(frequencyHz, SnapOctaves)?.Id ?? -1;
    }

    /// <summary>
    /// 鼠标离画出来的曲线是否在一指之内。查的是最近一次绘制的采样，不重算频响，
    /// 所以悬停判定和屏幕上看到的形状永远是同一条线。
    /// </summary>
    private bool IsNearCurve(Point position)
    {
        if (_curveSamples is not { Length: > 1 } samples || _curveSamplesPlot.Width <= 0)
        {
            return false;
        }

        var t = (position.X - _curveSamplesPlot.Left) / _curveSamplesPlot.Width;
        if (t < 0 || t > 1)
        {
            return false;
        }

        var index = t * (samples.Length - 1);
        var left = (int)Math.Floor(index);
        var fraction = index - left;
        var db = left + 1 < samples.Length
            ? samples[left] + (samples[left + 1] - samples[left]) * fraction
            : samples[left];

        return Math.Abs(position.Y - DbToY(db + PreAmpDb)) <= CurvePickPixels;
    }

    // 预增益会把整条曲线往下压，范围只取 GainRange 的话曲线会贴下边界被切掉。
    // 中心让出 PreAmp 的一半，半范围再补另一半，曲线就在视野里居中。
    private double EffectiveRange => Math.Max(GainRange, 3) + (Math.Abs(PreAmpDb) / 2);

    private double FreqToX(double hz)
    {
        var clamped = Math.Clamp(hz, MinHz, MaxHz);
        var t = (Math.Log10(clamped) - Math.Log10(MinHz)) / (Math.Log10(MaxHz) - Math.Log10(MinHz));
        return _plot.Left + t * _plot.Width;
    }

    private double XToFreq(double x)
    {
        if (_plot.Width <= 0)
        {
            return MinHz;
        }

        var t = Math.Clamp((x - _plot.Left) / _plot.Width, 0, 1);
        var ratio = Math.Log10(MaxHz) - Math.Log10(MinHz);
        return Math.Pow(10, Math.Log10(MinHz) + t * ratio);
    }

    /// <summary>
    /// 教程演示用：把频率 / 增益换算成屏幕坐标（和画曲线用的是同一套映射）。
    /// 调用方负责 PointFromScreen 转回自己的坐标。
    /// </summary>
    public Point PointOnGraph(double frequencyHz, double gainDb)
    {
        var clamped = Math.Clamp(frequencyHz, MinHz, MaxHz);
        var t = (Math.Log10(clamped) - Math.Log10(MinHz)) / (Math.Log10(MaxHz) - Math.Log10(MinHz));
        var x = _plot.Left + _plot.Width * t;
        return PointToScreen(new Point(x, DbToY(gainDb + PreAmpDb)));
    }

    private double DbToY(double db)
    {
        var range = EffectiveRange;
        var centered = db - (PreAmpDb / 2);
        var t = (range - Math.Clamp(centered, -range, range)) / (2 * range);
        return _plot.Top + t * _plot.Height;
    }

    private double YToDb(double y)
    {
        var range = EffectiveRange;
        var center = _plot.Top + _plot.Height / 2;
        var perDb = _plot.Height / (2 * range);
        return Math.Clamp((center - y) / perDb, -range, range) + (PreAmpDb / 2);
    }

    private static FormattedText Text(string content, double size, Brush brush, double pixelsPerDip) =>
        new(content, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, brush, pixelsPerDip);

    private static Pen CreateReferencePen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(110, 160, 170, 180)), 1.4)
        {
            DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0),
        };
        pen.Freeze();
        return pen;
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }
}
