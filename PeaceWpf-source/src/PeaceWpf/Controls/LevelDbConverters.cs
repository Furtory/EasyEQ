using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PeaceWpf.Controls;

/// <summary>
/// 电平数字的写法：-60 dB 以下就当作没有声音，写成 -∞。
///
/// 60 这个数和电平表的默认 FloorDb 是同一个值：表里的条子在那以下是空的，
/// 数字也跟着说「没有」，而不是报一个 −90.0 这种看起来像真实读数的数。
/// </summary>
public sealed class LevelDbTextConverter : IValueConverter
{
    private const double SilentDb = -60;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var db = value is double d && !double.IsNaN(d) ? d : SilentDb;
        return db <= SilentDb
            ? "-∞"
            : db.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 电平数字的颜色：-6 dB 起转红。-6 是表上最上面那条刻度，越过它只剩几 dB 余量，
/// 再响一点就要削波；颜色比数字更先被人看见，所以用它来提醒。
///
/// 两个 brush 都是冻结的静态对象：这个绑定每 16 ms 刷新一次，不该每帧新分配。
/// </summary>
public sealed class LevelDbBrushConverter : IValueConverter
{
    private const double WarnDb = -6;

    // 和旁边增益数字一样的浅灰（0.75 不透明度），红的时候换成同一档亮度。
    private static readonly Brush Normal = Frozen(Color.FromArgb(0xBF, 0xF0, 0xF0, 0xF0));
    private static readonly Brush Hot = Frozen(Color.FromRgb(0xFF, 0x6E, 0x6E));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double db && db >= WarnDb ? Hot : Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
