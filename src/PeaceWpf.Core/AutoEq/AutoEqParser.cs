using System.Globalization;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.AutoEq;

/// <summary>AutoEQ 的一个滤波器，字段含义和 EqualizerAPO 的一行 Filter 相同。</summary>
/// <param name="FrequencyHz">中心频率（架式是拐点）。</param>
/// <param name="GainDb">增益。</param>
/// <param name="Quality">Q 值。固定频带一律 1.41。</param>
/// <param name="FilterType">对应 <see cref="EqLimits.FilterTypeNames"/> 的序号。</param>
public sealed record AutoEqFilter(double FrequencyHz, double GainDb, double Quality, int FilterType);

/// <summary>
/// AutoEQ 压缩格式的解析器。
///
/// 格式（Peace 的 <c>$AutoEQCompressedVersion = 5</c>）是一行一条记录，首字母是类型：
/// <list type="bullet">
/// <item><c>D</c> 起始一台设备，<c>M</c> 测量者，<c>W</c> 佩戴方式（in-ear / over-ear）。</item>
/// <item><c>A</c> 开头的行是参数 EQ；第二个 chars 是 <c>F</c> 时是固定频带 EQ。</item>
/// <item><c>R</c> 开头是图形 EQ，127 个值紧挨着写，每个值前面一个 <c>R</c>。</item>
/// <item><c>O</c> 开头是 OPRA 数据库里这份结果的相对路径。</item>
/// </list>
///
/// 参数 EQ 行里，首字母 <c>A</c> 后面先跟预增益，然后是若干滤波器：
/// <c>P</c> 峰值（频率、增益、Q）、<c>F</c> 固定频带（只有增益）、
/// <c>L</c>/<c>H</c> 低架/高架、<c>M</c>/<c>I</c> 居中低架/高架。数字紧挨着字母，没有分隔符：
/// <c>A-6M105G5.5Q0.7P180G-3.3Q0.57</c> 就是「预增益 -6，105 Hz 低架 +5.5 dB Q 0.7，180 Hz 峰值 -3.3 dB Q 0.57」。
/// </summary>
public static class AutoEqParser
{
    /// <summary>固定频带 EQ 的频率，Peace 的 <c>$AutoEQFixedFrequencies</c>。</summary>
    public static IReadOnlyList<double> FixedFrequencies { get; } = new double[]
    {
        31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000,
    };

    /// <summary>固定频带 EQ 的 Q，Peace 的 <c>$AutoEQFixedQ</c>。</summary>
    public const double FixedQuality = 1.41;

    /// <summary>图形 EQ 的频率，Peace 的 <c>$AutoEQGraphicFrequencies</c>（127 点，20 Hz–19.9 kHz）。</summary>
    public static IReadOnlyList<double> GraphicFrequencies { get; } = new double[]
    {
        20, 21, 22, 23, 24, 26, 27, 29, 30, 32, 34, 36, 38, 40, 43, 45, 48, 50, 53, 56, 59, 63, 66, 70, 74, 78, 83, 87,
        92, 97, 103, 109, 115, 121, 128, 136, 143, 151, 160, 169, 178, 188, 199, 210, 222, 235, 248, 262, 277, 292, 309,
        326, 345, 364, 385, 406, 429, 453, 479, 506, 534, 565, 596, 630, 665, 703, 743, 784, 829, 875, 924, 977, 1032,
        1090, 1151, 1216, 1284, 1357, 1433, 1514, 1599, 1689, 1784, 1885, 1991, 2103, 2221, 2347, 2479, 2618, 2766, 2921,
        3086, 3260, 3443, 3637, 3842, 4058, 4287, 4528, 4783, 5052, 5337, 5637, 5955, 6290, 6644, 7018, 7414, 7831, 8272,
        8738, 9230, 9749, 10298, 10878, 11490, 12137, 12821, 13543, 14305, 15110, 15961, 16860, 17809, 18812, 19871,
    };

    /// <summary>参数 EQ 行：预增益 + 滤波器表。</summary>
    public readonly record struct ParametricResult(double PreAmpDb, IReadOnlyList<AutoEqFilter> Filters);

