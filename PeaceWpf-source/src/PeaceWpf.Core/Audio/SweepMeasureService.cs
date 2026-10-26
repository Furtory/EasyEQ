namespace PeaceWpf.Core.Audio;

using System.IO;
using System.Text;

/// <summary>一次扫频的进度。Fraction 从 0 到 1，LevelDb 是当前麦克风电平。</summary>
/// <param name="Fraction">整体进度。</param>
/// <param name="Stage">现在在干什么，直接显示给用户。</param>
/// <param name="LevelDb">麦克风当前电平（dBFS）；null 表示这一刻没有可显示的电平（还没送数据、或已经录完）。</param>
/// <param name="PartialPoints">
/// 已经测完的那几段。界面靠它边测边画曲线；`LevelDb` 是没归一化的原始 dBFS，
/// 画的时候自己按当前已测点的中位数归一就行。
/// </param>
public sealed record SweepProgress(
    double Fraction,
    string Stage,
    double? LevelDb,
    IReadOnlyList<MeasurementPoint>? PartialPoints = null,
    double? CurrentHz = null);

/// <summary>
/// 把「放扫频」和「录麦克风」串起来，最后交给 <see cref="SweepAnalyzer"/>。
///
/// 步骤是固定的：先开麦克风吃几百毫秒的本底噪声（分析时要用它算信噪比），
/// 然后开播放，等它自己走完，再多录半秒尾巴，最后一次性分析。
///
/// 这个过程不该在界面线程上跑；它靠 <see cref="Task.Delay"/> 等待，不占 CPU。
/// </summary>
public static class SweepMeasureService
{
    /// <summary>开播之前先录多久的环境声（毫秒）。</summary>
    private const int NoiseFloorMs = 500;

    /// <summary>播完再多录多久（毫秒），把混响尾巴和缓冲里剩下的声音收进来。</summary>
    private const int TailMs = 600;

    /// <summary>多久报告一次进度（毫秒）。</summary>
    private const int ProgressIntervalMs = 100;

    /// <summary>等播放结束的最长时间：总时长 + 这么多。</summary>
    private const int PlaybackTimeoutMs = 6000;

    /// <summary>整段录音的最大电平低于这个值就当作「没听到声音」。</summary>
    private const double AudibleThresholdDb = -60;

