namespace PeaceWpf.Core.Audio;

/// <summary>
/// 扫频测量的频率表和时间安排。
///
/// 用的不是连续扫频（chirp），而是「步进正弦」：一个频率只响一小段，然后静音一小段，
/// 再换下一个频率。理由很实际——连续扫频要靠反卷积或严格的时间对齐才能把每个频率分开，
/// 而这里从头到尾只有一个麦克风和一块声卡，播放与录音各走各的时钟，
/// 中间还隔着 EqualizerAPO 和 WASAPI 缓冲，对齐误差能到几十毫秒。
/// 步进正弦每个频率之间有一段静音，靠「能量有没有起来」就能把段切开，
/// 播放与录音的延迟、缓冲深度都不影响结果。
///
/// 间隔取 1/6 倍频程：20 Hz 到 20 kHz 约 10 个倍频程，正好 60 个频点。
/// 三分之一倍频程平滑之后，相邻两点已经分不开，再密就只是把测量时间拉长。
/// </summary>
public static class SweepPlan
{
    /// <summary>扫频的最低频率。</summary>
    public const double StartHz = 20;

    /// <summary>扫频的最高频率。超过 16 kHz 的部分只作参考——多数麦克风在那里已经开始掉。</summary>
    public const double EndHz = 20000;

    /// <summary>相邻频点的间隔，单位倍频程。</summary>
    public const double OctavesPerStep = 1.0 / 6.0;

    /// <summary>播放用的采样率。共享模式下 WASAPI 会自己重采样到设备实际格式。</summary>
    public const int PlaybackSampleRate = 48000;

    /// <summary>
    /// 扫频前面的同步音时长（毫秒）。
    ///
    /// 它比扫频本身响得多（1 kHz、接近满幅），任何麦克风和喇叭都能收到。
    /// 分析时先找到它，就知道第一条频点该从哪一秒开始——不用再靠「哪一段能量起来了」去猜。
    /// 20–56 Hz 这类音箱放不出、麦克风也收不好的频点，以前会让整条扫频错位。
    /// </summary>
    public const int SyncMs = 400;

    /// <summary>同步音结束到第一个频点之间的静音（毫秒），用来把两者分开。</summary>
    public const int SyncGapMs = 200;

    /// <summary>同步音的频率。</summary>
    public const double SyncHz = 1000;

    /// <summary>每个频点响多久（毫秒）。低频要留住几个完整周期，20 Hz 在 260 ms 里也有 5 个周期。</summary>
    public const int ToneMs = 260;

    /// <summary>每个频点之间的静音（毫秒）。它既是分段标记，也给播放缓冲一点余量。</summary>
    public const int GapMs = 120;

    /// <summary>找上升沿时用的分析帧长度（毫秒）。</summary>
    public const int GateFrameMs = 20;

    /// <summary>
    /// 测量窗相对上升沿的位置：跳过前 80 ms（淡入、系统延迟、声卡滤波器的建立时间），
    /// 到 240 ms 为止；这段刚好落在 260 ms 的响声里。
    /// </summary>
    public const int MeasureSkipMs = 80;

    /// <summary>测量窗的结束位置（毫秒），从上升沿算起。</summary>
    public const int MeasureEndMs = 240;

    /// <summary>判定「响起来了」的阈值，比噪声底高多少 dB。</summary>
    public const double GateThresholdDb = 12;

    /// <summary>一个频点占用多长时间（毫秒）：响声 + 静音。</summary>
    public static int BlockMs => ToneMs + GapMs;

    /// <summary>频点表，从低到高，对数等分：每 1/6 倍频程一个点，共 61 个。</summary>
    public static IReadOnlyList<double> Frequencies { get; } = BuildFrequencies(OctavesPerStep);

    /// <summary>
    /// 「快速」档：每 1/6 倍频程一个点，61 个，单遍约 25 秒。
    ///
    /// 和默认表同一密度，自动EQ 只扫一遍就出结果；整档控制在两分钟以内。
    /// 它是「先看个大形状」用的：点少不等于测得少，配合多轮反馈（每轮换 EQ 再测）已经够稳。
    /// 真正会重复同一套的只有「继续测量」，而那个固定只扫 1 遍。
    /// </summary>
    public const double FastOctavesPerStep = 1.0 / 6.0;

    /// <summary>
    /// 「标准」档：每 1/12 倍频程一个点，121 个，单遍约 48 秒，自动EQ 扫两遍。
    ///
    /// 点与点之间如果有窄而深的坑，61 个点会直接跨过去看不见，121 个点能看见一半。
    /// 两遍取中位数（先按 MAD 剔离群），整档约两分钟。
    /// </summary>
    public const double StandardOctavesPerStep = 1.0 / 12.0;

