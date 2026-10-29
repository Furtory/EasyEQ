using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PeaceWpf.Core.Audio;

/// <summary>
/// 试听用的连续升频扫频（chirp）：五秒从 20 Hz 一路扫到 20 kHz。
///
/// 和测量用的步进正弦不是一回事：那个要能切开、能对齐，所以每段之间留静音；
/// 这个是给人耳朵听的——一口气扫上去，哪里鼓、哪里塌、有没有共振，一遍就听出来了。
/// 走默认输出设备（共享模式），所以 EqualizerAPO 的滤镜同样作用在它身上，
/// 听到的就是「现在这套 EQ 之后」的声音。
///
/// 不动系统音量：测量时那种硬往上抬是为了让麦克风听清楚，试听是给人听的，
/// 按用户自己的音量放就行。
/// </summary>
public sealed class ChirpPreviewPlayer : IDisposable
{
    /// <summary>采样率，和测量保持一致。</summary>
    public const int SampleRate = 48000;

    /// <summary>扫频时长（秒）。</summary>
    public const double Seconds = 10;

    /// <summary>起点频率。</summary>
    public const double StartHz = 20;

    /// <summary>终点频率。</summary>
    public const double EndHz = 20000;

    /// <summary>输出幅度。试听不用很响，0.25 约 −12 dBFS。</summary>
    public const float Amplitude = 0.25f;

    /// <summary>淡入淡出的长度（秒），避免开头结尾「咔」一声。</summary>
    private const double FadeSeconds = 0.02;

    private readonly object _gate = new();

    private WasapiOut? _output;
    private RawSourceWaveStream? _source;
    private MemoryStream? _stream;
    private bool _disposed;

    /// <summary>最近一次失败的原因；正常为 null。</summary>
    public string? LastError { get; private set; }

    /// <summary>放一遍。正在放的话先停掉再重来。</summary>
    public bool Play() => PlayRange(StartHz, EndHz, Seconds);

    /// <summary>只放一段：低频对比听 20–200 Hz，高频对比听 1k–20k。</summary>
    public bool Play(double startHz, double endHz)
    {
        startHz = Math.Clamp(startHz, 20, 20000);
        endHz = Math.Clamp(endHz, startHz * 1.05, 20000);
        var octaves = Math.Log(endHz / startHz, 2);
        var seconds = Math.Clamp(Seconds * octaves / 9.97, 1.2, Seconds);
        return PlayRange(startHz, endHz, seconds);
    }

    /// <summary>按指定时长放一段：主界面的「秒」输入框走这里。</summary>
    public bool Play(double startHz, double endHz, double seconds)
    {
        startHz = Math.Clamp(startHz, 20, 20000);
        endHz = Math.Clamp(endHz, startHz * 1.05, 20000);
        return PlayRange(startHz, endHz, Math.Clamp(seconds, 0.5, 120));
    }

    private bool PlayRange(double startHz, double endHz, double seconds)
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
                var samples = Build(startHz, endHz, seconds);

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

    /// <summary>
    /// 生成一段指数扫频：频率按 f(t) = f0·(f1/f0)^(t/T) 走，相位是它的积分。
    /// 用指数而不是线性，是因为听觉本身就是对数刻度——线性扫上去，低频一晃就过去了，高频停半天。
    /// </summary>
    /// <summary>生成这段扫频的样本。分析端要拿同一段波形做反卷积，所以公开出来。</summary>
    public static float[] Build(double startHz, double endHz, double seconds)
    {
        var count = (int)Math.Round(SampleRate * seconds);
        var result = new float[count];
        var ratio = endHz / startHz;
        var k = Math.Log(ratio);
        var scale = 2 * Math.PI * startHz * seconds / k;

        for (var i = 0; i < count; i++)
        {
            var t = i / (double)SampleRate;
            var phase = scale * (Math.Exp(k * t / seconds) - 1);
            var envelope = Math.Min(1, t / FadeSeconds);
            envelope = Math.Min(envelope, Math.Min(1, (seconds - t) / FadeSeconds));

            result[i] = (float)(Amplitude * envelope * Math.Sin(phase));
        }

        return result;
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