    /// <summary>
    /// 跑一次完整的测量。
    /// </summary>
    /// <param name="captureDeviceId">录音设备 Id；null 用系统默认。</param>
    /// <param name="progress">进度回调，可以为 null。</param>
    /// <param name="token">取消用。</param>
    /// <exception cref="InvalidOperationException">设备打不开，或者没听到扫频。</exception>
    /// <exception cref="OperationCanceledException">被取消。</exception>
    public static async Task<SweepMeasurement> RunAsync(
        string? captureDeviceId,
        IProgress<SweepProgress>? progress,
        CancellationToken token,
        IReadOnlyList<double>? frequencies = null,
        List<MeasurementPoint>? liveSink = null)
    {
        var plan = frequencies ?? SweepPlan.Frequencies;
        using var microphone = new MicrophoneCaptureService();
        using var player = new ToneSweepPlayer();

        progress?.Report(new SweepProgress(0, "正在打开麦克风…", null));

        if (!microphone.Start(captureDeviceId))
        {
            throw new InvalidOperationException(
                "麦克风打不开：" + (microphone.LastError ?? "系统里没有可用的录音设备。"));
        }

        var sampleRate = microphone.SampleRate;
        if (sampleRate <= 0)
        {
            throw new InvalidOperationException("麦克风没有给出采样率。");
        }

        // 让 WasapiCapture 真正开始送数据，同时把开设备时的杂音丢掉。
        await Task.Delay(NoiseFloorMs, token).ConfigureAwait(false);
        microphone.TakeSamples();

        progress?.Report(new SweepProgress(0, "开始扫频…", MeasureLevel(microphone)));

        if (!player.Start(plan))
        {
            throw new InvalidOperationException(
                "输出设备打不开：" + (player.LastError ?? "系统里没有可用的播放设备。"));
        }

        var totalMs = SweepPlan.TotalMsFor(plan.Count);
        var timeout = totalMs + PlaybackTimeoutMs;
        var waited = 0;

        // 实时曲线：从同步音结束的位置起，按固定节奏一段一段量，测到哪画到哪。
        // 这里用的是「标称」起点（同步音 + 间隔），没扣系统延迟——预览会差几十毫秒，
        // 但形状是对的；最终结果仍然由 Analyze 用同步音对齐后重算。
        var originSample = (int)((long)sampleRate * (SweepPlan.SyncMs + SweepPlan.SyncGapMs) / 1000);
        var blockSamples = Math.Max(1, (int)((long)sampleRate * SweepPlan.BlockMs / 1000));
        var measureSkip = sampleRate * SweepPlan.MeasureSkipMs / 1000;
        var measureLength = (sampleRate * SweepPlan.MeasureEndMs / 1000) - measureSkip;
        var live = liveSink ?? new List<MeasurementPoint>(plan.Count);
        var lastBlock = 0;

        while (player.IsPlaying && waited < timeout)
        {
            await Task.Delay(ProgressIntervalMs, token).ConfigureAwait(false);
            waited += ProgressIntervalMs;

            var fraction = Math.Clamp(player.ElapsedMs / totalMs, 0, 0.98);

            var recorded = microphone.SampleCount;
            var complete = Math.Min(plan.Count, (recorded - originSample) / blockSamples);
            if (complete > lastBlock)
            {
                for (var i = lastBlock; i < complete; i++)
                {
                    var start = originSample + (i * blockSamples) + measureSkip;

                    // SnrDb 给个够用的值：这是「已经录到的信号」，不是「信噪比算出来的」。
                    // 填 0 的话，Fit 会把它当成不可用点，整条实时拟合直接抛异常。
                    var raw = microphone.WindowRmsDb(start, measureLength);
                    var point = new MeasurementPoint(plan[i], raw, 12, raw);

                    // 跨轮共用同一份列表：这一轮扫到哪就覆盖到哪，没扫到的保持上一轮的值。
                    // 这样界面上的实测线不会在每轮开头缩回去重新长。
                    if (i < live.Count)
                    {
                        live[i] = point;
                    }
                    else
                    {
                        live.Add(point);
                    }
                }

                lastBlock = complete;
            }

            progress?.Report(new SweepProgress(
                fraction,
                DescribeStage(fraction, plan),
                MeasureLevel(microphone),
                live.Count > 0 ? live.ToArray() : null,
                // 游标位置按播放进度算：plete 用的是累计采样数，跨轮不清，一上来就可能顶到末尾。
                plan[Math.Clamp((int)(fraction * (plan.Count - 1)), 0, plan.Count - 1)]));
        }

        player.Stop();

        // 关掉声音之后再多收一点，混响和缓冲里的尾巴也算进去（分析时只取每段的前 240 ms）。
        await Task.Delay(TailMs, token).ConfigureAwait(false);
        microphone.Stop();

        var samples = microphone.TakeSamples();
        if (samples.Length == 0)
        {
            throw new InvalidOperationException("一个采样都没录到。检查麦克风是不是被别的程序独占了。");
        }

        progress?.Report(new SweepProgress(0.99, "正在分析…", null));

        var measurement = await Task.Run(
            () =>
            {
                var result = SweepAnalyzer.Analyze(samples, sampleRate, out var analyzeError, plan);
                DumpDiagnostics(samples, sampleRate, result, analyzeError);
                return result ?? throw new InvalidOperationException(analyzeError ?? "分析失败。");
            },
            token).ConfigureAwait(false);

        if (measurement.Points.All(point => point.RawDb < AudibleThresholdDb))
        {
            throw new InvalidOperationException(
                "麦克风没听到扫频：确认输出不是静音、音量不是 0，麦克风对着耳朵或音箱。");
        }

        progress?.Report(new SweepProgress(1, "完成", null));
        return measurement;
    }

    private static string DescribeStage(double fraction, IReadOnlyList<double> frequencies)
    {
        var index = (int)Math.Clamp(fraction * frequencies.Count, 0, frequencies.Count - 1);
        var hz = frequencies[index];
        var text = hz >= 1000 ? $"{hz / 1000:0.0} kHz" : $"{hz:0} Hz";
        return $"扫频中 {fraction * 100:0}% · {text}";
    }

