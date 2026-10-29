namespace PeaceWpf.Core.AutoFlavor;

using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Model;

/// <summary>
/// 把录到的帧算成九条风味的频率、增益、Q。
///
/// 整个估计是一个纯函数：同一批帧，换方向或换强度就重算，不改数据。
/// 所以录制中能实时画、暂停后能拖滑杆、应用前能反复对比。
///
/// 步骤（详见 docs/自动风味-评估.md 第 6 节）：
/// ① 安静门（帧级百分位）② 逐带瞬态判定（按频率分档 + 重复出现豁免）
/// ③ 逐带中位数定位 ④ 两遍逐带剔除离群（要改的带少剔）⑤ 责任区间内拟合峰位与半高宽。
/// </summary>
public static class AutoFlavorAnalyzer
{
    /// <summary>少于这么多帧就不出建议——再少就是噪声。</summary>
    public const int MinFrames = 30;

    /// <summary>单次分析最多看这么多帧（约 20 分钟）。再长的会话只看最近的。</summary>
    public const int MaxAnalyzeFrames = 12000;

    /// <summary>瞬态判定：高出本带中位数多少算「冒头」。</summary>
    public const double TransientRiseDb = 12;

    /// <summary>同一形状出现这么多次就当内容保留，不判爆音。</summary>
    public const int RepeatThreshold = 3;

    /// <summary>峰值不到这个数就不动这一条。</summary>
    public const double MinPeakDb = 1.5;

    /// <summary>
    /// 人声方向固定补多少 dB：叠在预设目标的主带上，不再乘任何检测置信度。
    /// 依据见 docs/自动风味-评估.md 6.8——人声检测在歌曲混音上不可靠，不能拿它当开关。
    /// </summary>
    public const double VoiceBoostDb = 2.5;

    /// <summary>全局提升上限：爆炸 +6、余震 +6、齿音 +3、尖啸 +4、其余 +9。</summary>
    private static readonly double[] GlobalMaxGain = { 6, 6, 9, 9, 9, 9, 3, 9, 4 };

    /// <summary>全局衰减上限。</summary>
    private const double GlobalMinGain = -12;

    /// <summary>中间六条的 Q 范围；两条架式另算。</summary>
    private const double PeakQualityMin = 0.5;
    private const double PeakQualityMax = 4.0;
    private const double ShelfQualityMin = 0.5;
    private const double ShelfQualityMax = 1.2;

    /// <summary>
    /// 识别期间写进 peace.txt 的那份合并点列表，在 120 条带中心上的总增益。
    /// 回环抓到的是已过 EQ 的声音，不扣掉它就等于把用户自己调过的曲线当成内容。
    /// </summary>
    public static double[] EqGainByBand(EqPointList? merged)
    {
        var gains = new double[AutoFlavorRecorder.BandCount];
        if (merged is null || merged.Points.Count == 0)
        {
            return gains;
        }

        for (var i = 0; i < gains.Length; i++)
        {
            gains[i] = EqResponse.OthersGainAt(merged, AutoFlavorRecorder.BandCenter(i));
        }

        return gains;
    }

    /// <summary>
    /// 从录音器算一份建议。没有足够数据时返回 <see cref="AutoFlavorSuggestion.None"/>。
    /// </summary>
    public static AutoFlavorSuggestion Estimate(
        AutoFlavorRecorder recorder,
        AutoFlavorDirection direction,
        double strength,
        double[]? eqGainByBand = null)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        var total = recorder.FrameCount;
        if (total < MinFrames)
        {
            return AutoFlavorSuggestion.None($"只有 {total} 帧，至少要 {MinFrames} 帧", total);
        }

        var start = Math.Max(0, total - MaxAnalyzeFrames);
        var n = total - start;
        var profile = AutoFlavorStrength.From(strength);
        var info = AutoFlavorDirections.Get(direction);
        var eqGain = eqGainByBand ?? new double[AutoFlavorRecorder.BandCount];

