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

    /// <summary>
    /// 纯度低于这个值就当这一窗被别的声音污染了。
    ///
    /// 纯度 = 目标频点功率 / 窗口总功率。干净的正弦接近 0 dB；混进音乐、说话、宽带噪声，
    /// 总功率涨而目标频点不涨，纯度掉下来。−9 dB 意味着目标频点只占窗内能量的八分之一，
    /// 已经很脏了。旧引擎给不出这个数（NaN），不能因此把旧结果判死。
    /// </summary>
    public const double MinimumPurityDb = -9;

    /// <summary>
    /// 纯度（dB）：这个窗口里，目标频点占多少能量。NaN 表示这条路径没算（旧引擎）。
    /// </summary>
    public double PurityDb { get; init; } = double.NaN;

    /// <summary>
    /// 局部对齐把窗口挪了多少毫秒。NaN 表示没对齐（旧引擎）。
    ///
    /// 正常情况下应该在 ±几毫秒以内；越往后越偏，就是播放和录音两个时钟在漂。
    /// </summary>
    public double AlignOffsetMs { get; init; } = double.NaN;

    /// <summary>这一窗是不是干净（没算纯度的旧结果一律放行）。</summary>
    public bool IsClean => double.IsNaN(PurityDb) || PurityDb >= MinimumPurityDb;

    /// <summary>这个点能不能参与拟合。</summary>
    public bool IsUsable => double.IsFinite(LevelDb) && SnrDb >= MinimumSnrDb && IsClean;
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

    /// <summary>
    /// 每个频点各自的噪声底（dBFS），和 <see cref="Points"/> 一一对应。
    ///
    /// 宽带噪声底是一把尺子量所有频点，低频的隆隆声会把 20–80 Hz 的门限抬死。
    /// 分带之后每个频点的 SNR 说的是「这一根谱线比它自己的底高多少」。
    /// null 表示旧引擎，只有宽带那一份。
    /// </summary>
    public IReadOnlyList<double>? NoiseFloorsDb { get; init; }

    /// <summary>算这份结果的引擎名字，界面上和诊断里显示。</summary>
    public string? EngineName { get; init; }

    /// <summary>可用点数，界面上用来判断这次测量靠不靠谱。</summary>
    public int UsableCount => Points.Count(point => point.IsUsable);

    /// <summary>被纯度判据挡掉的点数。旧引擎永远是 0。</summary>
    public int DirtyCount => Points.Count(point => !point.IsClean);
}
