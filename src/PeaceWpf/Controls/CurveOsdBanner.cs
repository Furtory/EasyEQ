using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PeaceWpf.Controls;

/// <summary>
/// 屏幕顶部滑下来的那条通知：黑色半透明底 + 一条完全不透明的曲线 + 一行白字黑描边的提示。
///
/// 画法跟 <see cref="PresetCurveTip"/> 一套：横轴按采样点等分（<see cref="Core.Dsp.EqResponse.Curve"/>
/// 本来就是按对数频率等分的），纵轴按峰值取整到 3 dB 一档。区别是这里不画刻度、不画网格，
/// 也不画风味彩线 —— 通知要的是一眼看出「刚才换成什么形状」，多一条线反而糊。
///
/// 底色只有 40% 不透明，曲线和文字是实心的：这样叠在游戏画面或桌面上，形状仍然清楚。
/// </summary>
public sealed class CurveOsdBanner : FrameworkElement
{
    /// <summary>整条通知的高度：一行配置名 + 曲线区 + 一行提示。</summary>
    public const double PreferredHeight = 168;

    /// <summary>
    /// 背景向上多伸出去的一段。落定后这一截仍在屏幕外（被窗口边缘裁掉），
    /// 屏幕顶边那条亚像素的缝就被它盖住了。
    /// </summary>
    public const double TopBleed = 64;

    private const double PlotLeft = 22;
    private const double PlotTop = 32;
    private const double PlotRight = 22;
    private const double PlotBottom = 44;