        // ① 每帧的形状：扣掉当前 EQ，再按中频三点归零，得到「形状」而不是「能量」。
        var shape = new float[n * AutoFlavorRecorder.BandCount];
        var peaks = new double[n];
        var valid = new bool[n];
        var midReference = MidReferenceBands();

        for (var i = 0; i < n; i++)
        {
            var frame = start + i;
            peaks[i] = recorder.PeakAt(frame);

            var mid = 0.0;
            for (var k = 0; k < midReference.Length; k++)
            {
                var band = midReference[k];
                mid += recorder.BandAt(frame, band) - eqGain[band];
            }

            mid /= midReference.Length;

            for (var b = 0; b < AutoFlavorRecorder.BandCount; b++)
            {
                shape[(i * AutoFlavorRecorder.BandCount) + b] =
                    (float)((recorder.BandAt(frame, b) - eqGain[b]) - mid);
            }
        }

        // ② 安静门：帧级，不逐带——逐带丢安静值会让各带均值被抬高得不一样，形状会变形。
        var quiet = Percentile(peaks, null, n, profile.QuietPercent, AutoFlavorRecorder.FloorDb, 0, 0.5);
        var validCount = 0;
        for (var i = 0; i < n; i++)
        {
            valid[i] = peaks[i] >= quiet && peaks[i] < AutoFlavorRecorder.ClipPeakDb;
            if (valid[i])
            {
                validCount++;
            }
        }

        if (validCount < MinFrames)
        {
            return AutoFlavorSuggestion.None("有效声音太少（安静门和削波门之后不足 30 帧）", total);
        }

        // ③ 逐带中位数：只用来定位，不当结果（逐带中位数拼起来可能不是任何一帧真实存在的形状）。
        var median = new double[AutoFlavorRecorder.BandCount];
        var excluded = new bool[n * AutoFlavorRecorder.BandCount];
        var kept = new bool[n * AutoFlavorRecorder.BandCount];
        var scratch = new double[n];
        var distances = new double[n];

        for (var b = 0; b < AutoFlavorRecorder.BandCount; b++)
        {
            median[b] = BandMedian(shape, valid, n, b, scratch);
        }

        // ④ 逐带瞬态：只丢这条带这一段，不丢整帧。短于 T(f) 才算爆音；
        //    形状相近的事件出现 ≥3 次就当内容（枪声再短促也会连发）。
        MarkTransients(shape, valid, n, median, profile, excluded, kept);

        // ⑤ 两遍逐带剔除：第一遍用基础比例定位，第二遍按「这条带要多改」调整剔除量。
        var coarse = new double[AutoFlavorRecorder.BandCount];
        var final = new double[AutoFlavorRecorder.BandCount];
        for (var b = 0; b < AutoFlavorRecorder.BandCount; b++)
        {
            coarse[b] = BandMeanAfterReject(shape, valid, excluded, kept, n, b, median[b], profile.RejectPercent, scratch, distances);
        }

        var targetOffset = MidTargetOffset(direction);
        var voiceCompensation = VoiceCompensationFor(recorder, info, valid, start, n);
        var delta = new double[AutoFlavorRecorder.BandCount];
        var maxDelta = 1e-9;
        for (var b = 0; b < AutoFlavorRecorder.BandCount; b++)
        {
            // 人声强化叠在目标上：预设目标 + 补偿，再和实测比。
            var target = AutoFlavorDirections.TargetAt(direction, AutoFlavorRecorder.BandCenter(b))
                - targetOffset
                + (voiceCompensation.WeightAt(b) * voiceCompensation.BoostDb);
            delta[b] = target - coarse[b];
            maxDelta = Math.Max(maxDelta, Math.Abs(delta[b]));
        }

        for (var b = 0; b < AutoFlavorRecorder.BandCount; b++)
        {
            var importance = Math.Abs(delta[b]) / maxDelta;
            var reject = profile.RejectFor(importance);
            final[b] = BandMeanAfterReject(shape, valid, excluded, kept, n, b, median[b], reject, scratch, distances);
        }