    /// <summary>
    /// 按固定遍数测：扫满 <paramref name="maxRepeats"/> 遍，每点取中位数。
    ///
    /// 自动EQ 传 <paramref name="fixedRepeats"/> = true：快速 1 遍、标准 2 遍、更准 3 遍，
    /// 「继续测量」固定 1 遍。不再用「两次测稳就停」——那个判据只看测量重复性，
    /// 和贴不贴目标无关，自动停会让人以为还有轮次没跑。
    ///
    /// 判据不是「可用频点够不够」，而是「两次测出来是不是一样」——
    /// 用户要的是对目标曲线贴合得多准，不是测几次。数据在抖，再怎么拟合也只是把噪声拟合进去。
    ///
    /// 停止是软的：点停止只表示「跑完这一轮就别再开下一轮」，
    /// 当前这轮扫频会走完，不会把半截录音丢给分析器。
    /// </summary>
    /// <param name="captureDeviceId">录音设备 Id；null 用系统默认。</param>
    /// <param name="stabilityDb">两次测量之间允许的最大差（85 分位）。越小越挑。</param>
    /// <param name="maxRepeats">最多测几次，夹在 1..8；1 = 只扫一遍。</param>
    /// <param name="progress">进度回调，可以为 null。</param>
    /// <param name="token">硬取消（窗口关掉之类）。轮内也生效：取消会立刻停掉播放和录音。</param>
    /// <param name="shouldStop">软停止的可撤销信号：返回 true 就在本轮跑完后收手。可以在它变 true 之后再变回 false。</param>
    /// <param name="frequencies">要扫哪些频点；null 用默认的 61 点表。</param>
    /// <param name="fixedRepeats">
    /// true = 不管两遍像不像，都扫满 <paramref name="maxRepeats"/> 遍再合并。
    ///
    /// 自动EQ 现在用固定次数：快速 1、标准 2、更准 3，靠「继续测量」而不是靠自停来加精度。
    /// 稳定性判断只看「两次测出来是不是一样」，和「贴不贴目标」无关；自动停会让
    /// 用户以为还有更多轮，实际上一遍就收了。固定死更好预期。
    /// </param>
    public static async Task<SweepMeasurement> RunAdaptiveAsync(
        string? captureDeviceId,
        double stabilityDb,
        int maxRepeats,
        IProgress<SweepProgress>? progress,
        CancellationToken token,
        Func<bool>? shouldStop = null,
        IReadOnlyList<double>? frequencies = null,
        bool fixedRepeats = false)
    {
        const int minRepeats = 2;
        maxRepeats = Math.Clamp(maxRepeats, 1, 8);

        var results = new List<SweepMeasurement>(maxRepeats);

        // 整次测量共用一份实时点表：第二轮从这里继续覆盖，不重建。
        var live = new List<MeasurementPoint>();

        for (var round = 0; round < maxRepeats; round++)
        {
            // 软停止：这里才看用户的停止请求，轮内一概不取消。
            // 它可以在本轮跑完之前被撤销——用户再点一下按钮就行。
            if (token.IsCancellationRequested)
            {
                break;
            }

            if (results.Count > 0 && shouldStop?.Invoke() == true)
            {
                break;
            }

            if (round > 0)
            {
                // 让上一轮的混响和播放缓冲散掉。用 None：软停止不该把这一等也打断。
                // 硬取消（关窗口）例外：token 一取消，这一等也跟着结束。
                await Task.Delay(400, token).ConfigureAwait(false);
            }

            var index = round;
            var wrapped = progress is null
                ? null
                : new Progress<SweepProgress>(item => progress.Report(new SweepProgress(
                    (index + item.Fraction) / maxRepeats,
                    index == 0 ? item.Stage : $"第 {index + 1} 次 · {item.Stage}",
                    item.LevelDb,
                    item.PartialPoints,
                    item.CurrentHz)));

            // 这一轮也传 token：关窗口的硬取消不能等二十多秒的扫频走完。
            // 软停止仍然只走上面的 shouldStop，轮内不会被打断在半路上。
            results.Add(await RunAsync(captureDeviceId, wrapped, token, frequencies, live)
                .ConfigureAwait(false));

            if (!fixedRepeats && results.Count >= minRepeats && IsStable(results, stabilityDb))
            {
                break;
            }
        }

        if (results.Count == 0)
        {
            // 第一轮还没跑完就被硬取消：没有能合并的结果，别把空表喂给 Merge。
            throw new OperationCanceledException(token);
        }

        return Merge(results);
    }

