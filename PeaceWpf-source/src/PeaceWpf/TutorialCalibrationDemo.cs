using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;

namespace PeaceWpf;

/// <summary>
/// 教程里「自动EQ」那几步的演示实测。
///
/// 两条曲线都是现成的预设：左右用 0（<c>0.peace</c>），低音用 1（<c>1.peace</c>），
/// 推子原样抄在源码里当常量，运行时不读任何 <c>.peace</c> 文件：
/// 教程不该因为用户删了、改了预设就变样。
///
/// 自动EQ 窗口只吃一份「实测」，所以把两份预设拼成一条：
/// 100 Hz 以下取低音预设的反相，100 Hz 以上取左右预设的反相（目标平直时，实测 + 校正 = 0）。
/// 于是真实窗口用自己的拟合器算出来，上面就是 0 号那条、下面就是 1 号那条。
/// </summary>
internal static class TutorialCalibrationDemo
{
    /// <summary>演示的频段上限：和自动EQ 图的横轴一致。</summary>
    public const double MaxFrequency = 20000;

    /// <summary>两份预设拼接的位置，和自动EQ 的默认矫正起点一致。</summary>
    private const double SplitHz = 100;

    /// <summary>
    /// 左右（<c>0.peace</c>）的九段推子。那份的 <c>[Filters]</c> 是空的，所以全是 PK。
    /// </summary>
    private static readonly double[] MainFrequencies =
    {
        20, 43, 97, 369, 856, 2421, 7069, 12667, 20000,
    };

    private static readonly double[] MainGains =
    {
        0.1, -0.1, -0.5, 7.5, 4.3, -5.1, -2.2, 4.2, 5.9,
    };

    private static readonly double[] MainQualities =
    {
        0.61, 0.53, 1.41, 0.93, 0.93, 1.41, 1.07, 0.53, 0.3,
    };

    /// <summary>低音（<c>1.peace</c>）的四段推子；最后一段是 20 kHz 端点，增益 0。</summary>
    private static readonly double[] BassFrequencies =
    {
        20, 50, 106, 20000,
    };

    private static readonly double[] BassGains =
    {
        4.5, 7.1, -3.1, 0.0,
    };

    private static readonly double[] BassQualities =
    {
        0.81, 0.46, 0.61, 1.41,
    };

    private static readonly Lazy<SweepMeasurement> Measurement = new(CreateMeasurement);

    /// <summary>演示用的实测：低音段取 1 号预设的反相，其余取 0 号的反相。</summary>
    public static SweepMeasurement BuildMeasurement() => Measurement.Value;

    private static PeaceSnapshot BuildPreset(
        double[] frequencies, double[] gains, double[] qualities)
    {
        var bands = new List<SliderBand>(frequencies.Length);
        for (var i = 0; i < frequencies.Length; i++)
        {
            bands.Add(new SliderBand(
                i,
                frequencies[i],
                gains[i],
                qualities[i],
                EqLimits.PeakingFilter,
                true));
        }

        return new PeaceSnapshot(bands, 0, bands.Count);
    }

    private static SweepMeasurement CreateMeasurement()
    {
        var main = BuildPreset(MainFrequencies, MainGains, MainQualities);
        var bass = BuildPreset(BassFrequencies, BassGains, BassQualities);

        var points = new List<MeasurementPoint>();
        var frequencies = new List<double>();
        for (var i = 0; i <= 180; i++)
        {
            var hz = 20 * Math.Pow(1000, i / 180.0);
            if (hz > MaxFrequency)
            {
                break;
            }

            var correction = hz < SplitHz
                ? EqResponse.GainAt(bass, hz)
                : EqResponse.GainAt(main, hz);

            points.Add(new MeasurementPoint(hz, -correction, 40, -correction));
            frequencies.Add(hz);
        }

        return new SweepMeasurement(points, -90, 48000, frequencies);
    }
}