    private static readonly Brush BackgroundBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(102, 0x00, 0x00, 0x00)));

    private static readonly Brush TitleBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));
    private static readonly Brush DetailBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF)));

    /// <summary>
    /// 右下方向的浮动阴影：光从左上来，影子落在右下方。
    /// 整块（黑底、曲线、字）一起投影，看起来才像一根通知浮在画面上面。
    /// </summary>
    private static readonly DropShadowEffect FloatingShadow = CreateFloatingShadow();

    /// <summary>文字描边：黑色实心，压在白字后面，亮背景上也读得清。</summary>
    private static readonly Pen TextOutlinePen =
        Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0xD8, 0x00, 0x00, 0x00)), 3)
        {
            LineJoin = PenLineJoin.Round,
        });

    public CurveOsdBanner()
    {
        Effect = FloatingShadow;
    }

    /// <summary>
    /// 开关右下阴影。滑动过程中关掉：DropShadowEffect 每帧都要重算一次，
    /// 占着 GPU 会让滑入动画掉帧；落定之后再打开，静止画面里根本看不出区别。
    /// </summary>
    public void ShowShadow(bool visible)
    {
        Effect = visible ? FloatingShadow : null;
    }

    private static DropShadowEffect CreateFloatingShadow()
    {
        var shadow = new DropShadowEffect
        {
            Color = Colors.Black,

            // 45° = 右下。
            Direction = 45,
            ShadowDepth = 9,
            BlurRadius = 28,
            Opacity = 0.55,
        };

        shadow.Freeze();
        return shadow;
    }

    private string _title = string.Empty;
    private string _detail = string.Empty;
    private double[]? _curve;
    private Color _accent = Color.FromRgb(0x4C, 0xC2, 0xFF);
    private double _range = 12;

    /// <summary>上一次展示的曲线，这次从它变过来；没有上一条（第一次弹）就是 null。</summary>
    private double[]? _lastCurve;

    /// <summary>这次变形的起点。只在 <see cref="IsMorphing"/> 为真时有值。</summary>
    private double[]? _morphFrom;

    private double _previousRange = 12;

    /// <summary>从旧曲线到新曲线的过渡进度：0 = 还是旧形状，1 = 已经是新形状。</summary>
    public static readonly DependencyProperty MorphProgressProperty = DependencyProperty.Register(
        nameof(MorphProgress),
        typeof(double),
        typeof(CurveOsdBanner),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double MorphProgress
    {
        get => (double)GetValue(MorphProgressProperty);
        set => SetValue(MorphProgressProperty, value);
    }

    /// <summary>这次的内容能不能做「旧 → 新」的曲线变形。调用方据此决定要不要启动动画。</summary>
    public bool IsMorphing { get; private set; }

    /// <summary>换一套通知内容。curve 为空时只写字，不画曲线。</summary>
    public void Show(string title, string detail, IReadOnlyList<double>? curve, Color accent)
    {
        _title = title ?? string.Empty;
        _detail = detail ?? string.Empty;
        _accent = accent;

        var target = curve is { Count: >= 2 } ? curve.ToArray() : null;

        // 两条曲线的采样点数和频率网格一致（都来自 EqResponse.Curve），
        // 所以逐点插值等价于同一个频率上的增益在动，形状会平滑地变过去。
        IsMorphing = target is not null
            && _lastCurve is { } previous
            && previous.Length == target.Length;

        if (IsMorphing)
        {
            _morphFrom = _lastCurve;
            _previousRange = _range;
            MorphProgress = 0;
        }
        else
        {
            _morphFrom = null;
            MorphProgress = 1;
        }

        _curve = target;
        _range = ComputeRange(target);
        _lastCurve = target;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight > 0 ? ActualHeight : PreferredHeight + TopBleed;
        if (width <= 40 || height <= 40)
        {
            return;
        }

        // 顶上两个角是直角：通知贴着屏幕边缘滑下来，圆角在那里会露出一道缝；
        // 底下两个角留圆，收起来的时候不至于像一块硬切的色块。
        dc.DrawGeometry(BackgroundBrush, null, RoundedBottomRect(width, height, 14));

        // 内容整体下移 TopBleed：控件顶边在屏幕外，视觉上曲线和字仍在原来的位置。
        var plot = new Rect(
            PlotLeft,
            PlotTop + TopBleed,
            Math.Max(width - PlotLeft - PlotRight, 10),
            Math.Max(height - TopBleed - PlotTop - PlotBottom, 10));

        if (_curve is not null)
        {
            // 变形过程中两条曲线共用一套纵轴范围（取较大的那个），
            // 否则新旧范围不同时整条曲线会跟着缩放，看着像在跳。
            var range = IsMorphing && MorphProgress < 0.999
                ? Math.Max(_range, _previousRange)
                : _range;

            var values = BuildDisplayCurve();
            DrawCurveFill(dc, plot, values, range);
            DrawCurve(dc, plot, values, range);
        }

        DrawOutlinedText(dc, _title, 13.5, FontWeights.SemiBold, TitleBrush, new Point(24, TopBleed + 9), width);
        DrawOutlinedText(dc, _detail, 11.5, FontWeights.Normal, DetailBrush, new Point(24, plot.Bottom + 10), width);
    }

    /// <summary>
    /// 白字黑边：先用 pen 把文字轮廓描一遍，再填白色。
    /// FormattedText 本身不支持描边，转成几何形状是 WPF 里最直接的做法。
    /// </summary>
    private void DrawOutlinedText(
        DrawingContext dc,
        string text,
        double size,
        FontWeight weight,
        Brush brush,
        Point origin,
        double clipWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // 太长就裁到通知条以内：曲线和字的右边缘对齐，不往屏幕外跑。
        dc.PushClip(new RectangleGeometry(new Rect(0, origin.Y - 2, clipWidth, size + 8)));
        var geometry = formatted.BuildGeometry(origin);
        dc.DrawGeometry(null, TextOutlinePen, geometry);
        dc.DrawGeometry(brush, null, geometry);
        dc.Pop();
    }

    /// <summary>顶上直角、底下圆角的矩形路径：通知贴屏幕顶边，顶部不能露缝。</summary>
    private static Geometry RoundedBottomRect(double width, double height, double radius)
    {
        var r = Math.Min(radius, Math.Min(width, height) / 2);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(width, 0), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(width, height - r), isStroked: true, isSmoothJoin: false);
            ctx.ArcTo(
                new Point(width - r, height),
                new Size(r, r),
                0,
                isLargeArc: false,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
            ctx.LineTo(new Point(r, height), isStroked: true, isSmoothJoin: false);
            ctx.ArcTo(
                new Point(0, height - r),
                new Size(r, r),
                0,
                isLargeArc: false,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
            ctx.LineTo(new Point(0, 0), isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>纵轴范围：峰值取整到 3 dB 一档，最少 6、最多 24，和缩略图一个算法。</summary>
    private static double ComputeRange(double[]? curve)
    {
        var peak = 0.0;
        if (curve is not null)
        {
            foreach (var value in curve)
            {
                peak = Math.Max(peak, Math.Abs(value));
            }
        }

        return Math.Clamp(Math.Ceiling(peak / 3.0) * 3.0, 6, 24);
    }

    /// <summary>
    /// 当前该画的曲线：静止时就是目标曲线，变形期间按进度在旧、新两条之间逐点插值。
    /// </summary>
    private double[] BuildDisplayCurve()
    {
        if (_curve is null)
        {
            return Array.Empty<double>();
        }

        if (!IsMorphing || _morphFrom is not { } from || MorphProgress >= 0.999)
        {
            return _curve;
        }

        var t = Math.Clamp(MorphProgress, 0, 1);
        var result = new double[_curve.Length];
        for (var i = 0; i < _curve.Length; i++)
        {
            result[i] = from[i] + ((_curve[i] - from[i]) * t);
        }

        return result;
    }

    /// <summary>曲线下面的渐变填充：半透明，只是让形状更实，不算「不透明」的部分。</summary>
    private void DrawCurveFill(DrawingContext dc, Rect plot, IReadOnlyList<double> values, double range)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var zeroY = MapY(plot, range, 0);
            ctx.BeginFigure(new Point(plot.Left, zeroY), isFilled: true, isClosed: true);
            for (var i = 0; i < values.Count; i++)
            {
                ctx.LineTo(
                    new Point(MapX(plot, i, values.Count), MapY(plot, range, values[i])),
                    isStroked: false,
                    isSmoothJoin: false);
            }

            ctx.LineTo(new Point(plot.Right, zeroY), isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();

        var fill = new LinearGradientBrush(
            Color.FromArgb(72, _accent.R, _accent.G, _accent.B),
            Color.FromArgb(8, _accent.R, _accent.G, _accent.B),
            90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, geometry);
    }

    /// <summary>曲线本体：完全不透明，压在 40% 黑底上依然清楚。</summary>
    private void DrawCurve(DrawingContext dc, Rect plot, IReadOnlyList<double> values, double range)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(
                new Point(MapX(plot, 0, values.Count), MapY(plot, range, values[0])),
                false,
                false);
            for (var i = 1; i < values.Count; i++)
            {
                ctx.LineTo(new Point(MapX(plot, i, values.Count), MapY(plot, range, values[i])), true, false);
            }
        }

        geometry.Freeze();

        var pen = new Pen(new SolidColorBrush(_accent), 2.4)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static double MapX(Rect plot, int index, int count) =>
        count <= 1 ? plot.Left : plot.Left + plot.Width * index / (count - 1);

    private static double MapY(Rect plot, double range, double db)
    {
        var t = (range - Math.Clamp(db, -range, range)) / (2 * range);
        return plot.Top + plot.Height * t;
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
