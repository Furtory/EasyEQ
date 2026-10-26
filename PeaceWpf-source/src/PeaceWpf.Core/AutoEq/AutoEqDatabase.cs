namespace PeaceWpf.Core.AutoEq;

/// <summary>
/// 一个 AutoEQ 结果库（Harman / IEF / IEF+bass / OPRA）。
///
/// 文件是 Peace 的压缩文本格式，一条设备占 3–5 行。加载时只做切行和归类，
/// 滤波器等到真正选中那台设备时才解析——8853 台设备的库打开也不卡。
/// </summary>
public sealed class AutoEqDatabase
{
    private readonly List<AutoEqDevice> _devices;

    private AutoEqDatabase(string path, int version, int declaredCount, List<AutoEqDevice> devices)
    {
        Path = path;
        Version = version;
        DeclaredCount = declaredCount;
        _devices = devices;
    }

    /// <summary>数据文件路径。</summary>
    public string Path { get; }

    /// <summary>数据格式版本（文件第一行的第一个数字）。</summary>
    public int Version { get; }

    /// <summary>
    /// 文件第一行自称的数量。它数的是「结果」（一台设备可能有好几份），
    /// 不等于 <see cref="Devices"/> 的条数，只用来预分配容量。
    /// </summary>
    public int DeclaredCount { get; }

    /// <summary>解析出来的设备，顺序与文件一致。</summary>
    public IReadOnlyList<AutoEqDevice> Devices => _devices;

    /// <summary>读一个压缩格式的结果库。</summary>
    public static AutoEqDatabase Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
        {
            throw new InvalidDataException("AutoEQ 数据库是空的：" + path);
        }

        var (version, declared) = ParseHeader(lines[0]);
        var devices = new List<AutoEqDevice>(Math.Max(declared, 64));

        string measurer = string.Empty;
        string way = string.Empty;
        AutoEqDevice? current = null;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            switch (line[0])
            {
                case 'M':
                    measurer = line[1..].Trim();
                    break;

                case 'W':
                    way = line[1..].Trim();
                    break;

                case 'D':
                    current = new AutoEqDevice(line[1..].Trim(), measurer, way);
                    devices.Add(current);
                    break;

                case 'A' when current is not null:
                    // 两种 A 行都以 A + 预增益开头，靠预增益后面的第一个字母区分：
                    // F 是固定频带（10 段只有增益），其余是参数 EQ。
                    if (IsFixedBandLine(line))
                    {
                        current.FixedBandLine = line;
                    }
                    else
                    {
                        current.ParametricLine = line;
                    }

                    break;

                case 'R' when current is not null:
                    current.GraphicLine = line;
                    break;

                case 'O' when current is not null:
                    current.OpraPath = line[1..].Trim();
                    break;
            }
        }

        return new AutoEqDatabase(path, version, declared, devices);
    }

    /// <summary>
    /// 搜设备：空格分开的几个词要全部命中（型号、佩戴方式、测量者任一串里出现即可）。
    /// 空查询返回前 <paramref name="limit"/> 台。
    /// </summary>
    public IReadOnlyList<AutoEqDevice> Search(string? query, int limit = 500)
    {
        limit = Math.Clamp(limit, 1, 5000);

        if (string.IsNullOrWhiteSpace(query))
        {
            return _devices.Take(limit).ToArray();
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<AutoEqDevice>(Math.Min(limit, 64));

        foreach (var device in _devices)
        {
            var haystack = device.SearchText;
            var hit = true;
            foreach (var term in terms)
            {
                if (!haystack.Contains(term.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    hit = false;
                    break;
                }
            }

            if (!hit)
            {
                continue;
            }

            result.Add(device);
            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>取某台设备的某种等化；没有这种形态时返回 null。</summary>
    public AutoEqProfile? GetProfile(AutoEqDevice? device, AutoEqType type) =>
        device is null ? null : AutoEqProfile.Create(device, type);

    /// <summary>第一行是「版本 结果数」。</summary>
    private static (int Version, int DeclaredCount) ParseHeader(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var version = parts.Length > 0 && int.TryParse(parts[0], out var v) ? v : 0;
        var count = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 0;
        return (version, count);
    }

    /// <summary>固定频带行：A、预增益，然后第一个字母就是 F。</summary>
    private static bool IsFixedBandLine(string line)
    {
        var index = 1;
        return AutoEqParser.TryReadNumber(line, ref index, out _)
            && index < line.Length
            && line[index] == 'F';
    }
}