        // ⑥ 拟合九条：各自的责任区间里找峰位和半高宽。
        var result = FlavorSet.CreateDefault();
        var used = 0;
        for (var i = 0; i < FlavorSet.Count; i++)
        {
            used += FitBand(info, direction, strength, profile, final, result, i, voiceCompensation, out var updated);
            result = updated;
        }

        if (used == 0)
        {
            return AutoFlavorSuggestion.None("内容和目标已经很接近，不用改", total);
        }

        // 相邻两条的中心离得太近（不到 1/3 倍频程）时只留更符合的那条：
        // 两条峰叠在一处，不但各自的上限要分摊，合成的形状也没法解释。
        result = SeparatePeaks(result);

        // 分开之后，残余的带宽交叠仍可能把合成峰值推过上限；这一步是兜底，正常很少触发。
        result = LimitOverlap(result);

        return new AutoFlavorSuggestion(
            result,
            validCount,
            total,
            true,
            null,
            voiceCompensation.Ratio,
            voiceCompensation.BoostDb);
    }

    /// <summary>
    /// 相邻两条峰位靠得太近时，只保留「更符合」的那条，另一条归零。
    ///
    /// 判定顺序：先看该改得多不多（增益绝对值），差得明显就留大的那条；
    /// 差不多时看谁离自己的默认中心更近——那条更能代表这一段，另一条多半是被相邻内容带偏了。
    /// 归零只动增益，频率和 Q 保留，用户后面手动拉起来还是原来的位置。
    /// </summary>
    public static FlavorSet SeparatePeaks(FlavorSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        for (var i = 0; i < FlavorSet.Count - 1; i++)
        {
            var lower = set[i];
            var upper = set[i + 1];
            if (Math.Abs(lower.GainDb) < 0.5 || Math.Abs(upper.GainDb) < 0.5)
            {
                continue;
            }

            if (Math.Log2(upper.FrequencyHz / lower.FrequencyHz) >= MinCenterSeparationOctaves)
            {
                continue;
            }

            var lowerGain = Math.Abs(lower.GainDb);
            var upperGain = Math.Abs(upper.GainDb);
            bool keepLower;
            if (Math.Abs(lowerGain - upperGain) >= TieBreakDb)
            {
                keepLower = lowerGain > upperGain;
            }
            else
            {
                var lowerOff = Math.Abs(Math.Log2(lower.FrequencyHz / ToneBands.All[i].DefaultFrequencyHz));
                var upperOff = Math.Abs(Math.Log2(upper.FrequencyHz / ToneBands.All[i + 1].DefaultFrequencyHz));
                keepLower = lowerOff <= upperOff;
            }

            set = keepLower
                ? set.WithGain(i + 1, 0)
                : set.WithGain(i, 0);
        }

        return set;
    }

    /// <summary>两条中心至少要隔开这么多倍频程，否则算「撞在同一个频率上」。</summary>
    private const double MinCenterSeparationOctaves = 0.35;

    /// <summary>增益差不到这个数（dB）时，改用「离自己默认中心多远」来定谁留下。</summary>
    private const double TieBreakDb = 0.5;

    /// <summary>
    /// 相邻两条同时提升时，检查它们叠加后的峰值有没有超过这两条的上限。
    /// 超了就按比例把两条一起收回来——单看每条都合规，叠起来才是问题。
    /// 削不受影响（不产生削波），所以只处理两条都为正的情况。
    /// </summary>
    public static FlavorSet LimitOverlap(FlavorSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        for (var i = 0; i < FlavorSet.Count - 1; i++)
        {
            var lower = set[i];
            var upper = set[i + 1];
            if (lower.GainDb <= 0.01 || upper.GainDb <= 0.01)
            {
                continue;
            }

            var low = Math.Max(ToneBands.All[i].LowHz, ToneBands.All[i + 1].LowHz);
            var high = Math.Min(ToneBands.All[i].HighHz, ToneBands.All[i + 1].HighHz);
            if (high <= low)
            {
                continue;
            }

            var cap = Math.Max(GlobalMaxGain[i], GlobalMaxGain[i + 1]);
            var peak = 0.0;
            var step = Math.Pow(2, 1.0 / 24.0);
            for (var hz = low; hz <= high; hz *= step)
            {
                peak = Math.Max(peak, BandGain(lower, hz) + BandGain(upper, hz));
            }

            if (peak <= cap + 0.1)
            {
                continue;
            }

            var scale = cap / peak;
            set = set
                .WithGain(i, lower.GainDb * scale)
                .WithGain(i + 1, upper.GainDb * scale);
        }

        return set;
    }

    /// <summary>单独一条风味在某频点的增益（dB）。</summary>
    private static double BandGain(FlavorBand band, double frequencyHz)
    {
        if (Math.Abs(band.GainDb) < 0.005)
        {
            return 0;
        }

        var snapshot = new PeaceSnapshot(
            new[] { new SliderBand(0, band.FrequencyHz, band.GainDb, band.Quality, band.FilterType, true) },
            0,
            1);
        return EqResponse.GainAt(snapshot, frequencyHz);
    }

    /// <summary>中频三点（男声 / 女声 / 脚步）的带号，用来做帧级归零的参考。</summary>
    private static int[] MidReferenceBands() => new[]
    {
        AutoFlavorRecorder.BandIndexFor(ToneBands.Find("voiceM")!.DefaultFrequencyHz),
        AutoFlavorRecorder.BandIndexFor(ToneBands.Find("voiceF")!.DefaultFrequencyHz),
        AutoFlavorRecorder.BandIndexFor(ToneBands.Find("step")!.DefaultFrequencyHz),
    };

    /// <summary>
    /// 目标表也要按同样的三点归零，否则「内容本来就平衡」会被算成要抬中频。
    /// </summary>
    private static double MidTargetOffset(AutoFlavorDirection direction) =>
        MidReferenceBands().Average(band =>
            AutoFlavorDirections.TargetAt(direction, AutoFlavorRecorder.BandCenter(band)));

    /// <summary>
    /// 人声强化补偿：往哪一条风味上叠、叠多少。
    ///
    /// 权重按倍频程铺开，不是只给一条带加满：主带覆盖范围里给 1，往外一个倍频程内线性收到 0。
    /// 只加一条带会让峰位被硬拽过去，铺开才是「这一段人声更突出」。
    /// </summary>
    private readonly record struct VoiceCompensation(IReadOnlyList<int> BandIndexes, double BoostDb, double Ratio)
    {
        public static VoiceCompensation None => new(Array.Empty<int>(), 0, 0);

        public bool IsActive => BandIndexes.Count > 0 && BoostDb > 1e-9;

        /// <summary>第 band 条带分到多少人声补偿（0–1）。男女一起强时取两条主带里更大的那份，不叠加。</summary>
        public double WeightAt(int band)
        {
            if (!IsActive)
            {
                return 0;
            }

            var hz = AutoFlavorRecorder.BandCenter(band);
            var weight = 0.0;
            foreach (var index in BandIndexes)
            {
                var main = ToneBands.All[index];
                double current;
                if (hz >= main.LowHz && hz <= main.HighHz)
                {
                    current = 1;
                }
                else
                {
                    // 出了覆盖范围就按倍频程往回退：差一个倍频程正好归零。
                    var edge = hz < main.LowHz ? main.LowHz : main.HighHz;
                    var octaves = Math.Abs(Math.Log2(hz / edge));
                    current = Math.Max(0, 1 - octaves);
                }

                weight = Math.Max(weight, current);
            }

            return weight;
        }
    }

    /// <summary>
    /// 决定这次要不要强化人声频段。
    ///
    /// 只看方向本身：男声/女声方向一律给满额补偿，不再等「真的测到人声」——
    /// 人声检测在歌曲混音上不可靠（见 docs/自动风味-评估.md 6.8 的实测），
    /// 拿它当开关会让「听歌选人声方向」这件事直接失效。
    /// 检测占比只留作显示，不参与增益计算。
    /// </summary>
    private static VoiceCompensation VoiceCompensationFor(
        AutoFlavorRecorder recorder,
        AutoFlavorDirectionInfo info,
        bool[] valid,
        int start,
        int n)
    {
        if (info.VoiceBias == AutoFlavorVoiceBias.None)
        {
            return VoiceCompensation.None;
        }

        // 占比只用于界面说明，不影响补偿量。
        var validCount = 0;
        var voiceCount = 0;
        for (var i = 0; i < n; i++)
        {
            if (!valid[i])
            {
                continue;
            }

            validCount++;
            if (recorder.VoiceAt(start + i))
            {
                voiceCount++;
            }
        }

        var ratio = validCount == 0 ? 0 : (double)voiceCount / validCount;
        var indexes = info.VoiceBias switch
        {
            AutoFlavorVoiceBias.Lower => new[] { ToneBands.IndexOf("voiceM") },
            AutoFlavorVoiceBias.Higher => new[] { ToneBands.IndexOf("voiceF") },
            AutoFlavorVoiceBias.Both => new[] { ToneBands.IndexOf("voiceM"), ToneBands.IndexOf("voiceF") },
            _ => Array.Empty<int>(),
        };

        var usable = indexes.Where(index => index >= 0).ToArray();
        if (usable.Length == 0)
        {
            return VoiceCompensation.None;
        }

        return new VoiceCompensation(usable, VoiceBoostDb, ratio);
    }

    private static void MarkTransients(
        float[] shape,
        bool[] valid,
        int n,
        double[] median,
        AutoFlavorStrength profile,
        bool[] excluded,
        bool[] kept)
    {
        var bandCount = AutoFlavorRecorder.BandCount;
        var runs = new List<(int Band, int Start, int Length, long Signature)>();

        for (var b = 0; b < bandCount; b++)
        {
            var limitMs = profile.TransientLimitMs(AutoFlavorRecorder.BandCenter(b));
            var limitFrames = (int)Math.Max(1, Math.Round(limitMs / AutoFlavorRecorder.FrameMs));
            var threshold = median[b] + TransientRiseDb;

            var runStart = -1;
            for (var i = 0; i <= n; i++)
            {
                var hot = i < n && valid[i] && shape[(i * bandCount) + b] > threshold;
                if (hot && runStart < 0)
                {
                    runStart = i;
                }
                else if (!hot && runStart >= 0)
                {
                    var length = i - runStart;
                    if (length <= limitFrames)
                    {
                        runs.Add((b, runStart, length, Signature(shape, runStart)));
                    }

                    runStart = -1;
                }
            }
        }

        if (runs.Count == 0)
        {
            return;
        }

        var counts = new Dictionary<long, int>();
        foreach (var run in runs)
        {
            counts[run.Signature] = counts.TryGetValue(run.Signature, out var c) ? c + 1 : 1;
        }

        foreach (var run in runs)
        {
            if (counts[run.Signature] >= RepeatThreshold)
            {
                // 反复出现的形状是内容（连发、点射、换弹），不判爆音；
                // 同时标记成「保留」，免得下一道离群剔除又把它当成离群值扔掉。
                for (var i = run.Start; i < run.Start + run.Length; i++)
                {
                    kept[(i * bandCount) + run.Band] = true;
                }

                continue;
            }

            for (var i = run.Start; i < run.Start + run.Length; i++)
            {
                excluded[(i * bandCount) + run.Band] = true;
            }
        }
    }

    /// <summary>事件指纹：九条风味中心处的形状，量化到 3 dB 一档。</summary>
    private static long Signature(float[] shape, int frame)
    {
        var hash = 17L;
        for (var k = 0; k < FlavorSet.Count; k++)
        {
            var band = AutoFlavorRecorder.BandIndexFor(ToneBands.All[k].DefaultFrequencyHz);
            var bucket = (int)Math.Round(shape[(frame * AutoFlavorRecorder.BandCount) + band] / 3.0);
            hash = (hash * 31) + Math.Clamp(bucket, -60, 60);
        }

        return hash;
    }

    private static double BandMedian(float[] shape, bool[] valid, int n, int band, double[] scratch)
    {
        var count = Collect(shape, valid, null, n, band, scratch);
        return count == 0 ? 0 : Percentile(scratch, null, count, 0.5, -90, 30, 0.5);
    }

    /// <summary>
    /// 按与中位数的偏离程度剔掉最远的一截，再对剩下的取均值。
    /// 「反复出现、判定为内容」的值不参与剔除，直接计入——否则瞬态那一步的豁免会被这里抹掉。
    /// 阈值只按普通帧算距离分布，免得被保留的事件把分位点抬高。
    /// </summary>
    private static double BandMeanAfterReject(
        float[] shape,
        bool[] valid,
        bool[] excluded,
        bool[] kept,
        int n,
        int band,
        double median,
        double rejectPercent,
        double[] scratch,
        double[] distances)
    {
        var bandCount = AutoFlavorRecorder.BandCount;
        var count = 0;
        var keptSum = 0.0;
        var keptCount = 0;

        for (var i = 0; i < n; i++)
        {
            if (!valid[i])
            {
                continue;
            }

            var index = (i * bandCount) + band;
            if (excluded[index])
            {
                continue;
            }

            if (kept[index])
            {
                keptSum += shape[index];
                keptCount++;
                continue;
            }

            scratch[count++] = shape[index];
        }

        if (count == 0)
        {
            return keptCount > 0 ? keptSum / keptCount : median;
        }

        if (rejectPercent <= 0)
        {
            var sumAll = keptSum;
            for (var i = 0; i < count; i++)
            {
                sumAll += scratch[i];
            }

            return sumAll / (count + keptCount);
        }

        for (var i = 0; i < count; i++)
        {
            distances[i] = Math.Abs(scratch[i] - median);
        }

        var limit = Percentile(distances, null, count, 1 - rejectPercent, 0, 60, 0.5);
        var sum = keptSum;
        var used = keptCount;
        for (var i = 0; i < count; i++)
        {
            if (distances[i] <= limit)
            {
                sum += scratch[i];
                used++;
            }
        }

        return used == 0 ? median : sum / used;
    }

    /// <summary>把某条带在所有有效帧上的值收进 scratch，返回个数。</summary>
    private static int Collect(float[] shape, bool[] valid, bool[]? excluded, int n, int band, double[] scratch)
    {
        var bandCount = AutoFlavorRecorder.BandCount;
        var count = 0;
        for (var i = 0; i < n; i++)
        {
            var index = (i * bandCount) + band;
            if (!valid[i] || (excluded is not null && excluded[index]))
            {
                continue;
            }

            scratch[count++] = shape[index];
        }

        return count;
    }

    /// <summary>
    /// 直方图分位数：值域固定、桶宽固定，O(值域) 就能取，不用为每条带排序。
    /// </summary>
    private static double Percentile(double[] values, bool[]? valid, int count, double percentile, double min, double max, double bucket)
    {
        if (count <= 0)
        {
            return min;
        }

        var clamped = Math.Clamp(percentile, 0, 1);
        var buckets = (int)Math.Ceiling((max - min) / bucket) + 1;
        var histogram = new int[buckets];
        var total = 0;

        for (var i = 0; i < count; i++)
        {
            if (valid is not null && !valid[i])
            {
                continue;
            }

            var index = (int)Math.Round((values[i] - min) / bucket);
            histogram[Math.Clamp(index, 0, buckets - 1)]++;
            total++;
        }

        if (total == 0)
        {
            return min;
        }

        var wanted = (int)Math.Ceiling(total * clamped);
        wanted = Math.Clamp(wanted, 1, total);

        var running = 0;
        for (var i = 0; i < buckets; i++)
        {
            running += histogram[i];
            if (running >= wanted)
            {
                return min + (i * bucket);
            }
        }

        return max;
    }

    /// <summary>
    /// 在一条风味自己的「责任区间」里拟合：峰位定频率、半高宽定 Q、峰值定增益。
    /// 责任区间是相邻中心的中点，再夹进这条自己的 Covers——否则相邻两条会把同一段缺陷算两遍。
    /// </summary>
    private static int FitBand(
        AutoFlavorDirectionInfo info,
        AutoFlavorDirection direction,
        double strength,
        AutoFlavorStrength profile,
        double[] shape,
        FlavorSet current,
        int index,
        VoiceCompensation voiceCompensation,
        out FlavorSet updated)
    {
        updated = current;
        var definition = ToneBands.All[index];

        var low = definition.LowHz;
        if (index > 0)
        {
            low = Math.Max(low, Math.Sqrt(ToneBands.All[index - 1].DefaultFrequencyHz * definition.DefaultFrequencyHz));
        }

        var high = definition.HighHz;
        if (index < FlavorSet.Count - 1)
        {
            high = Math.Min(high, Math.Sqrt(definition.DefaultFrequencyHz * ToneBands.All[index + 1].DefaultFrequencyHz));
        }

        if (high <= low)
        {
            high = definition.HighHz;
            low = definition.LowHz;
        }

        // 往内收一点：两条相邻风味的责任区间在同一个中点上交接，
        // 不缩的话两边的峰都可能正好落在那个中点上，变成「两条调同一个频率」。
        var span = Math.Log2(high / low);
        if (span > 0.4)
        {
            var inset = Math.Pow(2, Math.Min(span * 0.12, 0.25));
            low *= inset;
            high /= inset;
        }

        var from = AutoFlavorRecorder.BandIndexFor(low);
        var to = AutoFlavorRecorder.BandIndexFor(high);
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var targetOffset = MidTargetOffset(direction);
        var bestDelta = 0.0;
        var bestBand = from;

        for (var band = from; band <= to; band++)
        {
            var value = DeltaAt(direction, shape, band, targetOffset, voiceCompensation);
            if (Math.Abs(value) > Math.Abs(bestDelta))
            {
                bestDelta = value;
                bestBand = band;
            }
        }

        // 峰按绝对值找：内容偏多时要削，偏少时要抬，两边都得管。
        if (!double.IsFinite(bestDelta) || Math.Abs(bestDelta) < MinPeakDb)
        {
            return 0;
        }

        var peakHz = AutoFlavorRecorder.BandCenter(bestBand);
        var half = Math.Abs(bestDelta) - 3;

        var leftBand = bestBand;
        while (leftBand > from && Math.Abs(DeltaAt(direction, shape, leftBand - 1, targetOffset, voiceCompensation)) > half)
        {
            leftBand--;
        }

        var rightBand = bestBand;
        while (rightBand < to && Math.Abs(DeltaAt(direction, shape, rightBand + 1, targetOffset, voiceCompensation)) > half)
        {
            rightBand++;
        }

        double quality;
        if (rightBand > leftBand)
        {
            var bandwidth = AutoFlavorRecorder.BandCenter(rightBand) - AutoFlavorRecorder.BandCenter(leftBand);
            quality = bandwidth > 0 ? peakHz / bandwidth : definition.DefaultQuality;
        }
        else
        {
            quality = definition.DefaultQuality;
        }

        var isShelf = EqLimits.IsShelfFilter(definition.FilterType);
        quality = Math.Clamp(
            quality,
            isShelf ? ShelfQualityMin : PeakQualityMin,
            isShelf ? ShelfQualityMax : PeakQualityMax);

        var gain = Math.Clamp(
            bestDelta * profile.GainScale,
            Math.Max(info.MinGainDb[index], GlobalMinGain),
            Math.Min(info.MaxGainDb[index], GlobalMaxGain[index]));

        updated = current
            .MoveFrequency(index, peakHz)
            .SetQuality(index, quality)
            .WithGain(index, gain);

        return 1;
    }

    private static double DeltaAt(
        AutoFlavorDirection direction,
        double[] shape,
        int band,
        double targetOffset,
        VoiceCompensation voiceCompensation) =>
        AutoFlavorDirections.TargetAt(direction, AutoFlavorRecorder.BandCenter(band))
        - targetOffset
        + (voiceCompensation.WeightAt(band) * voiceCompensation.BoostDb)
        - shape[band];
}
