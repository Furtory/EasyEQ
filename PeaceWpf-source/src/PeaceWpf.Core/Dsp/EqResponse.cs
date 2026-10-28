namespace PeaceWpf.Core.Dsp;

using PeaceWpf.Core.Model;

/// <summary>
/// EQ 频响仿真：把各段滤波器叠加成一条曲线，供界面自绘。
///
/// 峰值滤波器用标准的 RBJ biquad 系数，架式（低架 / 高架）也用 RBJ 的架式公式，
/// 再按 |H(e^jw)| 求幅度。AutoEQ 带进来的低架 / 高架因此能画对形状，
/// 而不是像第一版那样一律拿峰值公式近似。
///
/// 其它类型（低通、高通、带通、陷波、全通、巴特沃斯……）仍然按峰值公式近似：
/// 它们不在点模型里，走到这里只可能是别处塞进来的脏数据，形状不准也不影响保存。
/// </summary>
public static class EqResponse
{
    /// <summary>仿真用的采样率，与桌面音频的常见值一致。</summary>
    public const double SampleRate = 44100.0;

    /// <summary>计算整条曲线的增益，单位 dB。</summary>
    /// <param name="snapshot">当前快照。</param>
    /// <param name="pointCount">采样点数。</param>
    /// <param name="minHz">左端频率。</param>
    /// <param name="maxHz">右端频率。</param>
    public static double[] Curve(PeaceSnapshot snapshot, int pointCount, double minHz = 20, double maxHz = 20000)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var result = new double[Math.Max(pointCount, 2)];
        var ratio = Math.Log10(maxHz / minHz);

        for (var i = 0; i < result.Length; i++)
        {
            var t = (double)i / (result.Length - 1);
            var hz = minHz * Math.Pow(10, t * ratio);
            result[i] = GainAt(snapshot, hz);
        }

