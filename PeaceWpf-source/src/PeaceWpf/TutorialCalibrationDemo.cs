using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Calibration;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;

namespace PeaceWpf;

/// <summary>
/// 教程里「自动EQ → 自定义矫正起点」那一段的模拟数据。
///
/// 曲线不另画一套：喂一份固定的模拟实测给 <see cref="EqCurveFitter"/>，
/// 由主程序同一个拟合器算出配点，再用 <see cref="EqResponse.GainAt"/> 求值——
/// 和自动EQ 窗口里画的是同一套算法，只是实测是写死的。
/// 拖动起点时在相邻两份结果之间插值，所以线会跟着变，又不用每帧跑一遍解方程。
/// </summary>
internal static class TutorialCalibrationDemo
{
    /// <summary>演示的频段上限：和自动EQ 图的横轴一致。</summary>
    public const double MaxFrequency = 16000;

    /// <summary>预先算好的几个矫正起点（Hz）。拖动时在这几个之间插值。</summary>
    private static readonly double[] Floors = { 80, 110, 150, 200, 260, 320 };

    private static readonly Lazy<SweepMeasurement> Measurement = new(BuildMeasurement);
    private static readonly Lazy<Dictionary<double, PeaceSnapshot>> Plans = new(BuildPlans);

    /// <summary>模拟实测：几个宽窄不一的凹陷和隆起，够演示「起点一变，补的地方就变」。</summary>
    public static double MeasuredGain(double hz)
    {
        var log = Math.Log10(Math.Max(20, hz) / 20);
        var dip150 = -5.5 * Math.Exp(-0.5 * Math.Pow((log - Math.Log10(150 / 20.0)) / 0.05, 2));
        var bump1200 = 3.2 * Math.Exp(-0.5 * Math.Pow((log - Math.Log10(1200 / 20.0)) / 0.07, 2));
        var dip3200 = -6.0 * Math.Exp(-0.5 * Math.Pow((log - Math.Log10(3200 / 20.0)) / 0.06, 2));
        var dip8000 = -4.0 * Math.Exp(-0.5 * Math.Pow((log - Math.Log10(8000 / 20.0)) / 0.10, 2));
        return dip150 + bump1200 + dip3200 + dip8000;
    }

    /// <summary>主层校正：矫正起点以上由拟合器给出，起点以下保持 0（交给低音层）。</summary>
    public static double CorrectionGain(double floorHz, double hz)
    {
        var plans = Plans.Value;
        if (plans.Count == 0)
        {
            return 0;
        }

        var (lower, upper, t) = Bracket(floorHz);
        var low = Evaluate(plans, lower, hz);
        var high = Evaluate(plans, upper, hz);
        return low + (high - low) * t;
    }

    /// <summary>低音层：起点以下按实测反相补回去；起点以上不画。</summary>
    public static double BassGain(double floorHz, double hz) =>
        hz <= floorHz ? -MeasuredGain(hz) : 0;

    private static double Evaluate(Dictionary<double, PeaceSnapshot> plans, double floor, double hz) =>
        plans.TryGetValue(floor, out var snapshot) ? EqResponse.GainAt(snapshot, hz) : 0;

    /// <summary>找到夹住这个起点的两份预计算结果。</summary>
    private static (double Lower, double Upper, double T) Bracket(double floorHz)
    {
        floorHz = Math.Clamp(floorHz, Floors[0], Floors[^1]);
        for (var i = 0; i < Floors.Length - 1; i++)
        {
            if (floorHz > Floors[i + 1])
            {
                continue;
            }

            var span = Floors[i + 1] - Floors[i];
            var t = span <= 0 ? 0 : (floorHz - Floors[i]) / span;
            return (Floors[i], Floors[i + 1], t);
        }

        return (Floors[^1], Floors[^1], 0);
    }

    private static SweepMeasurement BuildMeasurement()
    {
        var points = new List<MeasurementPoint>();
        var frequencies = new List<double>();
        for (var i = 0; i <= 160; i++)
        {
            var hz = 20 * Math.Pow(1000, i / 160.0);
            if (hz > MaxFrequency)
            {
                break;
            }

            points.Add(new MeasurementPoint(hz, MeasuredGain(hz), 40, MeasuredGain(hz) - 30));
            frequencies.Add(hz);
        }

        return new SweepMeasurement(points, -90, 48000, frequencies);
    }

    private static Dictionary<double, PeaceSnapshot> BuildPlans()
    {
        var map = new Dictionary<double, PeaceSnapshot>();
        var measurement = Measurement.Value;
        foreach (var floor in Floors)
        {
            try
            {
                var plan = EqCurveFitter.Fit(
                    CalibrationTargetKind.Flat,
                    measurement,
                    targetOffsetDb: null,
                    density: FitDensity.Normal,
                    baselineEq: null,
                    movableBands: false,
                    floorHz: floor);
                map[floor] = plan.Points.ToSnapshot();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // 这份模拟实测拟合不出来就留空，演示退化成平线，不会让教程崩。
            }
        }

        return map;
    }
}
