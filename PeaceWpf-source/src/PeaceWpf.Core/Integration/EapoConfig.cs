using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.Integration;

/// <summary>
/// EqualizerAPO 配置文件的生成与解析。
///
/// 保存不再逐段遥控后端（那会让 Peace 重建界面、很慢），而是把合并后的 EQ
/// 直接写成 `peace.txt`。EqualizerAPO 监视配置文件，写完就生效，Peace 完全不参与。
/// </summary>
public static partial class EapoConfig
{
    /// <summary>Peace 使用的配置文件。</summary>
    public const string FileName = "peace.txt";

    /// <summary>平衡到端点时衰减多少 dB。</summary>
    public const double BalanceMaxDb = 12.0;

    /// <summary>开发/自测时用来指定 config 目录的环境变量。</summary>
    public const string ConfigDirectoryVariable = "PEACEWPF_CONFIG_DIR";

    /// <summary>
    /// 找 EqualizerAPO 的 config 目录。
    /// 环境变量 <see cref="ConfigDirectoryVariable"/> 优先，用来在不动真实安装的前提下试界面。
    /// </summary>
    public static string? FindConfigDirectory(string? overrideDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideDirectory) && Directory.Exists(overrideDirectory))
        {
            return overrideDirectory;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(ConfigDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidate = Path.Combine(programFiles, "EqualizerAPO", "config");
        return Directory.Exists(candidate) ? candidate : null;
    }

    /// <summary>文件带只读位时清掉它——不然覆盖写会抛 UnauthorizedAccessException。</summary>
    public static void ClearReadOnly(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.IsReadOnly)
            {
                info.IsReadOnly = false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 清不掉就让随后的写入抛它自己的错，别在这里吞掉真正的原因。
        }
    }

    /// <summary>配置目录能不能写。写不了时保存必然失败，提前告诉用户比事后报错好。</summary>
    public static bool CanWrite(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        try
        {
            var probe = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>生成整份配置。点按自己的滤波器类型写行，PK 仍是默认。</summary>
    public static string Build(
        IReadOnlyList<EqPoint> filters,
        double preAmpDb,
        double balance,
        string deviceLine)
    {
        ArgumentNullException.ThrowIfNull(filters);

        var builder = new StringBuilder();
        builder.AppendLine(NormalizeDevice(deviceLine));
        builder.AppendLine(Format("Preamp: {0:0.0} dB", preAmpDb));

        var clamped = Math.Clamp(balance, -1, 1);
        if (Math.Abs(clamped) > 0.0005)
        {
            // 往左滑衰减右声道，往右滑衰减左声道；写完切回所有声道。
            var channel = clamped < 0 ? "R" : "L";
            var attenuation = -Math.Abs(clamped) * BalanceMaxDb;
            builder.AppendLine($"Channel: {channel}");
            builder.AppendLine(Format("Preamp: {0:0.0} dB", attenuation));
            builder.AppendLine("Channel: all");
        }

        for (var i = 0; i < filters.Count; i++)
        {
            var point = filters[i];
            builder.AppendLine(Format(
                "Filter {0}: ON {1} Fc {2:0} Hz Gain {3:0.0} dB Q {4:0.00}",
                i + 1,
                EqLimits.FilterName(point.FilterType),
                point.FrequencyHz,
                point.GainDb,
                point.Quality));
        }

        return builder.ToString();
    }

    /// <summary>把设备行归一化；空的时候退回 `Device: all`。</summary>
    public static string NormalizeDevice(string? deviceLine)
    {
        var trimmed = deviceLine?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "Device: all" : trimmed;
    }

    /// <summary>
    /// 解析配置文件。只认 PK 滤波器；非 PK 的数量会单独报出来，
    /// 因为写回去的时候它们会变成 PK。
    /// </summary>
    public static EapoConfigData Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var device = "Device: all";
        var preAmp = 0.0;
        var preAmpSeen = false;
        var channelScoped = false;
        var points = new List<EqPoint>();
        var nonPeaking = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("Device:", StringComparison.OrdinalIgnoreCase))
            {
                device = line;
                continue;
            }

            if (line.StartsWith("Channel:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["Channel:".Length..].Trim();
                channelScoped = !string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (line.StartsWith("Preamp:", StringComparison.OrdinalIgnoreCase))
            {
                if (!preAmpSeen && !channelScoped && TryParseNumber(line["Preamp:".Length..], out var value))
                {
                    preAmp = value;
                    preAmpSeen = true;
                }

                continue;
            }

            var match = FilterRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var enabled = match.Groups["state"].Value.Equals("ON", StringComparison.OrdinalIgnoreCase);
            if (!enabled)
            {
                continue;
            }

            var type = match.Groups["type"].Value.ToUpperInvariant();
            var filterIndex = EqLimits.FilterIndex(type);
            if (!EqLimits.IsSupportedFilter(filterIndex))
            {
                nonPeaking++;
                continue;
            }

            var frequency = double.Parse(match.Groups["freq"].Value, CultureInfo.InvariantCulture);
            var gain = double.Parse(match.Groups["gain"].Value, CultureInfo.InvariantCulture);
            var quality = match.Groups["q"].Success
                ? double.Parse(match.Groups["q"].Value, CultureInfo.InvariantCulture)
                : EqLimits.DefaultQuality;

            points.Add(new EqPoint(0, frequency, gain, quality, false, filterIndex));
        }

        var list = EqPointList.FromPoints(points);
        return new EapoConfigData(device, preAmp, list, nonPeaking);
    }

    private static bool TryParseNumber(string text, out double value)
    {
        var token = text.Trim();
        var space = token.IndexOf(' ');
        if (space > 0)
        {
            token = token[..space];
        }

        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string Format(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);

    [GeneratedRegex(@"^Filter\s+\d+:\s+(?<state>ON|OFF)\s+(?<type>[A-Za-z0-9]+)\s+Fc\s+(?<freq>[\d.]+)\s+Hz\s+Gain\s+(?<gain>[-\d.]+)\s+dB(?:\s+Q\s+(?<q>[\d.]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex FilterRegex();
}

/// <summary>解析出来的配置。</summary>
/// <param name="DeviceLine">原始的设备行。</param>
/// <param name="PreAmpDb">全局 preamp。</param>
/// <param name="Points">PK 滤波器转成的点。</param>
/// <param name="NonPeakingFilters">非 PK 滤波器的数量（写回去会丢）。</param>
public sealed record EapoConfigData(
    string DeviceLine,
    double PreAmpDb,
    EqPointList Points,
    int NonPeakingFilters);
