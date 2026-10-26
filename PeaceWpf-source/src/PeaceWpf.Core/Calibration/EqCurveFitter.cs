using PeaceWpf.Core.Audio;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.Calibration;

/// <summary>
/// 拟合点的疏密。段数不该写死：段距决定段数和滤波器的 Q。
///
/// 段多不等于更准。三分之一倍频程的段贴得紧，但每个滤波器更窄、振铃更长，
/// 而且不能越过 Peace 的 31 段上限；一个倍频程的段省事，可两个点中间的凹陷补不动。
///
/// 所有档位都只从 100 Hz 开始：再往下是音箱下潜和麦克风低频响应的硬限制，
/// 补不出来，只会把提升额度浪费在没人听得见的地方，还拖低预增益。
/// </summary>
public enum FitDensity
{
    /// <summary>每半个倍频程一个点，16 段。给「快速」档：量得粗，推子也少，只求形状对。</summary>
    Coarse,

    /// <summary>每倍频程一个点，9 段。参数少、不容易互相打架；自测里用得着。</summary>
    Sparse,

    /// <summary>每三分之一倍频程一个点，23 段。默认档：和三分之一倍频程平滑同尺度。</summary>
    Normal,

    /// <summary>
    /// 100 Hz–4 kHz 每四分之一倍频程一个点，4–16 kHz 每三分之一倍频程一个点，共 29 段。
    /// 中低频是房间模式和单元谐振最窄的地方，值得多花推子；高频曲线本身更宽，再密只是振铃。
    /// </summary>
    Dense,
}

/// <summary>
/// 一次校准的完整方案：测到了什么、目标是什么、差多少、最后要写进 EQ 的点。
///
/// 它不是「原始测量」，而是已经算好的结果——界面直接拿来画三条线、套到当前层。
/// </summary>
public sealed class CalibrationPlan
{
    internal CalibrationPlan(
        CalibrationTargetKind target,
        IReadOnlyList<MeasurementPoint> measured,
        IReadOnlyList<double> targetDb,
        IReadOnlyList<double> correctionDb,
        EqPointList points,
        double preAmpDb,
        double targetOffsetDb,
        IReadOnlyList<double> residualDb,
        double rippleDb,
        double floorHz = EqCurveFitter.CalibrationFloorHz)
    {
        Target = target;
        Measured = measured;
        TargetDb = targetDb;
        CorrectionDb = correctionDb;
        Points = points;
        PreAmpDb = preAmpDb;
        TargetOffsetDb = targetOffsetDb;
        ResidualDb = residualDb;
        RippleDb = rippleDb;
        FloorHz = floorHz;
    }

    /// <summary>校准到哪条曲线。</summary>
    public CalibrationTargetKind Target { get; }

    /// <summary>这次拟合用的矫正起点（Hz）：这以上由本层拟合，以下交给低音层。</summary>
    public double FloorHz { get; }

    /// <summary>测到的电平（已按 1 kHz 归一化）。</summary>
    public IReadOnlyList<MeasurementPoint> Measured { get; }

    /// <summary>目标曲线，频率与 <see cref="Measured"/> 一一对应。</summary>
    public IReadOnlyList<double> TargetDb { get; }

    /// <summary>要补的增益：目标减实测，已经平滑、限幅。</summary>
    public IReadOnlyList<double> CorrectionDb { get; }

    /// <summary>最终要写进 EQ 的点。</summary>
    public EqPointList Points { get; }

    /// <summary>配套的预增益，避免提升之后削波。</summary>
    public double PreAmpDb { get; }

    /// <summary>
    /// 这次把目标曲线整体移动了多少 dB。负值表示往下压过。
    ///
    /// 设备够不着目标时（比如目标要低频 +6 dB，而实测在 -20 dB，得补 26 dB，
    /// 可提升上限只有 +9 dB），压低目标比硬加增益更诚实：加不上去的部分只是失真。
    /// </summary>
    public double TargetOffsetDb { get; }

    /// <summary>
    /// 每个测量频点上「应该补的量」和「EQ 实际补的量」之差，正数表示还差多少没补上。
    ///
    /// 这就是「贴不贴近目标」的量化：限幅、拟合近似、滤波器之间互相抵消，都会留在这里。
    /// </summary>
    public IReadOnlyList<double> ResidualDb { get; }

    /// <summary>残差的最大绝对值。越小越贴近目标。</summary>
    public double MaxResidualDb =>
        ResidualDb.Count == 0 ? 0 : ResidualDb.Max(value => Math.Abs(value));

    /// <summary>
    /// EQ 实际响应在 1/24 倍频程上的最大相邻落差（dB）。
    ///
    /// 这是「曲线会不会被 Q 弄成锯齿」的量化。测量只在 61 个频点上有数据，
    /// 点与点之间那截是拟合出来的——推子 Q 太窄时，中间就会鼓起来或瘪下去，
    /// 而那种起伏在 61 个点上是看不出来的。
    /// </summary>
    public double RippleDb { get; }

    /// <summary>拟合点（不含两端端点）的数量。</summary>
    public int FilterCount => Math.Max(0, Points.Count - 2);

    /// <summary>修正量的范围，状态栏里用。</summary>
    public (double Min, double Max) CorrectionRange
    {
        get
        {
            if (CorrectionDb.Count == 0)
            {
                return (0, 0);
            }

            return (CorrectionDb.Min(), CorrectionDb.Max());
        }
    }

    /// <summary>给用户看的一句话。</summary>
    public string Summary
    {
        get
        {
            var (min, max) = CorrectionRange;
            var bands = Points.Points.Where(point => !point.IsEndpoint).ToList();
            var qRange = bands.Count == 0
                ? string.Empty
                : $" · Q {bands.Min(point => point.Quality):0.0}–{bands.Max(point => point.Quality):0.0}";

            var text = $"{TargetCurve.DisplayName(Target)} · {FilterCount} 段{qRange} · "
                + $"修正 {min:+0.0;-0.0;0.0} ~ {max:+0.0;-0.0;0.0} dB · 预增益 {PreAmpDb:+0.0;-0.0;0.0} dB";

            if (TargetOffsetDb < -0.05)
            {
                text += $" · 目标整体降了 {-TargetOffsetDb:0.0} dB";
            }

            return text
                + $" · 只校准 {FloorHz:0} Hz 以上"
                + $" · 最大残差 {MaxResidualDb:0.0} dB · 起伏 {RippleDb:0.0} dB";
        }
    }
}

/// <summary>
/// 从测量结果算出一条 EQ。
///
/// 思路很直白：目标减去实测，就是每个频率该补多少。麻烦的地方有三处：
///
/// 1. 相邻的峰值滤波器会叠加，按点独立取值会补过头。所以先按 1 倍频程布点，
///    再用 <see cref="EqResponse"/> 仿真出这些点实际形成的曲线，把误差按比例压回去，迭代几次。
/// 2. 提升太多会削波、也会把失真推高。修正量夹在 +9 / -12 dB，并且算好预增益。
/// 3. 个别频点信噪比太低（比如 30 Hz 以下房间噪声太大），先按邻近点插值，不让一个坏点带偏整条线。
/// </summary>
public static class EqCurveFitter
{
    /// <summary>
    /// 校准的下限：100 Hz 以下一律不碰。
    ///
    /// 大多数书架箱、入耳式耳机和手机麦克风在 100 Hz 以下就已经掉下去了——
    /// 那不是频响的「颜色」，是单元冲程、箱体谐振和麦克风低频截止的硬限制。
    /// 拿 EQ 去补它，补出来的是失真，不是低频：提升 9 dB 换来的是更响的失真和一段
    /// 永远补不平的凹陷，还会把整条目标曲线往下压、把预增益压低，最后听着「修过了但变小了」。
    ///
    /// 所以拟合点从 100 Hz 开始；100–112 Hz 这一小段做线性淡入，
    /// 免得曲线在 100 Hz 上突然断成一个台阶。
    /// </summary>
    public const double CalibrationFloorHz = 100;

    /// <summary>100 Hz 之后用多宽过渡到完整修正（倍频程）。1/6 倍频程，约到 112 Hz。</summary>
    public const double FloorRampOctaves = 1.0 / 6.0;

    /// <summary>
    /// 默认的第一个拟合点：100 Hz 往上 1/6 倍频程。
    ///
    /// 不放在 100 Hz，是因为那会逼着最底那个推子去负责「100 Hz 处补 0 dB」：
    /// 解出来的增益是负的，101 Hz 附近反而被挖出一个 1–2 dB 的坑，比不补更糟。
    /// 从 112 Hz 起，第一个推子拿满额修正，它的裙边自然往 100 Hz 衰减，
    /// 100 Hz 以下则一个推子都没有——真正不碰。
    ///
    /// 自动EQ 里矫正起点可以被拖动；实际起点由 <see cref="BuildFrequencies(FitDensity, double)"/> 算。
    /// </summary>
    public const double FitStartHz = 112.2;

    /// <summary>拟合点的最高频率。</summary>
    public const double FitEndHz = 16000;

    /// <summary>「更准」档在中低频/高频之间的分界：4 kHz 以下用更密的 grid。</summary>
    public const double DenseCrossoverHz = 4000;

    /// <summary>
    /// 每种疏密对应的代表性倍频程间隔（「更准」档是它中低频那一段的间隔）。
    /// 只用来算一个基准 Q，实际每个点按自己的邻居算，见 <see cref="LocalStepOctaves"/>。
    /// </summary>
    public static double OctavesPerStep(FitDensity density) => density switch
    {
        FitDensity.Coarse => 0.5,
        FitDensity.Sparse => 1.0,
        FitDensity.Dense => 0.25,
        _ => 1.0 / 3.0,
    };

    /// <summary>
    /// Q 的收窄系数。
    ///
    /// 按「-3 dB 带宽 = 段距」算出来的 Q，相邻两个推子在对方中心处各掉 3 dB，
    /// 叠起来就是中点比中心低 6 dB 左右——段少的时候那就是肉眼可见的锯齿。
    /// 乘 0.8 让带宽比段距略宽，邻居之间互相兜一点，曲线才连得起来。
    /// 再低就糊成一片了：推子不再是「补这个频段」，而是「整体抬一片」。
    /// </summary>
    public const double SmoothnessFactor = 0.8;

    /// <summary>
    /// 拟合点用的 Q。带宽等于间隔时滤波器刚好首尾相接：Q = √(2^N)/(2^N−1)。
    /// 1 倍频程 → 1.41，半个倍频程 → 2.87，三分之一 → 4.32；再乘收窄系数。
    ///
    /// 这个值必须跟着疏密走：段距变了而 Q 不变的话，疏的时候中间会留洞、密的时候会叠在一起。
    /// </summary>
    public static double Quality(FitDensity density)
    {
        var bandwidth = Math.Pow(2, OctavesPerStep(density));
        return Math.Sqrt(bandwidth) / (bandwidth - 1) * SmoothnessFactor;
    }

