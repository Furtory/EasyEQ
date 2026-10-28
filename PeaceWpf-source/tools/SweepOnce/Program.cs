using System.Globalization;
using System.Text;
using PeaceWpf.Core;
using PeaceWpf.Core.Audio;

// 命令行跑一轮「快速」档测量，然后把同一段录音用两套引擎各分析一遍。
//
// 为什么不用 SweepMeasureService.RunAsync：那里面直接跑完就写诊断文件，
// 拿不回原始采样，也就没法在同一段录音上比两套算法。这里自己编排同一条流程
// （开麦 → 丢开设备杂音 → 录 300 ms 干净前导 → 播放 → 收尾巴），
// 采样留在手上，顺便存一份 wav，之后可以离线反复分析、不用再放声音。
//
// 用法：
//   dotnet run (project tools/SweepOnce)            播放 + 录音 + 双引擎分析
//   dotnet run (project tools/SweepOnce) probe      只检查数据目录能不能写

if (args.Contains("probe", StringComparer.OrdinalIgnoreCase))
{
    var target = AppPaths.DataDirectory;
    Console.WriteLine($"dataDir={target} exists={Directory.Exists(target)}");
    try
    {
        Directory.CreateDirectory(target);
        var probeFile = Path.Combine(target, "__probe.txt");
        File.WriteAllText(probeFile, "ok");
        Console.WriteLine($"write ok -> {probeFile}");
        File.Delete(probeFile);
    }
    catch (Exception ex) when (ex is not OutOfMemoryException)
    {
        Console.WriteLine($"write failed: {ex.GetType().Name}: {ex.Message}");
    }

    return 0;
}

if (args.Contains("devices", StringComparer.OrdinalIgnoreCase))
{
    foreach (var device in MicrophoneCaptureService.ListDevices())
    {
        Console.WriteLine($"{device.Id}\t{device.Name}");
    }

    Console.WriteLine($"default={MicrophoneCaptureService.DefaultDeviceId()}");
    return 0;
}

if (args.Contains("live", StringComparer.OrdinalIgnoreCase))
{
    // 验证 RunAsync 的实时点：用 11 个频点，几秒钟就跑完。
    var shortTable = SweepPlan.BuildFastFrequencies()
        .Where((_, index) => index % 6 == 0)
        .ToArray();

    var seen = 0;
    var liveProgress = new Progress<SweepProgress>(item =>
    {
        var count = item.PartialPoints?.Count ?? 0;
        if (count != seen)
        {
            seen = count;
            Console.WriteLine($"{item.Fraction * 100,3:0}% partial={count} hz={item.CurrentHz:0}");
        }
    });

    using var liveTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var liveResult = await SweepMeasureService.RunAsync(null, liveProgress, liveTimeout.Token, shortTable);
    Console.WriteLine($"done usable={liveResult.UsableCount}/{liveResult.Points.Count} maxPartial={seen}");
    return 0;
}

var frequencies = SweepPlan.BuildFastFrequencies();
var timing = args.Contains("coherent", StringComparer.OrdinalIgnoreCase) ? SweepTiming.Coherent : SweepTiming.Legacy;
var schedule = SweepSchedule.Build(frequencies, timing);
var deviceId = args.FirstOrDefault(a => a.StartsWith("device=", StringComparison.OrdinalIgnoreCase))?[7..];

Console.WriteLine(
    $"playing {frequencies.Length} points, schedule={schedule.Timing.Name}, total={schedule.TotalMs:0} ms, "
    + $"capture={(string.IsNullOrWhiteSpace(deviceId) ? "默认设备" : deviceId)}");

using var microphone = new MicrophoneCaptureService();
if (!microphone.Start(string.IsNullOrWhiteSpace(deviceId) ? null : deviceId))
{
    Console.Error.WriteLine($"mic failed: {microphone.LastError}");
    return 1;
}

var sampleRate = microphone.SampleRate;
var output = Path.Combine(Environment.CurrentDirectory, "attic");
Directory.CreateDirectory(output);

// 让设备真正开始送数据，再把开设备时的杂音丢掉。
await Task.Delay(500);
microphone.TakeSamples();

// 干净前导：分带噪声底就靠这一段。
await Task.Delay(300);

using var player = new ToneSweepPlayer();
if (!player.Start(schedule))
{
    Console.Error.WriteLine($"player failed: {player.LastError}");
    return 1;
}

