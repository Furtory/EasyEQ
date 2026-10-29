using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PeaceWpf.Core.Audio;

/// <summary>教程试听用的三种声音：不处理、宽低频示例、低音层增强。</summary>
public enum TutorialDemoEq
{
    None,
    Shaped,
    BassBoost,
    ShapedWithBass,
}

/// <summary>
/// 教程里的「听一下区别」：同一段五秒扫频放两遍，第二遍在软件里加一层示例 EQ。
/// </summary>
public sealed class TutorialSweepPlayer : IDisposable
{
    /// <summary>采样率，和自动EQ的试听保持一致。</summary>
    public const int SampleRate = 48000;

    /// <summary>一遍扫频的时长（秒）。</summary>
    public const double Seconds = 5;

    /// <summary>扫频起点。</summary>
    public const double StartHz = 20;

    /// <summary>扫频终点。</summary>
    public const double EndHz = 20000;

    /// <summary>输出幅度。试听用，压到 −12 dBFS 左右。</summary>
    public const float Amplitude = 0.25f;

    private const double FadeSeconds = 0.02;

    private readonly object _gate = new();

    private WasapiOut? _output;
    private RawSourceWaveStream? _source;
    private MemoryStream? _stream;
    private bool _disposed;

    /// <summary>最近一次失败的原因；正常为 null。</summary>
    public string? LastError { get; private set; }

    /// <summary>放一遍。正在放的话先停掉再重来。</summary>
    /// <param name="withDemoEq">true = 加上示例 EQ；false = 当前声音。</param>
    public bool Play(bool withDemoEq) =>
        Play(withDemoEq ? TutorialDemoEq.Shaped : TutorialDemoEq.None);

    /// <summary>按指定试听类型放一遍。</summary>
    public bool Play(TutorialDemoEq mode)
    {
        lock (_gate)
        {
            StopLocked();

            if (_disposed)
            {
                LastError = "已经释放了。";
                return false;
            }

            try
            {
                var samples = BuildSamples(mode);
                var stream = new MemoryStream(samples.Length * sizeof(float));
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    foreach (var sample in samples)
                    {
                        writer.Write(sample);
                    }
                }

                stream.Position = 0;
                var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
                var source = new RawSourceWaveStream(stream, format);

                var device = TryGetDefaultDevice();
                var output = device is null
                    ? new WasapiOut(AudioClientShareMode.Shared, 100)
                    : new WasapiOut(device, AudioClientShareMode.Shared, false, 100);

                output.Init(source);
                output.Play();

                _stream = stream;
                _source = source;
                _output = output;
                LastError = null;
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LastError = ex.Message;
                StopLocked();
                return false;
            }
        }
    }

    /// <summary>停止并释放输出。重复调用是安全的。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            StopLocked();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopLocked();
        }
    }

    /// <summary>
    /// 生成扫频样本。公开出来是为了自测能检查长度、峰值和 NaN，
    /// 不用真的去开声卡。
    /// </summary>
    public static float[] BuildSamples(bool withDemoEq) =>
        BuildSamples(withDemoEq ? TutorialDemoEq.Shaped : TutorialDemoEq.None);

    /// <summary>按指定试听类型生成扫频样本。</summary>
    public static float[] BuildSamples(TutorialDemoEq mode)
    {
        var count = (int)Math.Round(SampleRate * Seconds);
        var dry = new float[count];

        var ratio = EndHz / StartHz;
        var k = Math.Log(ratio);
        var scale = 2 * Math.PI * StartHz * Seconds / k;

        for (var i = 0; i < count; i++)
        {
            var t = i / (double)SampleRate;
            var phase = scale * (Math.Exp(k * t / Seconds) - 1);
            var envelope = Math.Min(1, t / FadeSeconds);
            envelope = Math.Min(envelope, Math.Min(1, (Seconds - t) / FadeSeconds));
            dry[i] = (float)(Amplitude * envelope * Math.Sin(phase));
        }

        return mode switch
        {
            TutorialDemoEq.Shaped => ApplyDemoEq(dry),
            TutorialDemoEq.BassBoost => ApplyBassBoost(dry),
            TutorialDemoEq.ShapedWithBass => ApplyBassBoost(ApplyDemoEq(dry)),
            _ => dry,
        };
    }

    /// <summary>
    /// 一阶低架 + 一阶高架。不是精确的 EQ 滤波器，只是给耳朵一个方向：
    /// 低频更厚、高频收一点。最后整体压一点，留出削波余量。
    /// </summary>
    private static float[] ApplyDemoEq(float[] dry)
    {
        var output = new float[dry.Length];
        var lowAlpha = 1 - Math.Exp(-2 * Math.PI * 140 / SampleRate);
        var highAlpha = 1 - Math.Exp(-2 * Math.PI * 5000 / SampleRate);

        double low = 0;
        double high = 0;

        for (var i = 0; i < dry.Length; i++)
        {
            var x = dry[i];
            low += lowAlpha * (x - low);
            high += highAlpha * (x - high);

            var highPart = x - high;
            var y = x + low - 0.25 * highPart;
            output[i] = (float)Math.Clamp(y * 0.72, -0.9, 0.9);
        }

        return output;
    }

    private static float[] ApplyBassBoost(float[] dry)
    {
        var output = new float[dry.Length];
        var lowAlpha = 1 - Math.Exp(-2 * Math.PI * 110 / SampleRate);
        double low = 0;

        for (var i = 0; i < dry.Length; i++)
        {
            var x = dry[i];
            low += lowAlpha * (x - low);
            output[i] = (float)Math.Clamp((x + 1.15 * low) * 0.76, -0.9, 0.9);
        }

        return output;
    }

    private void StopLocked()
    {
        var output = _output;
        var source = _source;
        var stream = _stream;
        _output = null;
        _source = null;
        _stream = null;

        if (output is not null)
        {
            try
            {
                output.Stop();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LastError = ex.Message;
            }

            try
            {
                output.Dispose();
            }
            catch (Exception)
            {
                // 释放不干净也不该把界面拖崩。
            }
        }

        source?.Dispose();

        try
        {
            stream?.Dispose();
        }
        catch (Exception)
        {
            // 同上。
        }
    }

    private static MMDevice? TryGetDefaultDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