    /// <summary>
    /// 「更准」档：每 1/24 倍频程一个点，240 个，单遍约 93 秒，自动EQ 扫三遍。
    ///
    /// 这是给「补完还不满意、想再抠一遍」用的：格子细到四分之一倍频程以下，
    /// 窄谐振和相位坑都跑不掉。整档约五分钟。
    /// </summary>
    public const double PreciseOctavesPerStep = 1.0 / 24.0;

    /// <summary>三档测量频点表，界面和自测共用。</summary>
    public static double[] BuildFastFrequencies() => BuildFrequencies(FastOctavesPerStep);

    public static double[] BuildStandardFrequencies() => BuildFrequencies(StandardOctavesPerStep);

    public static double[] BuildPreciseFrequencies() => BuildFrequencies(PreciseOctavesPerStep);

    /// <summary>按倍频程间隔生成频点表。</summary>
    public static double[] BuildFrequencies(double octavesPerStep)
    {
        var octaves = Math.Log(EndHz / StartHz, 2);
        var steps = (int)Math.Round(octaves / octavesPerStep);

        var result = new double[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var hz = StartHz * Math.Pow(2, i * octavesPerStep);
            result[i] = Math.Min(hz, EndHz);
        }

        return result;
    }

    /// <summary>整条扫频要播多久（毫秒）：同步音 + 间隔 + 所有频点。</summary>
    public static int TotalMs => TotalMsFor(Frequencies.Count);

    /// <summary>按频点个数算总时长。频点表可以换，时长得跟着换。</summary>
    public static int TotalMsFor(int frequencyCount) =>
        SyncMs + SyncGapMs + (BlockMs * Math.Max(0, frequencyCount));

    /// <summary>
    /// 按上一轮实测曲线的陡缓重新排频点：落差大的地方补一刀，平的地方不动。
    ///
    /// 只看得到上一轮测过的东西，所以第一次永远用均匀表；
    /// 从第二次起，上一轮圈出来的坑和峰才有人去看。
    /// </summary>
    /// <param name="measured">上一轮的实测点。</param>
    /// <param name="maxExtra">最多补几个点。补出来的点是原表两点的几何中点。</param>
    /// <param name="baseFrequencies">
    /// 这一轮用的基础频点表。传 null 用默认的 61 点表。
    ///
    /// 必须传当轮实际用的表：标准档已经是 1/12 倍频程的 121 点，
    /// 再拿 61 点表当底，等于把上一轮辛苦测到的一半频点丢掉。
    /// </param>
    public static double[] BuildAdaptiveFrequencies(
        IReadOnlyList<MeasurementPoint> measured,
        int maxExtra,
        IReadOnlyList<double>? baseFrequencies = null)
    {
        var result = new List<double>(baseFrequencies ?? Frequencies);
        if (maxExtra <= 0 || measured.Count < 8)
        {
            return result.ToArray();
        }

        var usable = measured
            .Where(point => point.IsUsable)
            .OrderBy(point => point.FrequencyHz)
            .ToList();

        if (usable.Count < 8)
        {
            return result.ToArray();
        }

        // 相邻两点的落差；落差越大，越值得在中间补一个点看个究竟。
        var steep = new List<(double Low, double High, double Steepness)>();
        for (var i = 0; i < usable.Count - 1; i++)
        {
            var steepness = Math.Abs(usable[i + 1].LevelDb - usable[i].LevelDb);
            if (steepness >= 1.0)
            {
                steep.Add((usable[i].FrequencyHz, usable[i + 1].FrequencyHz, steepness));
            }
        }

        foreach (var pair in steep.OrderByDescending(item => item.Steepness).Take(maxExtra))
        {
            var middle = Math.Sqrt(pair.Low * pair.High);
            if (middle > StartHz && middle < EndHz)
            {
                result.Add(middle);
            }
        }

        return result.Distinct().OrderBy(hz => hz).ToArray();
    }

    /// <summary>
    /// 把频率对到最近的频点下标；超出范围时夹到首尾。
    /// 拟合曲线和画图都要用它，避免两处各写一遍取整规则。
    /// </summary>
    public static int NearestIndex(double frequencyHz)
    {
        if (!double.IsFinite(frequencyHz) || frequencyHz <= StartHz)
        {
            return 0;
        }

        var ratio = Math.Log(frequencyHz / StartHz, 2) / OctavesPerStep;
        var index = (int)Math.Round(ratio);
        return Math.Clamp(index, 0, Frequencies.Count - 1);
    }
}
