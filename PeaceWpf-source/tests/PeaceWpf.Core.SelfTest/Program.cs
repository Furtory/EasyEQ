using PeaceWpf.Core.Audio;
using PeaceWpf.Core.AutoEq;
using PeaceWpf.Core.Calibration;
using PeaceWpf.Core.Dsp;
using PeaceWpf.Core.Editing;
using PeaceWpf.Core.Integration;
using PeaceWpf.Core.Model;

var failures = new List<string>();
var passed = 0;

// 顺手留一个诊断入口：dotnet run --project tests\PeaceWpf.Core.SelfTest -- --diagnose <目录>
// 用来在真实机器上问「这个 config 目录到底卡在哪」，输出跟界面上那个窗口一致。
if (args.Length >= 2 && args[0] == "--diagnose")
{
    var diagnosis = ConfigPermission.Diagnose(args[1]);
    Console.WriteLine($"{diagnosis.Problem} | {diagnosis.Summary}");
    Console.WriteLine("下一步：" + diagnosis.Action);
    foreach (var line in diagnosis.Details)
    {
        Console.WriteLine("  · " + line);
    }

    // 诊断说写不了时，把最原始的那次异常也打出来——分因逻辑再细，也别盖住系统说的话。
    try
    {
        var probe = Path.Combine(args[1], ".probe-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        Console.WriteLine("原始写入：成功");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"原始写入：{ex.GetType().Name} · {ex.Message}");
    }

    return diagnosis.Writable ? 0 : 1;
}

void Check(string name, bool condition, string? detail = null)
{
    if (condition)
    {
        passed++;
        return;
    }

    failures.Add(detail is null ? name : $"{name} —— {detail}");
}

static SliderBand Band(int index, double hz, double gain, int filter = 0, bool enabled = true, double q = 1.41) =>
    new(index, hz, gain, q, filter, enabled);

// 预设读写测试要一个能写文件的目录。先试程序自己旁边（一定在可写的构建目录里），
// 再试 %TEMP%；两处都被拒才报错——不要因为临时目录被策略挡住就整个自测崩掉。
static string CreateScratchDirectory()
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "selftest-tmp-" + Guid.NewGuid().ToString("N")),
        Path.Combine(Path.GetTempPath(), "peacewpf-peace-" + Guid.NewGuid().ToString("N")),
    };

    var problems = new List<string>();
    foreach (var candidate in candidates)
    {
        try
        {
            Directory.CreateDirectory(candidate);
            return candidate;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add(ex.GetType().Name + ": " + candidate);
        }
    }

    throw new IOException("没有可写的目录，预设读写测试跑不了。 " + string.Join(" | ", problems));
}

// 清理临时目录，删不掉也不该让自测失败。
static void TryDelete(string directory)
{
    try
    {
        Directory.Delete(directory, true);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // 删不掉就留着。
    }
}

// ---- S0 滤波器表 ----
var expected = new[] { "PK", "LPQ", "HPQ", "BP", "LS", "HS", "NO", "AP", "LSC", "HSC", "BWLP", "BWHP", "LRLP", "LRHP", "LSCQ", "HSCQ", "LSQ", "HSQ" };
Check("S0 类型数量是 18", EqLimits.FilterTypeNames.Count == 18, EqLimits.FilterTypeNames.Count.ToString());
Check("S0 第 0 项是 PK", EqLimits.FilterTypeNames[0] == "PK");
Check("S0 顺序与 $FilterTypes 一致", EqLimits.FilterTypeNames.SequenceEqual(expected),
    string.Join(",", EqLimits.FilterTypeNames));
Check("S0 FilterIndex(PK)=0", EqLimits.FilterIndex("PK") == 0);
Check("S0 FilterIndex(HSQ)=17", EqLimits.FilterIndex("HSQ") == 17);
Check("S0 越界名字返回 ?", EqLimits.FilterName(99) == "?");

// ---- S1 点列表 ----
var empty = EqPointList.CreateDefault();
Check("S1 默认两个端点", empty.Count == 2 && empty.Points.All(p => p.IsEndpoint));
Check("S1 端点频率 20/20k", empty.Points[0].FrequencyHz == 20 && empty.Points[1].FrequencyHz == 20000);
Check("S1 默认增益 0", empty.Points.All(p => p.GainDb == 0));

Check("S1 建点成功", empty.TryAdd(1000, out var withPoint, out var created));
Check("S1 新点增益 0", created.GainDb == 0);
Check("S1 新点 Q 1.41", Math.Abs(created.Quality - 1.41) < 1e-9);
Check("S1 新点不是端点", !created.IsEndpoint);
Check("S1 列表 3 个点", withPoint.Count == 3);

Check("S1 同频率不能再建", !withPoint.TryAdd(1000, out _, out _));

var moved = withPoint.Move(created.Id, 5000, 6.5);
Check("S1 频点可移动", Math.Abs(moved.Points[1].FrequencyHz - 5000) < 1e-6, moved.Points[1].FrequencyHz.ToString());
Check("S1 增益可移动", Math.Abs(moved.Points[1].GainDb - 6.5) < 1e-9);

var clampedRight = withPoint.Move(created.Id, 30000, 0);
Check("S1 频率不超过右邻居", clampedRight.Points[1].FrequencyHz < 20000);

var endpointMoved = moved.Move(moved.Points[0].Id, 200, -3);
Check("S1 端点频率锁死", endpointMoved.Points[0].FrequencyHz == 20);
Check("S1 端点增益可动", Math.Abs(endpointMoved.Points[0].GainDb + 3) < 1e-9);

Check("S1 端点不能删", endpointMoved.Remove(endpointMoved.Points[0].Id) is null);
var afterRemove = moved.Remove(created.Id);
Check("S1 普通点能删", afterRemove is not null && afterRemove.Count == 2);
Check("S1 只剩两个点时不能再删", afterRemove!.Remove(afterRemove.Points[1].Id) is null);

var q = withPoint.SetQuality(created.Id, 99);
Check("S1 Q 上限 30", q.Points[1].Quality == 30);
var qLow = withPoint.SetQuality(created.Id, 0);
Check("S1 Q 下限 0.1", qLow.Points[1].Quality == 0.1);

// ---- S1 从后端段转点 ----
var bands = new[]
{
    Band(0, 20, 0),
    Band(1, 100, 0),
    Band(2, 1000, 4.5),
    Band(3, 1000, 2.0),
    Band(4, 5000, -3, EqLimits.FilterTypeNames.ToList().IndexOf("LPQ")),
    Band(5, 20000, 0, enabled: false),
};
var fromBands = EqPointList.FromBands(bands);
Check("S1 两个同频率的 PK 段都保留", fromBands.Count == 4, fromBands.ToString());
Check("S1 同频率被拉开成两个点", Math.Abs(fromBands.Points[1].FrequencyHz - 1000) < 1e-6
    && fromBands.Points[2].FrequencyHz > fromBands.Points[1].FrequencyHz,
    fromBands.ToString());
Check("S1 自动补端点", fromBands.Points[0].IsEndpoint && fromBands.Points[^1].IsEndpoint);
Check("S1 频率严格递增", fromBands.Points.Zip(fromBands.Points.Skip(1)).All(pair => pair.First.FrequencyHz < pair.Second.FrequencyHz));

var noGain = EqPointList.FromBands(new[] { Band(0, 1000, 0), Band(1, 2000, 0) });
Check("S1 全平预设回到两个端点", noGain.Count == 2);

var manyBands = Enumerable.Range(0, 40)
    .Select(i => Band(i, 25 + i * 200, (i % 5) + 1))
    .ToArray();
var manyPoints = EqPointList.FromBands(manyBands);
// 40 段 + 补上的两个端点 = 42；点模型不再截断。
Check("S1 点数不设上限", manyPoints.Count == 42, manyPoints.Count.ToString());

var withPreamp = fromBands.ToSnapshot(0, 13);
Check("S1 转快照补齐槽位", withPreamp.Bands.Count == 13);
Check("S1 多出来的槽位不发声", withPreamp.Bands.Skip(fromBands.Count).All(b => b.GainDb == 0 && !b.Enabled));

// ---- S3 撤回栈 ----
var stack = new UndoStack<EqPointList>(50);
var state = EqPointList.CreateDefault();
stack.Reset(state);
Check("S3 初始不能撤回", !stack.CanUndo && !stack.CanRedo);

for (var i = 0; i < 60; i++)
{
    // 点数有 31 的上限，所以用“改同一个点的增益”来造 60 个不同状态。
    state = state.Move(state.Points[1].Id, null, -10 + i * 0.1);
    stack.Push(state);
}

var undoCount = 0;
while (stack.Undo() is not null)
{
    undoCount++;
}

Check("S3 最多撤回 50 步", undoCount == 50, undoCount.ToString());
Check("S3 到顶后不能继续撤回", !stack.CanUndo);
Check("S3 能重做", stack.Redo() is not null);

var sameStack = new UndoStack<EqPointList>(50);
var seed = EqPointList.CreateDefault();
sameStack.Reset(seed);
Check("S3 相同状态不重复入栈", !sameStack.Push(seed));

// ---- S1/S3 草稿 ----
var draft = new EqDraft(EqPointList.CreateDefault());
Check("S1 草稿初始不脏", !draft.IsDirty && !draft.IsEditing);

draft.Begin();
Check("S1 进入编辑", draft.IsEditing && !draft.IsDirty);

draft.Current.TryAdd(1000, out var draftList, out var draftPoint);
draft.Preview(draftList);
Check("S1 预览后脏", draft.IsDirty);
Check("S1 预览不进撤回", !draft.CanUndo);

draft.Commit();
Check("S1 提交后可撤回", draft.CanUndo);

var movedDraft = draft.Current.Move(draftPoint.Id, 2000, 9);
draft.Preview(movedDraft);
draft.Commit();
Check("S1 两次改动两步撤回", draft.CanUndo);

draft.Undo();
Check("S1 撤回一步回到 1 kHz", Math.Abs(draft.Current.Points[1].FrequencyHz - 1000) < 1e-6);
draft.Redo();
Check("S1 重做回到 2 kHz", Math.Abs(draft.Current.Points[1].FrequencyHz - 2000) < 1e-6);

draft.Cancel();
Check("S1 取消后回到已保存", !draft.IsDirty && draft.Current.Count == 2);
Check("S1 取消后不能撤回", !draft.CanUndo);

var saved = EqPointList.CreateDefault();
draft.Begin();
draft.Current.TryAdd(300, out var keep, out var keepPoint);
draft.Preview(keep);
draft.Commit();
draft.AcceptSaved(draft.Current);
Check("S1 保存后脏标记清零", !draft.IsDirty);
Check("S1 保存后撤回清空", !draft.CanUndo);

draft.Begin();
draft.Current.TryAdd(700, out var editAgain, out _);
draft.Preview(editAgain);
Check("S1 编辑中外部刷新不覆盖草稿", !draft.RefreshSaved(saved));
draft.End();

// ---- 双层合并 ----
var bassLayer = EqPointList.CreateDefault();
Check("S1 低音层加点", bassLayer.TryAdd(60, out var bassWithPoint, out var bassPoint));
bassWithPoint = bassWithPoint.Move(bassPoint.Id, null, -6);
var twoLayers = new EqLayerSet(fromBands, bassWithPoint);

Check("S1 关掉低音层只合主层", twoLayers.TryMerge(false, out var mainOnly, out _)
    && mainOnly.Count == fromBands.Count);
Check("S1 打开低音层会多一个点", twoLayers.TryMerge(true, out var merged, out _)
    && merged.Count == fromBands.Count + 1);
Check("S1 低音端点并入主层端点，不另占位", merged.Points.Count(p => p.FrequencyHz <= 20.001) == 1);
Check("S1 合并后仍是升序", merged.Points.Zip(merged.Points.Skip(1)).All(pair => pair.First.FrequencyHz < pair.Second.FrequencyHz));

// 教程那条低音曲线的第一个点就是 20 Hz 的方形端点（+9 dB）。它必须跟着叠加，
// 否则方形控制点在合并结果里永远是 0 dB，听感上也少了这一截。
var bassFirstSquare = EqPointList.FromPoints(new[]
{
    new EqPoint(1, 20, 9, 0.8, true),
    new EqPoint(2, 40, 7.5, 0.9, false),
    new EqPoint(3, 20000, 0, 0.7, true),
});
var squareLayers = new EqLayerSet(EqPointList.CreateDefault(), bassFirstSquare);
Check("S1 方形端点增益会叠加", squareLayers.TryMerge(true, out var squareMerged, out _)
    && Math.Abs(squareMerged.Points[0].GainDb - 9) < 1e-6,
    squareLayers.TryMerge(true, out var squareCheck, out _) ? squareCheck.Points[0].GainDb.ToString("0.0") : "合并失败");
