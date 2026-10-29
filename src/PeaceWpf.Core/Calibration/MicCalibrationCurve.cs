using System.Globalization;
using PeaceWpf.Core.Audio;

namespace PeaceWpf.Core.Calibration;

/// <summary>
/// 麦克风矫正曲线：频率 → 麦克风自己的响应偏差（dB）。
///
/// 测量麦不是一把绝对平的尺子：廉价电容麦在 8 kHz 以上慢慢掉、低频还会鼓。
/// 自动EQ 测到的是「喇叭 + 麦克风」，不先把麦克风那段减掉，拟合出来的 EQ 就是在
/// 替麦克风纠偏——换个麦克风听，声音就不对了。
///
/// 文件是最常见的那种两列文本：左边频率（Hz，可以写 1k / 1kHz），右边偏差。
/// REW、UMIK、miniDSP 导出的 txt / csv 都能直接吃：表头、注释、`Sens Factor` 那类行跳过即可。
/// </summary>
public sealed class MicCalibrationCurve
{
    /// <summary>用来当锚点的频率。实测本来就以它做 0 dB，矫正后也要保持是它不动。</summary>
    public const double AnchorHz = 1000;

    private readonly double[] _frequencies;
    private readonly double[] _gains;

    private MicCalibrationCurve(double[] frequencies, double[] gains, string? name)
    {
        _frequencies = frequencies;
        _gains = gains;
        Name = name;
        AnchorGainDb = GainAt(AnchorHz);
    }

    /// <summary>文件名或来源，界面上显示用。</summary>
    public string? Name { get; }

    /// <summary>频点个数。</summary>
    public int PointCount => _frequencies.Length;

    /// <summary>曲线覆盖的最低频率。</summary>
    public double MinFrequencyHz => _frequencies[0];

    /// <summary>曲线覆盖的最高频率。</summary>
    public double MaxFrequencyHz => _frequencies[^1];

    /// <summary>整条曲线里最小 / 最大的偏差，界面上给人一眼判断「这条曲线凶不凶」。</summary>
    public double MinGainDb => _gains.Min();

    /// <inheritdoc cref="MinGainDb"/>
    public double MaxGainDb => _gains.Max();

    /// <summary>这条曲线在 1 kHz 上的偏差。矫正时先把它当 0 点，1 kHz 锚点才不会跟着漂。</summary>
    public double AnchorGainDb { get; }

