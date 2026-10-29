using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PeaceWpf.Core.Model;
using PeaceWpf.Theme;

namespace PeaceWpf.Converters;

/// <summary>
/// 把风味换成它的图标几何。绑定 [ToneBand, IsInstrumentNaming, 保留键]：
/// 乐器视角用乐器剪影，男声/女声方向对应的那条仍用游戏视角的图标。
/// 几何表只有 <see cref="FlavorIcons"/> 一份。
/// </summary>
public sealed class FlavorIconConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 0)
        {
            return FlavorIcons.IconFor(null);
        }

        var key = values[0] switch
        {
            ToneBand band => band.Key,
            string text => text,
            _ => null,
        };

        var instrument = values.Length > 1 && values[1] is bool flag && flag;
        var overrideKey = values.Length > 2 ? values[2] as string : null;
        return FlavorIcons.IconFor(key, instrument, overrideKey);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("风味图标只读，不往回写。");
}