Check("S1 方形端点仍是端点、只占一个槽位", squareMerged.Points[0].IsEndpoint
    && squareMerged.Points.Count(p => p.FrequencyHz <= 20.001) == 1);
Check("S1 方形端点 Q 跟随增益更大的一层", Math.Abs(squareMerged.Points[0].Quality - 0.8) < 1e-6);
Check("S1 方形端点不额外增加点数", squareMerged.Count == 3, squareMerged.Count.ToString());
Check("S1 关掉低音时方形端点回到主层原值", squareLayers.TryMerge(false, out var squareOff, out _)
    && Math.Abs(squareOff.Points[0].GainDb) < 1e-6);
var squareOn20 = EqResponse.GainAt(squareMerged.ToSnapshot(), 20);
var squareOff20 = EqResponse.GainAt(squareOff.ToSnapshot(), 20);
Check("S1 方形端点叠加后 20 Hz 实际响应抬起来", squareOn20 > squareOff20 + 5,
    $"{squareOff20:0.0} → {squareOn20:0.0} dB");

var flat = Enumerable.Range(0, EqLimits.MaxBands - 2)
    .Select(i => Band(i, 30 + i * 500, 1))
    .ToArray();
var tooManyBass = EqPointList.CreateDefault();
foreach (var band in flat)
{
    if (!tooManyBass.TryAdd(band.FrequencyHz, out var grown, out var added))
    {
        continue;
    }

    tooManyBass = grown.Move(added.Id, null, 2);
}

var over = new EqLayerSet(fromBands, tooManyBass);
Check(
    "S1 合并超 31 段也能存",
    over.TryMerge(true, out var overMerged, out _) && overMerged.Count > EqLimits.PeaceDisplayBands,
    over.TryMerge(true, out var again, out _) ? again.Count.ToString() : "拒绝");

// ---- EqualizerAPO 配置读写 ----
var configText = EapoConfig.Build(
    new[] { new EqPoint(1, 1000, 3.5, 1.41, false), new EqPoint(2, 20000, -2, 2.0, true) },
    -0.3,
    0,
    "Device: all");
Check("S1 配置有设备行", configText.Contains("Device: all"));
Check("S1 配置有 preamp", configText.Contains("Preamp: -0.3 dB"));
Check("S1 配置有滤波器行", configText.Contains("Filter 1: ON PK Fc 1000 Hz Gain 3.5 dB Q 1.41"));
Check("S1 平衡居中不写声道", !configText.Contains("Channel: R") && !configText.Contains("Channel: L"));

var leftText = EapoConfig.Build(Array.Empty<EqPoint>(), 0, -0.5, "Device: all");
Check("S1 往左滑衰减右声道", leftText.Contains("Channel: R") && leftText.Contains("-6.0 dB"));
var rightText = EapoConfig.Build(Array.Empty<EqPoint>(), 0, 0.5, "Device: all");
Check("S1 往右滑衰减左声道", rightText.Contains("Channel: L") && rightText.Contains("-6.0 dB"));

var parsed = EapoConfig.Parse(configText);
// 解析出来的点会自动补上 20 Hz / 20 kHz 两个端点，所以是 2 + 1。
Check("S1 解析回同样的点数", parsed.Points.Count == 3, parsed.Points.Count.ToString());
Check("S1 解析回 preamp", Math.Abs(parsed.PreAmpDb + 0.3) < 1e-6);
Check("S1 解析回设备行", parsed.DeviceLine == "Device: all");
Check("S1 解析回增益", Math.Abs(parsed.Points.Nearest(1000)!.GainDb - 3.5) < 1e-6);

var peaceSample = "Device: all\nChannel: all\nPreamp: -0.3 dB\n"
    + "Filter 1: ON PK Fc 50 Hz Gain 4.1 dB Q 0.62\n"
    + "Filter 2: OFF PK Fc 100 Hz Gain 2.9 dB Q 1\n"
    + "Filter 3: ON LPQ Fc 500 Hz Gain 0 dB Q 0.7\n";
var sample = EapoConfig.Parse(peaceSample);
Check("S1 OFF 的段不算点", sample.Points.Count == 3, sample.Points.Count.ToString());
Check("S1 数出非 PK 段", sample.NonPeakingFilters == 1, sample.NonPeakingFilters.ToString());
Check("S1 频点按 22050 读进来也被收进范围", sample.Points.Points[^1].FrequencyHz <= 20000);

// ---- Peace 预设读写 ----
var peaceDir = CreateScratchDirectory();
try
{
    var peacePath = Path.Combine(peaceDir, "test.peace");
    var source = new[]
    {
        new EqPoint(1, 60, 6.5, 1.41, false),
        new EqPoint(2, 120, -3.2, 2.0, false),
        new EqPoint(3, 20000, 0, 1.41, true),
    };
    Check("S1 新建预设能写", PeaceConfig.Save(peacePath, source, out _));
    var loaded = PeaceConfig.Load(peacePath, out var nonPeaking);
    // 60 Hz 之前会补一个 20 Hz 端点，所以是 3 + 1。
    Check("S1 预设读回点数", loaded is not null && loaded.Count == 4, loaded?.Count.ToString() ?? "null");
    Check("S1 预设读回增益", loaded is not null && Math.Abs(loaded.Nearest(60)!.GainDb - 6.5) < 0.05);
    Check("S1 所有段都是 PK", nonPeaking == 0);

    var text = File.ReadAllText(peacePath);
    Check("S1 预设段顺序对", text.IndexOf("[Frequencies]") < text.IndexOf("[Gains]") && text.IndexOf("[Gains]") < text.IndexOf("[Qualities]"));

    File.WriteAllText(peacePath, "[General]\nDescription=keep me\n[Speakers]\nSpeakerId0=0\n");
    Check("S1 覆盖旧预设", PeaceConfig.Save(peacePath, source, out _));
    var mergedText = File.ReadAllText(peacePath);
    Check("S1 保留 General 段", mergedText.Contains("Description=keep me") && mergedText.Contains("[Speakers]"));
}
finally
{
    try
    {
        Directory.Delete(peaceDir, true);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // 删不掉就留着，不要让清理失败把自测搞崩。
    }
}

// ---- UiState ----
var ui = new UiState();
ui.SetLayers(new EqLayerSet(fromBands, bassWithPoint));
ui.Balance = -0.25;
var restored = ui.ToLayerSet();
Check("S1 界面状态能存两层", restored is not null && restored.Main.Count == fromBands.Count && restored.Bass.Count == bassWithPoint.Count);
Check("S1 界面状态能还原频率", restored is not null
    && Math.Abs(restored.Main.Points[1].FrequencyHz - fromBands.Points[1].FrequencyHz) < 1e-6);
Check("S1 界面状态能存平衡", Math.Abs(ui.Balance + 0.25) < 1e-9);
Check("S1 空状态还原为 null", new UiState().ToLayerSet() is null);

// ---- 预增益跟着「左右」预设走 ----
var preAmpState = new UiState();
Check("S1 没记过的预设没有预增益", !preAmpState.TryGetMainPreAmp("A.peace", out _));
preAmpState.SetMainPreAmp("A.peace", -6.5);
preAmpState.SetMainPreAmp("B.peace", 3);
Check("S1 预增益按预设分开记", preAmpState.TryGetMainPreAmp("A.peace", out var aPre) && aPre == -6.5,
    aPre.ToString("0.0"));
Check("S1 另一个预设互不影响", preAmpState.TryGetMainPreAmp("B.peace", out var bPre) && bPre == 3);
Check("S1 预设名不分大小写", preAmpState.TryGetMainPreAmp("a.PEACE", out var ciPre) && ciPre == -6.5);
preAmpState.SetMainPreAmp("A.PEACE", -1);
Check("S1 大小写不同不重复存", preAmpState.MainPreAmps.Count == 2, preAmpState.MainPreAmps.Count.ToString());
Check("S1 覆盖后取新值", preAmpState.TryGetMainPreAmp("a.peace", out var overPre) && overPre == -1);

var preAmpJson = System.Text.Json.JsonSerializer.Serialize(preAmpState);
var preAmpRound = System.Text.Json.JsonSerializer.Deserialize<UiState>(preAmpJson)!;
Check("S1 预增益能随界面状态落盘读回",
    preAmpRound.TryGetMainPreAmp("A.PEACE", out var jsonPre) && jsonPre == -1);

var preAmpCopy = new UiState();
preAmpCopy.CopyFrom(preAmpState);
Check("S1 导入备份会带上预增益", preAmpCopy.TryGetMainPreAmp("B.peace", out var copyPre) && copyPre == 3);

// ---- 频响 ----
var curve = EqResponse.Curve(fromBands.ToSnapshot(), 200);
Check("S1 曲线采样数对上", curve.Length == 200);
Check("S1 曲线都是有限数", curve.All(double.IsFinite));
Check("S1 4.5 dB 的点附近确实抬起来了", curve.Max() > 1.0, curve.Max().ToString("0.00"));

// ---- S5 频谱 ----
const int rate = 48000;

var fftImpulse = new System.Numerics.Complex[SpectrumAnalyzer.FftSize];
fftImpulse[0] = System.Numerics.Complex.One;
SpectrumAnalyzer.Fft(fftImpulse);
Check("S5 冲激的 FFT 全是 1", fftImpulse.All(value => Math.Abs(value.Magnitude - 1) < 1e-9));

var fftDc = new System.Numerics.Complex[SpectrumAnalyzer.FftSize];
Array.Fill(fftDc, System.Numerics.Complex.One);
SpectrumAnalyzer.Fft(fftDc);
Check("S5 直流的能量全在 0 号格", Math.Abs(fftDc[0].Real - SpectrumAnalyzer.FftSize) < 1e-6
    && fftDc.Skip(1).All(value => Math.Abs(value.Magnitude) < 1e-6));

var hann = SpectrumAnalyzer.BuildHannWindow(SpectrumAnalyzer.FftSize);
Check("S5 Hann 窗两端衰减到 0", Math.Abs(hann[0]) < 1e-12 && Math.Abs(hann[^1]) < 1e-4);
Check("S5 Hann 窗中点接近 1", Math.Abs(hann[SpectrumAnalyzer.FftSize / 2] - 1) < 1e-9);

Check("S5 频带数是 192", SpectrumAnalyzer.BandCount == 192, SpectrumAnalyzer.BandCount.ToString());
Check("S5 频带下边界是 20 Hz", Math.Abs(SpectrumAnalyzer.BandFrequency(0) - 20) < 1e-9,
    SpectrumAnalyzer.BandFrequency(0).ToString("0.###"));
Check("S5 频带上边界是 20 kHz",
    Math.Abs(SpectrumAnalyzer.BandFrequency(SpectrumAnalyzer.BandCount) - 20000) < 1e-6,
    SpectrumAnalyzer.BandFrequency(SpectrumAnalyzer.BandCount).ToString("0.###"));
Check("S5 频带严格递增", Enumerable.Range(0, SpectrumAnalyzer.BandCount)
    .All(index => SpectrumAnalyzer.BandFrequency(index) < SpectrumAnalyzer.BandFrequency(index + 1)));

var analyzer = new SpectrumAnalyzer(rate);
var tone = new float[SpectrumAnalyzer.FftSize * 2];
for (var i = 0; i < tone.Length; i++)
{
    tone[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / rate));
}

analyzer.Feed(tone);
var toneFrame = analyzer.Snapshot();
Check("S5 频谱长度对得上带子数", toneFrame.Length == SpectrumAnalyzer.BandCount);

var peakDb = toneFrame.Max();
var peakBand = Array.IndexOf(toneFrame, peakDb);
Check("S5 1 kHz 正弦的峰值落在 1 kHz 附近",
    SpectrumAnalyzer.BandFrequency(peakBand) is > 700 and < 1400,
    SpectrumAnalyzer.BandFrequency(peakBand).ToString("0"));
Check("S5 半幅正弦约 -4.5 dB", peakDb is > -9 and < -2, peakDb.ToString("0.0"));