    /// <summary>
    /// 读一个文件。读不出来时抛 <see cref="FormatException"/>，带一句能直接显示给人看的话。
    /// </summary>
    public static MicCalibrationCurve Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var text = File.ReadAllText(path);
        return Parse(text, Path.GetFileName(path));
    }

    /// <summary>
    /// 解析两列文本。认不出的行（表头、注释、序列号）直接跳过，不当作错误。
    /// </summary>
    /// <param name="text">整份文件内容。</param>
    /// <param name="name">界面上显示的名字，一般是文件名。</param>
    public static MicCalibrationCurve Parse(string text, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var points = new List<(double Hz, double Db)>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // REW 用 * 标出 0 dB 参考点：「*1000 0.0」是数据，不是注释。
            if (line[0] == '*')
            {
                line = line[1..].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
            }

            // 真注释（# ; //）和文字表头都不要。
            if (line[0] is '#' or ';' || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var tokens = line.Split(
                new[] { ' ', '\t', ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var numbers = new List<double>(2);
            foreach (var token in tokens)
            {
                if (TryParseNumber(token, out var value))
                {
                    numbers.Add(value);
                }
            }

            // 少于两列的不是数据行：表头、Sens Factor、SERNO 之类都在这里面。
            if (numbers.Count < 2)
            {
                continue;
            }

            var hz = numbers[0];
            var db = numbers[1];
            if (!double.IsFinite(hz) || !double.IsFinite(db) || hz <= 0)
            {
                continue;
            }

            points.Add((hz, db));
        }

        if (points.Count < 2)
        {
            throw new FormatException(
                "这个文件里没找到可用的两点以上「频率 偏差」。"
                + "两列文本就行，第一列是 Hz（也可以写 1k），第二列是 dB。");
        }

        // 频率升序；重复频率取平均（有的导出文件末尾会重复一行参考点）。
        points.Sort((a, b) => a.Hz.CompareTo(b.Hz));
        var frequencies = new List<double>(points.Count);
        var gains = new List<double>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            if (frequencies.Count > 0 && Math.Abs(frequencies[^1] - points[i].Hz) < 1e-6)
            {
                gains[^1] = (gains[^1] + points[i].Db) / 2;
                continue;
            }

            frequencies.Add(points[i].Hz);
            gains.Add(points[i].Db);
        }

        return new MicCalibrationCurve(frequencies.ToArray(), gains.ToArray(), name);
    }

    /// <summary>
    /// 某个频率上的麦克风偏差。按对数频率线性插值（两列数据在对数坐标里是直的）。
    /// 曲线范围以外沿用最近端点：麦克风不会在 20 Hz 突然变平。
    /// </summary>
    public double GainAt(double frequencyHz)
    {
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0)
        {
            return 0;
        }

        if (frequencyHz <= _frequencies[0])
        {
            return _gains[0];
        }

        if (frequencyHz >= _frequencies[^1])
        {
            return _gains[^1];
        }

        var index = Array.BinarySearch(_frequencies, frequencyHz);
        if (index >= 0)
        {
            return _gains[index];
        }

        var high = ~index;
        var low = high - 1;
        var logLow = Math.Log(_frequencies[low]);
        var logHigh = Math.Log(_frequencies[high]);
        var span = logHigh - logLow;
        if (span <= 0)
        {
            return _gains[low];
        }

        var t = (Math.Log(frequencyHz) - logLow) / span;
        return _gains[low] + (_gains[high] - _gains[low]) * t;
    }

    /// <summary>
    /// 把这条曲线套到一份实测上：<c>修正 = 实测 −（麦克风偏差 − 1 kHz 偏差）</c>。
    ///
    /// 减掉 1 kHz 那一项，是为了让锚点不动：实测原本以 1 kHz 为 0 dB，
    /// 不这么做整条线会整体平移，用户看到的是「一导入曲线，实测就上下跳了一格」。
    /// </summary>
    public SweepMeasurement ApplyTo(SweepMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        var points = new MeasurementPoint[measurement.Points.Count];
        for (var i = 0; i < points.Length; i++)
        {
            var point = measurement.Points[i];
            var deviation = GainAt(point.FrequencyHz) - AnchorGainDb;
            points[i] = point with
            {
                LevelDb = point.LevelDb - deviation,
                RawDb = point.RawDb - deviation,
            };
        }

        return new SweepMeasurement(points, measurement.NoiseFloorDb, measurement.SampleRate, measurement.Frequencies)
        {
            NoiseFloorsDb = measurement.NoiseFloorsDb,
            EngineName = measurement.EngineName,
        };
    }

    /// <summary>
    /// 画图用的采样：在对数频率上均匀取点，只取曲线覆盖的范围。
    /// </summary>
    public IReadOnlyList<(double Hz, double Db)> Sample(int count = 240, double fromHz = 20, double toHz = 20000)
    {
        if (count < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "采样点数至少 2 个。");
        }

        var start = Math.Max(fromHz, MinFrequencyHz);
        var end = Math.Min(toHz, MaxFrequencyHz);
        if (end <= start)
        {
            return Array.Empty<(double, double)>();
        }

        var result = new (double Hz, double Db)[count];
        var ratio = end / start;
        for (var i = 0; i < count; i++)
        {
            var hz = start * Math.Pow(ratio, i / (count - 1.0));
            result[i] = (hz, GainAt(hz));
        }

        return result;
    }

    /// <summary>
    /// 认一列数字：允许正负号、小数点、科学计数法，也允许 1k / 1kHz / 1000Hz 这几种写法。
    /// </summary>
    private static bool TryParseNumber(string token, out double value)
    {
        value = 0;
        var text = token.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        // 去掉单位，只留数字和 k 这类倍率。顺序很重要：先剥 kHz，再剥 Hz。
        var multiplier = 1.0;
        if (text.EndsWith("kHz", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^3];
            multiplier = 1000;
        }
        else if (text.EndsWith("Hz", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
        }
        else if (text.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^1];
            multiplier = 1000;
        }

        text = text.Trim();
        if (text.Length == 0 || text is "+" or "-" or ".")
        {
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed * multiplier;
        return double.IsFinite(value);
    }
}
