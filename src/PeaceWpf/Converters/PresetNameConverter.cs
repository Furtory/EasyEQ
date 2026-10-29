using System.Globalization;
using System.Windows.Data;

namespace PeaceWpf.Converters;

/// <summary>
/// 预设下拉里只显示名字，不带 <c>.peace</c> 后缀；选中的值仍然是完整文件名。
/// </summary>
public sealed class PresetNameConverter : IValueConverter
{
    private const string Extension = ".peace";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? text[..^Extension.Length]
            : text;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
