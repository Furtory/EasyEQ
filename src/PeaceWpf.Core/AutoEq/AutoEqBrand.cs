namespace PeaceWpf.Core.AutoEq;

/// <summary>
/// 型号名 → 品牌图标短名（对应 AutoEqData/brand-logos.json 里的键）。
///
/// 只收「能拿到官方图标」的品牌：图标来自 Simple Icons（CC0），是各家的商标矢量路径，
/// 不是我们现画的。音频小厂和国内牌子大多不在这个库里，它们没有图标，列表里就不显示，
/// 不拿自制的色块或首字母去凑。
/// </summary>
public static class AutoEqBrand
{
    /// <summary>
    /// 品牌前缀 → 图标键。按前缀长度从长到短排，避免短前缀抢先匹配。
    /// </summary>
    private static readonly (string Prefix, string Slug)[] Map =
    {
        ("Audio-Technica", "audio-technica"),
        ("Austrian Audio", "austrian"),
        ("SteelSeries", "steelseries"),
        ("Sennheiser", "sennheiser"),
        ("Beyerdynamic", "beyerdynamic"),
        ("Skullcandy", "skullcandy"),
        ("HiFiMan", "hifiman"),
        ("Panasonic", "panasonic"),
        ("Motorola", "motorola"),
        ("Monoprice", "monoprice"),
        ("Yamaha", "yamahacorporation"),
        ("OnePlus", "oneplus"),
        ("Samsung", "samsung"),
        ("Corsair", "corsair"),
        ("HyperX", "hyperx"),
        ("Xiaomi", "xiaomi"),
        ("Lenovo", "lenovo"),
        ("Google", "google"),
        ("Huawei", "huawei"),
        ("Edifier", "edifier"),
        ("Philips", "philips"),
        ("Klipsch", "klipsch"),
        ("Fostex", "fostex"),
        ("Shure", "shure"),
        ("Sony", "sony"),
        ("Bose", "bose"),
        ("JBL", "jbl"),
        ("Denon", "denon"),
        ("Sonos", "sonos"),
        ("Beats", "beats"),
        ("Apple", "apple"),
        ("Koss", "koss"),
        ("Meze", "meze"),
        ("Oppo", "oppo"),
        ("Nokia", "nokia"),
        ("Focal", "focal"),
        ("FiiO", "fiio"),
        ("Asus", "asus"),
        ("Honor", "honor"),
        ("Vivo", "vivo"),
        ("Meizu", "meizu"),
        ("Razer", "razer"),
        ("MSI", "msi"),
        ("AKG", "akg"),
        ("HP", "hp"),
        ("LG", "lg"),
    };

    /// <summary>
    /// 取型号名开头的品牌图标键；认不出返回 null。
    ///
    /// 前缀后面必须是非字母（空格、数字、连字符都行），免得「HP」误吃「HPTuner」这种型号名。
    /// </summary>
    public static string? TryMatch(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var (prefix, slug) in Map)
        {
            if (name.Length < prefix.Length
                || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.Length == prefix.Length || !char.IsLetter(name[prefix.Length]))
            {
                return slug;
            }
        }

        return null;
    }
}