    /// <summary>解析 <c>A</c> 开头的参数 EQ 行。行不成立时返回 false。</summary>
    public static bool TryParseParametric(string? line, out ParametricResult result)
    {
        result = default;
        if (string.IsNullOrEmpty(line) || line[0] != 'A' || line.Length < 3)
        {
            return false;
        }

        var index = 1;
        if (!TryReadNumber(line, ref index, out var preAmp))
        {
            return false;
        }

        var filters = new List<AutoEqFilter>(12);
        var fixedIndex = 0;

        while (index < line.Length)
        {
            var code = line[index];
            if (code == ' ')
            {
                index++;
                continue;
            }

            index++;

            if (code == 'F')
            {
                if (fixedIndex >= FixedFrequencies.Count || !TryReadNumber(line, ref index, out var fixedGain))
                {
                    return false;
                }

                filters.Add(new AutoEqFilter(
                    FixedFrequencies[fixedIndex],
                    fixedGain,
                    FixedQuality,
                    EqLimits.PeakingFilter));
                fixedIndex++;
                continue;
            }

            var filterType = code switch
            {
                'P' => EqLimits.PeakingFilter,
                'L' => EqLimits.LowShelfFilter,
                'H' => EqLimits.HighShelfFilter,
                'M' => EqLimits.LowShelfCenteredFilter,
                'I' => EqLimits.HighShelfCenteredFilter,
                _ => -1,
            };

            if (filterType < 0)
            {
                // 不认识的首字母：后面的内容不猜，前面已经解析出来的照样能用。
                break;
            }

            if (!TryReadFrequencyGainQuality(line, ref index, out var frequency, out var gain, out var quality))
            {
                return false;
            }

            // Peace 的写法：带 Q 的架子用「居中」类型，不带 Q 的用普通架子；
            // 峰值本来就带 Q。这样写回去的配置行和 Peace 自己给的一致。
            if (code == 'L' && quality.HasValue)
            {
                filterType = EqLimits.LowShelfCenteredFilter;
            }
            else if (code == 'H' && quality.HasValue)
            {
                filterType = EqLimits.HighShelfCenteredFilter;
            }

            filters.Add(new AutoEqFilter(
                frequency,
                gain,
                quality ?? EqLimits.DefaultQuality,
                filterType));
        }

        result = new ParametricResult(preAmp, filters);
        return true;
    }

    /// <summary>解析 <c>R</c> 开头的图形 EQ 行，值按 <see cref="GraphicFrequencies"/> 对齐。</summary>
    public static IReadOnlyList<double> ParseGraphic(string? line)
    {
        var values = new List<double>(GraphicFrequencies.Count);
        if (string.IsNullOrEmpty(line) || line[0] != 'R')
        {
            return values;
        }

        var index = 1;
        while (index < line.Length && values.Count < GraphicFrequencies.Count)
        {
            if (line[index] == 'R' || line[index] == ' ')
            {
                index++;
                continue;
            }

            if (!TryReadNumber(line, ref index, out var value))
            {
                break;
            }

            values.Add(value);
        }

        return values;
    }

    /// <summary>读一个数字（可带负号和小数点），成功时把游标推到数字后面。</summary>
    public static bool TryReadNumber(string text, ref int index, out double value)
    {
        value = 0;
        var start = index;
        if (index < text.Length && (text[index] == '-' || text[index] == '+'))
        {
            index++;
        }

        var digits = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
            digits++;
        }

        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
                digits++;
            }
        }

        if (digits == 0)
        {
            index = start;
            return false;
        }

        value = double.Parse(text.AsSpan(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>频率、增益、可选 Q：<c>105G5.5Q0.7</c>。</summary>
    private static bool TryReadFrequencyGainQuality(
        string line,
        ref int index,
        out double frequency,
        out double gain,
        out double? quality)
    {
        quality = null;

        if (!TryReadNumber(line, ref index, out frequency))
        {
            gain = 0;
            return false;
        }

        if (index >= line.Length || line[index] != 'G')
        {
            gain = 0;
            return false;
        }

        index++;
        if (!TryReadNumber(line, ref index, out gain))
        {
            return false;
        }

        if (index < line.Length && line[index] == 'Q')
        {
            index++;
            if (!TryReadNumber(line, ref index, out var value))
            {
                return false;
            }

            quality = value;
        }

        return true;
    }
}
