using System.Globalization;
using System.Windows.Data;
using PeaceWpf.Theme;

namespace PeaceWpf.Converters;

/// <summary>
/// 把风味的 Key 换成它的颜色。下拉里每一项的字体颜色靠它，
/// 颜色表只有 <see cref="TonePalette"/> 一份。
/// </summary>
public sealed class FlavorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        TonePalette.BrushFor(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("风味颜色只读，不往回写。");
}
