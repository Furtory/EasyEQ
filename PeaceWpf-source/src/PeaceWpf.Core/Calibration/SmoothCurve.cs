namespace PeaceWpf.Core.Calibration;

/// <summary>
/// 把一串频响点连成一条平滑曲线，用来画图。
///
/// 用的是单调三次插值（PCHIP，Fritsch–Carlson），横轴取 log10(Hz)：
/// - 曲线严格穿过每一个输入点，不会像最小二乘那样把窄谷抹平、把峰压低；
/// - 相邻两点之间是三次 Hermite，导数连续，所以看起来是圆的，不是折线；
/// - 单调段内不会过冲——普通三次样条在 20 Hz 的深谷旁边会甩出一个假峰，
///   那种「补出来的形状」正是拟合阶段最忌讳的东西，画图也不该编。
///
/// 它只做插值，不做平滑：看上去的圆滑来自导数的连续性，不是把数据磨平。
/// </summary>
public sealed class SmoothCurve
{
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _m;

    private SmoothCurve(double[] x, double[] y, double[] m)
    {
        _x = x;
        _y = y;
        _m = m;
    }

    /// <summary>输入点数。少于 2 个点连不成曲线。</summary>
    public int Count => _x.Length;

    /// <summary>
    /// 按频率升序建一条曲线。<paramref name="points"/> 里的 Hz 必须为正、dB 必须有限；
    /// 重复频率只留第一个。少于两个有效点时返回 null。
    /// </summary>
    public static SmoothCurve? Build(IReadOnlyList<(double Hz, double Db)> points)
    {
        if (points is null || points.Count < 2)
        {
            return null;
        }

        var ordered = points
            .Where(point => point.Hz > 0 && double.IsFinite(point.Db))
            .OrderBy(point => point.Hz)
            .ToList();

        // 同一频率出现两次（频点表重复、或者拖动过的格点重合）只能留一个，否则 h=0 会除爆。
        var unique = new List<(double Hz, double Db)>(ordered.Count);
        foreach (var point in ordered)
        {
            if (unique.Count > 0 && Math.Abs(point.Hz - unique[^1].Hz) <= unique[^1].Hz * 1e-9)
            {
                continue;
            }

            unique.Add(point);
        }

        if (unique.Count < 2)
        {
            return null;
        }

        var n = unique.Count;
        var x = new double[n];
        var y = new double[n];
        for (var i = 0; i < n; i++)
        {
            x[i] = Math.Log10(unique[i].Hz);
            y[i] = unique[i].Db;
        }

        var h = new double[n - 1];
        var slope = new double[n - 1];
        for (var i = 0; i < n - 1; i++)
        {
            h[i] = x[i + 1] - x[i];
            slope[i] = (y[i + 1] - y[i]) / h[i];
        }

        var m = new double[n];

        if (n == 2)
        {
            m[0] = slope[0];
            m[1] = slope[0];
        }
        else
        {
            m[0] = EdgeTangent(h[0], h[1], slope[0], slope[1]);
            m[n - 1] = EdgeTangent(h[n - 2], h[n - 3], slope[n - 2], slope[n - 3]);

            for (var i = 1; i < n - 1; i++)
            {
                var d0 = slope[i - 1];
                var d1 = slope[i];

                if (d0 * d1 <= 0)
                {
                    // 峰或谷：导数为零，曲线在这里停住再拐回去，不会冲过头。
                    m[i] = 0;
                }
                else
                {
                    var w1 = (2 * h[i]) + h[i - 1];
                    var w2 = h[i] + (2 * h[i - 1]);
                    m[i] = (w1 + w2) / ((w1 / d0) + (w2 / d1));
                }
            }
        }

        return new SmoothCurve(x, y, m);
    }

    /// <summary>在某个频率上取值；超出两端时夹到端点（不外推）。</summary>
    public double Evaluate(double hz)
    {
        if (!double.IsFinite(hz) || hz <= 0)
        {
            return _y[0];
        }

        var target = Math.Log10(hz);
        if (target <= _x[0])
        {
            return _y[0];
        }

        if (target >= _x[^1])
        {
            return _y[^1];
        }

        // 找 target 落在哪一段：最后一个 x[i] <= target。
        var low = 0;
        var high = _x.Length - 1;
        while (low < high - 1)
        {
            var middle = (low + high) / 2;
            if (_x[middle] <= target)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        var h = _x[low + 1] - _x[low];
        var t = (target - _x[low]) / h;
        var t2 = t * t;
        var t3 = t2 * t;

        // Hermite 基函数。
        var h00 = (2 * t3) - (3 * t2) + 1;
        var h10 = t3 - (2 * t2) + t;
        var h01 = (-2 * t3) + (3 * t2);
        var h11 = t3 - t2;

        return (h00 * _y[low])
            + (h10 * h * _m[low])
            + (h01 * _y[low + 1])
            + (h11 * h * _m[low + 1]);
    }

    /// <summary>
    /// 按 log 频率等距采样，给 Polyline 用。返回 <paramref name="samples"/> + 1 个点。
    /// </summary>
    public IReadOnlyList<(double Hz, double Db)> Sample(int samples = 400)
    {
        samples = Math.Clamp(samples, 2, 4000);
        var result = new (double Hz, double Db)[samples + 1];
        var span = _x[^1] - _x[0];

        for (var i = 0; i <= samples; i++)
        {
            var x = _x[0] + (span * i / samples);
            result[i] = (Math.Pow(10, x), Evaluate(Math.Pow(10, x)));
        }

        return result;
    }

    /// <summary>
    /// 端点的切线：单调三次的标准端点公式（Fritsch–Carlson）。
    /// 和内部点一样要保证不过冲——端点最容易在曲线开头甩出去。
    /// </summary>
    private static double EdgeTangent(double h0, double h1, double d0, double d1)
    {
        var tangent = (((2 * h0) + h1) * d0 - (h0 * d1)) / (h0 + h1);

        if (tangent * d0 <= 0)
        {
            return 0;
        }

        if (d0 * d1 <= 0 && Math.Abs(tangent) > Math.Abs(3 * d0))
        {
            return 3 * d0;
        }

        return tangent;
    }
}
