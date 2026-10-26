namespace PeaceWpf.Core.Calibration;

/// <summary>
/// 要校准到哪条曲线。
/// </summary>
public enum CalibrationTargetKind
{
    /// <summary>平直：每个频率一样响。适合音箱 / 房间，也是「先把系统拉平」的第一步。</summary>
    Flat,

    /// <summary>哈曼：入耳式耳机的目标响应，低频比中频高，10 kHz 以上再抬一点。</summary>
    HarmanInEar,
}

/// <summary>
/// 目标曲线。
///
/// 平直就是 0 dB。哈曼用的是公开的 Harman IE 目标的一份平滑近似：
/// 低频 +6 dB 左右，2–3 kHz 一个小凸起，5 kHz 附近凹下去，10 kHz 以上再上翘。
/// 它是一组控制点按对数频率线性插值出来的，不是官方原始数据，
/// 也不区分入耳式和头戴——这在界面上会写清楚，免得把近似值当成标准值。
/// </summary>
public static class TargetCurve
{
    /// <summary>哈曼（入耳式，近似）的控制点：频率、相对 1 kHz 的偏移。</summary>
    private static readonly (double Hz, double Db)[] HarmanPoints =
    {
        (20, 5.8),
        (40, 6.0),
        (63, 5.9),
        (100, 5.5),
        (160, 4.2),
        (250, 2.4),
        (400, 0.9),
        (630, 0.2),
        (1000, 0.0),
        (1250, 0.6),
        (1600, 1.4),
        (2000, 1.9),
        (2500, 1.6),
        (3150, 0.7),
        (4000, -0.3),
        (5000, -1.2),
        (6300, -1.0),
        (8000, -0.2),
        (10000, 1.4),
        (12500, 2.6),
        (16000, 3.0),
        (20000, 3.0),
    };

    /// <summary>界面上显示的名字。</summary>
    public static string DisplayName(CalibrationTargetKind kind) => kind switch
    {
        CalibrationTargetKind.Flat => "平直曲线",
        CalibrationTargetKind.HarmanInEar => "哈曼曲线",
        _ => kind.ToString(),
    };

    /// <summary>一句话说明，写进选择窗口。</summary>
    public static string Description(CalibrationTargetKind kind) => kind switch
    {
        CalibrationTargetKind.Flat => "每个频率一样响。音箱、房间、或者想先听「没有颜色」的声音时用这个。",
        CalibrationTargetKind.HarmanInEar => "入耳式耳机的目标：低频多 6 dB 左右，中高音有一点起伏。内置的是平滑近似，不是官方原始数据。",
        _ => string.Empty,
    };

    /// <summary>目标曲线在某个频率上的偏移（dB，相对 1 kHz）。</summary>
    public static double GainAt(CalibrationTargetKind kind, double frequencyHz)
    {
        if (kind == CalibrationTargetKind.Flat || !double.IsFinite(frequencyHz) || frequencyHz <= 0)
        {
            return 0;
        }

        return Interpolate(HarmanPoints, frequencyHz);
    }

    /// <summary>把目标曲线采成一条折线，画图和拟合都用它。</summary>
    public static double[] Sample(CalibrationTargetKind kind, IReadOnlyList<double> frequencies)
    {
        ArgumentNullException.ThrowIfNull(frequencies);
        var result = new double[frequencies.Count];
        for (var i = 0; i < frequencies.Count; i++)
        {
            result[i] = GainAt(kind, frequencies[i]);
        }

        return result;
    }

    /// <summary>
    /// 对数频率上的线性插值。用对数轴是因为耳朵按倍频程听，用线性轴插值会把低频挤成一团。
    /// </summary>
    private static double Interpolate((double Hz, double Db)[] points, double frequencyHz)
    {
        if (frequencyHz <= points[0].Hz)
        {
            return points[0].Db;
        }

        if (frequencyHz >= points[^1].Hz)
        {
            return points[^1].Db;
        }

        for (var i = 1; i < points.Length; i++)
        {
            if (frequencyHz > points[i].Hz)
            {
                continue;
            }

            var (hz0, db0) = points[i - 1];
            var (hz1, db1) = points[i];
            var t = Math.Log(frequencyHz / hz0) / Math.Log(hz1 / hz0);
            return db0 + (db1 - db0) * t;
        }

        return points[^1].Db;
    }
}
