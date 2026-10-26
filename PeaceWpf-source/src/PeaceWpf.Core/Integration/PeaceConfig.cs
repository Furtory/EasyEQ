using System.Globalization;
using System.Text;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.Integration;

/// <summary>
/// Peace 预设文件（`.peace`）的读写。
///
/// 只碰当前声道组合的三段：`[Frequencies]`、`[Gains]`、`[Qualities]`。
/// 其它段（其它组合、`[General]`、`[Speakers]`）原样保留，
/// 这样写回去的文件 Peace 能直接打开，也不会破坏别的组合。
/// </summary>
public static class PeaceConfig
{
    /// <summary>Peace 的预设扩展名。</summary>
    public const string Extension = ".peace";

    private static readonly string[] EditableSections = { "Frequencies", "Gains", "Qualities", "Filters" };

    /// <summary>读一个 .peace；文件不存在返回 null。</summary>
    public static EqPointList? Load(string path, out int nonPeakingFilters)
    {
        nonPeakingFilters = 0;
        if (!File.Exists(path))
        {
            return null;
        }

        var frequencies = new Dictionary<int, double>();
        var gains = new Dictionary<int, double>();
        var qualities = new Dictionary<int, double>();
        var filters = new Dictionary<int, int>();
        var section = string.Empty;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            var split = line.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim();

            switch (section)
            {
                case "Frequencies":
                    if (TryIndex(key, "Frequency", out var fi) && TryDouble(value, out var fv)) frequencies[fi] = fv;
                    break;
                case "Gains":
                    if (TryIndex(key, "Gain", out var gi) && TryDouble(value, out var gv)) gains[gi] = gv;
                    break;
                case "Qualities":
                    if (TryIndex(key, "Quality", out var qi) && TryDouble(value, out var qv)) qualities[qi] = qv;
                    break;
                case "Filters":
                    if (TryIndex(key, "Filter", out var ti) && int.TryParse(value, out var tv)) filters[ti] = tv;
                    break;
            }
        }

        if (frequencies.Count == 0)
        {
            return EqPointList.CreateDefault();
        }

        var points = new List<EqPoint>();
        foreach (var (index, frequency) in frequencies.OrderBy(pair => pair.Key))
        {
            var gain = gains.GetValueOrDefault(index);
            var quality = qualities.TryGetValue(index, out var q) ? q : EqLimits.DefaultQuality;
            var filter = filters.TryGetValue(index, out var t) ? t : EqLimits.PeakingFilter;

            // 低通、高通这些点模型表达不了的类型仍然跳过并计数；
            // 峰值和架式（AutoEQ 会带来低架 / 高架）原样保留。
            if (!EqLimits.IsSupportedFilter(filter))
            {
                nonPeakingFilters++;
                continue;
            }

            points.Add(new EqPoint(0, frequency, gain, quality, false, filter));
        }

        return EqPointList.FromPoints(points);
    }

    /// <summary>
    /// 把一个点列表写回 .peace。已有文件只替换三段，其它内容保留；
    /// 文件不存在就生成最小可用的一份。
    /// </summary>
    public static bool Save(string path, IReadOnlyList<EqPoint> points, out string error)
    {
        ArgumentNullException.ThrowIfNull(points);
        error = string.Empty;

        try
        {
            // 新文件拿同目录的现成预设当模板，保住 [General]/[Speakers]，Peace 才认得。
            var seed = File.Exists(path) ? path : FindTemplate(path);
            var sections = seed is not null
                ? ReadSections(File.ReadAllLines(seed))
                : new List<Section>();

            var replacements = new Dictionary<string, string[]>
            {
                ["Frequencies"] = BuildNumbers("Frequency", points.Select(p => p.FrequencyHz.ToString("0", CultureInfo.InvariantCulture))),
                ["Gains"] = BuildNumbers("Gain", points.Select(p => p.GainDb.ToString("0.0", CultureInfo.InvariantCulture))),
                ["Qualities"] = BuildNumbers("Quality", points.Select(p => p.Quality.ToString("0.##", CultureInfo.InvariantCulture))),

                // 和 Peace 一样稀疏写：只列不是 PK 的段，PK 不用写。
                ["Filters"] = BuildFilters(points),
            };

            var anchor = sections.FindIndex(s => s.Name.Equals("Frequencies", StringComparison.OrdinalIgnoreCase));
            var insertAt = anchor >= 0 ? anchor + 1 : sections.Count;

            foreach (var name in EditableSections)
            {
                var index = sections.FindIndex(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                var block = new[] { "[" + name + "]" }.Concat(replacements[name]).ToArray();

                if (index >= 0)
                {
                    sections[index] = new Section(name, block);
                    if (index < insertAt)
                    {
                        insertAt = index + 1;
                    }
                }
                else
                {
                    // 缺段就按 Frequencies → Gains → Qualities 的顺序补在锚点后面。
                    sections.Insert(insertAt, new Section(name, block));
                    insertAt++;
                }
            }

            var text = new StringBuilder();
            foreach (var section in sections)
            {
                foreach (var line in section.Lines)
                {
                    text.AppendLine(line);
                }
            }

            EapoConfig.ClearReadOnly(path);
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    /// <summary>找一个现成的预设当模板。</summary>
    private static string? FindTemplate(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            return null;
        }

        foreach (var name in new[] { "0.peace", "6.peace", "Last Configuration.peace" })
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var any = Directory.EnumerateFiles(directory, "*" + Extension).FirstOrDefault();
        return any;
    }

    /// <summary>列出目录里的 .peace 预设。</summary>
    public static IReadOnlyList<string> ListPresets(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(directory, "*" + Extension)
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string[] BuildNumbers(string prefix, IEnumerable<string> values)
    {
        var list = new List<string>();
        var index = 1;
        foreach (var value in values)
        {
            list.Add($"{prefix}{index}={value}");
            index++;
        }

        return list.ToArray();
    }

    /// <summary>只写不是 PK 的段：`Filter3=8` 表示第 3 段是 LSC。空数组就是全 PK。</summary>
    private static string[] BuildFilters(IReadOnlyList<EqPoint> points)
    {
        var list = new List<string>();
        for (var i = 0; i < points.Count; i++)
        {
            var type = EqLimits.ClampFilter(points[i].FilterType);
            if (type != EqLimits.PeakingFilter)
            {
                list.Add($"Filter{i + 1}={type}");
            }
        }

        return list.ToArray();
    }

    private static bool TryIndex(string key, string prefix, out int index)
    {
        index = 0;
        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key[prefix.Length..], out index)
            && index > 0;
    }

    private static bool TryDouble(string value, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);

    /// <summary>把文件拆成“段”，保留段外的内容（比如文件头注释）。</summary>
    private static List<Section> ReadSections(string[] lines)
    {
        var sections = new List<Section>();
        Section? current = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                current = new Section(trimmed[1..^1].Trim(), new List<string>());
                sections.Add(current);
                current.Lines.Add(line);
                continue;
            }

            if (current is null)
            {
                current = new Section(string.Empty, new List<string>());
                sections.Add(current);
            }

            current.Lines.Add(line);
        }

        return sections;
    }

    private sealed class Section
    {
        public Section(string name, IReadOnlyList<string> lines)
        {
            Name = name;
            Lines = lines.ToList();
        }

        public Section(string name, List<string> lines)
        {
            Name = name;
            Lines = lines;
        }

        public string Name { get; }

        public List<string> Lines { get; }
    }
}