    /// <summary>某种疏密会生成多少个拟合点（不含两端端点）。</summary>
    public static int PointCount(FitDensity density, double floorHz = CalibrationFloorHz) =>
        BuildFrequencies(density, floorHz).Length;

    /// <summary>
    /// 按疏密生成拟合点的中心频率。从 <paramref name="floorHz"/> 往上 1/6 倍频程开始，到 16 kHz 为止。
    ///
    /// 第一个推子不压在分界线上，而是留 1/6 倍频程的过渡：
    /// 压在分界线上会逼它去负责「分界线处补 0 dB」，解出来的增益是负的，
    /// 反而在分界线附近挖出一个坑。留一段，第一个推子拿满额修正，裙边自然往分界衰减。
    ///
    /// 除「更准」档外都是一段等距 grid；「更准」档分两段：4 kHz 以下每 1/4 倍频程一段，
    /// 4–16 kHz 每 1/3 倍频程一段。中低频是房间模式和单元谐振最窄的地方，值多花推子；
    /// 高频曲线本身更宽，再密只是振铃和相位问题。
    ///
    /// 每段结束时补一个恰好落在段末频率上的点，所以 16 kHz 永远是最后一个中心。
    /// </summary>
    public static double[] BuildFrequencies(FitDensity density, double floorHz = CalibrationFloorHz)
    {
        floorHz = Math.Clamp(floorHz, 20, FitEndHz / 4);
        var first = Math.Round(floorHz * Math.Pow(2, FloorRampOctaves), 1);

        var segments = density switch
        {
            FitDensity.Coarse => new[] { (Hz: FitEndHz, Step: 1.0 / 2.0) },
            FitDensity.Sparse => new[] { (Hz: FitEndHz, Step: 1.0) },
            FitDensity.Normal => new[] { (Hz: FitEndHz, Step: 1.0 / 3.0) },
            _ => new[] { (Hz: DenseCrossoverHz, Step: 1.0 / 4.0), (Hz: FitEndHz, Step: 1.0 / 3.0) },
        };

        var result = new List<double>(32) { first };
        var start = first;

        foreach (var (end, step) in segments)
        {
            if (end <= start)
            {
                continue;
            }

            for (var k = 1; ; k++)
            {
                var hz = start * Math.Pow(2, k * step);
                if (hz >= end - 0.05)
                {
                    result.Add(end);
                    break;
                }

                result.Add(Math.Round(hz, 1));
            }

            start = end;
        }

        return result.ToArray();
    }

    /// <summary>
    /// 第 index 段与左右邻居的平均距离（倍频程）。边缘段只有一侧，就用那一侧。
    /// Q 要按「自己这一格有多宽」算，不能按档位的平均段距算：
    /// 「更准」档的 grid 是两段不同的密度，用同一个 Q 会把 4 kHz 以上的推子挤在一起。
    /// </summary>
    public static double LocalStepOctaves(IReadOnlyList<double> fitFrequencies, int index)
    {
        if (fitFrequencies.Count < 2)
        {
            return 0.5;
        }

        var left = index > 0
            ? Math.Abs(Math.Log(fitFrequencies[index] / fitFrequencies[index - 1], 2))
            : double.NaN;
        var right = index + 1 < fitFrequencies.Count
            ? Math.Abs(Math.Log(fitFrequencies[index + 1] / fitFrequencies[index], 2))
            : double.NaN;

        if (double.IsNaN(left))
        {
            return right;
        }

        return double.IsNaN(right) ? left : (left + right) / 2;
    }

    /// <summary>按每个点自己的段距算一套基准 Q，后面的「按深度放宽」和「逐段收尾」都以它为起点。</summary>
    public static double[] LocalQualities(IReadOnlyList<double> fitFrequencies)
    {
        var result = new double[fitFrequencies.Count];
        for (var i = 0; i < fitFrequencies.Count; i++)
        {
            var bandwidth = Math.Pow(2, LocalStepOctaves(fitFrequencies, i));
            var quality = Math.Sqrt(bandwidth) / (bandwidth - 1) * SmoothnessFactor;
            result[i] = Math.Clamp(quality, 0.6, 10);
        }

        return result;
    }

    /// <summary>
    /// 单个滤波器的中心频率最多能离开等距格点多远（倍频程）。
    ///
    /// 能挪才谈得上「按实测摆位置」；挪太远，相邻两段的覆盖就断档，
    /// 补好一个坑又在别处留一个新坑。上限取半个段距，这里只是硬顶。
    /// </summary>
    public const double MaxBandShiftOctaves = 1.0 / 3.0;

    /// <summary>
    /// 两个滤波器中心之间至少留多远（倍频程）。
    ///
    /// 挨得太近，矩阵会接近奇异：两段在彼此中心上几乎一样高，解出来的增益一正一负互相抵消，
    /// 数字上看补上了，实际听感是一片糊。
    /// </summary>
    public const double MinBandGapOctaves = 1.0 / 6.0;

    /// <summary>在格点附近找位置时的步长（倍频程）。1/48 已经比人耳细，算力也不在乎。</summary>
    private const double BandSearchStepOctaves = 1.0 / 48.0;

    /// <summary>
    /// 把滤波器中心从等距格点上挪到修正曲线真正要动的地方。
    ///
    /// 等距格点管的是「每段一片」，不问那一片里有没有坑：3.2 kHz 的推子补不了 3.4 kHz 的窄谷，
    /// 因为它的中心压根不在谷上。多测几个点能看见那个谷，但看见不等于补得动——
    /// 这个方法是补得动的那一半。
    ///
    /// 做法：每段在自己的格点 ±半个段距内扫一圈，挑 |修正量| 最大的位置；
    /// 不许跨过上一段（留 <see cref="MinBandGapOctaves"/>），也不许越过下一格的另一半。
    /// 那一带本来就不需要补（修正量都接近 0），它就留在格点上。
    /// </summary>
    /// <param name="correction">已经平滑、限幅的修正量，与 <paramref name="frequencies"/> 一一对应。</param>
    /// <param name="frequencies">这次测了哪些频率。</param>
    /// <param name="density">疏密，决定格点和可挪的范围。</param>
    public static double[] PlaceFrequencies(
        double[] correction,
        IReadOnlyList<double> frequencies,
        FitDensity density,
        double floorHz = CalibrationFloorHz)
    {
        var grid = BuildFrequencies(density, floorHz);
        if (grid.Length == 0 || correction.Length < 4 || frequencies.Count < 4)
        {
            return grid;
        }

        var gap = Math.Pow(2, MinBandGapOctaves);
        var stepLog = BandSearchStepOctaves * Math.Log(2);
        var placed = new double[grid.Length];
        var previous = double.NaN;

        for (var i = 0; i < grid.Length; i++)
        {
            // 每段只在自己那一格的一半范围内挪：「更准」档 4 kHz 以上段距更宽，可挪范围也跟着宽。
            var reach = Math.Min(LocalStepOctaves(grid, i) * 0.5, MaxBandShiftOctaves);
            var gridLog = Math.Log(grid[i]);
            var lower = grid[i] * Math.Pow(2, -reach);
            var upper = grid[i] * Math.Pow(2, reach);

            // 下一格的另一半也是别人的地盘：留出最小间距，两段才不会挤在一处。
            var next = i + 1 < grid.Length ? grid[i + 1] : FitEndHz * 2;
            upper = Math.Min(upper, next / gap);

            if (!double.IsNaN(previous))
            {
                lower = Math.Max(lower, previous * gap);
            }

            if (lower >= upper)
            {
                placed[i] = Math.Max(grid[i], lower);
                previous = placed[i];
                continue;
            }

            var best = grid[i];
            var bestNeed = Math.Abs(SampleAtOrZero(correction, frequencies, grid[i]));
            var bestDistance = 0.0;

            for (var logHz = Math.Log(lower); logHz <= Math.Log(upper) + (stepLog * 0.5); logHz += stepLog)
            {
                var hz = Math.Min(Math.Exp(logHz), upper);
                var need = Math.Abs(SampleAtOrZero(correction, frequencies, hz));
                var distance = Math.Abs(logHz - gridLog);

                // 一样需要时选更靠近格点的那个：没必要时别乱挪。
                if (need > bestNeed + 1e-6
                    || (Math.Abs(need - bestNeed) <= 1e-6 && distance < bestDistance))
                {
                    best = hz;
                    bestNeed = need;
                    bestDistance = distance;
                }
            }

            placed[i] = Math.Round(best, 1);
            previous = placed[i];
        }

        // 四舍五入之后仍然要严格递增；真撞上了，后来那个让位。
        for (var i = 1; i < placed.Length; i++)
        {
            if (placed[i] <= placed[i - 1])
            {
                placed[i] = Math.Round(placed[i - 1] * 1.02, 1);
            }
        }

        return placed;
    }

    /// <summary>最多往上提多少 dB。再多，扬声器失真和削波都会明显。</summary>
    public const double MaxBoostDb = 9;

    /// <summary>最多往下压多少 dB。</summary>
    public const double MaxCutDb = 12;

    /// <summary>
    /// 目标曲线最多自动往下压多少 dB。
    ///
    /// 设备响度不够时，硬把增益提上去只会多出失真；把整条目标压下来，
    /// 让每一处提升都落在 +9 dB 以内，是更诚实的做法。
    ///
    /// 但压目标就是整体减音量：压 12 dB，整条曲线跟着低 12 dB，听着就是「校准完变小了」。
    /// 所以只留 6 dB 的余地，而且只按 100 Hz 以上的需要来算——低频下潜不够不再拖累整条曲线，
    /// 够不着的部分如实记进残差，不靠压缩音量去凑。
    /// </summary>
    public const double MaxAutoTargetDropDb = 6;

    /// <summary>目标偏移用的平滑宽度：三分之一倍频程。和频谱显示用的宽度一致。</summary>
    public const double SmoothingOctaves = 1.0 / 3.0;

    /// <summary>
    /// 修正量用的平滑宽度：六分之一倍频程。
    ///
    /// 比目标偏移那份窄一半。实测里 250–350 Hz 的房间峰往往只占六分之一倍频程，
    /// 用三分之一倍频程去平滑，+6.5 dB 的峰会被抹成 +2.4，拟合器只敢削一半，
    /// 下一轮再测还是「凸起」。窄到六分之一，只抹点与点之间的噪声，峰保留下来。
    /// </summary>
    public const double CorrectionSmoothingOctaves = 1.0 / 6.0;

