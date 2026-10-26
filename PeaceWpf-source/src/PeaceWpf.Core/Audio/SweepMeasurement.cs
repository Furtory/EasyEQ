namespace PeaceWpf.Core.Audio;

/// <summary>
/// 一个频点的测量结果。
/// </summary>
/// <param name="FrequencyHz">频率。</param>
/// <param name="LevelDb">以 1 kHz 为 0 dB 的相对电平。负数表示这个频段比 1 kHz 弱。</param>
/// <param name="SnrDb">这个点比噪声底高多少 dB。太低的值不可信，拟合时会跳过。</param>
/// <param name="RawDb">没有归一化过的绝对电平，只在排查设备问题时看。</param>
public sealed record MeasurementPoint(double FrequencyHz, double LevelDb, double SnrDb, double RawDb)
{
    /// <summary>信噪比低于这个值就当没测到，改用邻点插值。</summary>
    public const double MinimumSnrDb = 6;

    /// <summary>这个点能不能参与拟合。</summary>
    public bool IsUsable => double.IsFinite(LevelDb) && SnrDb >= MinimumSnrDb;
}

/// <summary>
/// 一次扫频测量的完整结果：每个频点的电平，加上噪声底和采样率，够诊断就行。
/// </summary>
public sealed class SweepMeasurement
{
    public SweepMeasurement(
        IReadOnlyList<MeasurementPoint> points,
        double noiseFloorDb,
        int sampleRate,
        IReadOnlyList<double> frequencies)
    {
        Points = points;
        NoiseFloorDb = noiseFloorDb;
        SampleRate = sampleRate;
        Frequencies = frequencies;
    }

    /// <summary>按频率升序的测量点。</summary>
    public IReadOnlyList<MeasurementPoint> Points { get; }

    /// <summary>测量期间的噪声底（dBFS）。</summary>
    public double NoiseFloorDb { get; }

    /// <summary>录音采样率。</summary>
    public int SampleRate { get; }

    /// <summary>这次扫了哪些频率。</summary>
    public IReadOnlyList<double> Frequencies { get; }

    /// <summary>可用点数，界面上用来判断这次测量靠不靠谱。</summary>
    public int UsableCount => Points.Count(point => point.IsUsable);
}