var silent = new SpectrumAnalyzer(rate);
silent.Feed(new float[SpectrumAnalyzer.FftSize * 2]);
Check("S5 静音时整条都是底噪",
    silent.Snapshot().All(db => Math.Abs(db - SpectrumAnalyzer.FloorDb) < 1e-3));

var fresh = new SpectrumAnalyzer(rate);
Check("S5 一帧都没攒够时返回底噪",
    fresh.Snapshot().All(db => Math.Abs(db - SpectrumAnalyzer.FloorDb) < 1e-3));

analyzer.Feed(new float[SpectrumAnalyzer.FftSize]);
var decayedPeak = analyzer.Snapshot().Max();
Check("S5 静音后一帧掉 3 dB",
    Math.Abs(decayedPeak - (peakDb - SpectrumAnalyzer.ReleaseDbPerSnapshot)) < 0.01,
    $"{peakDb:0.00} → {decayedPeak:0.00}");

// ---- 入口配置（config.txt 的 Include：接上 EqualizerAPO 的唯一一道手续） ----
Check("S6 认得出已经挂上的 Include", ApoSetup.IsIncluded("Include: peace.txt", "peace.txt"));
Check("S6 注释掉的 Include 不算", !ApoSetup.IsIncluded("# Include: peace.txt", "peace.txt"));
Check("S6 挂的是别的文件就不算", !ApoSetup.IsIncluded("Include: other.txt", "peace.txt"));
Check("S6 大小写和子目录都认", ApoSetup.IsIncluded("include: sub\\PEACE.TXT", "peace.txt"));
Check("S6 空配置里没有 Include", !ApoSetup.IsIncluded(string.Empty, "peace.txt"));

var apoDir = CreateScratchDirectory();
var apoBackupDir = Path.Combine(apoDir, "backup");
var apoConfigPath = Path.Combine(apoDir, ApoSetup.ConfigFileName);
try
{
    const string original = "Preamp: -3 dB\n";
    File.WriteAllText(apoConfigPath, original);

    var added = ApoSetup.EnsureInclude(apoDir, "peace.txt", apoBackupDir);
    var afterAdd = File.ReadAllText(apoConfigPath);
    Check("S6 没挂上时会追加一行 Include",
        added.Changed && added.Error is null && afterAdd.Contains("Include: peace.txt"), afterAdd);
    Check("S6 追加时原有内容一字不动", afterAdd.StartsWith(original, StringComparison.Ordinal), afterAdd);
    Check("S6 改之前留了备份", added.BackupPath is not null && File.Exists(added.BackupPath));

    var second = ApoSetup.EnsureInclude(apoDir, "peace.txt", apoBackupDir);
    Check("S6 已经挂上就不再改", !second.Changed && second.Error is null);
    Check("S6 重复调用不会写第二行",
        File.ReadAllText(apoConfigPath)
            .Split('\n')
            .Count(line => line.Contains("Include: peace.txt", StringComparison.OrdinalIgnoreCase)) == 1);
}
finally
{
    try
    {
        Directory.Delete(apoDir, recursive: true);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // 删不掉就留着，不要让清理失败把自测搞崩。
    }
}

// ---- 配置目录权限诊断（S11：写不进去时先分因，别一上来就喊「用管理员运行」） ----
var permDir = CreateScratchDirectory();
var permBackupDir = Path.Combine(permDir, "backup");
try
{
    var peacePath = Path.Combine(permDir, EapoConfig.FileName);
    File.WriteAllText(peacePath, "Preamp: 0.0 dB\n");
    File.WriteAllText(Path.Combine(permDir, ApoSetup.ConfigFileName), "Include: peace.txt\n");

    var healthy = ConfigPermission.Diagnose(permDir);
    Check("S11 可写的目录诊断成 None", healthy.Problem == ConfigProblem.None, healthy.Problem.ToString());
    Check("S11 可写时建议是「什么都不用做」", healthy.Action.Contains("什么都不用做"), healthy.Action);

    File.SetAttributes(peacePath, FileAttributes.ReadOnly);
    var readOnly = ConfigPermission.Diagnose(permDir);
    Check("S11 只读位诊断成 ReadOnlyFile",
        readOnly.Problem == ConfigProblem.ReadOnlyFile, readOnly.Problem.ToString());

    var cleared = ConfigPermission.Repair(readOnly, permBackupDir);
    Check("S11 清只读位成功", cleared.Ok, cleared.Message);
    Check("S11 只读位真的没了", !new FileInfo(peacePath).IsReadOnly);
    Check("S11 修完复诊是 None", ConfigPermission.Diagnose(permDir).Problem == ConfigProblem.None);

    using (new FileStream(peacePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        var busy = ConfigPermission.Diagnose(permDir);
        Check("S11 被别的程序以写方式占着诊断成 FileLocked",
            busy.Problem == ConfigProblem.FileLocked, busy.Problem.ToString());

        var notFixed = ConfigPermission.Repair(busy, permBackupDir);
        Check("S11 还占着的时候不假装修好", !notFixed.Ok, notFixed.Message);
    }

    var afterRelease = ConfigPermission.Diagnose(permDir);
    Check("S11 对方放手后复诊是 None",
        afterRelease.Problem == ConfigProblem.None, afterRelease.Problem.ToString());

    Check("S11 目录不存在诊断成 DirectoryMissing",
        ConfigPermission.Diagnose(Path.Combine(permDir, "no-such-dir")).Problem == ConfigProblem.DirectoryMissing);
    Check("S11 空路径不抛异常",
        ConfigPermission.Diagnose(null).Problem == ConfigProblem.DirectoryMissing);
}
finally
{
    try
    {
        var leftover = Path.Combine(permDir, EapoConfig.FileName);
        if (File.Exists(leftover))
        {
            File.SetAttributes(leftover, FileAttributes.Normal);
        }

        Directory.Delete(permDir, recursive: true);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // 删不掉就留着，不要让清理失败把自测搞崩。
    }
}

// ---- AutoEQ 压缩格式 ----
Check("S7 参数 EQ 行解析出预增益",
    AutoEqParser.TryParseParametric("A-6M105G5.5Q0.7P180G-3.3Q0.57", out var parametric)
    && Math.Abs(parametric.PreAmpDb + 6) < 1e-9);
Check("S7 参数 EQ 行解析出两个滤波器", parametric.Filters.Count == 2, parametric.Filters.Count.ToString());
Check("S7 带 Q 的低架换成居中低架 LSC",
    parametric.Filters.Count > 0 && parametric.Filters[0].FilterType == EqLimits.LowShelfCenteredFilter);
Check("S7 架式频率 / 增益 / Q",
    parametric.Filters.Count > 0
    && Math.Abs(parametric.Filters[0].FrequencyHz - 105) < 1e-9
    && Math.Abs(parametric.Filters[0].GainDb - 5.5) < 1e-9
    && Math.Abs(parametric.Filters[0].Quality - 0.7) < 1e-9);
Check("S7 峰值滤波器是 PK",
    parametric.Filters.Count > 1 && parametric.Filters[1].FilterType == EqLimits.PeakingFilter);
Check("S7 不带 Q 的架子保持 LS",
    AutoEqParser.TryParseParametric("A0L80G4P1000G1Q1", out var noQ)
    && noQ.Filters.Count == 2
    && noQ.Filters[0].FilterType == EqLimits.LowShelfFilter
    && Math.Abs(noQ.Filters[0].Quality - EqLimits.DefaultQuality) < 1e-9);

Check("S7 固定频带解析出 10 段",
    AutoEqParser.TryParseParametric("A-6.7F6.3F2.8F-0.6F-3.4F-1.1F1.9F-3.7F4.4F1.6F-1.6", out var fixedBand)
    && fixedBand.Filters.Count == AutoEqParser.FixedFrequencies.Count,
    fixedBand.Filters.Count.ToString());
Check("S7 固定频带第一段是 31 Hz Q1.41",
    fixedBand.Filters.Count > 0
    && Math.Abs(fixedBand.Filters[0].FrequencyHz - 31) < 1e-9
    && Math.Abs(fixedBand.Filters[0].Quality - AutoEqParser.FixedQuality) < 1e-9);

var graphicLine = "R" + string.Join("R", Enumerable.Repeat("-1.5", AutoEqParser.GraphicFrequencies.Count));
var graphicValues = AutoEqParser.ParseGraphic(graphicLine);
Check("S7 图形 EQ 解析出 127 个值", graphicValues.Count == AutoEqParser.GraphicFrequencies.Count,
    graphicValues.Count.ToString());
Check("S7 图形 EQ 的数值对得上", graphicValues.Count > 0 && Math.Abs(graphicValues[^1] + 1.5) < 1e-9);

// ---- AutoEQ 结果库 ----
var autoEqDir = CreateScratchDirectory();
try
{
    var databasePath = Path.Combine(autoEqDir, "sample.txt");
    File.WriteAllLines(databasePath, new[]
    {
        "5 1",
        "Mcrinacle",
        "W711 in-ear",
        "DTest Can",
        "A-6M105G5.5Q0.7P180G-3.3Q0.57",
        "A-6.7F6.3F2.8F-0.6F-3.4F-1.1F1.9F-3.7F4.4F1.6F-1.6",
        graphicLine,
    });

    var database = AutoEqDatabase.Load(databasePath);
    Check("S7 库版本是 5", database.Version == 5, database.Version.ToString());
    Check("S7 库解析出一台设备", database.Devices.Count == 1, database.Devices.Count.ToString());

    var device = database.Devices[0];
    Check("S7 设备名去掉 D 前缀", device.Name == "Test Can", device.Name);
    Check("S7 三种等化都在", device.HasParametric && device.HasFixedBand && device.HasGraphic);
    Check("S7 搜索命中型号", database.Search("test").Count == 1);
    Check("S7 搜索命中佩戴方式", database.Search("in-ear").Count == 1);
    Check("S7 搜索空结果", database.Search("zzz").Count == 0);

    var parametricProfile = database.GetProfile(device, AutoEqType.Parametric);
    Check("S7 参数 EQ 能转成点", parametricProfile is not null && parametricProfile.CanApplyToPoints);
    var parametricPoints = parametricProfile!.ToPointList();
    Check("S7 参数 EQ 转点补了两个端点", parametricPoints.Count == 4, parametricPoints.Count.ToString());
    Check("S7 转出来的低架仍是 LSC",
        parametricPoints.Points.Any(point => point.FilterType == EqLimits.LowShelfCenteredFilter));
    Check("S7 参数 EQ 的预增益 -6", Math.Abs(parametricProfile.PreAmpDb + 6) < 1e-9);

    var apoText = parametricProfile.ToEqualizerApoText();
    Check("S7 复制文本有预增益", apoText.Contains("Preamp: -6.0 dB"), apoText);
    Check("S7 复制文本有 LSC 行", apoText.Contains("ON LSC Fc 105 Hz Gain 5.5 dB Q 0.70"), apoText);

    var fixedProfile = database.GetProfile(device, AutoEqType.FixedBand);
    Check("S7 固定频带能转成 12 个点", fixedProfile?.ToPointList().Count == 12,
        fixedProfile?.ToPointList().Count.ToString() ?? "null");

    var graphicProfile = database.GetProfile(device, AutoEqType.Graphic);
    Check("S7 图形 EQ 不能直接写点", graphicProfile is { CanApplyToPoints: false });
    Check("S7 图形 EQ 的文本是 GraphicEQ 行",
        graphicProfile?.ToEqualizerApoText().StartsWith("GraphicEQ:", StringComparison.Ordinal) == true);

    // ---- 曲线预览：预设EQ 窗口现在画的是这条采样 ----
    var parametricCurve = parametricProfile.SampleResponse(64);
    Check("S7 参数 EQ 能采样成曲线", parametricCurve.Length == 64);
    Check("S7 采样都是有限数", parametricCurve.All(double.IsFinite));
    Check("S7 低架把低频抬起来了", parametricCurve[0] > 3, parametricCurve[0].ToString("0.0"));
    Check("S7 曲线尾端接近 0", Math.Abs(parametricCurve[^1]) < 3, parametricCurve[^1].ToString("0.0"));

    var graphicCurve = graphicProfile!.SampleResponse(64);
    Check("S7 图形 EQ 也能采样成曲线", graphicCurve.Length == 64);
    Check("S7 平坦的图形 EQ 采样也是平的", graphicCurve.All(value => Math.Abs(value + 1.5) < 0.5),
        graphicCurve.Min().ToString("0.0") + "~" + graphicCurve.Max().ToString("0.0"));

    Check("S7 滤波器频率给预览打点", parametricProfile.FilterFrequencies.Count == parametricProfile.Filters.Count);
    Check("S7 图形 EQ 没有单点", graphicProfile.FilterFrequencies.Count == 0);
}
finally
{
    TryDelete(autoEqDir);
}

// ---- 滤波器类型随预设 / 配置往返 ----
var shelfPoints = new[]
{
    new EqPoint(1, 105, 5.5, 0.7, false, EqLimits.LowShelfCenteredFilter),
    new EqPoint(2, 1000, -2.0, 1.41, false),
    new EqPoint(3, 10000, 3.0, 0.7, false, EqLimits.HighShelfCenteredFilter),
    new EqPoint(4, 20000, 0, 1.41, true),
};

var shelfText = EapoConfig.Build(shelfPoints, -1.5, 0, "Device: all");
Check("S7 配置里写出 LSC", shelfText.Contains("ON LSC Fc 105 Hz"), shelfText);
Check("S7 配置里写出 HSC", shelfText.Contains("ON HSC Fc 10000 Hz"), shelfText);

var shelfParsed = EapoConfig.Parse(shelfText);
Check("S7 读回来还是 LSC / HSC",
    shelfParsed.Points.Points.Any(point => point.FilterType == EqLimits.LowShelfCenteredFilter)
    && shelfParsed.Points.Points.Any(point => point.FilterType == EqLimits.HighShelfCenteredFilter));
Check("S7 架式不再算非 PK", shelfParsed.NonPeakingFilters == 0, shelfParsed.NonPeakingFilters.ToString());

var shelfDir = CreateScratchDirectory();
try
{
    var shelfPath = Path.Combine(shelfDir, "shelf.peace");
    Check("S7 带架式的预设能写", PeaceConfig.Save(shelfPath, shelfPoints, out var writeError), writeError);
    var shelfLoaded = PeaceConfig.Load(shelfPath, out var nonPeaking);
    // 存进去的第一个点是 105 Hz，读回来会补一个 20 Hz 端点，所以是 4 + 1。
    Check("S7 预设读回来点数一致", shelfLoaded is not null && shelfLoaded.Count == 5, shelfLoaded?.Count.ToString() ?? "null");
    Check("S7 预设里 [Filters] 段写出来了",
        File.ReadAllText(shelfPath).Contains("Filter1=8"), File.ReadAllText(shelfPath));
    Check("S7 预设读回来的类型没丢",
        shelfLoaded is not null
        && shelfLoaded.Points.Any(point => point.FilterType == EqLimits.LowShelfCenteredFilter)
        && shelfLoaded.Points.Any(point => point.FilterType == EqLimits.HighShelfCenteredFilter));
    Check("S7 预设读回来没有非 PK 计数", nonPeaking == 0, nonPeaking.ToString());

    var lowShelfCurve = EqResponse.Curve(shelfLoaded!.ToSnapshot(), 200);
    Check("S7 架式曲线是有限数", lowShelfCurve.All(double.IsFinite));
}
finally
{
    TryDelete(shelfDir);
}

// ---- 自动EQ：扫频、分析、拟合 ----

Check("S11 扫频有 61 个频点", SweepPlan.Frequencies.Count == 61, SweepPlan.Frequencies.Count.ToString());
Check("S11 起点是 20 Hz", Math.Abs(SweepPlan.Frequencies[0] - 20) < 0.01);
Check("S11 终点是 20 kHz", Math.Abs(SweepPlan.Frequencies[^1] - 20000) < 1);
Check("S11 频率单调递增", SweepPlan.Frequencies.Zip(SweepPlan.Frequencies.Skip(1)).All(pair => pair.Second > pair.First));
Check("S11 每个频点都不是整数倍关系", SweepPlan.Frequencies.All(hz => hz > 0));
Check("S11 一个频点占 380 ms", SweepPlan.BlockMs == 380, SweepPlan.BlockMs.ToString());
Check("S11 整条扫频约 24 秒（含 600 ms 同步音）", Math.Abs(SweepPlan.TotalMs - 23780) <= SweepPlan.BlockMs, SweepPlan.TotalMs.ToString());

// 三档时长预算：快速约 2 分钟、标准约 5 分钟、更准约 15 分钟。
// 单遍 ≈ 1.7 s + 0.38 s × 点数；次数上限 3 / 6 / 8，测稳了会提前收手。
var fastFrequencies = SweepPlan.BuildFastFrequencies();
var standardFrequencies = SweepPlan.BuildStandardFrequencies();
var preciseFrequencies = SweepPlan.BuildPreciseFrequencies();
Check("S11 快速档 61 个频点", fastFrequencies.Length == 61, fastFrequencies.Length.ToString());
Check("S11 标准档 121 个频点", standardFrequencies.Length == 121, standardFrequencies.Length.ToString());
Check("S11 更准档 240 个频点", preciseFrequencies.Length == 240, preciseFrequencies.Length.ToString());
Check("S11 快速档整档不超过两分钟",
    SweepPlan.TotalMsFor(fastFrequencies.Length) * 3 <= 120_000 + 5_000,
    (SweepPlan.TotalMsFor(fastFrequencies.Length) * 3 / 1000.0).ToString("0.0") + " s");
Check("S11 标准档整档不超过五分钟",
    SweepPlan.TotalMsFor(standardFrequencies.Length) * 6 <= 300_000 + 10_000,
    (SweepPlan.TotalMsFor(standardFrequencies.Length) * 6 / 1000.0).ToString("0.0") + " s");
Check("S11 更准档整档不超过十五分钟",
    SweepPlan.TotalMsFor(preciseFrequencies.Length) * 8 <= 900_000 + 30_000,
    (SweepPlan.TotalMsFor(preciseFrequencies.Length) * 8 / 1000.0).ToString("0.0") + " s");

// 教程里的「听区别」：两遍五秒扫频，第二遍在软件里加示例 EQ，不该产生 NaN 或削波。
var tutorialDry = TutorialSweepPlayer.BuildSamples(withDemoEq: false);
var tutorialDemo = TutorialSweepPlayer.BuildSamples(withDemoEq: true);
Check("S11 教程扫频是 5 秒", tutorialDry.Length == 48000 * 5, tutorialDry.Length.ToString());
Check("S11 教程扫频没有 NaN", tutorialDry.All(float.IsFinite));
Check("S11 教程示例 EQ 没有 NaN", tutorialDemo.All(float.IsFinite));
Check("S11 教程示例 EQ 确实改了波形",
    tutorialDry.Zip(tutorialDemo).Any(pair => Math.Abs(pair.First - pair.Second) > 1e-4));
Check("S11 教程扫频不越过 ±0.9",
    tutorialDry.All(sample => Math.Abs(sample) <= 0.9f)
    && tutorialDemo.All(sample => Math.Abs(sample) <= 0.9f));

// 合成一段「录音」：先放同步音，再按给定形状逐段响，中间夹静音，再加一点底噪。
// 分析器靠同步音定时间原点，所以这里必须把它一起造出来。
static float[] SynthesizeSweep(int sampleRate, Func<double, double> gainDb)
{
    var blockSamples = sampleRate * SweepPlan.BlockMs / 1000;
    var toneSamples = sampleRate * SweepPlan.ToneMs / 1000;
    var syncSamples = sampleRate * SweepPlan.SyncMs / 1000;
    var gapSamples = sampleRate * SweepPlan.SyncGapMs / 1000;
    var lead = syncSamples + gapSamples;
    var blocks = SweepPlan.Frequencies.Count;
    var buffer = new float[lead + (blocks * blockSamples)];
    var random = new Random(20261024);

    // 同步音：接近满幅的 1 kHz，长度 400 ms，分析器就是靠它对齐的
    for (var i = 0; i < syncSamples; i++)
    {
        var fade = sampleRate * 8 / 1000;
        var envelope = i < fade ? i / (double)fade : (syncSamples - i) < fade ? (syncSamples - i) / (double)fade : 1.0;
        buffer[i] = (float)(0.5 * envelope * Math.Sin(2 * Math.PI * SweepPlan.SyncHz * i / sampleRate));
    }

    for (var i = lead - gapSamples; i < lead; i++)
    {
        buffer[i] += (float)((random.NextDouble() - 0.5) * 2e-4);
    }

    for (var block = 0; block < blocks; block++)
    {
        var hz = SweepPlan.Frequencies[block];
        var amplitude = 0.2 * Math.Pow(10, gainDb(hz) / 20);
        var offset = lead + (block * blockSamples);

        for (var i = 0; i < toneSamples; i++)
        {
            buffer[offset + i] = (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / sampleRate));
        }

        for (var i = 0; i < blockSamples; i++)
        {
            buffer[offset + i] += (float)((random.NextDouble() - 0.5) * 2e-4);
        }
    }

    return buffer;
}

