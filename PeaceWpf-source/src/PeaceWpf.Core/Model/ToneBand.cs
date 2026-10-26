namespace PeaceWpf.Core.Model;

/// <summary>
/// 一条「风味」的静态定义：玩家听得出来、但说不出位置的那一类声音。
///
/// 这里只放表，不放当前值——当前值在 <see cref="FlavorSet"/> 里。
/// 滤波器类型用 <see cref="EqLimits.FilterTypeNames"/> 的序号：
/// 中间六条是峰值，最外侧两条（爆炸、空间/细节）是架式。
/// 架式的理由：往外已经没有东西可以“过渡”了，钟形在边界回落反而错。
/// </summary>
/// <param name="Key">稳定标识，落盘和配色都靠它；改名字不要改它。</param>
/// <param name="Label">下拉里显示的名字。</param>
/// <param name="Hint">这个类型“听到的是什么”，给 tooltip 用。</param>
/// <param name="DefaultFrequencyHz">默认中心频率（架式是拐点频率）。</param>
/// <param name="DefaultQuality">默认 Q。用户能用滚轮改，这里只是起点。</param>
/// <param name="FilterType">滤波器类型序号。</param>
/// <param name="LowHz">频率能拖到的最小值。</param>
/// <param name="HighHz">频率能拖到的最大值。</param>
public sealed record ToneBand(
    string Key,
    string Label,
    string Hint,
    double DefaultFrequencyHz,
    double DefaultQuality,
    int FilterType,
    double LowHz,
    double HighHz)
{
    /// <summary>这个频率落不落在本类型的覆盖范围里。</summary>
    public bool Covers(double frequencyHz) => frequencyHz >= LowHz && frequencyHz <= HighHz;
}

/// <summary>
/// 风味类型的表。顺序就是下拉顺序，也是 <see cref="FlavorSet"/> 里 Band 下标的顺序。
///
/// 频点是“听感起点值”，不是实验室测量结果：Q 用户可以自己改，
/// 所以这里不追求精确，追求的是六个字能让人对上耳朵里的东西。
/// </summary>
public static class ToneBands
{
    /// <summary>全部类型，按频率升序。</summary>
    public static IReadOnlyList<ToneBand> All { get; } = new[]
    {
        new ToneBand(
            "boom", "爆炸/载具", "爆炸、引擎、远处轰鸣；抬多了糊住脚步",
            80, 0.7, EqLimits.LowShelfFilter, 30, 150),
        new ToneBand(
            "voiceM", "男声", "队友语音、男解说；抬的是清晰度不是音高",
            700, 0.9, EqLimits.PeakingFilter, 300, 1200),
        new ToneBand(
            "voiceF", "女声", "女角色、女解说、播报；再往上就是齿音",
            1800, 0.9, EqLimits.PeakingFilter, 1000, 3500),
        new ToneBand(
            "step", "脚步", "走、跑、落地；定位线索那一下",
            2500, 1.0, EqLimits.PeakingFilter, 1500, 4000),
        new ToneBand(
            "gun", "枪声", "击发脆度、弹壳、远处枪响的尖",
            4000, 1.1, EqLimits.PeakingFilter, 2000, 7000),
        new ToneBand(
            "mech", "换弹/机械", "拉栓、换弹、开门、装备摩擦的金属声",
            5500, 1.3, EqLimits.PeakingFilter, 3000, 9000),
        new ToneBand(
            "sibil", "齿音", "枪声扎耳、女声 s/sh、金属嘶声",
            7000, 1.6, EqLimits.PeakingFilter, 5000, 10000),
        new ToneBand(
            "air", "空间/细节", "雨、远处空气感、脚步的空间尾音",
            12000, 0.7, EqLimits.HighShelfFilter, 10000, 16000),
    };

    /// <summary>默认选中的类型：脚步。</summary>
    public static ToneBand Default => All[3];

    /// <summary>按 Key 找类型，找不到返回 null。</summary>
    public static ToneBand? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        foreach (var band in All)
        {
            if (string.Equals(band.Key, key, StringComparison.Ordinal))
            {
                return band;
            }
        }

        return null;
    }

    /// <summary>按 Key 找下标，找不到返回 -1。</summary>
    public static int IndexOf(string? key)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i].Key, key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