    /// <summary>
    /// 窄坑判据：修正量降到峰值一半处的频率全宽，小于这个值就算「针尖」，整个放弃。
    ///
    /// 不用「平滑后的宽带需要 + 允许量」了——那条判据依赖 `Smooth` 的峰值保留率（传 1.0
    /// 时脉冲只剩一成），宽窄信息在平滑里已经糊掉。半高宽是直接量的：
    /// 1/6 倍频程宽的房间谷测得出来，1/14 的干涉零点也测得出来，不经过任何平滑。
    /// </summary>
    private const double NarrowNotchOctaves = 1.0 / 12.0;

    /// <summary>
    /// 针尖还要深到这个程度才放弃。单点谷的半高宽是 0，和单点针尖量出来一样，
    /// 只有「深」能把它们分开：1/14 的干涉零点 14 dB，640 Hz 那种看得见的谷 5 dB。
    /// </summary>
    private const double NarrowNotchDepthDb = 10.0;

    /// <summary>解完线性系统后用真实响应修正几轮，抹掉「dB 可叠加」这个近似的残差。</summary>
    private const int ResidualPasses = 3;

    /// <summary>
    /// 相邻 1/24 倍频程的落差超过这个值就算「曲线毛躁」。
    /// 1.5 dB 在这么窄的间隔上已经是肉眼可见的锯齿了。
    /// </summary>
    public const double MaxRippleDb = 1.5;

    /// <summary>
    /// 毛躁时用来挑 Q 的候选缩放。每个都跑一遍完整拟合，按「起伏 + 残差惩罚」打分取最好的。
    ///
    /// 最低只到 0.6，不再一路放到 0.4：0.4 那一档在快速档（段距 1/2 倍频程）会把 Q 从 2.3
    /// 压到 0.9 左右，一个推子的带宽比段距宽两倍多，相邻段只能靠一正一负互相抵消凑平——
    /// 采样点上是平的，点与点之间是一鼓一瘪，第二轮再测就看见「多了个宽包」。
    /// </summary>
    private static readonly double[] QualitySearchScales = { 1.0, 0.85, 0.7, 0.6 };

    /// <summary>
    /// 残差超过这个值，平滑的收益就要打折——不能为了好看把该补的丢掉。
    ///
    /// 原来是 2.5，等于 2.5 dB 以内的残差完全免费：Q 越宽曲线越顺，搜索就一路往最宽那档跑，
    /// 补出来的东西在测量点上能加平、实物上却是一整条宽包。0.5 让残差从第一分贝就参与打分。
    /// </summary>
    private const double ResidualBudgetDb = 0.5;

    /// <summary>
    /// 残差超过这个值，就在那一带补一段再解一次（残差驱动局部加密）。
    ///
    /// 3 dB 是「明显听得出来」的门槛；低于它的起伏靠现有格点就够了。
    /// </summary>
    private const double ResidualAddThresholdDb = 3.0;

    /// <summary>推子总数上限：Peace / EqualizerAPO 的配置里最多这么多段。</summary>
    public const int MaxFitPoints = 31;

    /// <summary>把 Q 自动调小时试的几档：一次只走一档，够宽就停。</summary>
    private static readonly double[] QualityRelaxScales = { 0.8, 0.65, 0.5 };

    /// <summary>波峰波谷上的 Q 最多能放宽多少比例。</summary>
    private const double MaxWidenFactor = 0.35;

    /// <summary>
    /// Q 调到这个值以下就没意义了：那已经比一个倍频程还宽，不再是「补这一段」。
    ///
    /// 原来是 0.7。实测发现放宽到 1.2 时，一个只占 1/3 倍频程的目标会逼着推子用
    /// +9 dB 去堆（宽滤波器中心只有靠大增益才能把那一小段抬起来），
    /// 结果目标只补 4 dB，EQ 实际抬了 8.8 dB——补过头，预增益被压掉 8 dB。
    /// 2.0 大约是一格半的宽度：够宽，不至于在段与段之间咬出锯齿；又不至于宽到失控。
    /// </summary>
    private const double MinRelaxedQuality = 2.0;

    /// <summary>「更平」要平出这么多才算数，免得为了 0.001 dB 反复解方程。</summary>
    private const double FlatnessEpsilon = 0.01;

    /// <summary>调宽 Q 时可以接受的残差损失：宽一点通常换来准确度差一点，但曲线更顺。</summary>
    private const double ResidualSlackDb = 0.3;

    /// <summary>
    /// 算一条 EQ。
    /// </summary>
    /// <param name="target">校准到哪条曲线。</param>
    /// <param name="measurement">一次扫频的测量结果。</param>
    /// <param name="targetOffsetDb">
    /// 把目标曲线整体移动多少 dB（负数往下压）。留 null 就自动判断：
    /// 需要的提升超过 <see cref="MaxBoostDb"/> 时，压到刚好够得着为止。
    /// </param>
    /// <param name="density">拟合的疏密，决定用多少个推子。</param>
    /// <param name="baselineEq">
    /// 这次测量时喇叭上已经套着的 EQ。不是 null 就先把它的响应从实测里减掉，
    /// 再算「还差多少」——否则同一个凹陷会被补第二遍。
    /// </param>
    /// <param name="movableBands">
    /// 允许滤波器中心离开等距格点，挪到修正曲线真正要动的地方（见 <see cref="PlaceFrequencies"/>）。
    ///
    /// 挪与不挪各解一遍，按「起伏 + 残差」打分取好的那个——挪过去不更贴就退回格点，
    /// 所以打开它不会比固定格点更差，只是多花两遍解方程的时间。
    /// </param>
    /// <param name="floorHz">
    /// 矫正起点。这一频率以下不由本层补：主层默认 100 Hz，自动EQ 里可以由用户拖动分界线改。
    /// 低音层会以同一个频率为上沿做反相，两层在分界线附近交接。
    /// </param>
    /// <param name="quick">
    /// 实时预览用的轻量模式：不摆位、只解一套 Q、不做逐段收尾。
    /// 测量途中每几百毫秒要重算一次，完整搜索会把 UI 线程拖死；测完正式结果仍然走完整流程。
    /// </param>
    /// <exception cref="ArgumentException">测量结果为空，或者一个可用的点都没有。</exception>
    public static CalibrationPlan Fit(
        CalibrationTargetKind target,
        SweepMeasurement measurement,
        double? targetOffsetDb = null,
        FitDensity density = FitDensity.Normal,
        EqPointList? baselineEq = null,
        bool movableBands = false,
        double floorHz = CalibrationFloorHz,
        bool quick = false,
        bool aggressive = false)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        // 激进轮：放宽限幅、跳过窄坑保护、减少平滑。
        // 不放松这三道闸，desired 算得再深也会被解回常规那套，用户看到的就是「曲线没变」。
        var maxBoost = aggressive ? MaxBoostDb * 1.7 : MaxBoostDb;
        var maxCut = aggressive ? MaxCutDb * 1.5 : MaxCutDb;
        var smoothing = aggressive ? CorrectionSmoothingOctaves * 0.5 : CorrectionSmoothingOctaves;

        floorHz = Math.Clamp(floorHz, 20, FitEndHz / 4);
        // 残差从第一个推子（矫正起点往上 1/6 倍频程）起算：
        // 起点到第一个推子之间是故意不补的过渡带，算进去会让「最大残差」永远虚高。
        var bounds = new FitBounds(floorHz * Math.Pow(2, FloorRampOctaves), FitEndHz);

        if (measurement.Points.Count == 0)
        {
            throw new ArgumentException("测量结果是空的。", nameof(measurement));
        }

        var frequencies = measurement.Frequencies;
        var levels = FillGaps(measurement);
        if (levels is null)
        {
            throw new ArgumentException("这次测量没有一个点可用：声音太轻，或者麦克风没录到东西。", nameof(measurement));
        }

        // 当前 EQ 在这些频点上的贡献。实测是「喇叭 + 它」，要还原成裸喇叭再算。
        double[]? baselineGains = null;
        if (baselineEq is not null)
        {
            var baselineSnapshot = baselineEq.ToSnapshot();
            baselineGains = new double[frequencies.Count];
            for (var i = 0; i < frequencies.Count; i++)
            {
                baselineGains[i] = EqResponse.GainAt(baselineSnapshot, frequencies[i]);
            }
        }

        var baseTarget = TargetCurve.Sample(target, frequencies);
        var offset = targetOffsetDb ?? AutoTargetOffset(baseTarget, levels, frequencies, floorHz);

        var targetDb = new double[baseTarget.Length];
        for (var i = 0; i < baseTarget.Length; i++)
        {
            targetDb[i] = baseTarget[i] + offset;
        }

        var correction = new double[frequencies.Count];
        for (var i = 0; i < correction.Length; i++)
        {
            correction[i] = targetDb[i] - levels[i] + (baselineGains?[i] ?? 0);
        }

        // 分界线以下一律不补也不削：先把那一段的修正量归零，再平滑、再归零一次。
        // 顺序很重要——先平滑的话，低频「需要补 30 dB」会顺着三分之一倍频程的窗口渗进
        // 分界线上方，拟合又被拖回去补下潜，权重等于白设。
        correction = ApplyFloorWeight(correction, frequencies, floorHz);

        // 窄坑判据用「半高宽 + 深度」，两者都在平滑前量：平滑会把针尖抹宽、抹浅，
        // 14 dB 的零点平滑后只剩 6 dB，深度判据就失效了。
        var rawCorrection = (double[])correction.Clone();
        var notchWidths = NotchWidths(rawCorrection, frequencies);

        correction = Smooth(correction, frequencies, smoothing);
        correction = ApplyFloorWeight(correction, frequencies, floorHz);

        for (var i = 0; i < correction.Length; i++)
        {
            correction[i] = Math.Clamp(correction[i], -maxCut, maxBoost);
        }

        // 激进轮跳过窄坑保护：那一道就是「深谷整个放弃」，留着它激进曲线会被打回原形。
        if (!aggressive)
        {
            correction = LimitNarrowBoosts(correction, notchWidths, rawCorrection);
        }

        var gridFrequencies = BuildFrequencies(density, floorHz);
        var baseQualities = LocalQualities(gridFrequencies);

        // 测量没覆盖满整段时（实时预览）不按深度放宽 Q：边界外的推子看不见，
        // 放宽会把增益漏到还没测的地方去。等测完再说了。
        var maxWiden = frequencies[^1] >= FitEndHz ? MaxWidenFactor : 0;

        // 摆法也进搜索：固定格点一份，按实测挪过的一份。挪过去不更贴就退回格点，
        // 所以打开 movableBands 不会更差，只是多花一遍解方程。
        // 实时预览（quick）只解固定格点，省掉一遍完整拟合。
        var placements = new List<double[]> { gridFrequencies };
        if (movableBands && !quick)
        {
            var moved = PlaceFrequencies(correction, frequencies, density, floorHz);
            if (!moved.SequenceEqual(gridFrequencies))
            {
                placements.Add(moved);
            }
        }