// 低频少 6 dB，中高频平坦——这就是典型的「要补低频」的耳机。
static double DipShape(double hz) => hz <= 200 ? -6 : 0;

var synthetic = SynthesizeSweep(48000, DipShape);
var analyzed = SweepAnalyzer.Analyze(synthetic, 48000, out var analyzeError);
Check("S11 合成的扫频能分段", analyzed is not null, analyzeError);
Check("S11 分出 61 段", analyzed?.Points.Count == SweepPlan.Frequencies.Count,
    analyzed?.Points.Count.ToString() ?? "null");

var lowPoint = analyzed?.Points.OrderBy(point => Math.Abs(Math.Log(point.FrequencyHz / 100))).First();
Check("S11 100 Hz 测到 -6 dB", lowPoint is not null && Math.Abs(lowPoint.LevelDb + 6) < 1.0,
    lowPoint?.LevelDb.ToString("0.00") ?? "null");

var highPoint = analyzed?.Points.OrderBy(point => Math.Abs(Math.Log(point.FrequencyHz / 1000))).First();
Check("S11 1 kHz 是参考 0 dB", highPoint is not null && Math.Abs(highPoint.LevelDb) < 0.6,
    highPoint?.LevelDb.ToString("0.00") ?? "null");

// 节拍不对时不该硬凑：少播一段就该报错，而不是把频率错位地写进结果。
var truncated = synthetic.Take(synthetic.Length - 48000 * SweepPlan.BlockMs / 1000).ToArray();
var truncatedResult = SweepAnalyzer.Analyze(truncated, 48000, out var truncatedError);
Check("S11 少一段会报错", truncatedResult is null && truncatedError is not null, truncatedError ?? "没有报错");

// 目标曲线
Check("S11 平直全 0", TargetCurve.GainAt(CalibrationTargetKind.Flat, 500) == 0);
Check("S11 哈曼 1 kHz 是 0", Math.Abs(TargetCurve.GainAt(CalibrationTargetKind.HarmanInEar, 1000)) < 0.01);
Check("S11 哈曼 20 Hz 约 +5.8", Math.Abs(TargetCurve.GainAt(CalibrationTargetKind.HarmanInEar, 20) - 5.8) < 0.1);
Check("S11 哈曼 2 kHz 有凸起", TargetCurve.GainAt(CalibrationTargetKind.HarmanInEar, 2000) > 1.0);
Check("S11 哈曼 5 kHz 是凹的", TargetCurve.GainAt(CalibrationTargetKind.HarmanInEar, 5000) < 0);