        return result;
    }

    /// <summary>某个频率处所有段叠加后的总增益，单位 dB。</summary>
    public static double GainAt(PeaceSnapshot snapshot, double frequencyHz)
    {
        var sum = 0.0;
        foreach (var band in snapshot.Bands)
        {
            if (!band.Enabled || band.GainDb == 0 || band.FrequencyHz <= 0)
            {
                continue;
            }

            sum += BandGain(band, frequencyHz);
        }

        return sum;
    }

    /// <summary>
    /// 除某颗推子之外，其它推子在给定频率上的总增益（dB）。
    /// 曲线是各段 dB 直接相加画出来的（见 <see cref="GainAt"/>），所以这里也直接相加。
    /// </summary>
    public static double OthersGainAt(EqPointList points, double frequencyHz, int? excludeId = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        var sum = 0.0;
        foreach (var point in points.Points)
        {
            if (excludeId is { } id && point.Id == id)
            {
                continue;
            }

            sum += BandGain(BandOf(point), frequencyHz);
        }

        return sum;
    }

    /// <summary>
    /// 反推增益：把这颗推子的圆心放在 <paramref name="frequencyHz"/> 上，要它在该点贡献
    /// <paramref name="wantedDb"/>，总增益该设多少。
    ///
    /// 峰值滤波器在中心处贡献就等于增益；架式在拐点只给一半，所以不能照抄，得解。
    /// 架式和峰值的响应都随增益单调，二分一定收敛。
    /// </summary>
    public static double GainForContribution(EqPoint template, double frequencyHz, double wantedDb)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0 || !double.IsFinite(wantedDb))
        {
            return 0;
        }

        var low = EqLimits.MinGainDb;
        var high = EqLimits.MaxGainDb;
        for (var i = 0; i < 50; i++)
        {
            var mid = (low + high) / 2;
            var band = BandOf(template with { FrequencyHz = frequencyHz, GainDb = mid });
            if (BandGain(band, frequencyHz) < wantedDb)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return (low + high) / 2;
    }

    /// <summary>
    /// 让整条曲线在 <paramref name="frequencyHz"/> 处正好经过 <paramref name="targetDb"/>：
    /// 先扣掉别的推子在同一点的贡献，再反推这颗推子该有的增益。拖曲线改推子就靠它。
    /// </summary>
    public static double GainForCurveThrough(EqPointList points, int id, double frequencyHz, double targetDb)
    {
        ArgumentNullException.ThrowIfNull(points);

        var point = points.Find(id);
        if (point is null)
        {
            return 0;
        }

        var wanted = targetDb - OthersGainAt(points, frequencyHz, id);
        return GainForContribution(point, frequencyHz, wanted);
    }

    /// <summary>
    /// 新建一颗推子时同样压在鼠标上：新点是峰值，在自己中心处贡献就等于增益，扣掉别人的就行。
    /// </summary>
    public static double GainForNewPointAt(EqPointList points, double frequencyHz, double targetDb)
    {
        ArgumentNullException.ThrowIfNull(points);
        return targetDb - OthersGainAt(points, frequencyHz);
    }

    /// <summary>点模型转仿真需要的单段。索引和开关不影响单段响应。</summary>
    private static SliderBand BandOf(EqPoint point) =>
        new(0, point.FrequencyHz, point.GainDb, point.Quality, point.FilterType, true);

    /// <summary>单段在给定频率处的增益，单位 dB。</summary>
    public static double BandGain(SliderBand band, double frequencyHz)
    {
        // 环境音里高于奈奎斯特的频率没有意义。
        if (frequencyHz >= SampleRate / 2 || frequencyHz <= 0)
        {
            return 0;
        }

        if (band.GainDb == 0)
        {
            return 0;
        }

        var (b0, b1, b2, a0, a1, a2) = Coefficients(band);
        var w = 2 * Math.PI * frequencyHz / SampleRate;
        var cos1 = Math.Cos(w);
        var cos2 = Math.Cos(2 * w);
        var sin1 = Math.Sin(w);
        var sin2 = Math.Sin(2 * w);

        var numRe = b0 + b1 * cos1 + b2 * cos2;
        var numIm = -(b1 * sin1 + b2 * sin2);
        var denRe = a0 + a1 * cos1 + a2 * cos2;
        var denIm = -(a1 * sin1 + a2 * sin2);

        var num = Math.Sqrt(numRe * numRe + numIm * numIm);
        var den = Math.Sqrt(denRe * denRe + denIm * denIm);
        if (den <= double.Epsilon)
        {
            return 0;
        }

        return 20 * Math.Log10(num / den);
    }

    /// <summary>按滤波器类型挑系数。低架 / 高架走架式公式，其余按峰值近似。</summary>
    public static (double b0, double b1, double b2, double a0, double a1, double a2) Coefficients(SliderBand band)
    {
        if (EqLimits.IsHighShelf(band.FilterType))
        {
            return HighShelfCoefficients(band.FrequencyHz, band.Quality, band.GainDb, usesQuality: UsesQuality(band.FilterType));
        }

        if (EqLimits.IsLowShelf(band.FilterType))
        {
            return LowShelfCoefficients(band.FrequencyHz, band.Quality, band.GainDb, usesQuality: UsesQuality(band.FilterType));
        }

        return PeakingCoefficients(band.FrequencyHz, band.Quality, band.GainDb);
    }

    /// <summary>LS / HS 不带 Q，用最大坡度；LSC / HSC / LSQ / HSQ 带 Q。</summary>
    private static bool UsesQuality(int filterType) =>
        filterType is EqLimits.LowShelfCenteredFilter or EqLimits.HighShelfCenteredFilter
            or EqLimits.LowShelfQFilter or EqLimits.HighShelfQFilter;

    /// <summary>RBJ peaking EQ 系数，已按 a0 归一化。</summary>
    private static (double b0, double b1, double b2, double a0, double a1, double a2) PeakingCoefficients(
        double frequencyHz, double quality, double gainDb)
    {
        var q = Math.Max(quality, 0.05);
        var w0 = Omega(frequencyHz);
        var cosW = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2 * q);
        var a = Math.Pow(10, gainDb / 40.0);

        var b0 = 1 + alpha * a;
        var b1 = -2 * cosW;
        var b2 = 1 - alpha * a;
        var a0 = 1 + alpha / a;
        var a1 = -2 * cosW;
        var a2 = 1 - alpha / a;

        return (b0 / a0, b1 / a0, b2 / a0, 1, a1 / a0, a2 / a0);
    }

    /// <summary>RBJ low shelf。Q 当坡度用；不带 Q 时取 S = 1（最陡且不翘）。</summary>
    private static (double b0, double b1, double b2, double a0, double a1, double a2) LowShelfCoefficients(
        double frequencyHz, double quality, double gainDb, bool usesQuality)
    {
        var a = Math.Pow(10, gainDb / 40.0);
        var w0 = Omega(frequencyHz);
        var cosW = Math.Cos(w0);
        var alpha = shelfAlpha(w0, a, quality, usesQuality);
        var twoSqrtAAlpha = 2 * Math.Sqrt(a) * alpha;

        var b0 = a * ((a + 1) - (a - 1) * cosW + twoSqrtAAlpha);
        var b1 = 2 * a * ((a - 1) - (a + 1) * cosW);
        var b2 = a * ((a + 1) - (a - 1) * cosW - twoSqrtAAlpha);
        var a0 = (a + 1) + (a - 1) * cosW + twoSqrtAAlpha;
        var a1 = -2 * ((a - 1) + (a + 1) * cosW);
        var a2 = (a + 1) + (a - 1) * cosW - twoSqrtAAlpha;

        return (b0 / a0, b1 / a0, b2 / a0, 1, a1 / a0, a2 / a0);
    }

    /// <summary>RBJ high shelf。</summary>
    private static (double b0, double b1, double b2, double a0, double a1, double a2) HighShelfCoefficients(
        double frequencyHz, double quality, double gainDb, bool usesQuality)
    {
        var a = Math.Pow(10, gainDb / 40.0);
        var w0 = Omega(frequencyHz);
        var cosW = Math.Cos(w0);
        var alpha = shelfAlpha(w0, a, quality, usesQuality);
        var twoSqrtAAlpha = 2 * Math.Sqrt(a) * alpha;

        var b0 = a * ((a + 1) + (a - 1) * cosW + twoSqrtAAlpha);
        var b1 = -2 * a * ((a - 1) + (a + 1) * cosW);
        var b2 = a * ((a + 1) + (a - 1) * cosW - twoSqrtAAlpha);
        var a0 = (a + 1) - (a - 1) * cosW + twoSqrtAAlpha;
        var a1 = 2 * ((a - 1) - (a + 1) * cosW);
        var a2 = (a + 1) - (a - 1) * cosW - twoSqrtAAlpha;

        return (b0 / a0, b1 / a0, b2 / a0, 1, a1 / a0, a2 / a0);
    }

    /// <summary>架式的 alpha：带 Q 用 Q，不带 Q 用 RBJ 的 S = 1。</summary>
    private static double shelfAlpha(double w0, double a, double quality, bool usesQuality)
    {
        if (usesQuality)
        {
            return Math.Sin(w0) / (2 * Math.Max(quality, 0.05));
        }

        const double s = 1.0;
        return Math.Sin(w0) / 2 * Math.Sqrt((a + 1 / a) * (1 / s - 1) + 2);
    }

    private static double Omega(double frequencyHz) =>
        2 * Math.PI * Math.Clamp(frequencyHz, 1, SampleRate / 2 - 1) / SampleRate;
}
