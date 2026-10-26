using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PeaceWpf.Theme;

namespace PeaceWpf.Converters;

/// <summary>
/// 把风味的 Key 换成它的图标几何（Lucide 路径）。下拉里每一项前面那个小图标靠它，
/// 几何表只有 <see cref="FlavorIcons"/> 一份。
/// </summary>
public sealed class FlavorIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        FlavorIcons.IconFor(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("风味图标只读，不往回写。");
}
