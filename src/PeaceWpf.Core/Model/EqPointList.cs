
namespace PeaceWpf.Core.Model;

/// <summary>
/// 一条按频率升序排好的点列表。2 到 31 个点，首尾两个是固定频率的端点。
///
/// 列表不可变：所有“改”都返回一份新列表。这样撤回栈里存的就是完整状态，
/// 脏标记也只需要一次逐点比较，不必记录每一步改了什么。
/// </summary>
public sealed class EqPointList : IEquatable<EqPointList>
{
    /// <summary>频率轴左端，也是左端点所在的位置。</summary>
    public const double MinFrequencyHz = EqLimits.MinEditableFrequencyHz;

    /// <summary>频率轴右端，也是右端点所在的位置。</summary>
    public const double MaxFrequencyHz = EqLimits.MaxEditableFrequencyHz;

    /// <summary>把首尾点吸附成端点时允许的误差。</summary>
    private const double EndpointToleranceHz = 0.5;

    /// <summary>相邻两点之间至少留 0.1% 的距离，避免频率完全相同导致拖动死锁。</summary>
    private const double NeighbourRatio = 1.001;

    private readonly EqPoint[] _points;

    private EqPointList(EqPoint[] points)
    {
        _points = points;
    }

    /// <summary>空列表。只用于还没读到数据时占位。</summary>
    public static EqPointList Empty { get; } = new(Array.Empty<EqPoint>());

    /// <summary>按频率升序的点。</summary>
    public IReadOnlyList<EqPoint> Points => _points;

    public int Count => _points.Length;

    public bool IsEmpty => _points.Length == 0;

    /// <summary>下一个可用的 Id，保证同一次会话里不重号。</summary>
    public int NextId
    {
        get
        {
            var max = 0;
            foreach (var point in _points)
            {
                if (point.Id > max)
                {
                    max = point.Id;
                }
            }

            return max + 1;
        }
    }

    /// <summary>要把这些点原样写回后端，至少需要多少个段槽位。</summary>
    public int RequiredBandCount =>
        Math.Clamp(Math.Max(Count, EqLimits.MinBands), EqLimits.MinBands, EqLimits.MaxBands);

    /// <summary>R1 规定的初始状态：一条平线，只有 20 Hz 与 20 kHz 两个端点。</summary>
    public static EqPointList CreateDefault() => new(new[]
    {
        new EqPoint(1, MinFrequencyHz, 0, EqLimits.DefaultQuality, true),
        new EqPoint(2, MaxFrequencyHz, 0, EqLimits.DefaultQuality, true),
    });

    /// <summary>从界面存的点恢复。会清洗越界值、补端点、重排 Id。</summary>
    public static EqPointList FromPoints(IEnumerable<EqPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var raw = points
            .Where(point => point is not null && point.IsFinite)
            .Select(point => point with
            {
                FrequencyHz = Math.Clamp(point.FrequencyHz, MinFrequencyHz, MaxFrequencyHz),
                GainDb = ClampGain(point.GainDb),
                Quality = ClampQuality(point.Quality),
                FilterType = EqLimits.ClampFilter(point.FilterType),
            })
            .OrderBy(point => point.FrequencyHz)
            .ToList();

        return Normalize(raw, sequentialIds: true);
    }

    /// <summary>
    /// 把后端读回来的段转成点。
    ///
    /// 只收“真的在改变声音”的段：启用、增益不为 0、类型是点模型表达得了的（PK 或架式）。
    /// 端点是后补的，所以一条全平的预设读进来就是两个端点。
    /// 低通、高通、带通这类类型不在点模型里，读的时候会被丢掉 —— 调用方要单独提示，
    /// 不能悄悄把它们当 0 dB 峰值写回去。
    /// </summary>
    public static EqPointList FromBands(IEnumerable<SliderBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);

