namespace PeaceWpf.Core.AutoEq;

/// <summary>
/// AutoEQ 结果库里的一台设备。原始数据行按需解析，加载时只切行，不建对象图。
/// </summary>
public sealed class AutoEqDevice
{
    internal AutoEqDevice(string name, string measurer, string way)
    {
        Name = name;
        Measurer = measurer;
        Way = way;
    }

    /// <summary>型号名，例如 <c>1Custom SA02</c>。</summary>
    public string Name { get; }

    /// <summary>测量者 / 来源，例如 <c>crinacle</c>。</summary>
    public string Measurer { get; }

    /// <summary>佩戴与耦合方式，例如 <c>711 in-ear</c>。</summary>
    public string Way { get; }

    /// <summary>OPRA 数据库里的相对路径；Harman / IEF 库里是空的。</summary>
    public string? OpraPath { get; internal set; }

    internal string? ParametricLine { get; set; }

    internal string? FixedBandLine { get; set; }

    internal string? GraphicLine { get; set; }

    /// <summary>有没有参数 EQ。</summary>
    public bool HasParametric => !string.IsNullOrEmpty(ParametricLine);

    /// <summary>有没有固定频带 EQ。</summary>
    public bool HasFixedBand => !string.IsNullOrEmpty(FixedBandLine);

    /// <summary>有没有图形 EQ。</summary>
    public bool HasGraphic => !string.IsNullOrEmpty(GraphicLine);

    /// <summary>这种形态在不在。</summary>
    public bool Has(AutoEqType type) => type switch
    {
        AutoEqType.Parametric => HasParametric,
        AutoEqType.FixedBand => HasFixedBand,
        AutoEqType.Graphic => HasGraphic,
        _ => false,
    };

    /// <summary>列表里显示的一行：型号 · 方式 · 测量者。</summary>
    public string Display
    {
        get
        {
            var parts = new List<string>(3) { Name };
            if (!string.IsNullOrWhiteSpace(Way))
            {
                parts.Add(Way);
            }

            if (!string.IsNullOrWhiteSpace(Measurer))
            {
                parts.Add(Measurer);
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>列表第二行：佩戴方式 + 测量者。</summary>
    public string Subtitle => string.Join(
        " · ",
        new[] { Way, Measurer }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>搜索用的一整串（型号 + 方式 + 测量者），已转小写。</summary>
    public string SearchText => _searchText ??= (Name + " " + Way + " " + Measurer).ToLowerInvariant();

    private string? _searchText;

    public override string ToString() => Display;
}
