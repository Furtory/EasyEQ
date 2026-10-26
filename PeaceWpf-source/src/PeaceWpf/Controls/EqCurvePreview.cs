using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PeaceWpf.Controls;

/// <summary>
/// 只读的频响预览：一条曲线、一套对数频率网格、几条 dB 参考线。
///
/// 和主界面的 <see cref="EqGraphView"/> 分开写，是因为这里的职责只有“画”：
/// 没有拖动、没有命中判定、曲线也不压暗。预设EQ 窗口拿它替掉原来那块纯文本，
/// 选一台设备就能看出这条校正到底是抬低音还是压齿音。
/// </summary>
public sealed class EqCurvePreview : FrameworkElement
{
    /// <summary>横轴左端，和 <c>EqPointList</c> 的端点一致。</summary>
    public const double MinHz = 20;

    /// <summary>横轴右端。</summary>
    public const double MaxHz = 20000;

    private static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Color.FromArgb(18, 128, 128, 128)));
    private static readonly Brush FillBrush = Freeze(new SolidColorBrush(Color.FromArgb(32, 0x4C, 0xC2, 0xFF)));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromArgb(160, 200, 200, 200)));
    private static readonly Brush HintBrush = Freeze(new SolidColorBrush(Color.FromArgb(110, 200, 200, 200)));
    private static readonly Pen GridPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(26, 128, 128, 128)), 1));
    private static readonly Pen ZeroPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(80, 170, 170, 170)), 1));
    private static readonly Pen CurvePen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xC2, 0xFF)), 1.6));
    private static readonly Brush MarkerBrush = Freeze(new SolidColorBrush(Color.FromArgb(235, 0xCF, 0xE8, 0xFF)));

    private static readonly double[] GridFrequencies = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
    private static readonly double[] LabelFrequencies = { 20, 100, 1000, 10000, 20000 };

    private IReadOnlyList<double>? _gains;
    private IReadOnlyList<double>? _markers;

    /// <summary>没数据时中间那句话。</summary>
    public string EmptyText { get; set; } = "选一台设备看曲线";

    /// <summary>
    /// 换一条曲线。gains 是在 20 Hz–20 kHz 上按对数等分的 dB 值，
    /// markers 是滤波器中心频率（可以不传），会在曲线上打点。
    /// </summary>
    public void SetCurve(IReadOnlyList<double>? gainsDb, IReadOnlyList<double>? markersHz = null)
    {
        _gains = gainsDb is { Count: >= 2 } ? gainsDb : null;
        _markers = markersHz;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 8 || height <= 8)
        {
            return;
        }

        dc.DrawRoundedRectangle(BackgroundBrush, null, new Rect(0, 0, width, height), 6, 6);

        var plot = new Rect(42, 14, Math.Max(width - 56, 10), Math.Max(height - 38, 10));

        if (_gains is null)
        {
            DrawLabel(dc, EmptyText, plot.X + plot.Width / 2, plot.Y + plot.Height / 2, TextAlignment.Center, HintBrush, 12, centerVertically: true);
            return;
        }

        var range = ComputeRange(_gains);

        DrawGrid(dc, plot, range);
        DrawCurve(dc, plot, range, _gains);
        DrawMarkers(dc, plot, range);
    }

    /// <summary>纵轴范围：按曲线最大值取到 3 dB 的整数倍，最少 ±6，最多 ±24。</summary>
    private static double ComputeRange(IReadOnlyList<double> gains)
    {
        var peak = 0.0;
        foreach (var value in gains)
        {
            if (double.IsFinite(value))
            {
                peak = Math.Max(peak, Math.Abs(value));
            }
        }

        return Math.Clamp(Math.Ceiling(peak / 3.0) * 3.0, 6, 24);
    }

    private void DrawGrid(DrawingContext dc, Rect plot, double range)
    {
        foreach (var hz in GridFrequencies)
        {
            var x = MapX(plot, hz);
            dc.DrawLine(GridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
        }

        foreach (var hz in LabelFrequencies)
        {
            var text = hz >= 1000
                ? string.Create(CultureInfo.InvariantCulture, $"{hz / 1000:0.#}k")
                : string.Create(CultureInfo.InvariantCulture, $"{hz:0}");
            DrawLabel(dc, text, MapX(plot, hz), plot.Bottom + 4, TextAlignment.Center);
        }

        // 0 dB 单独一根亮一点的线，曲线在它上面还是下面一眼能看出来。
        var zeroY = MapY(plot, range, 0);
        dc.DrawLine(ZeroPen, new Point(plot.X, zeroY), new Point(plot.Right, zeroY));
        DrawLabel(dc, "0", plot.X - 8, zeroY, TextAlignment.Right, TextBrush, 10, centerVertically: true);

        foreach (var db in new[] { range, range / 2, -range / 2, -range })
        {
            var y = MapY(plot, range, db);
            dc.DrawLine(GridPen, new Point(plot.X, y), new Point(plot.Right, y));
            DrawLabel(
                dc,
                db.ToString("+0;-0;0", CultureInfo.InvariantCulture),
                plot.X - 8,
                y,
                TextAlignment.Right,
                TextBrush,
                10,
                centerVertically: true);
        }
    }

    private static void DrawCurve(DrawingContext dc, Rect plot, double range, IReadOnlyList<double> gains)
    {
        var count = gains.Count;

        var fill = new StreamGeometry();
        using (var context = fill.Open())
        {
            var zeroY = MapY(plot, range, 0);
            context.BeginFigure(new Point(plot.X, zeroY), isFilled: true, isClosed: true);
            context.LineTo(new Point(plot.X, MapY(plot, range, gains[0])), isStroked: false, isSmoothJoin: false);
            for (var i = 1; i < count; i++)
            {
                context.LineTo(
                    new Point(MapX(plot, IndexToHz(i, count)), MapY(plot, range, gains[i])),
                    isStroked: false,
                    isSmoothJoin: false);
            }

            context.LineTo(new Point(plot.Right, zeroY), isStroked: false, isSmoothJoin: false);
        }

        fill.Freeze();
        dc.DrawGeometry(FillBrush, null, fill);

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(new Point(plot.X, MapY(plot, range, gains[0])), isFilled: false, isClosed: false);
            for (var i = 1; i < count; i++)
            {
                context.LineTo(
                    new Point(MapX(plot, IndexToHz(i, count)), MapY(plot, range, gains[i])),
                    isStroked: true,
                    isSmoothJoin: true);
            }
        }

        line.Freeze();
        dc.DrawGeometry(null, CurvePen, line);
    }

    private void DrawMarkers(DrawingContext dc, Rect plot, double range)
    {
        if (_markers is null || _gains is null || _markers.Count == 0)
        {
            return;
        }

        foreach (var hz in _markers)
        {
            if (!double.IsFinite(hz) || hz < MinHz || hz > MaxHz)
            {
                continue;
            }

            var t = Math.Log10(hz / MinHz) / Math.Log10(MaxHz / MinHz);
            var index = Math.Clamp(t * (_gains.Count - 1), 0, _gains.Count - 1);
            var gain = _gains[(int)Math.Round(index)];
            dc.DrawEllipse(MarkerBrush, null, new Point(MapX(plot, hz), MapY(plot, range, gain)), 2.6, 2.6);
        }
    }

    private static double MapX(Rect plot, double hz)
    {
        var t = Math.Log10(Math.Clamp(hz, MinHz, MaxHz) / MinHz) / Math.Log10(MaxHz / MinHz);
        return plot.X + plot.Width * t;
    }

    private static double MapY(Rect plot, double range, double db)
    {
        var t = (range - Math.Clamp(db, -range, range)) / (2 * range);
        return plot.Y + plot.Height * t;
    }

    private static double IndexToHz(int index, int count) =>
        MinHz * Math.Pow(MaxHz / MinHz, (double)index / (count - 1));

    private void DrawLabel(
        DrawingContext dc,
        string text,
        double x,
        double y,
        TextAlignment alignment,
        Brush? brush = null,
        double size = 10,
        bool centerVertically = false)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush ?? TextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var originX = alignment switch
        {
            TextAlignment.Right => x - formatted.Width,
            TextAlignment.Center => x - formatted.Width / 2,
            _ => x,
        };

        var originY = centerVertically ? y - formatted.Height / 2 : y;
        dc.DrawText(formatted, new Point(originX, originY));
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