// 拟合：平直目标 + 低频凹陷 → 应该在低频补回来。
if (analyzed is not null)
{
    var plan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, analyzed);
    Check("S11 拟合成 23 段", plan.FilterCount == 23, plan.FilterCount.ToString());
    Check("S11 预增益不为正", plan.PreAmpDb <= 0, plan.PreAmpDb.ToString("0.0"));

    // 段数不该写死：三档疏密各给各的段数，Q 跟着段距走，三档都能拟合。
    // 所有档都从 100 Hz 起：低频补不出来，别浪费推子。
    Check("S11 快速档 16 个拟合点", EqCurveFitter.PointCount(FitDensity.Coarse) == 16,
        EqCurveFitter.PointCount(FitDensity.Coarse).ToString());
    Check("S11 粗档 9 个拟合点", EqCurveFitter.PointCount(FitDensity.Sparse) == 9,
        EqCurveFitter.PointCount(FitDensity.Sparse).ToString());
    Check("S11 标准档 23 个拟合点", EqCurveFitter.PointCount(FitDensity.Normal) == 23,
        EqCurveFitter.PointCount(FitDensity.Normal).ToString());
    Check("S11 细档 28 个拟合点", EqCurveFitter.PointCount(FitDensity.Dense) == 28,
        EqCurveFitter.PointCount(FitDensity.Dense).ToString());

    // 拟合器的推子从 112 Hz 起（100 Hz 以下一个都没有，100–112 留给过渡）；加两个端点也必须还在 31 段里。
    foreach (var density in new[] { FitDensity.Coarse, FitDensity.Sparse, FitDensity.Normal, FitDensity.Dense })
    {
        var grid = EqCurveFitter.BuildFrequencies(density);
        Check($"S11 {density} 的网格从 112 Hz 起",
            Math.Abs(grid[0] - EqCurveFitter.FitStartHz) < 0.05
            && grid[0] > EqCurveFitter.CalibrationFloorHz,
            grid[0].ToString("0.0"));
        Check($"S11 {density} 的网格到 16 kHz 止", Math.Abs(grid[^1] - EqCurveFitter.FitEndHz) < 0.01, grid[^1].ToString("0"));
        Check($"S11 {density} 的网格严格递增",
            grid.Zip(grid.Skip(1)).All(pair => pair.Second > pair.First));
        Check($"S11 {density} 加端点不超过 31 段", grid.Length + 2 <= EqLimits.MaxBands,
            (grid.Length + 2).ToString());
    }

    // 「更准」档是两段密度：4 kHz 以下 1/4 倍频程，以上 1/3 倍频程。
    var denseGrid = EqCurveFitter.BuildFrequencies(FitDensity.Dense);
    Check("S11 更准档中低频更密",
        EqCurveFitter.LocalStepOctaves(denseGrid, 4) < 0.30,
        EqCurveFitter.LocalStepOctaves(denseGrid, 4).ToString("0.00"));
    Check("S11 更准档高频放宽到三分之一倍频程",
        EqCurveFitter.LocalStepOctaves(denseGrid, denseGrid.Length - 2) > 0.30,
        EqCurveFitter.LocalStepOctaves(denseGrid, denseGrid.Length - 2).ToString("0.00"));
    Check("S11 段距越密 Q 越大",
        EqCurveFitter.Quality(FitDensity.Sparse) < EqCurveFitter.Quality(FitDensity.Normal)
        && EqCurveFitter.Quality(FitDensity.Normal) < EqCurveFitter.Quality(FitDensity.Dense),
        $"{EqCurveFitter.Quality(FitDensity.Sparse):0.00} / {EqCurveFitter.Quality(FitDensity.Normal):0.00} / {EqCurveFitter.Quality(FitDensity.Dense):0.00}");

    var sparsePlan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, analyzed, null, FitDensity.Sparse);
    var densePlan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, analyzed, null, FitDensity.Dense);
    // 局部加密会在残差大的地方补段，所以「档位段数」看格点，最终段数只保证不超过 31。
    var sparseGrid = EqCurveFitter.BuildFrequencies(FitDensity.Sparse);
    Check("S11 粗档格点 9 段", sparseGrid.Length == 9, sparseGrid.Length.ToString());
    Check("S11 粗档加密后不超过 31 段",
        sparsePlan.FilterCount >= 9 && sparsePlan.FilterCount <= EqCurveFitter.MaxFitPoints,
        sparsePlan.FilterCount.ToString());
    Check("S11 细档格点 28 段", EqCurveFitter.BuildFrequencies(FitDensity.Dense).Length == 28,
        EqCurveFitter.BuildFrequencies(FitDensity.Dense).Length.ToString());
    Check("S11 细档加密后不超过 31 段",
        densePlan.FilterCount >= 28 && densePlan.FilterCount <= EqCurveFitter.MaxFitPoints,
        densePlan.FilterCount.ToString());

    // 残差驱动局部加密：一个 1/6 倍频程宽的 8 dB 窄谷，标准档 1/3 倍频程的格点盖不住，
    // 解完应该在那一带补段，最终段数超过格点数。
    var narrowDip = new SweepMeasurement(
        SweepPlan.Frequencies
            .Select(hz =>
            {
                var dip = -8.0 * Math.Exp(-Math.Pow(Math.Log2(hz / 640.0) / 0.08, 2));
                return new MeasurementPoint(hz, dip, 40, dip);
            })
            .ToList(),
        -70,
        48000,
        SweepPlan.Frequencies);

    var narrowPlan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, narrowDip, null, FitDensity.Normal);
    var normalGrid = EqCurveFitter.BuildFrequencies(FitDensity.Normal);
    Check("S11 残差驱动局部加密会补段",
        narrowPlan.FilterCount > normalGrid.Length,
        $"{narrowPlan.FilterCount} vs 格点 {normalGrid.Length}");

    // 曲线毛躁度：拟合完要按 1/24 倍频程扫一遍实际响应，太毛躁就该自己放宽 Q。
    Check("S11 标准档曲线起伏在阈值内",
        plan.RippleDb <= EqCurveFitter.MaxRippleDb + 0.3,
        plan.RippleDb.ToString("0.00"));
    Check("S11 细档曲线起伏也压住了",
        densePlan.RippleDb <= EqCurveFitter.MaxRippleDb + 0.3,
        densePlan.RippleDb.ToString("0.00"));

    // 推子间距尺度的锯齿：相邻两个推子中点，比它们中心低多少。
    // 这就是「段少时曲线看着像锯齿」的那个深度，段距越疏越容易露出来。
    static double MaxMidpointDip(CalibrationPlan fitted)
    {
        var snapshot = fitted.Points.ToSnapshot();
        var bands = fitted.Points.Points.Where(point => !point.IsEndpoint).ToList();
        var worst = 0.0;

        for (var i = 0; i < bands.Count - 1; i++)
        {
            var low = bands[i].FrequencyHz;
            var high = bands[i + 1].FrequencyHz;
            var middle = Math.Sqrt(low * high);
            var edge = (EqResponse.GainAt(snapshot, low) + EqResponse.GainAt(snapshot, high)) / 2;
            worst = Math.Max(worst, edge - EqResponse.GainAt(snapshot, middle));
        }

        return worst;
    }

    Check("S11 粗档中点凹陷不超过 1.2 dB", MaxMidpointDip(sparsePlan) <= 1.2,
        MaxMidpointDip(sparsePlan).ToString("0.00"));
    Check("S11 标准档中点凹陷不超过 0.8 dB", MaxMidpointDip(plan) <= 0.8,
        MaxMidpointDip(plan).ToString("0.00"));
    Check("S11 细档中点凹陷不超过 0.8 dB", MaxMidpointDip(densePlan) <= 0.8,
        MaxMidpointDip(densePlan).ToString("0.00"));

    // 实时预览：只测到十来个点的时候也得能拟合，否则界面上连实测线都画不出来。
    // SnrDb 必须给够（>= 6），不然 FillGaps 会把所有点判成不可用，Fit 直接抛。
    var partialPoints = new List<MeasurementPoint>();
    for (var i = 0; i < 10; i++)
    {
        var hz = SweepPlan.Frequencies[i];
        var level = -40 + (i * 0.5);
        partialPoints.Add(new MeasurementPoint(hz, level, 12, level));
    }

    var partialFrequencies = partialPoints.Select(point => point.FrequencyHz).ToArray();
    var partialMeasurement = new SweepMeasurement(partialPoints, -60, 48000, partialFrequencies);
    CalibrationPlan? partialPlan = null;
    try
    {
        partialPlan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, partialMeasurement, null, FitDensity.Normal);
    }
    catch (Exception)
    {
        // 抛了就让下面的断言失败，好看见。
    }

    Check("S11 只测到 10 个点也能拟合出方案",
        partialPlan is not null && partialPlan.FilterCount == 23,
        partialPlan?.FilterCount.ToString() ?? "null");

    // 只测到 56 Hz 的时候，1 kHz 以上还没测，那些推子必须留在 0 dB。
    // 要是越界夹到端点值，整条高频都会继承最后一个点的增益，看起来像「全算完了」。
    var unmeasuredBands = partialPlan!.Points.Points
        .Where(point => !point.IsEndpoint && point.FrequencyHz > 1000)
        .ToList();
    var maxUnmeasured = unmeasuredBands.Count == 0
        ? 0
        : unmeasuredBands.Max(point => Math.Abs(point.GainDb));

    Check("S11 未测频段的推子留在 0 dB", maxUnmeasured < 0.5, maxUnmeasured.ToString("0.00"));

    // 第二轮按上一轮的陡缓加密：只在落差大的地方补刀，且补完仍然有序。
    var adaptive = SweepPlan.BuildAdaptiveFrequencies(analyzed.Points, 8);
    Check("S11 自适应频点会补在陡处", adaptive.Length > SweepPlan.Frequencies.Count,
        adaptive.Length.ToString());
    Check("S11 自适应频点仍然有序",
        adaptive.Zip(adaptive.Skip(1)).All(pair => pair.Second > pair.First));

    // 加密要以当轮的表为底：标准档 121 点不能被 61 点表覆盖掉一半。
    var adaptiveStandard = SweepPlan.BuildAdaptiveFrequencies(analyzed.Points, 0, standardFrequencies);
    Check("S11 标准档不加点时保留原表",
        adaptiveStandard.Length == standardFrequencies.Length
        && standardFrequencies.All(hz => adaptiveStandard.Contains(hz)),
        adaptiveStandard.Length.ToString());
    var adaptiveFast = SweepPlan.BuildAdaptiveFrequencies(analyzed.Points, 8, fastFrequencies);
    Check("S11 快速档在 61 点表上补点",
        adaptiveFast.Length > fastFrequencies.Length && fastFrequencies.All(hz => adaptiveFast.Contains(hz)),
        adaptiveFast.Length.ToString());

    // 第二次测量：喇叭上已经套着第一次的 EQ。拟合结果应该回到同一套推子，
    // 而不是把同一个凹陷再补一遍（那样增益会翻倍）。
    var firstSnapshot = plan.Points.ToSnapshot();
    var withEqPoints = analyzed.Points
        .Select(point => point with
        {
            LevelDb = point.LevelDb + EqResponse.GainAt(firstSnapshot, point.FrequencyHz),
        })
        .ToList();

    var withEq = new SweepMeasurement(withEqPoints, analyzed.NoiseFloorDb, analyzed.SampleRate, analyzed.Frequencies);
    var secondPlan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, withEq, null, FitDensity.Normal, plan.Points);

    var firstBands = plan.Points.Points.Where(point => !point.IsEndpoint).ToList();
    var secondBands = secondPlan.Points.Points.Where(point => !point.IsEndpoint).ToList();
    var maxDrift = firstBands.Zip(secondBands)
        .Max(pair => Math.Abs(pair.First.GainDb - pair.Second.GainDb));

    Check("S11 带 baseline 拟合回到同一套推子", maxDrift < 1.5, maxDrift.ToString("0.00"));

    var snapshot = plan.Points.ToSnapshot();
    var boostAt126 = EqResponse.GainAt(snapshot, 126);
    var boostAt100 = EqResponse.GainAt(snapshot, 100);
    var boostAt63 = EqResponse.GainAt(snapshot, 63);
    Check("S11 126 Hz 补了约 6 dB", Math.Abs(boostAt126 - 6) < 2.5,
        boostAt126.ToString("0.00") + " | " + string.Join(",", plan.Points.Points.Select(p => $"{p.FrequencyHz:0}:{p.GainDb:0.0}")));
    Check("S11 100 Hz 只是过渡，没补满", boostAt100 < 4.5, boostAt100.ToString("0.00"));
    Check("S11 63 Hz 不补——那是下潜，不是颜色", Math.Abs(boostAt63) < 1.0, boostAt63.ToString("0.00"));

    var boostAt1k = EqResponse.GainAt(snapshot, 1000);
    Check("S11 1 kHz 基本不动", Math.Abs(boostAt1k) < 1.5, boostAt1k.ToString("0.00"));

    var harmanPlan = EqCurveFitter.Fit(CalibrationTargetKind.HarmanInEar, analyzed);
    Check("S11 哈曼目标也能拟合", harmanPlan.FilterCount == 23);
    Check("S11 哈曼目标低频补得更多", EqResponse.GainAt(harmanPlan.Points.ToSnapshot(), 126) > boostAt126);

    // ---- 低频矫正：单独写给「低音」层 ----
    // 主层 100 Hz 以下不补；这段交给低音层，按当前实测反相（平直）或补到目标（哈曼）。
    var bassGrid = EqCurveFitter.BuildBassFrequencies();
    Check("S11 低频网格从 22.4 Hz 起", Math.Abs(bassGrid[0] - EqCurveFitter.BassFitStartHz) < 0.05,
        bassGrid[0].ToString("0.0"));
    Check("S11 低频网格到 100 Hz 止", Math.Abs(bassGrid[^1] - EqCurveFitter.BassCeilingHz) < 0.01,
        bassGrid[^1].ToString("0.0"));
    Check("S11 低频网格严格递增", bassGrid.Zip(bassGrid.Skip(1)).All(pair => pair.Second > pair.First));
    Check("S11 低频网格避开 20 Hz 端点", bassGrid[0] > EqCurveFitter.BassFloorHz,
        bassGrid[0].ToString("0.0"));

    var bassFlat = EqCurveFitter.FitBass(CalibrationTargetKind.Flat, analyzed);
    var bassBands = bassFlat.Points.Where(point => !point.IsEndpoint).ToList();
    Check("S11 低频矫正有推子", bassBands.Count > 0, bassBands.Count.ToString());
    Check("S11 低频推子都在 20–100 Hz 里",
        bassBands.All(point => point.FrequencyHz > EqCurveFitter.BassFloorHz
            && point.FrequencyHz <= EqCurveFitter.BassCeilingHz + 0.01),
        string.Join(",", bassBands.Select(point => point.FrequencyHz.ToString("0.0"))));
    Check("S11 20 Hz 是端点、增益 0",
        bassFlat.Points[0].IsEndpoint && Math.Abs(bassFlat.Points[0].GainDb) < 0.01,
        $"{bassFlat.Points[0].IsEndpoint} {bassFlat.Points[0].GainDb:0.00}");

    var bassSnapshot = bassFlat.ToSnapshot();
    var bassAt45 = EqResponse.GainAt(bassSnapshot, 45);
    var bassAt100 = EqResponse.GainAt(bassSnapshot, 100);
    Check("S11 低频 45 Hz 补了约 6 dB", Math.Abs(bassAt45 - 6) < 2.0, bassAt45.ToString("0.00"));
    Check("S11 低频 100 Hz 淡出到 0", Math.Abs(bassAt100) < 1.0,
        $"{bassAt100:0.00} | {string.Join(",", bassFlat.Points.Select(point => $"{point.FrequencyHz:0.0}:{point.GainDb:0.0}"))}");
    Check("S11 低频能被低音层合并",
        new EqLayerSet(EqPointList.CreateDefault(), bassFlat).BassContributors.Count > 0);

    var bassHarman = EqCurveFitter.FitBass(CalibrationTargetKind.HarmanInEar, analyzed);
    var bassHarmanAt45 = EqResponse.GainAt(bassHarman.ToSnapshot(), 45);
    Check("S11 哈曼目标的低频多抬约 6 dB", bassHarmanAt45 - bassAt45 > 4.5,
        $"{bassAt45:0.00} -> {bassHarmanAt45:0.00}");

    // 提升上限：实测掉 30 dB 时也不许推过 +18。
    var deepLow = SweepAnalyzer.Analyze(SynthesizeSweep(48000, hz => hz <= 100 ? -30 : 0), 48000, out var deepError);
    Check("S11 深低频凹陷能分析", deepLow is not null, deepError);
    if (deepLow is not null)
    {
        var bassDeep = EqCurveFitter.FitBass(CalibrationTargetKind.Flat, deepLow);
        var deepest = bassDeep.Points.Where(point => !point.IsEndpoint).Max(point => point.GainDb);
        Check("S11 低频提升不超过 +18 dB", deepest <= EqCurveFitter.BassMaxBoostDb + 0.01,
            deepest.ToString("0.00"));
    }

    Check("S11 PeakGainDb 量的是真正最高点",
        EqCurveFitter.PeakGainDb(EqPointList.FromPoints(new[]
        {
            new EqPoint(0, 50, 10, 1.0, false),
        })) >= 9.5);

    // ---- 可调矫正起点 ----
    // 图上那条竖线：之上主层拟合，之下低音层反相。默认 100 Hz，这里验证换起点时两层都跟着走。
    Check("S11 200 Hz 起点的第一个推子往上留过渡",
        Math.Abs(EqCurveFitter.BuildFrequencies(FitDensity.Normal, 200)[0]
            - 200 * Math.Pow(2, EqCurveFitter.FloorRampOctaves)) < 0.1,
        EqCurveFitter.BuildFrequencies(FitDensity.Normal, 200)[0].ToString("0.0"));

    var raisedPlan = EqCurveFitter.Fit(
        CalibrationTargetKind.Flat, analyzed, null, FitDensity.Normal, null, true, 200);
    Check("S11 换起点后方案记住起点", Math.Abs(raisedPlan.FloorHz - 200) < 0.01,
        raisedPlan.FloorHz.ToString("0.0"));

    var at150 = Enumerable.Range(0, analyzed.Frequencies.Count)
        .MinBy(index => Math.Abs(Math.Log(analyzed.Frequencies[index] / 150)));
    Check("S11 新起点以下主层不补", Math.Abs(raisedPlan.CorrectionDb[at150]) < 0.01,
        raisedPlan.CorrectionDb[at150].ToString("0.00"));

    var bassAt60 = EqCurveFitter.FitBass(CalibrationTargetKind.Flat, analyzed, ceilingHz: 60);
    Check("S11 低频层上沿能降到 60 Hz",
        Math.Abs(EqCurveFitter.BuildBassFrequencies(60)[^1] - 60) < 0.01
        && bassAt60.Points.Where(point => !point.IsEndpoint).All(point => point.FrequencyHz <= 60.01),
        string.Join(",", bassAt60.Points.Select(point => $"{point.FrequencyHz:0.0}")));

    var bassAt200 = EqCurveFitter.FitBass(CalibrationTargetKind.Flat, analyzed, ceilingHz: 200);
    Check("S11 低频层上沿能抬到 200 Hz",
        Math.Abs(EqCurveFitter.BuildBassFrequencies(200)[^1] - 200) < 0.01
        && bassAt200.Points.Where(point => !point.IsEndpoint).All(point => point.FrequencyHz <= 200.01),
        string.Join(",", bassAt200.Points.Select(point => $"{point.FrequencyHz:0.0}")));
    Check("S11 200 Hz 上沿在交界处淡出到 0",
        Math.Abs(EqResponse.GainAt(bassAt200.ToSnapshot(), 200)) < 1.5,
        EqResponse.GainAt(bassAt200.ToSnapshot(), 200).ToString("0.00"));

    // 光加点还不够：段的中心也能动。先直接测摆位函数——它是纯计算，跟拟合打分无关，
    // 这样挪错了能立刻看见，而不是被「打分也许更喜欢另一套」盖住。
    var placeGrid = EqCurveFitter.BuildFrequencies(FitDensity.Dense);

    var still = EqCurveFitter.PlaceFrequencies(
        new double[SweepPlan.Frequencies.Count], SweepPlan.Frequencies, FitDensity.Dense);
    Check("S11 不用补的地方中心留在格点上", still.SequenceEqual(placeGrid),
        string.Join(",", still.Where((hz, index) => Math.Abs(hz - placeGrid[index]) > 0.05).Select(hz => hz.ToString("0"))));

    // 一条单调下行的修正量：越往高频越该补，每段都在自己那一格里往上挪。
    var ramp = Enumerable.Range(0, SweepPlan.Frequencies.Count).Select(index => -(double)index).ToArray();
    var pushed = EqCurveFitter.PlaceFrequencies(ramp, SweepPlan.Frequencies, FitDensity.Dense);

    var withinReach = new bool[pushed.Length];
    for (var i = 0; i < pushed.Length; i++)
    {
        var reach = Math.Min(
            EqCurveFitter.LocalStepOctaves(placeGrid, i) * 0.5,
            EqCurveFitter.MaxBandShiftOctaves);
        withinReach[i] = pushed[i] >= placeGrid[i] - 0.01
            && Math.Log(pushed[i] / placeGrid[i], 2) <= reach + 0.02;
    }

    Check("S11 每段只在自家格子里往上挪", withinReach.All(ok => ok),
        string.Join(",", pushed.Select(hz => hz.ToString("0"))));
    Check("S11 高频段确实挪得动",
        Math.Log(pushed[^1] / placeGrid[^1], 2) > 0.1,
        Math.Log(pushed[^1] / placeGrid[^1], 2).ToString("0.000"));

    Check("S11 挪完仍然递增、不挤在一起",
        pushed.Zip(pushed.Skip(1)).All(pair =>
            Math.Log(pair.Second / pair.First, 2) >= EqCurveFitter.MinBandGapOctaves - 0.01),
        string.Join(",", pushed.Select(hz => hz.ToString("0"))));

    // 拟合那一路：打开开关以后，出来的仍然是同一套合法点（段数、递增）。
    var movablePlan = EqCurveFitter.Fit(
        CalibrationTargetKind.Flat, analyzed, null, FitDensity.Dense, null, movableBands: true);
    var movableBands = movablePlan.Points.Points.Where(point => !point.IsEndpoint).ToList();
    Check("S11 打开可调中心后段数不变",
        movableBands.Count == EqCurveFitter.PointCount(FitDensity.Dense),
        movableBands.Count.ToString());
    Check("S11 打开可调中心后仍是递增的一套点",
        movableBands.Zip(movableBands.Skip(1)).All(pair => pair.Second.FrequencyHz > pair.First.FrequencyHz));

    // Q 自动调小之后，不许把曲线调出新尖刺：拿 1/24 倍频程扫「实测 + EQ」。
    // 源曲线本身有台阶（200 Hz 以下 −6 dB），所以阈值比平滑信号宽。
    static double MaxStepAfterEq(CalibrationPlan fitted)
    {
        var eqSnapshot = fitted.Points.ToSnapshot();
        var measured = fitted.Measured;
        var bands = measured.Select(point => point.FrequencyHz).ToArray();
        var levels = measured.Select(point => point.LevelDb).ToArray();
        var step = Math.Pow(2, 1.0 / 24);
        var previous = levels[0] + EqResponse.GainAt(eqSnapshot, bands[0]);
        var worst = 0.0;

        for (var hz = bands[0] * step; hz <= bands[^1]; hz *= step)
        {
            var current = Interpolate(levels, bands, hz) + EqResponse.GainAt(eqSnapshot, hz);
            worst = Math.Max(worst, Math.Abs(current - previous));
            previous = current;
        }

        return worst;

        static double Interpolate(double[] values, double[] frequency, double hz)
        {
            if (hz <= frequency[0])
            {
                return values[0];
            }

            if (hz >= frequency[^1])
            {
                return values[^1];
            }

            var index = 1;
            while (index < frequency.Length - 1 && frequency[index] < hz)
            {
                index++;
            }

            var t = (Math.Log(hz) - Math.Log(frequency[index - 1]))
                / (Math.Log(frequency[index]) - Math.Log(frequency[index - 1]));
            return values[index - 1] + ((values[index] - values[index - 1]) * t);
        }
    }

    Check("S11 调小 Q 之后曲线没有更抖", MaxStepAfterEq(plan) < 2.5,
        MaxStepAfterEq(plan).ToString("0.00"));

    // 一个平滑的窄谷：调小 Q 之后套上去的曲线该是顺的，不该长出新尖刺。
    var smoothPoints = SweepPlan.Frequencies
        .Select(hz =>
        {
            var octaves = Math.Log(hz / 1000, 2) / 0.5;
            var level = -8 * Math.Exp(-(octaves * octaves));
            return new MeasurementPoint(hz, level, 40, level);
        })
        .ToList();

    var smoothPlan = EqCurveFitter.Fit(
        CalibrationTargetKind.Flat,
        new SweepMeasurement(smoothPoints, -70, 48000, SweepPlan.Frequencies),
        null,
        FitDensity.Normal);

    Check("S11 平滑窄谷拟合完不长尖刺", MaxStepAfterEq(smoothPlan) < 1.5,
        MaxStepAfterEq(smoothPlan).ToString("0.00"));

    Check("S11 调小 Q 之后 Q 仍然在合法范围",
        smoothPlan.Points.Points
            .Where(point => !point.IsEndpoint)
            .All(point => point.Quality >= 0.5 && point.Quality <= EqLimits.MaxQuality));


    // 设备整体比目标低 30 dB：需要的提升远超 +9 dB。
    // 这时该把整条目标压下来，但最多 6 dB——压目标就是整体减音量，
    // 不能为了一个够不着的目标把人声一起削掉；够不着的部分如实记进残差。
    var hopeless = new SweepMeasurement(
        SweepPlan.Frequencies
            .Select(hz => new MeasurementPoint(hz, -30, 40, -30))
            .ToList(),
        -70,
        48000,
        SweepPlan.Frequencies);

    var dropped = EqCurveFitter.Fit(CalibrationTargetKind.Flat, hopeless);
    Check("S11 够不着目标时整体压低目标", dropped.TargetOffsetDb < -1, dropped.TargetOffsetDb.ToString("0.0"));
    Check("S11 目标最多只压 6 dB",
        dropped.TargetOffsetDb >= -EqCurveFitter.MaxAutoTargetDropDb - 0.01,
        dropped.TargetOffsetDb.ToString("0.00"));
    Check("S11 压低后修正不突破上限",
        dropped.CorrectionDb.Max() <= EqCurveFitter.MaxBoostDb + 0.01,
        dropped.CorrectionDb.Max().ToString("0.00"));
    Check("S11 残差记下压到下限也补不上的部分", dropped.MaxResidualDb > 3, dropped.MaxResidualDb.ToString("0.0"));
    Check("S11 够得着的时候不动目标",
        Math.Abs(EqCurveFitter.Fit(CalibrationTargetKind.Flat, analyzed).TargetOffsetDb) < 0.01);

    // 100 Hz 以下掉 40 dB 是硬件下潜不够：不补、不削、不拖低目标，也不压预增益。
    // 这是「校准完音量变小」的主因——旧算法会为了够这个下潜把整条目标压低最多 12 dB。
    var lowOnly = new SweepMeasurement(
        SweepPlan.Frequencies
            .Select(hz => new MeasurementPoint(hz, hz < 100 ? -40 : 0, 40, hz < 100 ? -40 : 0))
            .ToList(),
        -70,
        48000,
        SweepPlan.Frequencies);

    var lowPlan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, lowOnly);
    Check("S11 低频深谷不拖低目标", Math.Abs(lowPlan.TargetOffsetDb) < 0.01,
        lowPlan.TargetOffsetDb.ToString("0.00"));
    Check("S11 100 Hz 以下的修正量归零",
        lowPlan.CorrectionDb
            .Where((_, index) => SweepPlan.Frequencies[index] < EqCurveFitter.CalibrationFloorHz)
            .All(db => Math.Abs(db) < 1e-6));
    Check("S11 最低的推子在 100 Hz",
        lowPlan.Points.Points.Where(point => !point.IsEndpoint).Min(point => point.FrequencyHz)
            >= EqCurveFitter.CalibrationFloorHz - 0.01,
        lowPlan.Points.Points.Where(point => !point.IsEndpoint).Min(point => point.FrequencyHz).ToString("0.0"));
    Check("S11 100 Hz 以下不补也不削",
        new[] { 30.0, 50, 63, 80 }
            .All(hz => Math.Abs(EqResponse.GainAt(lowPlan.Points.ToSnapshot(), hz)) < 0.5),
        string.Join(",", new[] { 30.0, 50, 63, 80 }
            .Select(hz => $"{hz:0}:{EqResponse.GainAt(lowPlan.Points.ToSnapshot(), hz):0.00}")));
    Check("S11 低频深谷不算残差", lowPlan.MaxResidualDb < 1.0, lowPlan.MaxResidualDb.ToString("0.00"));
    Check("S11 低频深谷不压低预增益", lowPlan.PreAmpDb >= -0.5, lowPlan.PreAmpDb.ToString("0.00"));

    // 窄坑保护：一个只占十几分之一倍频程、深 14 dB 的坑，别拿预增益去换。
    // 实测里这种坑（麦克风/单元干涉）补了也听不出来，但提升它会把整段音乐压低 8 dB。
    var narrowNotch = new SweepMeasurement(
        SweepPlan.Frequencies
            .Select(hz =>
            {
                var dip = -14.0 * Math.Exp(-Math.Pow(Math.Log(hz / 640.0, 2) / 0.03, 2));
                return new MeasurementPoint(hz, dip, 40, dip);
            })
            .ToList(),
        -70,
        48000,
        SweepPlan.Frequencies);

    var notchPlan = EqCurveFitter.Fit(CalibrationTargetKind.Flat, narrowNotch);
    Check("S11 窄坑不压低整体音量", notchPlan.PreAmpDb >= -4.2, notchPlan.PreAmpDb.ToString("0.00"));
    Check("S11 窄坑之外仍然是平的",
        Math.Abs(EqResponse.GainAt(notchPlan.Points.ToSnapshot(), 300)) < 1.0
        && Math.Abs(EqResponse.GainAt(notchPlan.Points.ToSnapshot(), 2000)) < 1.0);

    // Smooth 的实际行为：脉冲法量一次，钉住「参数 ≠ 实际宽度」这件事。
    // 以后谁想按倍频程改窗口，先看这两条——它们会告诉他实际发生了什么。
    var smoothProbe = SweepPlan.BuildStandardFrequencies();
    var pulseIndex = smoothProbe.Length / 2;
    var pulse = new double[smoothProbe.Length];
    pulse[pulseIndex] = 1;

    var wideSmooth = EqCurveFitter.Smooth(pulse, smoothProbe, 1.0);
    Check("S11 Smooth 传 1.0 时脉冲只留一成",
        Math.Abs(wideSmooth[pulseIndex] - 0.115) < 0.03,
        wideSmooth[pulseIndex].ToString("0.000"));

    var narrowSmooth = EqCurveFitter.Smooth(pulse, smoothProbe, 1.0 / 6.0);
    Check("S11 Smooth 传 1/6 时脉冲留六成",
        Math.Abs(narrowSmooth[pulseIndex] - 0.62) < 0.08,
        narrowSmooth[pulseIndex].ToString("0.000"));

    // 两轮模拟：一个有房间模式的「喇叭」+ 低频落差，第一轮拟合后把 EQ 加上再测第二轮。
    // 这是你在硬件上看到的现象——第二轮不该还是「起伏很大」。
    static double SpeakerShape(double hz)
    {
        // 低频自然滚降（不是台阶）：140 Hz 以下开始掉，100 Hz 约 -4.8 dB。
        var bass = -6.0 / (1.0 + Math.Pow(hz / 140.0, 4));
        var peak500 = 4.0 * Math.Exp(-Math.Pow(Math.Log(hz / 500.0, 2) / 0.2, 2));
        var dip2000 = -5.0 * Math.Exp(-Math.Pow(Math.Log(hz / 2000.0, 2) / 0.2, 2));
        var peak3200 = 5.0 * Math.Exp(-Math.Pow(Math.Log(hz / 3200.0, 2) / 0.18, 2));
        return bass + peak500 + dip2000 + peak3200;
    }

    // 只看真正摆推子的那一段（112 Hz 起）。100–112 Hz 是留给过渡的，按设计不补，
    // 把那 2 dB 算进来只会盖住「第二轮有没有变好」这件事。
    static double MaxRemaining(CalibrationPlan fitted, SweepMeasurement measured)
    {
        var eq = fitted.Points.ToSnapshot();
        return measured.Points
            .Where(point => point.FrequencyHz >= EqCurveFitter.FitStartHz)
            .Max(point => Math.Abs(point.LevelDb + EqResponse.GainAt(eq, point.FrequencyHz)));
    }

    var roomReference = SpeakerShape(1000);
    var roomPoints = standardFrequencies
        .Select(hz =>
        {
            var level = SpeakerShape(hz) - roomReference;
            return new MeasurementPoint(hz, level, 40, level);
        })
        .ToList();

    var roomMeasurement = new SweepMeasurement(
        roomPoints, -70, 48000, roomPoints.Select(point => point.FrequencyHz).ToArray());

    var firstPass = EqCurveFitter.Fit(CalibrationTargetKind.Flat, roomMeasurement);
    var afterEqPoints = roomPoints
        .Select(point => point with
        {
            LevelDb = point.LevelDb + EqResponse.GainAt(firstPass.Points.ToSnapshot(), point.FrequencyHz),
        })
        .ToList();

    var afterEqMeasurement = new SweepMeasurement(
        afterEqPoints, -70, 48000, afterEqPoints.Select(point => point.FrequencyHz).ToArray());

    var secondPass = EqCurveFitter.FitFeedback(
        CalibrationTargetKind.Flat, firstPass, afterEqMeasurement);

    // 第二轮是整层替换，不是把新 EQ 叠在旧 EQ 上：
    // 剩下的响应 = 裸喇叭（roomMeasurement）+ 第二轮的 EQ。
    var firstRemaining = MaxRemaining(firstPass, roomMeasurement);
    var secondRemaining = MaxRemaining(secondPass, roomMeasurement);

    Check("S11 第一轮后剩下的误差有量级", firstRemaining > 0.5 && firstRemaining < 8,
        firstRemaining.ToString("0.00"));
    Check("S11 第二轮把剩下的大半吃掉（不再「起伏很大」）",
        secondRemaining < firstRemaining * 0.6,
        $"第一轮 {firstRemaining:0.00} dB → 第二轮 {secondRemaining:0.00} dB");
    Check("S11 第二轮之后 100 Hz 以上贴近目标",
        secondRemaining < 1.5,
        secondRemaining.ToString("0.00"));
    Check("S11 第二轮也不去补 100 Hz 以下",
        secondPass.Points.Points
            .Where(point => !point.IsEndpoint && point.FrequencyHz < EqCurveFitter.CalibrationFloorHz)
            .All(point => Math.Abs(point.GainDb) < 0.01),
        string.Join(",", secondPass.Points.Points.Select(point => $"{point.FrequencyHz:0}:{point.GainDb:0.0}")));
    Check("S11 第二轮整体音量没有被拖下去",
        secondPass.PreAmpDb >= -9.5, secondPass.PreAmpDb.ToString("0.00"));

    // 反馈轮的「实测」必须是真测到的，不能是拟合用的合成目标。
    // 否则图上那条灰线画的是新 EQ 的反相，看起来像「实测一个大鼓包、低频被画成 0」。
    Check("S11 反馈轮保留真实实测点（含低频）",
        secondPass.Measured.Count == afterEqMeasurement.Points.Count
        && secondPass.Measured.Zip(afterEqMeasurement.Points)
            .All(pair => Math.Abs(pair.First.LevelDb - pair.Second.LevelDb) < 1e-9),
        secondPass.Measured.Count.ToString());
    Check("S11 反馈轮实测含 20 Hz 那一段",
        secondPass.Measured.Any(point => point.FrequencyHz <= 25),
        secondPass.Measured.Count.ToString());

    // 残差要回答「调完之后离目标多远」，而不是「拟合器复现期望曲线复现得怎样」。
    // 统计范围和方案里的残差一致：从第一个推子（矫正起点往上 1/6 倍频程）起，
    // 起点到第一个推子之间的过渡带故意不补，不算进去。
    var secondEq = secondPass.Points.ToSnapshot();
    var residualFloor = EqCurveFitter.CalibrationFloorHz * Math.Pow(2, EqCurveFitter.FloorRampOctaves);
    var trueRemaining = roomPoints
        .Where(point => point.FrequencyHz >= residualFloor)
        .Max(point => Math.Abs(point.LevelDb + EqResponse.GainAt(secondEq, point.FrequencyHz)));

    Check("S11 反馈轮的残差按真实系统算",
        Math.Abs(secondPass.MaxResidualDb - trueRemaining) < 0.5,
        $"{secondPass.MaxResidualDb:0.00} vs {trueRemaining:0.00}");
}

