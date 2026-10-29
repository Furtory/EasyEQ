namespace PeaceWpf.Core.Model;

/// <summary>
/// 一条风味滤波器的当前状态。数量固定，靠下标认人（和 <see cref="ToneBands.All"/> 同序）。
/// </summary>
public sealed record FlavorBand(
    string Key,
    double FrequencyHz,
    double GainDb,
    double Quality,
    int FilterType);

/// <summary>
/// 风味层：九条固定滤波器，叠加在左右曲线上，不写进 .peace。
///
/// 和 <see cref="EqPointList"/> 的区别是“不许增删”：用户只能改每条的
/// 频率、增益、Q，条目本身永远是这九条。所以这里没有 Add/Remove，只有 With*。
///
/// 不可变：所有“改”都返回一份新集合，方便比较、也方便界面上一次改一帧。
/// </summary>
public sealed class FlavorSet : IEquatable<FlavorSet>
{
    /// <summary>固定条数，和 <see cref="ToneBands.All"/> 一一对应。</summary>
    public const int Count = 9;

    /// <summary>点一次「增强/减弱」改变的增益。改手感只改这一个数。</summary>
    public const double StepDb = 1.0;

    /// <summary>增益绝对值小于这个数就当没在改变声音（和低音层合并同一条线）。</summary>
    private const double SilentGainDb = 0.005;

    private readonly FlavorBand[] _bands;

    private FlavorSet(FlavorBand[] bands)
    {
        _bands = bands;
    }

    /// <summary>各条风味，顺序与 <see cref="ToneBands.All"/> 一致。</summary>
    public IReadOnlyList<FlavorBand> Bands => _bands;

    /// <summary>按下标取一条；越界返回第一条（界面上永远不该越界）。</summary>
    public FlavorBand this[int index] => _bands[Math.Clamp(index, 0, Count - 1)];

