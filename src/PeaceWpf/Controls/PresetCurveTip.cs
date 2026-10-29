using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PeaceWpf.Core.Model;
using PeaceWpf.Theme;

namespace PeaceWpf.Controls;

/// <summary>
/// 预设下拉的悬浮缩略图：一条主曲线 + 各条风味自己的彩线。
///
/// 和 <see cref="EqCurvePreview"/> 的分工：那边是“选一台设备看校正”，
/// 有频率刻度、有预增益；这边只求一眼看出形状，所以不画刻度、不算预增益，
/// 主曲线直接是预设文件里那套滤波器算出来的响应。
///
/// 颜色只有两个来源：主曲线由调用方给（左右蓝 / 低音紫，和顶栏按钮一致）；
/// 风味一律走 <see cref="TonePalette"/>，和主界面主图上那八条彩线同一套颜色。
/// 风味不叠加进主曲线——图上能分清“预设本来长什么样”和“风味额外动了哪里”。
/// </summary>
public sealed class PresetCurveTip : FrameworkElement
{
    /// <summary>横轴左端，和主图的点模型一致。</summary>
    public const double MinHz = 20;

    /// <summary>横轴右端。</summary>
    public const double MaxHz = 20000;

    private static readonly Brush BackgroundBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(0xF2, 0x1A, 0x1A, 0x1A)));

    private static readonly Pen BorderPen =
        Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3A)), 1));

    private static readonly Brush TitleBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)));
    private static readonly Brush FooterBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xC0, 0xA6, 0xA6, 0xA6)));
    private static readonly Brush HintBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xA0, 0xA0, 0xA0, 0xA0)));

    private static readonly Pen GridPen =
        Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0x2E, 0x80, 0x80, 0x80)), 1));

    private static readonly Pen ZeroPen =
        Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0x78, 0xC0, 0xC0, 0xC0)), 1));

    private static readonly double[] GridFrequencies = { 20, 100, 1000, 10000, 20000 };

    private string _title = string.Empty;
    private string _footer = string.Empty;
    private Color _accent = Color.FromRgb(0x4C, 0xC2, 0xFF);
    private double[]? _curve;
    private readonly List<(string Key, double[] Values)> _flavors = new();
    private double _range = 12;
    private IReadOnlyList<string>? _footerLines;

    /// <summary>没给脚注时的基准高度（一行脚注）。</summary>
    public const double BaseHeight = 148;

    /// <summary>脚注每多一行，高度加这么多。</summary>
    public const double FooterLineHeight = 14;

    /// <summary>
    /// 按当前内容算出来的建议高度。气泡的宽高由调用方设，所以这里只给数：
    /// 一行脚注是 <see cref="BaseHeight"/>，多一行加 <see cref="FooterLineHeight"/>。
    /// </summary>
    public double PreferredHeight { get; private set; } = BaseHeight;

    /// <summary>
    /// 换一套内容。curve 为空时中间写 <paramref name="emptyText"/>；
    /// flavors 传 null 或空表就不画彩线（低音预设没有风味）。
    /// footerLines 非空时用它当脚注（一行一条，按顺序画），否则画「风味 …」那一行。
    /// </summary>
    public void Show(
        string title,
        IReadOnlyList<double>? curve,
        Color accent,
        IReadOnlyList<(string Key, double[] Values)>? flavors,
        string emptyText = "这条预设还没落盘",
        IReadOnlyList<string>? footerLines = null)
    {
        _title = title;
        _accent = accent;
        _curve = curve is { Count: >= 2 } ? curve.ToArray() : null;
        _footer = emptyText;
        _footerLines = footerLines is { Count: > 0 } ? footerLines : null;
        PreferredHeight = BaseHeight + (((_footerLines?.Count ?? 1) - 1) * FooterLineHeight);

        _flavors.Clear();
        if (flavors is not null)
        {
            foreach (var (key, values) in flavors)
            {
                if (values.Length >= 2 && !IsFlat(values))
                {
                    _flavors.Add((key, values));
                }
            }
        }

        _range = ComputeRange(_curve, _flavors);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 24 || height <= 24)
        {
            return;
        }

        dc.DrawRoundedRectangle(BackgroundBrush, BorderPen, new Rect(0.5, 0.5, width - 1, height - 1), 8, 8);

        var title = CreateText(_title, 12, TitleBrush);
        dc.DrawText(title, new Point(12, 8));

        var plot = new Rect(12, 30, Math.Max(width - 24, 10), Math.Max(height - 56, 10));

        if (_curve is null)
        {
            var hint = CreateText(_footer, 11, HintBrush);
            dc.DrawText(
                hint,
                new Point(plot.X + (plot.Width - hint.Width) / 2, plot.Y + (plot.Height - hint.Height) / 2));
            return;
        }

        DrawGrid(dc, plot);
        DrawCurveFill(dc, plot, _curve);
        DrawPolyline(dc, plot, _curve, Freeze(new Pen(new SolidColorBrush(_accent), 2)));

        // 风味：每条一条细线，颜色和主图、风味下拉里的名字一致。
        var labels = new List<string>();
        foreach (var (key, values) in _flavors)
        {
            DrawPolyline(dc, plot, values, TonePalette.CurvePenFor(key, selected: false));
            if (ToneBands.Find(key) is { } band)
            {
                labels.Add(band.Label);
            }
        }

        // 脚注：给了自定义行就用它（风味方向气泡放「会动哪几条 + 来源」），
        // 否则沿用预设下拉的「风味 爆炸 · 脚步 …」。
        IReadOnlyList<string> footerLines;
        if (_footerLines is { Count: > 0 })
        {
            footerLines = _footerLines;
        }
        else
        {
            footerLines = new[]
            {
                labels.Count == 0
                    ? "无风味"
                    : "风味 " + string.Join(" · ", labels.Take(3)) + (labels.Count > 3 ? " …" : string.Empty),
            };
        }

        var lineY = plot.Bottom + 8;
        foreach (var line in footerLines)
        {
            // 太长就裁掉，气泡框不会跟着变宽（宽度是定死的，位置才好算）。
            var footer = CreateText(line, 10, FooterBrush);
            dc.PushClip(new RectangleGeometry(new Rect(plot.X, lineY, plot.Width, FooterLineHeight)));
            dc.DrawText(footer, new Point(plot.X, lineY));
            dc.Pop();
            lineY += FooterLineHeight;
        }
    }

    /// <summary>每一条曲线都按同一个纵轴范围画：主曲线和风味同框才比得出来高低。</summary>
    private static double ComputeRange(double[]? curve, List<(string Key, double[] Values)> flavors)
    {
        var peak = 0.0;
        if (curve is not null)
        {
            foreach (var value in curve)
            {
                peak = Math.Max(peak, Math.Abs(value));
            }
        }

        foreach (var (_, values) in flavors)
        {
            foreach (var value in values)
            {
                peak = Math.Max(peak, Math.Abs(value));
            }
        }

        return Math.Clamp(Math.Ceiling(peak / 3.0) * 3.0, 6, 24);
    }

    private void DrawGrid(DrawingContext dc, Rect plot)
    {
        foreach (var hz in GridFrequencies)
        {
            var x = MapX(plot, hz);
            dc.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        var zeroY = MapY(plot, _range, 0);
        dc.DrawLine(ZeroPen, new Point(plot.Left, zeroY), new Point(plot.Right, zeroY));
    }

    private void DrawCurveFill(DrawingContext dc, Rect plot, IReadOnlyList<double> values)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var zeroY = MapY(plot, _range, 0);
            ctx.BeginFigure(new Point(plot.Left, zeroY), isFilled: true, isClosed: true);
            for (var i = 0; i < values.Count; i++)
            {
                ctx.LineTo(
                    new Point(MapX(plot, i, values.Count), MapY(plot, _range, values[i])),
                    isStroked: false,
                    isSmoothJoin: false);
            }

            ctx.LineTo(new Point(plot.Right, zeroY), isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();

        var fill = new LinearGradientBrush(
            Color.FromArgb(56, _accent.R, _accent.G, _accent.B),
            Color.FromArgb(6, _accent.R, _accent.G, _accent.B),
            90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, geometry);
    }

    private void DrawPolyline(DrawingContext dc, Rect plot, IReadOnlyList<double> values, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(MapX(plot, 0, values.Count), MapY(plot, _range, values[0])), false, false);
            for (var i = 1; i < values.Count; i++)
            {
                ctx.LineTo(new Point(MapX(plot, i, values.Count), MapY(plot, _range, values[i])), true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static double MapX(Rect plot, int index, int count) =>
        count <= 1 ? plot.Left : plot.Left + plot.Width * index / (count - 1);

    private static double MapX(Rect plot, double hz)
    {
        var t = Math.Log10(Math.Clamp(hz, MinHz, MaxHz) / MinHz) / Math.Log10(MaxHz / MinHz);
        return plot.Left + plot.Width * t;
    }

    private static double MapY(Rect plot, double range, double db)
    {
        var t = (range - Math.Clamp(db, -range, range)) / (2 * range);
        return plot.Top + plot.Height * t;
    }

    private static bool IsFlat(IReadOnlyList<double> values)
    {
        foreach (var value in values)
        {
            if (Math.Abs(value) >= 0.05)
            {
                return false;
            }
        }

        return true;
    }

    private FormattedText CreateText(string text, double size, Brush brush) =>
        new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

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