// ---- 真实数据（设了环境变量才跑，不把仓库里的 3.5 MB 数据变成测试依赖） ----
var realAutoEqDir = Environment.GetEnvironmentVariable(AutoEqCatalog.DataDirectoryVariable);
if (!string.IsNullOrWhiteSpace(realAutoEqDir) && Directory.Exists(realAutoEqDir))
{
    var harman = AutoEqCatalog.Sources[0];
    var realDatabase = AutoEqCatalog.TryLoad(harman, out var loadError);
    Check("S7 真实库能读", realDatabase is not null, loadError);
    Check("S7 真实库自称 8853 份结果", realDatabase?.DeclaredCount == 8853,
        realDatabase?.DeclaredCount.ToString() ?? "null");
    Check("S7 真实库有六千多台设备", realDatabase?.Devices.Count > 6000,
        realDatabase?.Devices.Count.ToString() ?? "null");
    Check("S7 真实库第一台带参数 EQ", realDatabase?.Devices[0].HasParametric == true);

    var firstProfile = realDatabase is null
        ? null
        : realDatabase.GetProfile(realDatabase.Devices[0], AutoEqType.Parametric);
    Check("S7 真实库第一台能转成点", firstProfile is not null && firstProfile.ToPointList().Count > 2);
}

// ---- S12 品牌图标匹配 ----
Check("S12 森海塞尔认出品牌", AutoEqBrand.TryMatch("Sennheiser HD 600") == "sennheiser");
Check("S12 索尼认出品牌", AutoEqBrand.TryMatch("Sony WH-1000XM4") == "sony");
Check("S12 铁三角认出品牌", AutoEqBrand.TryMatch("Audio-Technica ATH-M50x") == "audio-technica");
Check("S12 雅马哈认出品牌", AutoEqBrand.TryMatch("Yamaha YH-1000") == "yamahacorporation");
Check("S12 摩托罗拉认出品牌", AutoEqBrand.TryMatch("Motorola Edge 30") == "motorola");
Check("S12 拜雅认出品牌", AutoEqBrand.TryMatch("Beyerdynamic DT 770") == "beyerdynamic");
Check("S12 AKG 认出品牌", AutoEqBrand.TryMatch("AKG K702") == "akg");
Check("S12 舒尔认出品牌", AutoEqBrand.TryMatch("Shure SE846") == "shure");
Check("S12 奥地利音频认出品牌", AutoEqBrand.TryMatch("Austrian Audio Hi-X65") == "austrian");
Check("S12 海菲曼认出品牌", AutoEqBrand.TryMatch("HIFIMAN Sundara") == "hifiman");
Check("S12 飞傲认出品牌", AutoEqBrand.TryMatch("FiiO FH3") == "fiio");
Check("S12 Skullcandy 认出品牌", AutoEqBrand.TryMatch("Skullcandy Crusher") == "skullcandy");
Check("S12 大小写不敏感", AutoEqBrand.TryMatch("sony mdr-7506") == "sony");
Check("S12 没有图标的品牌返回空", AutoEqBrand.TryMatch("Final Audio A8000") is null);
Check("S12 前缀要卡在词边界", AutoEqBrand.TryMatch("HPTuner X1") is null);
Check("S12 纯型号名不误认", AutoEqBrand.TryMatch("LGX-900") is null);
Check("S12 空名字不崩", AutoEqBrand.TryMatch(null) is null && AutoEqBrand.TryMatch("") is null);

