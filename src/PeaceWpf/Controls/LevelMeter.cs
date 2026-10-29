using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PeaceWpf.Controls;

/// <summary>
/// 一条竖着的电平表：从底往上填充，读到的是系统输出的瞬时峰值。
///
/// 只画，不做任何测量——数值来自 ViewModel，抓不到音频时是底噪，条子就空着。
/// 画法是拟物的：先挖一条有内阴影的槽，再往槽里灌一根带左侧高光的亮条。
/// </summary>
public sealed class LevelMeter : FrameworkElement
{
    public static readonly DependencyProperty LevelDbProperty = DependencyProperty.Register(
        nameof(LevelDb),
        typeof(double),
        typeof(LevelMeter),
        new FrameworkPropertyMetadata(-90.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FloorDbProperty = DependencyProperty.Register(
        nameof(FloorDb),
        typeof(double),
        typeof(LevelMeter),
        new FrameworkPropertyMetadata(-60.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double TrackWidth = 12;
    private const double TrackLeft = 2;
    private const double TrackInsetY = 2;

    private static readonly Brush WellEdge = Freeze(new SolidColorBrush(Color.FromArgb(0xA6, 0, 0, 0)));
    private static readonly Brush GlossBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF)));
    private static readonly Pen TickPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x9A, 0x9A, 0x9A)), 1));
    private static readonly Brush TickBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xB0, 0x8C, 0x8C, 0x8C)));
    private static readonly Typeface Face = new("Segoe UI");

    private static readonly double[] Ticks = { -6, -12, -24, -48 };

    /// <summary>当前电平，0 dB 是满幅，底噪以下就是空。</summary>
    public double LevelDb
    {
        get => (double)GetValue(LevelDbProperty);
        set => SetValue(LevelDbProperty, value);
    }

    public double FloorDb
    {
        get => (double)GetValue(FloorDbProperty);
        set => SetValue(FloorDbProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 14 || height < 30)
        {
            return;
        }

        var floor = Math.Min(FloorDb, -6);
        var track = new Rect(TrackLeft, TrackInsetY, TrackWidth, height - (TrackInsetY * 2));
        var radius = TrackWidth / 2;

        // 开口：槽外面再套一圈更暗的边，看起来是嵌进面板里的
        dc.DrawRoundedRectangle(
            WellEdge,
            null,
            new Rect(track.X - 1, track.Y - 1, track.Width + 2, track.Height + 2),
            radius + 1,
            radius + 1);

        dc.DrawRoundedRectangle(CreateWell(), null, track, radius, radius);

        // 槽内的上暗下亮，是凹陷感的来源
        dc.PushClip(new RectangleGeometry(track, radius, radius));
        dc.DrawRectangle(CreateTopInset(track), null, new Rect(track.X, track.Y, track.Width, Math.Min(12, track.Height * 0.2)));
        dc.DrawRectangle(CreateBottomInset(track), null, new Rect(track.X, track.Bottom - Math.Min(8, track.Height * 0.12), track.Width, 8));

        // 电平条
        var t = Math.Clamp((Math.Clamp(LevelDb, floor, 0) - floor) / -floor, 0, 1);
        var filled = track.Height * t;
        if (filled > 1)
        {
            var bar = new Rect(track.X + 2, track.Bottom - filled, track.Width - 4, filled);
            dc.DrawRoundedRectangle(CreateFill(track), null, bar, 2.5, 2.5);

            // 左侧一条竖向高光：玻璃管子的反光
            var glossHeight = Math.Max(0, bar.Height - 3);
            if (glossHeight > 2)
            {
                dc.DrawRoundedRectangle(
                    GlossBrush,
                    null,
                    new Rect(bar.X + 1, bar.Y + 1.5, 2.2, glossHeight),
                    1,
                    1);
            }

            // 顶端一小段亮边
            dc.DrawRoundedRectangle(GlossBrush, null, new Rect(bar.X + 1, bar.Y, bar.Width - 2, 1.5), 0.75, 0.75);
        }

        dc.Pop();

        // 刻度：短线露在槽右边，数字跟在后面
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var tick in Ticks)
        {
            if (tick < floor)
            {
                continue;
            }

            var ty = track.Bottom - (track.Height * ((tick - floor) / -floor));
            dc.DrawLine(TickPen, new Point(track.Right + 1, ty), new Point(track.Right + 4, ty));

            var text = new FormattedText(
                Math.Abs(tick).ToString("0", CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Face,
                9,
                TickBrush,
                dpi);

            dc.DrawText(text, new Point(track.Right + 5, ty - (text.Height / 2)));
        }
    }

    /// <summary>槽底：横向渐变，左壁亮、右壁暗，读成一根圆柱的内壁。</summary>
    private static Brush CreateWell()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x05, 0x05, 0x05), 0.0),
                new GradientStop(Color.FromRgb(0x1E, 0x1E, 0x1E), 0.16),
                new GradientStop(Color.FromRgb(0x3C, 0x3C, 0x3C), 0.38),
                new GradientStop(Color.FromRgb(0x14, 0x14, 0x14), 1.0),
            },
        };

        brush.Freeze();
        return brush;
    }

    private static Brush CreateTopInset(Rect track)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(track.Left, track.Top),
            EndPoint = new Point(track.Left, track.Top + Math.Min(12, track.Height * 0.2)),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0xB8, 0, 0, 0), 0.0),
                new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1.0),
            },
        };

        brush.Freeze();
        return brush;
    }

    private static Brush CreateBottomInset(Rect track)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(track.Left, track.Bottom - 8),
            EndPoint = new Point(track.Left, track.Bottom),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 0.0),
                new GradientStop(Color.FromArgb(0x59, 0x00, 0x00, 0x00), 1.0),
            },
        };

        brush.Freeze();
        return brush;
    }

    /// <summary>颜色固定在轨道上（绝对坐标），条子长短变了颜色也不会跟着漂。</summary>
    private static Brush CreateFill(Rect track)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(track.Left, track.Bottom),
            EndPoint = new Point(track.Left, track.Top),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x3A, 0xA8, 0xE8), 0.0),
                new GradientStop(Color.FromRgb(0x5A, 0xD0, 0x9B), 0.55),
                new GradientStop(Color.FromRgb(0xE8, 0xB0, 0x3C), 0.82),
                new GradientStop(Color.FromRgb(0xE8, 0x5A, 0x5A), 1.0),
            },
        };

        brush.Freeze();
        return brush;
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
