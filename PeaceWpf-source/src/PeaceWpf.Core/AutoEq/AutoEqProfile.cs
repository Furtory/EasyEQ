using System.Globalization;
using System.Text;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.AutoEq;

/// <summary>
/// 一台设备的一种等化结果，已经解析成可以直接用的形态。
/// </summary>
public sealed class AutoEqProfile
{
    private AutoEqProfile(
        AutoEqDevice device,
        AutoEqType type,
        double preAmpDb,
        IReadOnlyList<AutoEqFilter> filters,
        IReadOnlyList<double> graphicValues)
    {
        Device = device;
        Type = type;
        PreAmpDb = preAmpDb;
        Filters = filters;
        GraphicValues = graphicValues;
    }

    /// <summary>这份结果属于哪台设备。</summary>
    public AutoEqDevice Device { get; }

    /// <summary>哪种形态。</summary>
    public AutoEqType Type { get; }

    /// <summary>预增益（dB）。图形 EQ 原数据里没有，固定为 0。</summary>
    public double PreAmpDb { get; }

    /// <summary>参数 EQ / 固定频带 EQ 的滤波器。</summary>
    public IReadOnlyList<AutoEqFilter> Filters { get; }

    /// <summary>图形 EQ 的 127 个值，按 <see cref="AutoEqParser.GraphicFrequencies"/> 对齐。</summary>
    public IReadOnlyList<double> GraphicValues { get; }

    /// <summary>能不能直接写进点模型（图形 EQ 是卷积，不能）。</summary>
    public bool CanApplyToPoints => Type != AutoEqType.Graphic && Filters.Count > 0;

    /// <summary>滤波器中心频率，给曲线预览打点用；图形 EQ 没有单点，返回空。</summary>
    public IReadOnlyList<double> FilterFrequencies =>
        Filters.Select(filter => filter.FrequencyHz).ToArray();

    /// <summary>
    /// 在 20 Hz–20 kHz 的对数频率轴上取 <paramref name="samples"/> 个点，
    /// 返回这条等化的频响（dB，不含预增益）。图形 EQ 直接在 127 个测量点之间插值。
    /// </summary>
    public double[] SampleResponse(int samples = 240, double minHz = 20, double maxHz = 20000)
    {
        samples = Math.Clamp(samples, 2, 4096);
        minHz = Math.Max(minHz, 1);
        maxHz = Math.Max(maxHz, minHz * 1.01);

        var result = new double[samples];
        var ratio = Math.Log10(maxHz / minHz);

        if (Type == AutoEqType.Graphic)
        {
            for (var i = 0; i < samples; i++)
            {
                var hz = minHz * Math.Pow(10, (double)i / (samples - 1) * ratio);
                result[i] = GraphicGainAt(hz);
            }

            return result;
        }

        var snapshot = ToPointList().ToSnapshot(0, 0);
        for (var i = 0; i < samples; i++)
        {
            var hz = minHz * Math.Pow(10, (double)i / (samples - 1) * ratio);
            result[i] = EqResponse.GainAt(snapshot, hz);
        }

        return result;
    }

    /// <summary>图形 EQ 在两个相邻频点之间按对数频率线性插值。</summary>
    private double GraphicGainAt(double frequencyHz)
    {
        var frequencies = AutoEqParser.GraphicFrequencies;
        var count = Math.Min(frequencies.Count, GraphicValues.Count);
        if (count == 0)
        {
            return 0;
        }

        if (frequencyHz <= frequencies[0])
        {
            return GraphicValues[0];
        }

        for (var i = 1; i < count; i++)
        {
            if (frequencyHz > frequencies[i])
            {
                continue;
            }

            var low = Math.Log10(frequencies[i - 1]);
            var high = Math.Log10(frequencies[i]);
            var t = (Math.Log10(frequencyHz) - low) / (high - low);
            return GraphicValues[i - 1] + (GraphicValues[i] - GraphicValues[i - 1]) * t;
        }

        return GraphicValues[count - 1];
    }