// ---- S13 无延迟防爆音 ----
{
    // 和其他需要落盘的测试一样走 CreateScratchDirectory：它先试程序目录再试 Temp，
    // 哪边能写就用哪边。直接写 %TEMP% 在受限的容器里会被拒。
    var root = CreateScratchDirectory();
    var configDir = Path.Combine(root, "config");
    Directory.CreateDirectory(configDir);
    File.WriteAllText(Path.Combine(configDir, "config.txt"), "Include: peace.txt\n");

    var enabledText = PeakGuardSetup.BuildEnabledText(@"C:\x\ClipOnly364.dll");
    Check("S13 打开文本含 VSTPlugin 行", enabledText.Contains("VSTPlugin: Library \"C:\\x\\ClipOnly364.dll\""));
    Check("S13 打开文本不碰 Preamp", !enabledText.Contains("Preamp:", StringComparison.OrdinalIgnoreCase));
    Check("S13 关闭文本不含 VSTPlugin", !PeakGuardSetup.BuildDisabledText().Contains("VSTPlugin", StringComparison.OrdinalIgnoreCase));

    File.WriteAllText(Path.Combine(configDir, "protect.txt"), enabledText);
    Check("S13 认得出已挂上", PeakGuardSetup.IsApplied(configDir));

    File.WriteAllText(Path.Combine(configDir, "protect.txt"), PeakGuardSetup.BuildDisabledText());
    Check("S13 关掉后不算挂上", !PeakGuardSetup.IsApplied(configDir));

    var missing = PeakGuardSetup.Apply(null, true);
    Check("S13 没有 config 目录时给原因", missing.Error is not null);

    TryDelete(root);
}