    /// <summary>
    /// 最近两次测得一样吗。
    ///
    /// 用 85 分位而不是最大值：一两个被噪声碰到的点不该拖住整轮测量，
    /// 但如果一成半以上的频点都在抖，那就是真的没测稳。
    /// </summary>
    private static bool IsStable(IReadOnlyList<SweepMeasurement> results, double stabilityDb)
    {
        if (results.Count < 2)
        {
            return false;
        }

        var last = results[^1];
        var previous = results[^2];
        if (last.Points.Count != previous.Points.Count || last.Points.Count == 0)
        {
            return false;
        }

        var diffs = new double[last.Points.Count];
        for (var i = 0; i < diffs.Length; i++)
        {
            diffs[i] = Math.Abs(last.Points[i].LevelDb - previous.Points[i].LevelDb);
        }

        Array.Sort(diffs);
        var index = (int)(diffs.Length * 0.85);
        return diffs[Math.Min(index, diffs.Length - 1)] <= stabilityDb;
    }

    /// <summary>每个频点取各轮的中位数。偶数轮取中间偏后的那个，不做平均（平均会被一个坏值拖走）。</summary>
    private static SweepMeasurement Merge(IReadOnlyList<SweepMeasurement> results)
    {
        var first = results[0];
        var points = new List<MeasurementPoint>(first.Points.Count);

        for (var i = 0; i < first.Points.Count; i++)
        {
            var raws = new double[results.Count];
            var snrs = new double[results.Count];
            var levels = new double[results.Count];

            for (var r = 0; r < results.Count; r++)
            {
                var point = results[r].Points[i];
                raws[r] = point.RawDb;
                snrs[r] = point.SnrDb;
                levels[r] = point.LevelDb;
            }

            Array.Sort(raws);
            Array.Sort(snrs);
            Array.Sort(levels);
            var mid = results.Count / 2;

            points.Add(new MeasurementPoint(
                first.Points[i].FrequencyHz,
                levels[mid],
                snrs[mid],
                raws[mid]));
        }

        var noiseFloors = results.Select(result => result.NoiseFloorDb).OrderBy(value => value).ToArray();

        return new SweepMeasurement(
            points,
            noiseFloors[noiseFloors.Length / 2],
            first.SampleRate,
            first.Frequencies);
    }

    /// <summary>实时电平：只看最近一小段，不清空缓冲——缓冲里是等下要分析的数据。</summary>
    private static double? MeasureLevel(MicrophoneCaptureService microphone) =>
        microphone.RecentLevelDb();

    /// <summary>
    /// 把这次测量的原始数字写到 %LOCALAPPDATA%\PeaceWpf\sweep-last.txt。
    ///
    /// 界面只显示「可用频点 N/61」，看不出问题出在噪声底、段错位，还是某个频段真的没声音。
    /// 这里按固定 380 ms 网格把每个频点的「响声窗」和「间隙窗」都记下来，事后能直接对账。
    /// </summary>
    private static void DumpDiagnostics(float[] samples, int sampleRate, SweepMeasurement? measurement, string? error)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PeaceWpf");
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            var durationMs = samples.Length * 1000.0 / Math.Max(1, sampleRate);
            sb.AppendLine($"# 录音 {samples.Length} 样本 / {sampleRate} Hz / {durationMs:0} ms");
            sb.AppendLine($"# error={error ?? "-"}");
            if (measurement is not null)
            {
                sb.AppendLine($"# noiseFloor={measurement.NoiseFloorDb:0.0} dBFS  usable={measurement.UsableCount}/{measurement.Points.Count}");
            }

            // 每 20 ms 一帧的整段电平。这是最直接的证据：响声和静音有没有排开，一眼就能看出来。
            var frameSamples = Math.Max(1, sampleRate * SweepPlan.GateFrameMs / 1000);
            var frameCount = samples.Length / frameSamples;
            sb.AppendLine($"# frames={frameCount} frameMs={SweepPlan.GateFrameMs}");
            for (var f = 0; f < frameCount; f++)
            {
                var value = SweepAnalyzer.WindowDb(samples, f * frameSamples, (f + 1) * frameSamples);
                sb.Append($"{value:0} ");
                if ((f + 1) % 10 == 0)
                {
                    sb.AppendLine();
                }
            }
            sb.AppendLine();

            if (measurement is not null)
            {
                sb.AppendLine();
                sb.AppendLine("hz,raw_db,snr_db,level_db");
                foreach (var point in measurement.Points)
                {
                    sb.AppendLine($"{point.FrequencyHz:0.0},{point.RawDb:0.0},{point.SnrDb:0.0},{point.LevelDb:0.0}");
                }
            }

            File.WriteAllText(Path.Combine(dir, "sweep-last.txt"), sb.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 诊断写不出来不该把测量拖失败。
        }
    }
}