    /// <summary>
    /// 从设备的原始行里解析一份结果；这种形态不存在时返回 null。
    /// </summary>
    public static AutoEqProfile? Create(AutoEqDevice device, AutoEqType type)
    {
        ArgumentNullException.ThrowIfNull(device);

        switch (type)
        {
            case AutoEqType.Parametric:
            case AutoEqType.FixedBand:
            {
                var line = type == AutoEqType.Parametric ? device.ParametricLine : device.FixedBandLine;
                if (!AutoEqParser.TryParseParametric(line, out var parsed) || parsed.Filters.Count == 0)
                {
                    return null;
                }

                return new AutoEqProfile(device, type, parsed.PreAmpDb, parsed.Filters, Array.Empty<double>());
            }

            case AutoEqType.Graphic:
            {
                var values = AutoEqParser.ParseGraphic(device.GraphicLine);
                if (values.Count == 0)
                {
                    return null;
                }

                return new AutoEqProfile(device, type, 0, Array.Empty<AutoEqFilter>(), values);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// 转成点列表，供曲线和滑杆使用。端点由 <see cref="EqPointList"/> 自动补齐。
    /// </summary>
    public EqPointList ToPointList()
    {
        if (!CanApplyToPoints)
        {
            return EqPointList.CreateDefault();
        }

        var points = Filters
            .Where(filter => double.IsFinite(filter.FrequencyHz)
                && double.IsFinite(filter.GainDb)
                && double.IsFinite(filter.Quality))
            .Select(filter => new EqPoint(
                0,
                filter.FrequencyHz,
                filter.GainDb,
                filter.Quality,
                false,
                EqLimits.ClampFilter(filter.FilterType)));

        return EqPointList.FromPoints(points);
    }

    /// <summary>
    /// 还原成 EqualizerAPO 的命令文本，和 Peace 里「复制」按钮给出的内容一致。
    /// 图形 EQ 写成一行 <c>GraphicEQ:</c>。
    /// </summary>
    public string ToEqualizerApoText()
    {
        var builder = new StringBuilder();

        if (Type == AutoEqType.Graphic)
        {
            builder.Append("GraphicEQ:");
            var count = Math.Min(GraphicValues.Count, AutoEqParser.GraphicFrequencies.Count);
            for (var i = 0; i < count; i++)
            {
                builder.Append(' ')
                    .Append(Format("{0:0}", AutoEqParser.GraphicFrequencies[i]))
                    .Append(' ')
                    .Append(Format("{0:0.0}", GraphicValues[i]))
                    .Append(';');
            }

            if (count > 0)
            {
                builder.Length--;
            }

            builder.AppendLine();
            return builder.ToString();
        }

        if (Math.Abs(PreAmpDb) > 0.001)
        {
            builder.AppendLine(Format("Preamp: {0:0.0} dB", PreAmpDb));
        }

        for (var i = 0; i < Filters.Count; i++)
        {
            var filter = Filters[i];
            builder.AppendLine(Format(
                "Filter {0}: ON {1} Fc {2:0} Hz Gain {3:0.0} dB Q {4:0.00}",
                i + 1,
                EqLimits.FilterName(filter.FilterType),
                filter.FrequencyHz,
                filter.GainDb,
                filter.Quality));
        }

        return builder.ToString();
    }

    /// <summary>界面上那行说明：类型 · 滤波器数 · 预增益。</summary>
    public string Summary
    {
        get
        {
            if (Type == AutoEqType.Graphic)
            {
                return $"图形 EQ · {GraphicValues.Count} 个频点 · 原数据没有预增益";
            }

            var shelves = Filters.Count(filter => EqLimits.IsShelfFilter(filter.FilterType));
            var peaks = Filters.Count - shelves;
            var parts = new List<string> { AutoEqTypes.ShortName(Type) + " EQ" };
            parts.Add($"{peaks} 个峰值");
            if (shelves > 0)
            {
                parts.Add($"{shelves} 个架式");
            }

            parts.Add($"预增益 {Format("{0:+0.0;-0.0;0.0}", PreAmpDb)} dB");
            return string.Join(" · ", parts);
        }
    }

    private static string Format(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