    /// <summary>九条全是 0 dB：不产生任何滤波器，曲线也不用画。</summary>
    public bool IsFlat
    {
        get
        {
            foreach (var band in _bands)
            {
                if (Math.Abs(band.GainDb) >= SilentGainDb)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>出厂状态：频率、Q、类型取表里的默认值，增益全 0。</summary>
    public static FlavorSet CreateDefault() =>
        new(ToneBands.All
            .Select(band => new FlavorBand(
                band.Key,
                band.DefaultFrequencyHz,
                0,
                band.DefaultQuality,
                band.FilterType))
            .ToArray());

    /// <summary>
    /// 从落盘的数据重建：按表里的顺序排，缺的补默认，多的丢掉，越界的夹回来。
    /// 这样以后往表里加一条类型，老存档读进来也只是那一条是默认值。
    /// </summary>
    public static FlavorSet FromBands(IEnumerable<FlavorBand>? stored)
    {
        var byKey = new Dictionary<string, FlavorBand>(StringComparer.Ordinal);
        if (stored is not null)
        {
            foreach (var band in stored)
            {
                if (band is null || string.IsNullOrWhiteSpace(band.Key))
                {
                    continue;
                }

                byKey[band.Key] = band;
            }
        }

        var result = new FlavorBand[Count];
        for (var i = 0; i < Count; i++)
        {
            var definition = ToneBands.All[i];
            result[i] = byKey.TryGetValue(definition.Key, out var band)
                ? Sanitize(definition, band)
                : NewBand(definition);
        }

        return new FlavorSet(result);
    }

    /// <summary>改增益（夹在 ±30），只动这一条。</summary>
    public FlavorSet WithGain(int index, double gainDb)
    {
        if (index < 0 || index >= Count || !double.IsFinite(gainDb))
        {
            return this;
        }

        var band = _bands[index];
        var gain = Math.Clamp(gainDb, EqLimits.MinGainDb, EqLimits.MaxGainDb);
        if (gain.Equals(band.GainDb))
        {
            return this;
        }

        return Replace(index, band with { GainDb = gain });
    }

    /// <summary>改频率（夹在本类型的覆盖范围内），只动这一条。</summary>
    public FlavorSet MoveFrequency(int index, double frequencyHz)
    {
        if (index < 0 || index >= Count || !double.IsFinite(frequencyHz))
        {
            return this;
        }

        var definition = ToneBands.All[index];
        var band = _bands[index];
        var hz = Math.Clamp(frequencyHz, definition.LowHz, definition.HighHz);
        if (hz.Equals(band.FrequencyHz))
        {
            return this;
        }

        return Replace(index, band with { FrequencyHz = hz });
    }

    /// <summary>改 Q（夹在界面允许的范围里），只动这一条。</summary>
    public FlavorSet SetQuality(int index, double quality)
    {
        if (index < 0 || index >= Count || !double.IsFinite(quality))
        {
            return this;
        }

        var band = _bands[index];
        var q = Math.Clamp(quality, EqLimits.MinQuality, EqLimits.MaxQuality);
        if (q.Equals(band.Quality))
        {
            return this;
        }

        return Replace(index, band with { Quality = q });
    }

    /// <summary>
    /// 真正要写进 peace.txt 的那几条：0 dB 的不写。
    /// Id 用“下标 + 1”，只在合并时临时用一下，落盘不认它。
    /// </summary>
    public IReadOnlyList<EqPoint> Contributors
    {
        get
        {
            var result = new List<EqPoint>(Count);
            for (var i = 0; i < Count; i++)
            {
                var band = _bands[i];
                if (Math.Abs(band.GainDb) < SilentGainDb)
                {
                    continue;
                }

                result.Add(new EqPoint(i + 1, band.FrequencyHz, band.GainDb, band.Quality, false, band.FilterType));
            }

            return result;
        }
    }

    /// <summary>画风味曲线用的快照：九条全给，0 dB 的段在仿真里本来就不参与。</summary>
    public PeaceSnapshot ToSnapshot()
    {
        var bands = new SliderBand[Count];
        for (var i = 0; i < Count; i++)
        {
            var band = _bands[i];
            bands[i] = new SliderBand(i, band.FrequencyHz, band.GainDb, band.Quality, band.FilterType, true);
        }

        return new PeaceSnapshot(bands, 0, Count);
    }

    public bool Equals(FlavorSet? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || other._bands.Length != _bands.Length)
        {
            return false;
        }

        for (var i = 0; i < _bands.Length; i++)
        {
            if (!_bands[i].Equals(other._bands[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is FlavorSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var band in _bands)
        {
            hash.Add(band);
        }

        return hash.ToHashCode();
    }

    public override string ToString() =>
        string.Join(
            " · ",
            _bands.Where(band => Math.Abs(band.GainDb) >= SilentGainDb)
                .Select(band => $"{band.Key} {band.FrequencyHz:0} Hz {band.GainDb:+0.0;-0.0;0.0} dB"));

    private FlavorSet Replace(int index, FlavorBand band)
    {
        var copy = (FlavorBand[])_bands.Clone();
        copy[index] = band;
        return new FlavorSet(copy);
    }

    private static FlavorBand NewBand(ToneBand definition) =>
        new(definition.Key, definition.DefaultFrequencyHz, 0, definition.DefaultQuality, definition.FilterType);

    private static FlavorBand Sanitize(ToneBand definition, FlavorBand band) =>
        new(
            definition.Key,
            double.IsFinite(band.FrequencyHz)
                ? Math.Clamp(band.FrequencyHz, definition.LowHz, definition.HighHz)
                : definition.DefaultFrequencyHz,
            double.IsFinite(band.GainDb)
                ? Math.Clamp(band.GainDb, EqLimits.MinGainDb, EqLimits.MaxGainDb)
                : 0,
            double.IsFinite(band.Quality)
                ? Math.Clamp(band.Quality, EqLimits.MinQuality, EqLimits.MaxQuality)
                : definition.DefaultQuality,
            EqLimits.ClampFilter(band.FilterType));
}