        // 不平滑就自己找：每个候选 Q 都完整拟合一次，量它的起伏和残差，挑综合最好的。
        // 段数少的时候，窄 Q 会在推子之间咬出锯齿；放宽能压平，但补得糊。
        // 打分就是这两者的折中——起伏算一分，残差超出预算的部分算两分。
        // 实时预览只跑基准 Q 这一档，测完再用完整搜索。
        var qualityScales = quick ? new[] { QualitySearchScales[0] } : QualitySearchScales;

        var bestScore = double.MaxValue;
        var found = false;
        var bestTarget = Array.Empty<double>();
        var bestFitFrequencies = gridFrequencies;
        var bestQualities = Array.Empty<double>();

        foreach (var fitFrequencies in placements)
        {
            var fitTarget = new double[fitFrequencies.Length];
            for (var i = 0; i < fitTarget.Length; i++)
            {
                fitTarget[i] = SampleAtOrZero(correction, frequencies, fitFrequencies[i]);
            }

            foreach (var scale in qualityScales)
            {
                var qualities = QualityPerPoint(fitTarget, baseQualities, scale, maxWiden);

                // 点本身可以不要，这一轮只是在挑「哪套摆法 + 哪个 Q」打分最好。
                var (_, _, score) = Evaluate(
                    fitTarget, fitFrequencies, qualities, targetDb, levels, frequencies, baselineGains,
                    bounds, maxBoost, maxCut);

                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestTarget = fitTarget;
                    bestFitFrequencies = fitFrequencies;
                    bestQualities = qualities;
                }
            }
        }

        // 波峰波谷上的 Q 再自动调小一轮：只在「实测曲线更平」时才留下。
        EqPointList points;
        double ripple;

        if (quick)
        {
            // 实时预览：解出来就直接用，不做逐段放宽。完整搜索留给测完那一次。
            var evaluated = Evaluate(
                bestTarget, bestFitFrequencies, bestQualities, targetDb, levels, frequencies, baselineGains,
                bounds, maxBoost, maxCut);
            points = evaluated.Points;
            ripple = evaluated.Ripple;
        }
        else
        {
            var relaxed = RelaxQuality(
                bestTarget, bestFitFrequencies, bestQualities, targetDb, levels, frequencies, baselineGains,
                bounds, floorHz, null, maxBoost, maxCut);
            points = relaxed.Points;
            ripple = relaxed.Ripple;
        }

        var preAmp = ComputePreAmp(points, floorHz);
        var residual = ComputeResidual(points, targetDb, levels, frequencies, baselineGains, bounds);

        // 残差驱动局部加密：格点之间漏掉的窄谷/窄峰，在残差大的地方补段再解一次。
        // 640 Hz 那种 4 dB 的谷，标准档 1/3 倍频程的格点盖不住——不是增益不给，是宽度不够。
        // 只补「残差 > 3 dB 且离已有格点够远」的频点，总数到 31 段为止。
        if (!quick)
        {
            var budget = Math.Max(0, MaxFitPoints - gridFrequencies.Length);
            var extra = budget > 0
                ? ResidualBands(residual, frequencies, gridFrequencies, floorHz)
                    .Take(budget)
                    .OrderBy(hz => hz)
                    .ToArray()
                : Array.Empty<double>();

            if (extra.Length > 0)
            {
                var denseGrid = gridFrequencies
                    .Concat(extra)
                    .Distinct()
                    .OrderBy(hz => hz)
                    .ToArray();

                var denseTarget = new double[denseGrid.Length];
                for (var i = 0; i < denseGrid.Length; i++)
                {
                    denseTarget[i] = SampleAtOrZero(correction, frequencies, denseGrid[i]);
                }

                var denseBase = LocalQualities(denseGrid);
                var denseScore = double.MaxValue;
                double[]? denseQualities = null;

                foreach (var scale in QualitySearchScales)
                {
                    var candidate = QualityPerPoint(denseTarget, denseBase, scale, maxWiden);
                    var (_, _, score) = Evaluate(
                        denseTarget, denseGrid, candidate, targetDb, levels, frequencies, baselineGains,
                        bounds, maxBoost, maxCut);

                    if (denseQualities is null || score < denseScore)
                    {
                        denseScore = score;
                        denseQualities = candidate;
                    }
                }

                if (denseQualities is not null)
                {
                    var denseRelaxed = RelaxQuality(
                        denseTarget, denseGrid, denseQualities, targetDb, levels, frequencies, baselineGains,
                        bounds, floorHz, null, maxBoost, maxCut);

                    points = denseRelaxed.Points;
                    ripple = denseRelaxed.Ripple;
                    preAmp = ComputePreAmp(points, floorHz);
                    residual = ComputeResidual(points, targetDb, levels, frequencies, baselineGains, bounds);
                }
            }
        }

        return new CalibrationPlan(
            target, measurement.Points, targetDb, correction, points, preAmp, offset, residual, ripple, floorHz);
    }

    /// <summary>
    /// 第二轮起的反馈拟合：在现有曲线上按实测误差走一步，而不是从头重算。
    ///
    /// 实测里含着已经套着的那套，所以「离目标还差多少」直接看得见。
    /// 新曲线 = 现在这条 + <paramref name="gain"/> 的误差，每段最多挪 <paramref name="maxStepDb"/> dB。
    /// 默认九成、6 dB：一到两轮就能贴住，一次测偏也不会让整条翻过去。
    ///
    /// 100 Hz 以下不追目标，而是每轮最多往回撤 <paramref name="maxStepDb"/> dB，直到归零——
    /// 上一版算法留在低频的提升会自己退掉，预增益跟着回正，整体音量不再被拖低。
    /// </summary>
    /// <param name="target">校准到哪条曲线。</param>
    /// <param name="appliedPlan">这次测量时喇叭上已经套着的那套。</param>
    /// <param name="measurement">这一轮的实测（含已套用的 EQ）。</param>
    /// <param name="density">拟合疏密。</param>
    /// <param name="gain">误差补几成。</param>
    /// <param name="maxStepDb">每段一轮最多挪多少 dB。</param>
    /// <param name="floorHz">矫正起点；分界线以下保持 0，低频补偿由低音层负责。</param>
    /// <param name="quick">实时预览用的轻量模式，见 <see cref="Fit"/>。</param>
    public static CalibrationPlan FitFeedback(
        CalibrationTargetKind target,
        CalibrationPlan appliedPlan,
        SweepMeasurement measurement,
        FitDensity density = FitDensity.Normal,
        double gain = 0.9,
        double maxStepDb = 6,
        double floorHz = CalibrationFloorHz,
        bool quick = false,
        bool aggressive = false)
    {
        ArgumentNullException.ThrowIfNull(appliedPlan);
        ArgumentNullException.ThrowIfNull(measurement);

        floorHz = Math.Clamp(floorHz, 20, FitEndHz / 4);

        var applied = appliedPlan.Points.ToSnapshot();
        var input = measurement.Points;
        var points = new List<MeasurementPoint>(input.Count);
        var frequencies = new List<double>(input.Count);
        var errors = new double[input.Count];

        for (var i = 0; i < input.Count; i++)
        {
            var hz = input[i].FrequencyHz;
            frequencies.Add(hz);
            errors[i] = TargetCurve.GainAt(target, hz) - input[i].LevelDb;
        }

        // 窄坑保护也要作用在反馈步长上，否则每轮都会往同一个窄坑里再加 6 dB：
        // 坑永远补不平，预增益却一轮一轮往下掉。判据和第一轮一样：半高宽。
        var notchWidths = NotchWidths(errors, frequencies);

        for (var i = 0; i < input.Count; i++)
        {
            var point = input[i];
            var hz = point.FrequencyHz;
            var targetDb = TargetCurve.GainAt(target, hz);
            var appliedGain = EqResponse.GainAt(applied, hz);

            double desired;
            if (!IsCalibrated(hz, floorHz))
            {
                // 分界线以下保持原样就是目标：0 dB。每轮只退一步，免得旧的补偿一次断掉。
                desired = appliedGain + Math.Clamp(-appliedGain, -maxStepDb, maxStepDb);
            }
            else
            {
                var error = errors[i];
                if (!aggressive && error > 0 && notchWidths[i] < NarrowNotchOctaves && error > NarrowNotchDepthDb)
                {
                    // 窄坑：不追。追也追不平，只会每轮往预增益里再压 2–3 dB。
                    error = 0;
                }

                desired = appliedGain + Math.Clamp(error * gain, -maxStepDb, maxStepDb);
            }

            // Fit 算的是 target − levels，这里反着填，让它算出 desired 那条曲线。
            points.Add(point with { LevelDb = targetDb - desired, SnrDb = 100 });
        }

        var synthetic = new SweepMeasurement(
            points, measurement.NoiseFloorDb, measurement.SampleRate, frequencies);

        // 目标偏移固定 0：这条曲线本身就可能起伏十几 dB，自动压低目标会把整条压扁。
        var fitted = Fit(
            target, synthetic, 0, density, null, movableBands: !quick, floorHz: floorHz, quick: quick,
            aggressive: aggressive);

        // 拟合用的是「目标 − 期望曲线」这条合成数据，但界面上那条灰线必须是**真测到的**。
        // 拿合成数据当 Measured，灰线画出来就是新 EQ 的反相（一个 +13 dB 的鼓包），
        // 低频频段也被 draw 成 0——看起来像「实测不平直、低频丢了」，其实是画错了对象。
        var residual = new double[input.Count];
        var snapshot = fitted.Points.ToSnapshot();
        // 残差从第一个推子（矫正起点往上 1/6 倍频程）起算：
        // 起点到第一个推子之间是故意不补的过渡带，算进去会让「最大残差」永远虚高。
        var bounds = new FitBounds(floorHz * Math.Pow(2, FloorRampOctaves), FitEndHz);
        for (var i = 0; i < input.Count; i++)
        {
            var hz = input[i].FrequencyHz;
            if (!bounds.Contains(hz))
            {
                residual[i] = 0;
                continue;
            }

            // 第二轮是整层替换：扣掉旧的 EQ、加上新的 EQ，才是调完之后真正的响应。
            var bare = input[i].LevelDb - EqResponse.GainAt(applied, hz);
            var targetDb = TargetCurve.GainAt(target, hz);
            residual[i] = targetDb - bare - EqResponse.GainAt(snapshot, hz);
        }

        return new CalibrationPlan(
            fitted.Target,
            input,
            fitted.TargetDb,
            fitted.CorrectionDb,
            fitted.Points,
            fitted.PreAmpDb,
            fitted.TargetOffsetDb,
            residual,
            fitted.RippleDb,
            fitted.FloorHz);
    }

    // ---- 低频矫正：单独写给「低音」层 ----
    //
    // 主层从 100 Hz 起才动推子，100 Hz 以下交给低音层。这一段不用主层那套
    // 「窄坑保护 + 自动压目标」，原因是测量时低音层不叠加：测到的就是主设备自己的低频，
    // 反相（或者补到目标）只有一个答案，不存在「测一轮再逼近」的反馈过程。
    //
    // 但「不反馈」不等于「不多测」：低频的坑往往比一个倍频程还宽，
    // 一段推子盖不住，所以这里仍然按 1/3 倍频程布点，并把相邻推子的叠加算进线性系统里。

    /// <summary>低频矫正的最低频率。20 Hz 是频轴端点，真正参与合并的推子从它上面开始。</summary>
    public const double BassFloorHz = 20;

    /// <summary>
    /// 第一个低频推子的中心频率：20 Hz 往上 1/6 倍频程。
    ///
    /// 20 Hz 那一点会被 <see cref="EqPointList"/> 吸附成端点；端点现在会并进主层端点参与合并，
    /// 但拟合这里仍从 22.4 Hz 起：20–22.4 Hz 这一小截交给第一个推子的裙边，
    /// 不让端点独自背修正，交界处更平。
    /// </summary>
    public const double BassFitStartHz = 22.4;

    /// <summary>低频矫正的上限：和主层的 112 Hz 起头衔接。</summary>
    public const double BassCeilingHz = 100;

    /// <summary>从这一频率开始往 0 dB 淡出，到 <see cref="BassCeilingHz"/> 正好回零，不和主层推子叠。</summary>
    public const double BassFadeStartHz = 80;

    /// <summary>低频推子的段距：三分之一倍频程。20–100 Hz 大约 8 个点。</summary>
    public const double BassStepOctaves = 1.0 / 3.0;

    /// <summary>低频修正量的平滑宽度。比主层的 1/3 窄一点：低频段本来就长，抹太宽会把房间模式一起抹平。</summary>
    public const double BassSmoothingOctaves = 1.0 / 6.0;

    /// <summary>低频最多往上提多少 dB。比主层的 +9 宽：低音炮/大单元的下潜补偿需要更多余量。</summary>
    public const double BassMaxBoostDb = 18;

    /// <summary>低频最多往下压多少 dB。</summary>
    public const double BassMaxCutDb = 18;

    /// <summary>
    /// 残差与平坦度的统计范围。
    ///
    /// 主层只统计「矫正起点–16 kHz」；低频层只统计「20 Hz–矫正起点」。范围外的点不参与打分：
    /// 低频那 30 dB 下潜不该让主层的每个候选都背同一个常数，
    /// 反过来主层的窄坑也不该影响低频的 Q 选择。
    /// </summary>
    private readonly record struct FitBounds(double LowHz, double HighHz)
    {
        public bool Contains(double frequencyHz) => frequencyHz >= LowHz && frequencyHz <= HighHz;
    }

    /// <summary>
    /// 低频推子的中心频率：22.4 Hz 起，每 1/3 倍频程一个，最后补一个 <paramref name="ceilingHz"/> 的锚点。
    ///
    /// 锚点那一点的修正量会被淡出权重压成 0，它只负责让曲线在交界处收住，
    /// 不参与合并（0 dB 点不会成为 <see cref="EqLayerSet.BassContributors"/>）。
    /// </summary>
    public static double[] BuildBassFrequencies(double ceilingHz = BassCeilingHz)
    {
        ceilingHz = Math.Clamp(ceilingHz, BassFitStartHz * 1.05, 1000);

        var result = new List<double>(20) { BassFitStartHz };
        var hz = BassFitStartHz;
        var step = Math.Pow(2, BassStepOctaves);

        while (true)
        {
            hz *= step;
            if (hz >= ceilingHz - 0.05)
            {
                result.Add(Math.Round(ceilingHz, 1));
                break;
            }

            result.Add(Math.Round(hz, 1));
        }

        return result.ToArray();
    }

    /// <summary>
    /// 从一次测量算出低音层的矫正曲线：20 Hz–<paramref name="ceilingHz"/>，最后 20% 线性淡出。
    ///
    /// 公式就是主层第一轮那一条——目标减实测——只是换成低频段、把提升上限放到 +18 dB。
    /// 它不需要「校验实测是否接近目标」：测量时低音层不叠加，测回来的永远是主设备的裸低频，
    /// 同一个实测只会得到同一条曲线。所以套用、继续测量都不会改变这条低频曲线的算法。
    /// </summary>
    /// <param name="target">校准到哪条曲线。平直就是反相，哈曼会额外抬上低频那 6 dB。</param>
    /// <param name="measurement">这一轮的实测（不含低音层）。</param>
    /// <param name="baselineEq">这次测量时主层已经套着的 EQ；它在低频基本是 0，但减掉更干净。</param>
    /// <param name="targetOffsetDb">
    /// 目标整体移动量。默认 0：主层的自动压目标只按矫正起点以上算，
    /// 低频不该跟着被压低，否则低音层还没补就先少了一截。
    /// </param>
    /// <param name="ceilingHz">低频层的上沿，通常就是自动EQ 图里那条可拖的分界线。</param>
    /// <param name="quick">实时预览用的轻量模式，见 <see cref="Fit"/>。</param>
    /// <exception cref="ArgumentException">低频测到的点太少，没法拟合。</exception>
    public static EqPointList FitBass(
        CalibrationTargetKind target,
        SweepMeasurement measurement,
        EqPointList? baselineEq = null,
        double targetOffsetDb = 0,
        double ceilingHz = BassCeilingHz,
        bool quick = false)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        ceilingHz = Math.Clamp(ceilingHz, BassFitStartHz * 1.05, 1000);
        var bounds = new FitBounds(BassFloorHz, ceilingHz);

        if (measurement.Points.Count == 0)
        {
            throw new ArgumentException("测量结果是空的。", nameof(measurement));
        }

        var frequencies = measurement.Frequencies;
        var levels = FillGaps(measurement);
        if (levels is null)
        {
            throw new ArgumentException("这次测量没有一个点可用：声音太轻，或者麦克风没录到东西。", nameof(measurement));
        }

        double[]? baselineGains = null;
        if (baselineEq is not null)
        {
            var baselineSnapshot = baselineEq.ToSnapshot();
            baselineGains = new double[frequencies.Count];
            for (var i = 0; i < frequencies.Count; i++)
            {
                baselineGains[i] = EqResponse.GainAt(baselineSnapshot, frequencies[i]);
            }
        }

        // 只取低频这一段，多留 1/12 倍频程给淡出收尾。
        var margin = Math.Pow(2, 1.0 / 12);
        var indices = new List<int>(32);
        for (var i = 0; i < frequencies.Count; i++)
        {
            if (frequencies[i] <= ceilingHz * margin)
            {
                indices.Add(i);
            }
        }

        if (indices.Count < 4)
        {
            throw new ArgumentException("低频测到的点太少，算不出低频矫正曲线。", nameof(measurement));
        }

        var lowFrequencies = new double[indices.Count];
        var lowLevels = new double[indices.Count];
        var lowBaseline = baselineGains is null ? null : new double[indices.Count];
        for (var i = 0; i < indices.Count; i++)
        {
            lowFrequencies[i] = frequencies[indices[i]];
            lowLevels[i] = levels[indices[i]];
            if (lowBaseline is not null)
            {
                lowBaseline[i] = baselineGains![indices[i]];
            }
        }

        var targetDb = TargetCurve.Sample(target, lowFrequencies);
        var correction = new double[lowFrequencies.Length];
        for (var i = 0; i < correction.Length; i++)
        {
            correction[i] = targetDb[i] + targetOffsetDb - lowLevels[i] + (lowBaseline?[i] ?? 0);
        }

        // 顺序和主层一样：先淡出（不然低频的大补偿会顺着平滑窗口渗到分界线以上），
        // 再平滑，再淡出一次，最后限幅。
        correction = ApplyBassFade(correction, lowFrequencies, ceilingHz);
        correction = Smooth(correction, lowFrequencies, BassSmoothingOctaves);
        correction = ApplyBassFade(correction, lowFrequencies, ceilingHz);

        for (var i = 0; i < correction.Length; i++)
        {
            correction[i] = Math.Clamp(correction[i], -BassMaxCutDb, BassMaxBoostDb);
        }

        // 残差/平坦度用的目标：correction 加上当时的实测，再减掉已套用的主层。
        // 这样 MaxResidualOf 里的「target − 裸实测 − EQ」正好化简成「correction − EQ」。
        var bassTargetDb = new double[lowFrequencies.Length];
        for (var i = 0; i < bassTargetDb.Length; i++)
        {
            bassTargetDb[i] = correction[i] + lowLevels[i] - (lowBaseline?[i] ?? 0);
        }

        var fitFrequencies = BuildBassFrequencies(ceilingHz);
        var fitTarget = new double[fitFrequencies.Length];
        for (var i = 0; i < fitTarget.Length; i++)
        {
            fitTarget[i] = SampleAtOrZero(correction, lowFrequencies, fitFrequencies[i]);
        }

        // 和主层同一套 Q 搜索：几个候选缩放各解一遍，按「起伏 + 残差」打分取最好的。
        // 低频段段距宽、推子互相叠得厉害，固定 Q 直接解会一段鼓一段瘪，听感就是锯齿。
        var baseQualities = LocalQualities(fitFrequencies);
        var maxWiden = lowFrequencies[^1] >= ceilingHz ? MaxWidenFactor : 0;

        var bestScore = double.MaxValue;
        var found = false;
        var bestQualities = Array.Empty<double>();
        var qualityScales = quick ? new[] { QualitySearchScales[0] } : QualitySearchScales;

        foreach (var scale in qualityScales)
        {
            var qualities = QualityPerPoint(fitTarget, baseQualities, scale, maxWiden);
            var (_, _, score) = Evaluate(
                fitTarget, fitFrequencies, qualities,
                bassTargetDb, lowLevels, lowFrequencies, lowBaseline,
                bounds, BassMaxBoostDb, BassMaxCutDb);

            if (!found || score < bestScore)
            {
                found = true;
                bestScore = score;
                bestQualities = qualities;
            }
        }

        EqPointList fitted;
        if (quick)
        {
            fitted = Evaluate(
                fitTarget, fitFrequencies, bestQualities,
                bassTargetDb, lowLevels, lowFrequencies, lowBaseline,
                bounds, BassMaxBoostDb, BassMaxCutDb).Points;
        }
        else
        {
            // 再逐段试更宽的 Q：只在「实测 + 补出来的」更平时才留下，和主层同一条规则。
            var relaxed = RelaxQuality(
                fitTarget, fitFrequencies, bestQualities,
                bassTargetDb, lowLevels, lowFrequencies, lowBaseline,
                bounds, BassFloorHz, ceilingHz,
                BassMaxBoostDb, BassMaxCutDb);
            fitted = relaxed.Points;
        }

        // 几乎没有动作的点留在低音层里只会碍事：合并时会被过滤掉，界面上却多出一排 0 dB。
        var kept = fitted.Points
            .Where(point => point.IsEndpoint
                || (Math.Abs(point.GainDb) >= 0.05 && point.FrequencyHz > BassFloorHz + 1))
            .ToList();

        return EqPointList.FromPoints(kept);
    }

    /// <summary>
    /// 低频修正量的淡出权重：上沿的最后 20% 线性收到 0，上沿以上归零。
    ///
    /// 低音层不淡出的话，它和主层从分界线往上 1/6 倍频程起的第一个推子会叠在一起，
    /// 同一个凹陷被补两遍。淡出到 0 就是「交接给主层」。
    /// </summary>
    private static double[] ApplyBassFade(double[] correction, IReadOnlyList<double> frequencies, double ceilingHz)
    {
        // 默认 100 Hz 上沿时就是 80–100 Hz 淡出；上沿被拖动时按同一比例跟着走。
        var fadeStart = ceilingHz * (BassFadeStartHz / BassCeilingHz);

        for (var i = 0; i < correction.Length; i++)
        {
            var hz = frequencies[i];
            if (!double.IsFinite(hz) || hz <= fadeStart)
            {
                continue;
            }

            correction[i] *= hz >= ceilingHz
                ? 0
                : (ceilingHz - hz) / (ceilingHz - fadeStart);
        }

        return correction;
    }

    /// <summary>
    /// 整条曲线在 20 Hz–20 kHz 上的最高点（dB）。
    ///
    /// 低音层叠进主层后，合并曲线的峰值会决定预增益；只看主层会漏掉低频的提升，
    /// 结果就是「补了低频也响了，但整体削波」。
    /// </summary>
    public static double PeakGainDb(EqPointList points, double fromHz = 20)
    {
        var snapshot = points.ToSnapshot();
        var peak = double.NegativeInfinity;
        var step = Math.Pow(2, 1.0 / 12);

        for (var hz = Math.Max(20, fromHz); hz <= 20000; hz *= step)
        {
            peak = Math.Max(peak, EqResponse.GainAt(snapshot, hz));
        }

        return double.IsFinite(peak) ? peak : 0;
    }

    /// <summary>
    /// 把 EQ 的实际响应按 1/24 倍频程扫一遍，量最大相邻落差。
    ///
    /// 这是连续扫频校验要看的那个量：61 个测量点只能告诉你点上的值，
    /// 点与点之间是推子撑起来的。Q 太窄的话，中间会鼓一圈或瘪下去，
    /// 听感上就是「补是补了，但变成了波浪」。
    /// </summary>
    private static double ComputeRipple(EqPointList points)
    {
        var snapshot = points.ToSnapshot();
        var step = Math.Pow(2, 1.0 / 24);
        var previous = EqResponse.GainAt(snapshot, 20);
        var worst = 0.0;

        for (var hz = 20 * step; hz <= 20000; hz *= step)
        {
            var current = EqResponse.GainAt(snapshot, hz);
            worst = Math.Max(worst, Math.Abs(current - previous));
            previous = current;
        }

        return worst;
    }

    /// <summary>这个频率在不在校准带里（默认 100 Hz 及以上）。</summary>
    public static bool IsCalibrated(double frequencyHz) => frequencyHz >= CalibrationFloorHz;

    /// <summary>这个频率在不在校准带里（分界线以上）。</summary>
    public static bool IsCalibrated(double frequencyHz, double floorHz) => frequencyHz >= floorHz;

    /// <summary>
    /// 在不在「真的摆推子」的那一段里：分界线–16 kHz。
    ///
    /// 残差只能在这段里统计。16 kHz 以上没有推子，麦克风自己在那儿掉 12 dB，
    /// 把它算进残差只会让「最大残差」永远是个吓人的数，收敛判断也永远过不去。
    /// </summary>
    public static bool IsInFitRange(double frequencyHz) =>
        frequencyHz >= CalibrationFloorHz && frequencyHz <= FitEndHz;

    /// <summary>在不在「真的摆推子」的那一段里：分界线–16 kHz。</summary>
    public static bool IsInFitRange(double frequencyHz, double floorHz) =>
        frequencyHz >= floorHz && frequencyHz <= FitEndHz;

    /// <summary>
    /// 把修正量按 100 Hz 的权重压一遍：100 Hz 以下是 0，100–112 Hz 线性淡入，再往上原样。
    ///
    /// 配合「起点往上 1/6 倍频程」这个第一个推子位置，这段淡入实际上不会产生任何推子——
    /// 它只是让「100 Hz 以上才开始校准」这件事在修正曲线上连续，采样时不留台阶。
    /// </summary>
    private static double[] ApplyFloorWeight(double[] correction, IReadOnlyList<double> frequencies, double floorHz)
    {
        var rampTop = floorHz * Math.Pow(2, FloorRampOctaves);

        for (var i = 0; i < correction.Length; i++)
        {
            var hz = frequencies[i];
            if (!double.IsFinite(hz) || hz >= rampTop)
            {
                continue;
            }

            correction[i] *= hz <= floorHz
                ? 0
                : Math.Log(hz / floorHz, 2) / FloorRampOctaves;
        }

        return correction;
    }

    /// <summary>
    /// 窄坑保护：只按宽带需要发提升许可。
    ///
    /// 实测里常有只占十几分之一倍频程的深坑（麦克风/单元干涉、房间零点）。EQ 能把它抬起来，
    /// 但抬它要花掉 9 dB 的提升额度，预增益跟着压 8 dB——整段音乐都变小了，而那个坑本身听不出来。
    /// 所以正修正量封顶在「一个倍频程窗口里的需要 + 2.5 dB」；削减方向不受影响。
    /// </summary>
    private static double[] LimitNarrowBoosts(
        double[] correction, double[] notchWidths, double[] rawCorrection)
    {
        for (var i = 0; i < correction.Length; i++)
        {
            if (correction[i] <= 0)
            {
                continue;
            }

            // 半高宽比阈值还窄、原始深度超过 10 dB：整个放弃。留「只补一点」会在目标曲线上
            // 形成一个窄鼓包，宽 Q 的推子要堆大增益才能跟上，结果就是 +9 和 −6 交替的梳状滤波。
            // 深度用平滑前的值——平滑会把 14 dB 的零点抹到 6 dB，用平滑后的就判不成针尖了。
            if (notchWidths[i] < NarrowNotchOctaves && rawCorrection[i] > NarrowNotchDepthDb)
            {
                correction[i] = 0;
            }
        }

        return correction;
    }

    /// <summary>
    /// 设备够不着目标时，整条目标该往下压多少。
    ///
    /// 先平滑再取最大值：不平滑的话，某一两个频点的尖峰会把整条目标拖下来。
    /// 只看分界线以上：低频掉 30 dB 是喇叭下潜不够，不该让整条目标跟着矮 30 ——
    /// 那会把所有频段的音量一起拉低，而低频还是补不上。
    /// 返回 0 表示够得着，不用动。
    /// </summary>
    private static double AutoTargetOffset(
        double[] targetDb,
        double[] levels,
        IReadOnlyList<double> frequencies,
        double floorHz)
    {
        var need = new double[targetDb.Length];
        for (var i = 0; i < need.Length; i++)
        {
            need[i] = targetDb[i] - levels[i];
        }

        need = Smooth(need, frequencies, SmoothingOctaves);

        var maxNeed = double.NegativeInfinity;
        for (var i = 0; i < frequencies.Count; i++)
        {
            if (IsCalibrated(frequencies[i], floorHz))
            {
                maxNeed = Math.Max(maxNeed, need[i]);
            }
        }

        if (!double.IsFinite(maxNeed) || maxNeed <= MaxBoostDb)
        {
            return 0;
        }

        return Math.Max(MaxBoostDb - maxNeed, -MaxAutoTargetDropDb);
    }

    /// <summary>
    /// 校准带内「目标 − 裸实测 − EQ」的最大绝对值。给 Q 搜索打分用：
    /// 平滑不能以丢掉该补的东西为代价，残差一旦超出预算就往回罚。
    ///
    /// 100 Hz 以下不算：那边本来就不补，把 20 Hz 差 30 dB 算进来，
    /// 每个候选都带着同一个常数，打分立刻失去区分度，收敛判断也永远过不去。
    /// </summary>
    private static double MaxResidualOf(
        EqPointList points,
        double[] targetDb,
        double[] levels,
        IReadOnlyList<double> frequencies,
        double[]? baselineGains,
        FitBounds bounds)
    {
        var snapshot = points.ToSnapshot();
        var worst = 0.0;

        for (var i = 0; i < frequencies.Count; i++)
        {
            if (!bounds.Contains(frequencies[i]))
            {
                continue;
            }

            var bare = levels[i] - (baselineGains?[i] ?? 0);
            var residual = targetDb[i] - bare - EqResponse.GainAt(snapshot, frequencies[i]);
            worst = Math.Max(worst, Math.Abs(residual));
        }

        return worst;
    }

    /// <summary>
    /// 每个频点上「（压低后的）目标」和「实测 + EQ 实际补出来的」差多少，正数表示还差多少没达到。
    ///
    /// 注意不是「应该补的」减「补出来的」——那个只反映拟合误差。
    /// 这里要回答的是用户真正问的：调完之后，还离目标多远。
    /// 限幅吃掉的部分、压到下限还够不着的部分，都会出现在这里。
    /// </summary>
    private static double[] ComputeResidual(
        EqPointList points,
        double[] targetDb,
        double[] levels,
        IReadOnlyList<double> frequencies,
        double[]? baselineGains,
        FitBounds bounds)
    {
        var snapshot = points.ToSnapshot();
        var residual = new double[frequencies.Count];

        for (var i = 0; i < frequencies.Count; i++)
        {
            // 分界线以下本来就不校准，残差记 0：那里「差 30 dB」是硬件限制，
            // 不是这次拟合没做到，不该混进「最大残差」去吓人，也不该拖住收敛判断。
            if (!bounds.Contains(frequencies[i]))
            {
                residual[i] = 0;
                continue;
            }

            // levels 是「喇叭 + 已套用的 EQ」的实测。先减掉那层，还原成裸喇叭，
            // 再和新拟合的 EQ 比——不然会把自己的上一次结果又算一遍。
            var bare = levels[i] - (baselineGains?[i] ?? 0);
            residual[i] = targetDb[i] - bare - EqResponse.GainAt(snapshot, frequencies[i]);
        }

        return residual;
    }

    /// <summary>
    /// 把有效的测量点填成一条完整的曲线：无效点用左右两边最近的有效点在 dB 域上插值。
    /// 一个可用点都没有时返回 null。
    /// </summary>
    private static double[]? FillGaps(SweepMeasurement measurement)
    {
        var points = measurement.Points;
        var result = new double[points.Count];
        var valid = new List<int>();

        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].IsUsable)
            {
                valid.Add(i);
                result[i] = points[i].LevelDb;
            }
            else
            {
                result[i] = double.NaN;
            }
        }

        if (valid.Count == 0)
        {
            return null;
        }

        if (valid.Count == points.Count)
        {
            return result;
        }

        var frequency = measurement.Frequencies;
        for (var i = 0; i < result.Length; i++)
        {
            if (!double.IsNaN(result[i]))
            {
                continue;
            }

            var left = valid.LastOrDefault(index => index < i, -1);
            var right = valid.FirstOrDefault(index => index > i, -1);

            if (left < 0)
            {
                result[i] = result[right];
            }
            else if (right < 0)
            {
                result[i] = result[left];
            }
            else
            {
                var t = (Math.Log(frequency[i]) - Math.Log(frequency[left]))
                    / (Math.Log(frequency[right]) - Math.Log(frequency[left]));
                result[i] = result[left] + (result[right] - result[left]) * t;
            }
        }

        return result;
    }

    /// <summary>
    /// 按三分之一倍频程做三角形加权平滑。
    /// 频点是按对数等距排的，所以每个点的邻居数量一样，直接用下标取邻域就等价于按倍频程取。
    /// </summary>
    /// <summary>
    /// 频域线性插值平滑。宽度参数虽然叫 octaves，内部按自然对数算距离，
    /// 传 1.0 的实际窗口是 1/ln2 ≈ 1.44 倍频程；而且三角形权重加归一化后，
    /// 单点脉冲只剩一成（见自测里的脉冲法）。改窗口前先看那两条数字。
    /// </summary>
    internal static double[] Smooth(double[] values, IReadOnlyList<double> frequencies, double widthOctaves) =>
        SmoothCore(values, frequencies, _ => widthOctaves);

    private static double[] SmoothCore(
        double[] values,
        IReadOnlyList<double> frequencies,
        Func<double, double> widthAt)
    {
        var result = new double[values.Length];
        var logFrequencies = frequencies.Select(hz => Math.Log(hz)).ToArray();

        for (var i = 0; i < values.Length; i++)
        {
            var half = widthAt(frequencies[i]) / 2;
            var sum = 0.0;
            var weightSum = 0.0;

            for (var j = 0; j < values.Length; j++)
            {
                var distance = Math.Abs(logFrequencies[j] - logFrequencies[i]);
                if (distance >= half)
                {
                    continue;
                }

                var weight = 1 - distance / half;
                sum += values[j] * weight;
                weightSum += weight;
            }

            result[i] = weightSum > 0 ? sum / weightSum : values[i];
        }

        return result;
    }

    /// <summary>
    /// 把目标曲线在每段的中心频率上解一遍，再按真实响应修几轮。
    ///
    /// 每个滤波器在别的滤波器中心频率上也有一点增益（半倍频程间距、Q 2.8 时约 −3 dB），
    /// 所以「每个点填修正值」会整体补过头。这里把它当成线性系统 A·x = 目标：
    /// A[i][j] 是第 j 段在 f_i 处、单位增益时的增益，解出来的 x 才让每个频点落在目标上。
    /// 解完再用真实响应修正两轮，抹掉「dB 可叠加」这个近似的残差。
    ///
    /// Q 由调用方给：全局搜、按深度放宽、逐段收尾，都要拿不同的 Q 反复解同一条曲线。
    /// </summary>
    private static EqPointList FitPoints(double[] target, double[] fitFrequencies, double[] qualities) =>
        FitPoints(target, fitFrequencies, qualities, MaxBoostDb, MaxCutDb);

    /// <summary>
    /// 同 <see cref="FitPoints(double[], double[], double[])"/>，但允许换一套限幅。
    /// 低音层用 ±18 dB，主层用 +9/−12 dB；除限幅之外两边解的是同一个方程。
    /// </summary>
    private static EqPointList FitPoints(
        double[] target,
        double[] fitFrequencies,
        double[] qualities,
        double maxBoostDb,
        double maxCutDb)
    {
        var fit = new double[target.Length];
        if (!Solve(BuildGainMatrix(fitFrequencies, qualities), target, fit))
        {
            // 矩阵解不出来（理论上有，实际上没碰到过）就退回直接取值。
            Array.Copy(target, fit, target.Length);
        }

        var points = BuildPointList(Clamp(fit, maxBoostDb, maxCutDb), fitFrequencies, qualities);
        fit = points.Points.Where(point => !point.IsEndpoint).Select(point => point.GainDb).ToArray();

        for (var pass = 0; pass < ResidualPasses; pass++)
        {
            var snapshot = points.ToSnapshot();
            var residual = new double[target.Length];
            var worst = 0.0;

            for (var i = 0; i < target.Length; i++)
            {
                residual[i] = target[i] - EqResponse.GainAt(snapshot, fitFrequencies[i]);
                worst = Math.Max(worst, Math.Abs(residual[i]));
            }

            if (worst < 0.1)
            {
                break;
            }

            for (var i = 0; i < fit.Length; i++)
            {
                fit[i] = Math.Clamp(fit[i] + residual[i] * 0.8, -maxCutDb, maxBoostDb);
            }

            points = BuildPointList(fit, fitFrequencies, qualities);
        }

        return points;
    }

    /// <summary>把一套 Q 完整解一遍，给出点、起伏和打分。</summary>
    private static (EqPointList Points, double Ripple, double Score) Evaluate(
        double[] target,
        double[] fitFrequencies,
        double[] qualities,
        double[] targetDb,
        double[] levels,
        IReadOnlyList<double> frequencies,
        double[]? baselineGains,
        FitBounds bounds,
        double maxBoostDb,
        double maxCutDb)
    {
        var points = FitPoints(target, fitFrequencies, qualities, maxBoostDb, maxCutDb);
        var ripple = ComputeRipple(points);
        var residual = MaxResidualOf(points, targetDb, levels, frequencies, baselineGains, bounds);
        var score = ripple + (Math.Max(0, residual - ResidualBudgetDb) * 2.0);
        return (points, ripple, score);
    }

    /// <summary>
    /// 波峰波谷上的 Q 自动调小，条件是「套上之后曲线更平」。
    ///
    /// 从最该动的段开始（增益绝对值最大），一次只动一段，试几档更宽的 Q，整条重新解一遍：
    /// 更平就留下，不更平就退回上一档。所以它只会把曲线调平，不会调坏——
    /// 它不接受「更抖」的结果，哪怕残差因此好看一点。
    ///
    /// 为什么往宽走：61 个点、三分之一倍频程平滑之后，尖峰和窄谷本身已经被抹宽了一次。
    /// 再用窄 Q 去追，补出来的是一个比源曲线还细的尖刺，下一轮测回来只会看见「又多了一个峰」。
    /// </summary>
    private static (EqPointList Points, double Ripple) RelaxQuality(
        double[] target,
        double[] fitFrequencies,
        double[] startQualities,
        double[] targetDb,
        double[] levels,
        IReadOnlyList<double> frequencies,
        double[]? baselineGains,
        FitBounds bounds,
        double lowHz,
        double? highHz,
        double maxBoostDb,
        double maxCutDb)
    {
        var qualities = (double[])startQualities.Clone();
        var current = Evaluate(
            target, fitFrequencies, qualities, targetDb, levels, frequencies, baselineGains,
            bounds, maxBoostDb, maxCutDb);

        // 只测了一截的时候不做这一步（实时预览就是这种）：格点里那些还没测到的频段目标值是 0，
        // 可放宽 Q 会让它们跟测到的那段互相串味——测到的地方平了一点点，没测的地方反倒鼓起来。
        // 100 Hz 以下不再要求测到：那里本来就不校准，只要高频测到了就算完整。
        if (frequencies[^1] < bounds.HighHz)
        {
            return (current.Points, current.Ripple);
        }

        var currentResidual = MaxResidualOf(current.Points, targetDb, levels, frequencies, baselineGains, bounds);
        var currentFlatness = ComputeFlatness(current.Points, levels, frequencies, baselineGains, lowHz, highHz);

        // 整轮只给这么多残差预算，不是每段各给一次——否则十几段各让一步，加起来就散了。
        var residualBudget = currentResidual + ResidualSlackDb;

        var gains = current.Points.Points
            .Where(point => !point.IsEndpoint)
            .Select(point => point.GainDb)
            .ToArray();

        var order = Enumerable.Range(0, Math.Min(gains.Length, fitFrequencies.Length))
            .OrderByDescending(index => Math.Abs(gains[index]))
            .ToArray();

        foreach (var band in order)
        {
            foreach (var factor in QualityRelaxScales)
            {
                var candidateQualities = (double[])qualities.Clone();
                candidateQualities[band] = Math.Max(MinRelaxedQuality, candidateQualities[band] * factor);

                if (Math.Abs(candidateQualities[band] - qualities[band]) < 1e-9)
                {
                    break;
                }

                var candidate = Evaluate(
                    target, fitFrequencies, candidateQualities, targetDb, levels, frequencies, baselineGains,
                    bounds, maxBoostDb, maxCutDb);
                var candidateResidual = MaxResidualOf(candidate.Points, targetDb, levels, frequencies, baselineGains, bounds);
                var candidateFlatness = ComputeFlatness(candidate.Points, levels, frequencies, baselineGains, lowHz, highHz);

                if (candidateFlatness < currentFlatness - FlatnessEpsilon
                    && candidateResidual <= residualBudget)
                {
                    qualities = candidateQualities;
                    current = candidate;
                    currentResidual = candidateResidual;
                    currentFlatness = candidateFlatness;
                    break;
                }
            }
        }

        return (current.Points, current.Ripple);
    }

    /// <summary>
    /// 套上这套 EQ 之后，「实测 + 补出来的」这条曲线有多平：
    /// 在 1/24 倍频程上扫一遍，量相邻两点的最大落差。
    ///
    /// 这和 <see cref="ComputeRipple"/> 不是一回事：那个只看 EQ 自己的形状，
    /// 这个看加上喇叭之后的结果——EQ 自己有起伏、但正好抵掉喇叭的起伏时，这里反而是平的。
    /// </summary>
    private static double ComputeFlatness(
        EqPointList points,
        double[] levels,
        IReadOnlyList<double> frequencies,
        double[]? baselineGains,
        double lowHz,
        double? highHz = null)
    {
        var snapshot = points.ToSnapshot();
        var step = Math.Pow(2, 1.0 / 24);
        // 从校准带的下沿开始量；上沿默认到测到的最高频。
        // 低频层传 highHz = 100，别把 100–105 那段淡出边界算成「新起伏」。
        var start = Math.Max(frequencies[0], lowHz);
        var end = highHz.HasValue ? Math.Min(frequencies[^1], highHz.Value) : frequencies[^1];
        var worst = 0.0;
        var previous = TotalAt(start);

        for (var hz = start * step; hz <= end; hz *= step)
        {
            var current = TotalAt(hz);
            worst = Math.Max(worst, Math.Abs(current - previous));
            previous = current;
        }

        return worst;

        double TotalAt(double hz)
        {
            var level = SampleAt(levels, frequencies, hz);
            var bare = baselineGains is null ? level : level - SampleAt(baselineGains, frequencies, hz);
            return bare + EqResponse.GainAt(snapshot, hz);
        }
    }

    /// <summary>
    /// 每个频点自己的 Q：动得多的段放宽，动得少的段照旧。
    ///
    /// 为什么是「动得多就放宽」：一个 9 dB 的深坑，用段距那么窄的推子去追，补出来是个尖刺——
    /// 中心补过头、两侧没补到，下一轮测回来就是「又多了一个峰」。宽一点，把整片抬起来，
    /// 曲线更平，听感也更自然。
    ///
    /// 上限放宽 35%（12 dB 的坑才到顶）：再多就不是「补这一段」，而是整片抬了。
    /// 放宽之后还会再过一轮 <see cref="RelaxQuality"/>，那一轮只在「实测曲线更平」时才继续放。
    /// </summary>
    /// <summary>
    /// 每个点「修正量降到峰值一半」的频率全宽（倍频程）。只对正向修正（谷）有意义。
    ///
    /// 直接扫描原始曲线，不经过任何平滑——平滑把针尖抹宽之后，宽窄信息就丢了。
    /// 峰越深，半高阈值越高、量出来的宽度越窄，正好对应「深而细的干涉零点不该追」。
    /// </summary>
    private static double[] NotchWidths(double[] correction, IReadOnlyList<double> frequencies)
    {
        var result = new double[correction.Length];

        for (var i = 0; i < correction.Length; i++)
        {
            result[i] = double.PositiveInfinity;
            if (correction[i] <= 0)
            {
                continue;
            }

            var half = correction[i] * 0.5;
            var low = i;
            var high = i;

            // 停在「最后一个还高于半高」的点上，宽度才是满足区间的宽度；
            // 停在第一个不满足的点上，单点针尖会被量成两个点距，就永远判不成针尖。
            while (low > 0 && correction[low - 1] >= half)
            {
                low--;
            }

            while (high < correction.Length - 1 && correction[high + 1] >= half)
            {
                high++;
            }

            result[i] = Math.Log2(frequencies[high] / frequencies[low]);
        }

        return result;
    }
    ///
    /// 只补两类之外的：分界线以下的过渡带（本来就不校准）、16 kHz 以上（没有推子）。
    /// 离已有格点太近的也不补——两段挨得太近，矩阵会接近奇异，解出来一正一负互相抵消。
    /// 返回按 |残差| 从大到小排，调用方按段数上限截断。
    /// </summary>
    private static double[] ResidualBands(
        double[] residual,
        IReadOnlyList<double> frequencies,
        IReadOnlyList<double> grid,
        double floorHz)
    {
        var rampTop = floorHz * Math.Pow(2, FloorRampOctaves);
        var result = new List<(double Hz, double Worst)>();

        for (var i = 0; i < residual.Length && i < frequencies.Count; i++)
        {
            var hz = frequencies[i];
            if (!double.IsFinite(hz) || hz < rampTop || hz > FitEndHz)
            {
                continue;
            }

            var worst = Math.Abs(residual[i]);
            if (worst <= ResidualAddThresholdDb)
            {
                continue;
            }

            if (grid.Any(g => Math.Abs(Math.Log(g / hz, 2)) < MinBandGapOctaves * 0.5))
            {
                continue;
            }

            if (result.Any(item => Math.Abs(Math.Log(item.Hz / hz, 2)) < MinBandGapOctaves))
            {
                continue;
            }

            result.Add((hz, worst));
        }

        return result
            .OrderByDescending(item => item.Worst)
            .Select(item => item.Hz)
            .ToArray();
    }

    private static double[] QualityPerPoint(
        double[] target,
        double[] baseQualities,
        double scale,
        double maxWiden)
    {
        var result = new double[target.Length];

        for (var i = 0; i < target.Length; i++)
        {
            var depth = Math.Abs(target[i]);
            var widen = Math.Clamp(depth / 12.0, 0, maxWiden);

            // 下限不能再用 0.6：那比一格还宽，解出来是「一正一负互相抵消」的病态解。
            // 至少留基准 Q 的一半，并且不低于 MinRelaxedQuality——和逐段收尾那条路同一个底线。
            var floor = Math.Max(MinRelaxedQuality, baseQualities[i] * 0.5);
            result[i] = Math.Clamp(baseQualities[i] * scale * (1 - widen), floor, 10);
        }

        return result;
    }

    /// <summary>
    /// 增益矩阵：第 j 段在 f_i 处、单位增益（1 dB）时的增益。
    /// 峰值滤波器的 dB 响应在 1 dB 附近近似线性，所以把它当成系数用。
    /// </summary>
    private static double[,] BuildGainMatrix(double[] fitFrequencies, double[] qualities)
    {
        var count = fitFrequencies.Length;
        var matrix = new double[count, count];

        for (var row = 0; row < count; row++)
        {
            for (var column = 0; column < count; column++)
            {
                var band = new SliderBand(
                    0,
                    fitFrequencies[column],
                    1.0,
                    qualities[column],
                    EqLimits.PeakingFilter,
                    true);
                matrix[row, column] = EqResponse.BandGain(band, fitFrequencies[row]);
            }
        }

        return matrix;
    }

    /// <summary>列主元高斯消元。方程只有十几二十个未知数，直接解比反复迭代省心。</summary>
    private static bool Solve(double[,] matrix, double[] right, double[] solution)
    {
        var size = right.Length;
        var a = (double[,])matrix.Clone();
        var b = (double[])right.Clone();

        for (var column = 0; column < size; column++)
        {
            var pivot = column;
            for (var row = column + 1; row < size; row++)
            {
                if (Math.Abs(a[row, column]) > Math.Abs(a[pivot, column]))
                {
                    pivot = row;
                }
            }

            if (Math.Abs(a[pivot, column]) < 1e-9)
            {
                return false;
            }

            if (pivot != column)
            {
                for (var k = 0; k < size; k++)
                {
                    (a[column, k], a[pivot, k]) = (a[pivot, k], a[column, k]);
                }

                (b[column], b[pivot]) = (b[pivot], b[column]);
            }

            for (var row = column + 1; row < size; row++)
            {
                var factor = a[row, column] / a[column, column];
                if (factor == 0)
                {
                    continue;
                }

                for (var k = column; k < size; k++)
                {
                    a[row, k] -= factor * a[column, k];
                }

                b[row] -= factor * b[column];
            }
        }

        for (var row = size - 1; row >= 0; row--)
        {
            var sum = b[row];
            for (var k = row + 1; k < size; k++)
            {
                sum -= a[row, k] * solution[k];
            }

            solution[row] = sum / a[row, row];
        }

        return true;
    }

    /// <summary>把解出来的增益夹进允许范围，并吃掉 NaN。</summary>
    private static double[] Clamp(double[] gains) => Clamp(gains, MaxBoostDb, MaxCutDb);

    /// <summary>同 <see cref="Clamp(double[])"/>，但换一套限幅（低音层用 ±18）。</summary>
    private static double[] Clamp(double[] gains, double maxBoostDb, double maxCutDb)
    {
        for (var i = 0; i < gains.Length; i++)
        {
            gains[i] = double.IsFinite(gains[i])
                ? Math.Clamp(gains[i], -maxCutDb, maxBoostDb)
                : 0;
        }

        return gains;
    }

    private static EqPointList BuildPointList(double[] gains, double[] fitFrequencies, double[] qualities)
    {
        var points = new List<EqPoint>(gains.Length + 2);
        for (var i = 0; i < gains.Length; i++)
        {
            points.Add(new EqPoint(0, fitFrequencies[i], gains[i], qualities[i], false));
        }

        return EqPointList.FromPoints(points);
    }

    /// <summary>
    /// 在修正曲线上取「这个频率该补多少」，但只取已测范围以内的。
    ///
    /// 实时预览时测量只覆盖了一部分频段。范围外一律返回 0（也就是不补），
    /// 不能夹到端点值——那会让还没测到的高频段继承最后一个点的增益，
    /// 看起来像「全都算完了」。正式测量时频率表覆盖全段，这个判断自然不生效。
    /// </summary>
    private static double SampleAtOrZero(double[] values, IReadOnlyList<double> frequencies, double frequencyHz)
    {
        // 留六分之一个倍频程的余量：刚好压在边界上的点，取端点的值比直接归零合理。
        var margin = Math.Pow(2, 1.0 / 12);
        if (frequencyHz < frequencies[0] / margin || frequencyHz > frequencies[^1] * margin)
        {
            return 0;
        }

        return SampleAt(values, frequencies, frequencyHz);
    }

    /// <summary>在修正曲线上取某个频率的值（对数频率线性插值）。</summary>
    private static double SampleAt(double[] values, IReadOnlyList<double> frequencies, double frequencyHz)
    {
        if (frequencyHz <= frequencies[0])
        {
            return values[0];
        }

        if (frequencyHz >= frequencies[^1])
        {
            return values[^1];
        }

        for (var i = 1; i < frequencies.Count; i++)
        {
            if (frequencyHz > frequencies[i])
            {
                continue;
            }

            var t = (Math.Log(frequencyHz) - Math.Log(frequencies[i - 1]))
                / (Math.Log(frequencies[i]) - Math.Log(frequencies[i - 1]));
            return values[i - 1] + (values[i] - values[i - 1]) * t;
        }

        return values[^1];
    }

    /// <summary>预增益：整条 EQ 曲线最高点在 0 dB 以上就压回去，免得响的地方削波。
    /// 只压不提——本来就没提升时保持 0 dB。
    /// </summary>
    private static double ComputePreAmp(EqPointList points, double fromHz = CalibrationFloorHz)
    {
        // 只按矫正起点以上算：起点以下归低音层，低音提多少由它自己负责，
        // 不该把左右层整体压下去——那会让中高频一起变小，图也往下掉。
        var peak = PeakGainDb(points, fromHz);
        if (peak <= 0.05)
        {
            return 0;
        }

        return Math.Round(Math.Max(peak * -1, -20), 1);
    }
}
