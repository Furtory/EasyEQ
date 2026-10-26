using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PeaceWpf.Core.AutoEq;

namespace PeaceWpf.Controls;

/// <summary>
/// 型号 → 品牌图标。返回值有三种：Geometry（矢量）、BitmapImage（位图）、null（没有图标）。
///
/// 两个来源：
/// 1. AutoEqData/brand-logos.json —— Simple Icons（CC0）里的矢量路径，正方形标记；
/// 2. AutoEqData/brand-logos/*.png —— 维基共享资源上的品牌字标/标记，已经统一转成白色单色、
///    去底、按 64px 高保存，深色主题下直接可用。
///
/// 都是现成素材，没有现画的占位图；缺哪个就在列表里不显示，文字照常顶格。
/// </summary>
public sealed class BrandLogoConverter : IValueConverter
{
    private static readonly Dictionary<string, object?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string>? _paths;
    private static bool _loadedPaths;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AutoEqDevice device)
        {
            return null;
        }

        var slug = AutoEqBrand.TryMatch(device.Name);
        if (slug is null)
        {
            return null;
        }

        if (!Cache.TryGetValue(slug, out var icon))
        {
            icon = (object?)BuildGeometry(slug) ?? BuildImage(slug);
            Cache[slug] = icon;
        }

        return icon;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Geometry? BuildGeometry(string slug)
    {
        EnsurePathsLoaded();

        if (_paths is null || !_paths.TryGetValue(slug, out var data))
        {
            return null;
        }

        try
        {
            var geometry = Geometry.Parse(data);
            geometry.Freeze();
            return geometry;
        }
        catch (FormatException)
        {
            // 某一条路径写坏了不该让整个列表崩掉，跳过它就行。
            return null;
        }
    }

    private static BitmapImage? BuildImage(string slug)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "AutoEqData", "brand-logos", slug + ".png");
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(file, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            // 图坏了就当没有，不影响列表。
            return null;
        }
    }

    private static void EnsurePathsLoaded()
    {
        if (_loadedPaths)
        {
            return;
        }

        _loadedPaths = true;

        var file = Path.Combine(AppContext.BaseDirectory, "AutoEqData", "brand-logos.json");
        if (!File.Exists(file))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (entry.Value.TryGetProperty("path", out var path)
                    && path.GetString() is { Length: > 0 } data)
                {
                    map[entry.Name] = data;
                }
            }

            _paths = map;
        }
        catch (Exception)
        {
            // 读不出来就当作没有矢量图标，后面还有位图兜底。
        }
    }
}

/// <summary>
/// 按图标类型选模板：Geometry 画 Path（正方形，15px），BitmapImage 放 Image（字标，高度 12，最宽 56）。
/// </summary>
public sealed class BrandIconTemplateSelector : System.Windows.Controls.DataTemplateSelector
{
    public System.Windows.DataTemplate? Vector { get; set; }

    public System.Windows.DataTemplate? Bitmap { get; set; }

    public override System.Windows.DataTemplate? SelectTemplate(object item, System.Windows.DependencyObject container) =>
        item is Geometry ? Vector : Bitmap;
}
