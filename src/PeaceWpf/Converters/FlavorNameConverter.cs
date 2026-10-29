using System.Globalization;
using System.Windows.Data;
using PeaceWpf.Core.AutoFlavor;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Converters;

/// <summary>
/// 风味名称的两套叫法：游戏视角（爆炸、脚步…）和乐器视角（低音提琴、长笛…）。
/// 绑定 [ToneBand, IsInstrumentNaming, 保留键]；保留键命中时（自动风味选男声/女声）
/// 那一条仍是自己的游戏名，不跟着换成乐器。
/// </summary>
public sealed class FlavorNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not ToneBand band)
        {
            return string.Empty;
        }

        var instrument = values[1] is bool flag && flag;
        var overrideKey = values.Length > 2 ? values[2] as string : null;
        return FlavorNaming.NameOf(band, instrument, overrideKey);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
