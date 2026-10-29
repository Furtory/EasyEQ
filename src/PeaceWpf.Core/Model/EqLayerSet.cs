
namespace PeaceWpf.Core.Model;

/// <summary>
/// 两层 EQ：主层（界面上的“左右”曲线）和低音层。
///
/// 后端只有一套段槽位，所以两层在保存时合并成一套点写进去。
/// 低音层的中间点直接追加；20 Hz / 20 kHz 端点不另占槽位，
/// 而是把增益并进主层的同名端点——同频只留一个滤波器，
/// 否则合并后会出现两对端点，既白占槽位，方形控制点也叠不上去。
/// </summary>
public sealed class EqLayerSet
{
    public EqLayerSet(EqPointList main, EqPointList bass)
    {
        Main = main ?? throw new ArgumentNullException(nameof(main));
        Bass = bass ?? throw new ArgumentNullException(nameof(bass));
    }

    /// <summary>主层，界面上的“左右”曲线。</summary>
    public EqPointList Main { get; }

    /// <summary>低音层，是否参与合并由开关决定。</summary>
    public EqPointList Bass { get; }

    /// <summary>两条曲线都是空的（还没读到数据）。</summary>
    public static EqLayerSet Default { get; } =
        new(EqPointList.CreateDefault(), EqPointList.CreateDefault());

    public EqLayerSet WithMain(EqPointList main) => new(main, Bass);

    public EqLayerSet WithBass(EqPointList bass) => new(Main, bass);

    /// <summary>
    /// 低音层里直接追加进合并列表的点：不是端点、增益不为 0。
    /// 端点走 <see cref="MergeEndpoint"/>，并进主层端点，不从这里拿。
    /// </summary>
    public IReadOnlyList<EqPoint> BassContributors =>
        Bass.Points.Where(point => !point.IsEndpoint && Math.Abs(point.GainDb) > 0.005).ToArray();

    /// <summary>
    /// 合并两层，得到要写后端的点列表。
    ///
    /// 合并的物理含义就是“两套滤波器同时生效”：把这些点按频率排好，
    /// 依次写进后端的段槽位，总响应是两层的叠加。
    /// </summary>
    public bool TryMerge(bool includeBass, out EqPointList merged, out string error) =>
        TryMerge(includeBass, null, out merged, out error);

    /// <summary>
    /// 合并两层再加一层风味，得到要写后端的点列表。
    ///
    /// 风味不是曲线内容，是叠加上去的一层固定滤波器：它只进 peace.txt，
    /// 不进 .peace、不进撤回栈。全平的风味贡献 0 条，结果与不吃风味时逐点相同。
    /// </summary>
    /// <param name="includeBass">低音层参不参与。</param>
    /// <param name="flavor">风味层；null 或全平时等于不吃风味。</param>
    /// <param name="merged">合并结果。</param>
    /// <param name="error">失败原因（目前不会失败，保留签名给以后的上限校验）。</param>
    public bool TryMerge(bool includeBass, FlavorSet? flavor, out EqPointList merged, out string error)
    {
        merged = Main;
        error = string.Empty;

        var points = new List<EqPoint>(Main.Points);
        if (includeBass)
        {
            // 先并端点，再追加中间点：端点必须保持首尾各一个，BassContributors 里没有端点。
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i].IsEndpoint)
                {
                    points[i] = MergeEndpoint(points[i], Bass);
                }
            }

            points.AddRange(BassContributors);
        }

        if (flavor is not null)
        {
            points.AddRange(flavor.Contributors);
        }

        // 合并后可以超过 31：EqualizerAPO 引擎没有段位上限（实测 40 条全部生效），
        // 31 只是 Peace 界面的滑杆数，超过时由界面提示“Peace 显示不全”。
        merged = EqPointList.FromPoints(points.OrderBy(point => point.FrequencyHz));
        return true;
    }

    /// <summary>
    /// 把低音层的同名端点并进主层端点：同频只留一个滤波器。
    ///
    /// 增益相加（端点在两层里都是真实滤波器，叠在一起就是两段响应相加）；
    /// Q 跟增益更大的那一层走，形状才不会由 0 dB 的那一层决定。
    /// 20 Hz / 20 kHz 的端点频率是锁死的，两层必然重合，所以不需要处理别的频率。
    /// </summary>
    private static EqPoint MergeEndpoint(EqPoint mainEndpoint, EqPointList bass)
    {
        var bassEndpoint = bass.Points.FirstOrDefault(point =>
            point.IsEndpoint && Math.Abs(point.FrequencyHz - mainEndpoint.FrequencyHz) < 0.5);

        if (bassEndpoint is null || Math.Abs(bassEndpoint.GainDb) <= 0.005)
        {
            return mainEndpoint;
        }

        var gain = Math.Clamp(
            mainEndpoint.GainDb + bassEndpoint.GainDb,
            EqLimits.MinGainDb,
            EqLimits.MaxGainDb);

        var quality = Math.Abs(bassEndpoint.GainDb) > Math.Abs(mainEndpoint.GainDb)
            ? bassEndpoint.Quality
            : mainEndpoint.Quality;

        return mainEndpoint with { GainDb = gain, Quality = quality };
    }

    /// <summary>合并后的“当前声音”曲线，只读状态和均衡模式用来画底图。</summary>
    public EqPointList MergedForDisplay(bool includeBass, FlavorSet? flavor = null)
    {
        if (!TryMerge(includeBass, flavor, out var merged, out _))
        {
            return Main;
        }

        return merged;
    }
}
