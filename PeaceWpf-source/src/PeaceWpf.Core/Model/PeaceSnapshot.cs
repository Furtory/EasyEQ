namespace PeaceWpf.Core.Model;

/// <summary>一段推子的完整状态。</summary>
/// <param name="Index">段序号，0 开始。</param>
/// <param name="FrequencyHz">中心频率。</param>
/// <param name="GainDb">增益。</param>
/// <param name="Quality">Q 值。</param>
/// <param name="FilterType">滤波器类型序号，0 表示无。</param>
/// <param name="Enabled">该段是否参与运算。</param>
public sealed record SliderBand(
    int Index,
    double FrequencyHz,
    double GainDb,
    double Quality,
    int FilterType,
    bool Enabled)
{
    /// <summary>该段实际上是否在改变声音。</summary>
    public bool IsActive => Enabled && GainDb != 0;
}

/// <summary>一次从后端读回来的完整快照。</summary>
/// <param name="Bands">各段推子。</param>
/// <param name="PreAmpDb">当前声道组合的 preamp。</param>
/// <param name="BandCount">后端报告的推子数量。</param>
public sealed record PeaceSnapshot(
    IReadOnlyList<SliderBand> Bands,
    double PreAmpDb,
    int BandCount)
{
    public static PeaceSnapshot Empty { get; } = new(Array.Empty<SliderBand>(), 0, 0);
}