        var candidates = bands
            .Where(band => band is not null
                && band.Enabled
                && EqLimits.IsSupportedFilter(band.FilterType)
                && Math.Abs(band.GainDb) >= 0.005
                && double.IsFinite(band.FrequencyHz)
                && band.FrequencyHz > 0)
            .OrderByDescending(band => Math.Abs(band.GainDb))
            .Select(band => new EqPoint(
                0,
                band.FrequencyHz,
                band.GainDb,
                band.Quality,
                false,
                EqLimits.ClampFilter(band.FilterType)))
            .OrderBy(point => point.FrequencyHz)
            .ToList();

        return candidates.Count == 0 ? CreateDefault() : Normalize(candidates, sequentialIds: true);
    }

    /// <summary>按 Id 找点，没有就返回 null。</summary>
    public EqPoint? Find(int id)
    {
        var index = IndexOfId(id);
        return index >= 0 ? _points[index] : null;
    }

    /// <summary>按 Id 找下标，没有返回 -1。</summary>
    public int IndexOfId(int id)
    {
        for (var i = 0; i < _points.Length; i++)
        {
            if (_points[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>找频率上最近的点。命中判定在界面按像素做，这里给状态栏和测试用。</summary>
    public EqPoint? Nearest(double frequencyHz)
    {
        EqPoint? best = null;
        var bestDistance = double.MaxValue;
        foreach (var point in _points)
        {
            var distance = Math.Abs(Math.Log(point.FrequencyHz) - Math.Log(Math.Max(frequencyHz, 1)));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        return best;
    }

    /// <summary>
    /// 在指定频率新建一个点，增益 0。教程和自测用的简写，规则与下面这个重载完全一致。
    /// </summary>
    public bool TryAdd(double frequencyHz, out EqPointList list, out EqPoint point) =>
        TryAdd(frequencyHz, 0, out list, out point);

    /// <summary>
    /// 找“这个频率属于哪个推子”：只在普通点里找，对数频率距离不超过 <paramref name="maxOctaves"/> 的才算候选，
    /// 取最近的一个。没有就返回 null —— 调用方据此新建一个。
    ///
    /// 端点是频轴的固定地基、没有自己的钟形，抓取只在它们自己的手柄上生效。
    /// </summary>
    public EqPoint? NearestWithin(double frequencyHz, double maxOctaves)
    {
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0 || !(maxOctaves > 0))
        {
            return null;
        }

        EqPoint? best = null;
        var bestDistance = double.MaxValue;
        foreach (var point in _points)
        {
            if (point.IsEndpoint)
            {
                continue;
            }

            var distance = Math.Abs(Math.Log2(frequencyHz / point.FrequencyHz));
            if (distance > maxOctaves || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = point;
        }

        return best;
    }

    /// <summary>
    /// 在指定频率、指定增益新建一个点，Q 按左右邻居自动挑（见 <see cref="SuggestQuality"/>）。
    /// 只拒绝“与已有点太近”；点数不设上限。
    /// </summary>
    public bool TryAdd(double frequencyHz, double gainDb, out EqPointList list, out EqPoint point)
    {
        list = this;
        point = null!;

        if (!double.IsFinite(frequencyHz))
        {
            return false;
        }

        var hz = Math.Clamp(frequencyHz, MinFrequencyHz, MaxFrequencyHz);
        foreach (var existing in _points)
        {
            if (Math.Abs(existing.FrequencyHz - hz) <= existing.FrequencyHz * (NeighbourRatio - 1))
            {
                return false;
            }
        }

        var created = new EqPoint(NextId, hz, ClampGain(gainDb), SuggestQuality(hz), false);
        var array = _points.Append(created).OrderBy(item => item.FrequencyHz).ToArray();
        list = new EqPointList(array);
        point = created;
        return true;
    }

    /// <summary>
    /// 给新点挑 Q：让钟形的半功率点正好落在最近的那个邻居身上 ——
    /// 旁边挤就窄，空地就宽。夹在 <see cref="EqLimits.MinAutoQuality"/>–<see cref="EqLimits.MaxAutoQuality"/> 之间，
    /// 免得一个点糊满半个频段，或者细成一根针。
    ///
    /// 换算用的是峰值滤波器自己的带宽关系：Q = √(2ⁿ) / (2ⁿ − 1)，n 是倍频程带宽。
    /// </summary>
    private double SuggestQuality(double frequencyHz)
    {
        var previous = 0.0;
        var next = double.MaxValue;
        foreach (var existing in _points)
        {
            if (existing.FrequencyHz < frequencyHz)
            {
                previous = Math.Max(previous, existing.FrequencyHz);
            }
            else if (existing.FrequencyHz > frequencyHz)
            {
                next = Math.Min(next, existing.FrequencyHz);
            }
        }

        // 两侧的空隙，取窄的那一边：钟形只是不该压到最近的邻居，远的那侧不用管。
        var gapOctaves = double.MaxValue;
        if (previous > 0)
        {
            gapOctaves = Math.Min(gapOctaves, Math.Log2(frequencyHz / previous));
        }

        if (next < double.MaxValue)
        {
            gapOctaves = Math.Min(gapOctaves, Math.Log2(next / frequencyHz));
        }

        if (!double.IsFinite(gapOctaves) || gapOctaves <= 0)
        {
            return EqLimits.DefaultQuality;
        }

        // 半功率点落在邻居身上，所以整条带宽是两边空隙之和 —— 这里按窄的那侧翻倍算。
        var octaves = Math.Clamp(gapOctaves * 2, 1e-6, 12);
        var ratio = Math.Pow(2, octaves);
        var quality = Math.Sqrt(ratio) / (ratio - 1);
        return Math.Clamp(quality, EqLimits.MinAutoQuality, EqLimits.MaxAutoQuality);
    }

    /// <summary>删掉一个点。端点和“只剩两个点”的情况都拒绝，返回 null。</summary>
    public EqPointList? Remove(int id)
    {
        var index = IndexOfId(id);
        if (index < 0 || _points[index].IsEndpoint || Count <= 2)
        {
            return null;
        }

        return new EqPointList(_points.Where(point => point.Id != id).ToArray());
    }

    /// <summary>
    /// 移动一个点。频率会夹在相邻两点之间（留 0.1% 余量），端点的频率锁死；
    /// 增益夹在 ±30 dB。两个参数给 null 表示这一维不动。
    /// </summary>
    public EqPointList Move(int id, double? frequencyHz = null, double? gainDb = null)
    {
        var index = IndexOfId(id);
        if (index < 0)
        {
            return this;
        }

        var point = _points[index];
        var gain = gainDb.HasValue ? ClampGain(gainDb.Value) : point.GainDb;
        var frequency = point.FrequencyHz;

        if (!point.IsEndpoint && frequencyHz.HasValue)
        {
            var lower = index > 0 ? _points[index - 1].FrequencyHz * NeighbourRatio : MinFrequencyHz;
            var upper = index < Count - 1 ? _points[index + 1].FrequencyHz / NeighbourRatio : MaxFrequencyHz;
            var wanted = Math.Clamp(frequencyHz.Value, MinFrequencyHz, MaxFrequencyHz);
            frequency = lower <= upper ? Math.Clamp(wanted, lower, upper) : point.FrequencyHz;
        }

        if (frequency.Equals(point.FrequencyHz) && gain.Equals(point.GainDb))
        {
            return this;
        }

        var copy = (EqPoint[])_points.Clone();
        copy[index] = point with { FrequencyHz = frequency, GainDb = gain };
        return new EqPointList(copy);
    }

    /// <summary>改 Q。端点也允许改 Q。</summary>
    public EqPointList SetQuality(int id, double quality)
    {
        var index = IndexOfId(id);
        if (index < 0)
        {
            return this;
        }

        var clamped = ClampQuality(quality);
        if (clamped.Equals(_points[index].Quality))
        {
            return this;
        }

        var copy = (EqPoint[])_points.Clone();
        copy[index] = copy[index] with { Quality = clamped };
        return new EqPointList(copy);
    }

    /// <summary>
    /// 转成频响仿真需要的快照。bandCount 大于点数时，多出来的槽位补“停用 + 0 dB”，
    /// 这样曲线不会因为后端段数多而多出东西。
    /// </summary>
    public PeaceSnapshot ToSnapshot(double preAmpDb = 0, int bandCount = 0)
    {
        var total = Math.Max(bandCount, Count);
        var bands = new List<SliderBand>(total);

        for (var i = 0; i < Count; i++)
        {
            var point = _points[i];
            bands.Add(new SliderBand(
                i,
                point.FrequencyHz,
                point.GainDb,
                point.Quality,
                point.FilterType,
                true));
        }

        for (var i = Count; i < total; i++)
        {
            bands.Add(new SliderBand(
                i,
                1000,
                0,
                EqLimits.DefaultQuality,
                EqLimits.PeakingFilter,
                false));
        }

        return new PeaceSnapshot(bands, preAmpDb, total);
    }

    public bool Equals(EqPointList? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || other._points.Length != _points.Length)
        {
            return false;
        }

        for (var i = 0; i < _points.Length; i++)
        {
            if (!_points[i].Equals(other._points[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EqPointList other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var point in _points)
        {
            hash.Add(point);
        }

        return hash.ToHashCode();
    }

    public override string ToString() =>
        string.Join(", ", _points.Select(point => $"{EqFormat.Frequency(point.FrequencyHz)} {EqFormat.Gain(point.GainDb)}"));

    /// <summary>排序、去重、补端点、限数量，最后按顺序重排 Id。</summary>
    private static EqPointList Normalize(List<EqPoint> ordered, bool sequentialIds)
    {
        var result = new List<EqPoint>(ordered.Count + 2);
        var lastFrequency = 0.0;

        foreach (var point in ordered)
        {
            var frequency = Math.Clamp(point.FrequencyHz, MinFrequencyHz, MaxFrequencyHz);
            if (result.Count > 0)
            {
                var minimum = lastFrequency * NeighbourRatio;
                if (frequency < minimum)
                {
                    frequency = minimum;
                }

                if (frequency > MaxFrequencyHz)
                {
                    continue;
                }
            }

            result.Add(point with
            {
                Id = sequentialIds ? result.Count + 1 : point.Id,
                FrequencyHz = frequency,
                GainDb = ClampGain(point.GainDb),
                Quality = ClampQuality(point.Quality),
            });
            lastFrequency = frequency;
        }

        if (result.Count == 0)
        {
            result.Add(new EqPoint(1, MinFrequencyHz, 0, EqLimits.DefaultQuality, true));
            result.Add(new EqPoint(2, MaxFrequencyHz, 0, EqLimits.DefaultQuality, true));
        }
        else
        {
            if (result[0].FrequencyHz > MinFrequencyHz + EndpointToleranceHz)
            {
                result.Insert(0, new EqPoint(0, MinFrequencyHz, 0, EqLimits.DefaultQuality, true));
            }

            if (result[^1].FrequencyHz < MaxFrequencyHz - EndpointToleranceHz)
            {
                result.Add(new EqPoint(0, MaxFrequencyHz, 0, EqLimits.DefaultQuality, true));
            }
        }

        // 首尾就是端点，频率吸附到 20 / 20k；端点不能被后面的逻辑当成普通点删掉。
        result[0] = result[0] with { FrequencyHz = MinFrequencyHz, IsEndpoint = true };
        result[^1] = result[^1] with { FrequencyHz = MaxFrequencyHz, IsEndpoint = true };

        if (sequentialIds)
        {
            for (var i = 0; i < result.Count; i++)
            {
                result[i] = result[i] with { Id = i + 1 };
            }
        }

        return new EqPointList(result.ToArray());
    }

    private static double ClampGain(double gainDb) =>
        double.IsFinite(gainDb) ? Math.Clamp(gainDb, EqLimits.MinGainDb, EqLimits.MaxGainDb) : 0;

    private static double ClampQuality(double quality) =>
        double.IsFinite(quality) ? Math.Clamp(quality, EqLimits.MinQuality, EqLimits.MaxQuality) : EqLimits.DefaultQuality;
}