var started = DateTime.UtcNow;
while (player.IsPlaying && (DateTime.UtcNow - started).TotalMilliseconds < schedule.TotalMs + 8000)
{
    await Task.Delay(50);
}

player.Stop();

// 收混响尾巴。
await Task.Delay(600);
microphone.Stop();

var samples = microphone.TakeSamples();
Console.WriteLine($"recorded {samples.Length} samples / {sampleRate} Hz / {samples.Length * 1000.0 / sampleRate:0} ms");

var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
var wavPath = Path.Combine(output, $"sweep-{stamp}.wav");
WriteWav(wavPath, samples, sampleRate);
Console.WriteLine($"wav -> {wavPath}");

var legacy = SweepAnalyzer.Analyze(samples, sampleRate, out var legacyError, frequencies);
var coherent = SweepAnalyzer.AnalyzeCoherent(samples, sampleRate, out var coherentError, frequencies, schedule);

Console.WriteLine($"legacy   : {(legacy is null ? legacyError : $"usable={legacy.UsableCount}/{legacy.Points.Count}")}");
Console.WriteLine($"coherent : {(coherent is null ? coherentError : $"usable={coherent.UsableCount}/{coherent.Points.Count} dirty={coherent.DirtyCount}")}");

if (coherent is null)
{
    return 1;
}

if (legacy is null)
{
    // 低频加长之后块长不再相等，旧引擎对不上，只输出新引擎的结果。
    var single = new StringBuilder();
    single.AppendLine("hz,level_db,snr_db,purity_db,align_ms,noise_db");
    foreach (var point in coherent.Points)
    {
        single.AppendLine(
            $"{point.FrequencyHz:0.0},{point.LevelDb:0.00},{point.SnrDb:0.00},{point.PurityDb:0.00},{point.AlignOffsetMs:0.00}");
    }

    File.WriteAllText(Path.Combine(output, $"sweep-{stamp}-coherent.csv"), single.ToString());
    return 0;
}

var csv = new StringBuilder();
csv.AppendLine("hz,legacy_level,coherent_level,delta_db,legacy_snr,coherent_snr,purity_db,align_ms,noise_db");
Console.WriteLine();
Console.WriteLine("    hz   legacy  coherent   delta   snr_old snr_new  purity  align  noise");

for (var i = 0; i < frequencies.Length; i++)
{
    var old = legacy.Points[i];
    var now = coherent.Points[i];
    var noise = coherent.NoiseFloorsDb is { } floors && i < floors.Count ? floors[i] : double.NaN;
    var delta = now.LevelDb - old.LevelDb;

    csv.AppendLine(
        $"{now.FrequencyHz:0.0},{old.LevelDb:0.00},{now.LevelDb:0.00},{delta:0.00},"
        + $"{old.SnrDb:0.00},{now.SnrDb:0.00},{now.PurityDb:0.00},{now.AlignOffsetMs:0.00},{noise:0.00}");

    Console.WriteLine(
        $"{now.FrequencyHz,7:0.0} {old.LevelDb,8:0.00} {now.LevelDb,9:0.00} {delta,7:0.00} "
        + $"{old.SnrDb,8:0.0} {now.SnrDb,7:0.0} {now.PurityDb,7:0.0} {now.AlignOffsetMs,6:0.0} {noise,6:0.0}");
}

var csvPath = Path.Combine(output, $"sweep-{stamp}.csv");
File.WriteAllText(csvPath, csv.ToString());
Console.WriteLine();
Console.WriteLine($"csv -> {csvPath}");

return 0;

// 16-bit 单声道 WAV，够离线复查用。
static void WriteWav(string path, float[] samples, int rate)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);

    var dataBytes = samples.Length * 2;
    writer.Write("RIFF"u8.ToArray());
    writer.Write(36 + dataBytes);
    writer.Write("WAVE"u8.ToArray());
    writer.Write("fmt "u8.ToArray());
    writer.Write(16);
    writer.Write((short)1);
    writer.Write((short)1);
    writer.Write(rate);
    writer.Write(rate * 2);
    writer.Write((short)2);
    writer.Write((short)16);
    writer.Write("data"u8.ToArray());
    writer.Write(dataBytes);

    foreach (var sample in samples)
    {
        var clamped = Math.Clamp(sample, -1f, 1f);
        writer.Write((short)Math.Round(clamped * short.MaxValue));
    }
}