// ---- S14 风味定制（独立叠加层，八条固定推子） ----
{
    Check("S14 表有八条", ToneBands.All.Count == 8, ToneBands.All.Count.ToString());
    Check(
        "S14 Key 不重复",
        ToneBands.All.Select(band => band.Key).Distinct(StringComparer.Ordinal).Count() == ToneBands.All.Count);
    Check(
        "S14 Label 不重复",
        ToneBands.All.Select(band => band.Label).Distinct(StringComparer.Ordinal).Count() == ToneBands.All.Count);
    Check(
        "S14 默认频率落在覆盖范围内",
        ToneBands.All.All(band => band.LowHz < band.DefaultFrequencyHz && band.DefaultFrequencyHz < band.HighHz));
    Check(
        "S14 Q 与滤波器类型都合法",
        ToneBands.All.All(band =>
            band.DefaultQuality >= EqLimits.MinQuality
            && band.DefaultQuality <= EqLimits.MaxQuality
            && EqLimits.IsSupportedFilter(band.FilterType)));
    Check("S14 默认选脚步", ToneBands.Default.Key == "step");

    var flavors = FlavorSet.CreateDefault();
    Check("S14 默认八条全平", flavors.Bands.Count == 8 && flavors.IsFlat);
    Check("S14 默认值取自表", flavors[3].FrequencyHz == ToneBands.All[3].DefaultFrequencyHz);

    var stepped = flavors.WithGain(3, 4);
    Check("S14 增益按绝对值设置", Math.Abs(stepped[3].GainDb - 4) < 1e-9, stepped[3].GainDb.ToString("0.0"));
    Check("S14 改一条不影响别的", Math.Abs(stepped[4].GainDb) < 1e-9);
    Check("S14 增益夹在 +30", Math.Abs(flavors.WithGain(3, 99)[3].GainDb - EqLimits.MaxGainDb) < 1e-9);
    Check("S14 增益夹在 -30", Math.Abs(flavors.WithGain(3, -99)[3].GainDb - EqLimits.MinGainDb) < 1e-9);

    var dragged = flavors.MoveFrequency(3, 12000);
    Check(
        "S14 频率夹在本条覆盖范围内",
        Math.Abs(dragged[3].FrequencyHz - ToneBands.All[3].HighHz) < 1e-9,
        dragged[3].FrequencyHz.ToString("0"));
    Check("S14 Q 夹在合法范围", Math.Abs(flavors.SetQuality(3, 999)[3].Quality - EqLimits.MaxQuality) < 1e-9);

    Check("S14 全平不贡献滤波器", flavors.Contributors.Count == 0);
    Check("S14 抬一条就贡献一条", stepped.Contributors.Count == 1);
    Check(
        "S14 贡献条目的类型与 Q 对得上",
        stepped.Contributors[0].FilterType == ToneBands.All[3].FilterType
        && Math.Abs(stepped.Contributors[0].Quality - ToneBands.All[3].DefaultQuality) < 1e-9);

    var layers = new EqLayerSet(EqPointList.CreateDefault(), EqPointList.CreateDefault());
    layers.TryMerge(true, out var withoutFlavor, out _);
    layers.TryMerge(true, flavors, out var sameAsWithout, out _);
    Check("S14 全平风味不改变合并结果", withoutFlavor.Equals(sameAsWithout));

    layers.TryMerge(true, stepped, out var withFlavor, out _);
    Check("S14 抬一条后合并多一条", withFlavor.Count == withoutFlavor.Count + 1);
    Check("S14 合并后只有一对端点", withFlavor.Points.Count(point => point.IsEndpoint) == 2);
    Check(
        "S14 合并后风味点的频率与类型保留",
        withFlavor.Points.Any(point => Math.Abs(point.FrequencyHz - ToneBands.All[3].DefaultFrequencyHz) < 1e-6
            && point.FilterType == ToneBands.All[3].FilterType));

    var flavorState = new UiState();
    flavorState.SetMainFlavors("EsportsMain.peace", stepped);
    Check(
        "S14 落盘后能读回（大小写不敏感）",
        flavorState.TryGetMainFlavors("esportsmain.PEACE", out var loaded)
        && Math.Abs(loaded[3].GainDb - 4) < 1e-9);
    Check(
        "S14 没记过的预设给默认全平",
        !flavorState.TryGetMainFlavors("Other.peace", out var missing) && missing.IsFlat);

    var partial = new UiState
    {
        MainFlavors = new Dictionary<string, List<UiFlavor>>
        {
            ["X.peace"] = new()
            {
                new UiFlavor { Key = "step", FrequencyHz = 3000, GainDb = 3, Quality = 2, FilterType = EqLimits.PeakingFilter },
                new UiFlavor { Key = "not-a-band", FrequencyHz = 1000, GainDb = 9, Quality = 1, FilterType = 0 },
            },
        },
    };
    partial.TryGetMainFlavors("X.peace", out var rebuilt);
    Check(
        "S14 缺的条目补默认、多的丢掉",
        rebuilt.Bands.Count == 8
        && Math.Abs(rebuilt[3].FrequencyHz - 3000) < 1e-9
        && rebuilt.Bands.All(band => band.Key != "not-a-band"));

    var legacy = System.Text.Json.JsonSerializer.Deserialize<UiState>("{\"balance\":0.5}")!;
    Check(
        "S14 旧档没有 mainFlavors 也不炸",
        !legacy.TryGetMainFlavors("A.peace", out var legacyFlavors) && legacyFlavors.IsFlat);

    static int FilterLines(string text) =>
        text.Split('\n').Count(line => line.TrimStart().StartsWith("Filter ", StringComparison.Ordinal));

    var textWithout = EapoConfig.Build(withoutFlavor.Points, 0, 0, "Device: all");
    var textWith = EapoConfig.Build(withFlavor.Points, 0, 0, "Device: all");
    Check(
        "S14 风味在 peace.txt 里只多一行 Filter",
        FilterLines(textWith) == FilterLines(textWithout) + 1,
        $"{FilterLines(textWithout)} → {FilterLines(textWith)}");

    var imported = new UiState();
    imported.CopyFrom(flavorState);
    imported.MainFlavors["EsportsMain.peace"][3].GainDb = -7;
    Check(
        "S14 导入备份是深拷",
        flavorState.TryGetMainFlavors("EsportsMain.peace", out var original) && Math.Abs(original[3].GainDb - 4) < 1e-9);

    // 风味和曲线共用一条草稿：只调风味也要算脏、能撤回、能取消
    var flavorDraft = new EqDraft(EqPointList.CreateDefault(), FlavorSet.CreateDefault());
    flavorDraft.Begin();
    Check("S14 刚进编辑不算脏", !flavorDraft.IsDirty);
    flavorDraft.PreviewFlavors(flavorDraft.CurrentFlavors.WithGain(3, 3));
    Check("S14 只改风味也算脏", flavorDraft.IsDirty);
    Check("S14 没压栈前不能撤回", !flavorDraft.CanUndo);
    flavorDraft.Commit();
    Check("S14 一次风味改动进撤回栈", flavorDraft.CanUndo);
    flavorDraft.Undo();
    Check(
        "S14 撤回把风味退回去",
        Math.Abs(flavorDraft.CurrentFlavors[3].GainDb) < 1e-9 && !flavorDraft.IsDirty);
    flavorDraft.Redo();
    Check("S14 重做把风味放回来", Math.Abs(flavorDraft.CurrentFlavors[3].GainDb - 3) < 1e-9);
    flavorDraft.Cancel();
    Check(
        "S14 取消把风味退回已保存那份",
        !flavorDraft.IsDirty && Math.Abs(flavorDraft.CurrentFlavors[3].GainDb) < 1e-9);

    flavorDraft.PreviewFlavors(flavorDraft.CurrentFlavors.WithGain(5, 2));
    flavorDraft.Commit();
    flavorDraft.AcceptSaved(flavorDraft.Current, flavorDraft.CurrentFlavors);
    Check(
        "S14 保存后风味成为新的基线",
        !flavorDraft.IsDirty && Math.Abs(flavorDraft.SavedFlavors[5].GainDb - 2) < 1e-9);
    flavorDraft.End();
}

// ---- 结果 ----
Console.WriteLine($"通过 {passed} 项。");
if (failures.Count == 0)
{
    Console.WriteLine("全部通过。");
    return 0;
}

foreach (var failure in failures)
{
    Console.WriteLine($"失败：{failure}");
}

return 1;

