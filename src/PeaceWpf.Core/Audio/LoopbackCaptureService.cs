using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PeaceWpf.Core.Audio;

/// <summary>
/// 抓系统正在放出来的声音（WASAPI loopback），转成单声道喂给 <see cref="SpectrumAnalyzer"/>。
///
/// 抓不到不是错误：没有声卡、没有默认输出、驱动被占，都只是安静地没有频谱，界面照常能用。
/// 默认输出设备换了（插拔耳机、切到蓝牙）会自己重连。
/// </summary>
public sealed class LoopbackCaptureService : IDisposable
{
    private readonly object _gate = new();

    private WasapiLoopbackCapture? _capture;
    private SpectrumAnalyzer? _analyzer;
    private System.Threading.Timer? _watchdog;
    private float[] _mono = Array.Empty<float>();
    private int _channels = 2;
    private bool _floatSamples = true;
    private int _sampleRate = 48000;
    private string? _deviceId;
    private bool _disposed;
    private float _blockPeakDb = -90f;
    private long _lastBlockAt;

    // 语音唤醒：和自动风味的录音器共用这一路采集，不另开 WASAPI 客户端。
    private PeaceWpf.Core.AutoFlavor.SileroVoiceDetector? _voiceWake;
    private readonly float[] _wakePending = new float[9600];
    private int _wakeCount;
    private long _lastVoiceWakeAt;

    /// <summary>判定闸门：攒最近 0.6 秒的人声分数，单块高分不算数。</summary>
    private readonly VoiceWakeGate _voiceWakeGate = new();

    /// <summary>
    /// 语音唤醒用的检测器。接上之后每 100 ms 判一次，连着判到「在说话」才触发
    /// <see cref="VoiceWakeDetected"/>；不接就完全不算，跟没加一样。
    /// </summary>
    public PeaceWpf.Core.AutoFlavor.SileroVoiceDetector? VoiceWake
    {
        get => _voiceWake;
        set
        {
            _voiceWake = value;
            _wakeCount = 0;
            _voiceWakeGate.Reset();
            value?.Reset();
        }
    }

    /// <summary>有人声。在采集线程上触发，订阅方要自己切回界面线程。</summary>
    public event EventHandler? VoiceWakeDetected;

    /// <summary>正在抓的时候是 true。</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _capture is not null;
            }
        }
    }

    /// <summary>最近一次失败的原因；正常为 null。</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// 自动风味的录音器。接上之后，每个数据块会额外喂它一份单声道采样——
    /// 复用同一个 WASAPI 客户端，不另开一路采集。
    /// </summary>
    private AutoFlavor.AutoFlavorRecorder? _autoFlavor;

    /// <summary>自动风味录制器；接上时立刻同步设备的真实采样率。</summary>
    public AutoFlavor.AutoFlavorRecorder? AutoFlavor
    {
        get => _autoFlavor;
        set
        {
            _autoFlavor = value;
            if (value is not null && CaptureSampleRate >= 8000)
            {
                value.SampleRate = CaptureSampleRate;
            }
        }
    }

    /// <summary>回环设备混音格式的采样率；没连上时是 0。</summary>
    public int CaptureSampleRate { get; private set; }

    /// <summary>
    /// 最近一个数据块的峰值（dB，0 是满幅）。
    /// 超过 300 ms 没有新数据就当作静音——设备空闲时 WASAPI 不一定会继续送块。
    /// </summary>
    public float PeakDb
    {
        get
        {
            if (Environment.TickCount64 - Interlocked.Read(ref _lastBlockAt) > 300)
            {
                return -90f;
            }

            return _blockPeakDb;
        }
    }

    /// <summary>开始抓。已经抓着就直接返回 true。失败返回 false，原因在 <see cref="LastError"/>。</summary>
    public bool Start()
    {
        lock (_gate)
        {
            if (_capture is not null)
            {
                return true;
            }

            if (_disposed)
            {
                LastError = "已经释放了。";
                return false;
            }

            WasapiLoopbackCapture? capture = null;
            try
            {
                capture = new WasapiLoopbackCapture();
                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;

                var format = capture.WaveFormat;
                _channels = Math.Max(1, format.Channels);
                _sampleRate = format.SampleRate;
                _floatSamples = format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32;
                _analyzer = new SpectrumAnalyzer(format.SampleRate);
                CaptureSampleRate = format.SampleRate;
                if (_autoFlavor is { } recorder)
                {
                    recorder.SampleRate = format.SampleRate;
                }

                capture.StartRecording();

                _capture = capture;
                _deviceId = TryGetDefaultDeviceId();
                LastError = null;

                _watchdog ??= new System.Threading.Timer(_ => Watch(), null, 2000, 2000);
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LastError = ex.Message;
                _analyzer = null;

                if (capture is not null)
                {
                    capture.DataAvailable -= OnDataAvailable;
                    capture.RecordingStopped -= OnRecordingStopped;

                    try
                    {
                        capture.Dispose();
                    }
                    catch (Exception)
                    {
                        // 关不掉就算了，反正不会再用了。
                    }
                }

                return false;
            }
        }
    }

    /// <summary>停止并释放采集。重复调用是安全的。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            StopLocked();
        }
    }

    /// <summary>取一帧频谱；没在抓的时候返回 null。</summary>
    public float[]? Snapshot()
    {
        SpectrumAnalyzer? analyzer;
        lock (_gate)
        {
            analyzer = _analyzer;
        }

        return analyzer?.Snapshot();
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
            _watchdog?.Dispose();
            _watchdog = null;
        }
    }

    private void StopLocked()
    {
        var capture = _capture;
        _capture = null;
        _analyzer = null;
        _blockPeakDb = -90f;
        Interlocked.Exchange(ref _lastBlockAt, 0);

        if (capture is null)
        {
            return;
        }

        // 先摘掉事件再停：不然 StopRecording 触发的 RecordingStopped 会被当成“设备掉了”去重连。
        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;

        try
        {
            capture.StopRecording();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastError = ex.Message;
        }
        finally
        {
            try
            {
                capture.Dispose();
            }
            catch (Exception)
            {
                // 释放不干净也不该把界面拖崩。
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var analyzer = _analyzer;
        if (analyzer is null || e.BytesRecorded <= 0)
        {
            return;
        }

        try
        {
            var bytes = e.Buffer.AsSpan(0, e.BytesRecorded);
            var channels = Math.Max(1, _channels);

            if (_floatSamples)
            {
                var samples = MemoryMarshal.Cast<byte, float>(bytes);
                var frames = samples.Length / channels;
                if (frames <= 0)
                {
                    return;
                }

                EnsureMonoBuffer(frames);
                for (var i = 0; i < frames; i++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                    {
                        sum += samples[(i * channels) + c];
                    }

                    _mono[i] = sum / channels;
                }

                UpdatePeak(_mono, frames);
                analyzer.Feed(_mono.AsSpan(0, frames));
                AutoFlavor?.Feed(_mono.AsSpan(0, frames));
                FeedVoiceWake(_mono.AsSpan(0, frames));
            }
            else
            {
                var samples = MemoryMarshal.Cast<byte, short>(bytes);
                var frames = samples.Length / channels;
                if (frames <= 0)
                {
                    return;
                }

                EnsureMonoBuffer(frames);
                for (var i = 0; i < frames; i++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                    {
                        sum += samples[(i * channels) + c] / 32768f;
                    }

                    _mono[i] = sum / channels;
                }

                UpdatePeak(_mono, frames);
                analyzer.Feed(_mono.AsSpan(0, frames));
                AutoFlavor?.Feed(_mono.AsSpan(0, frames));
                FeedVoiceWake(_mono.AsSpan(0, frames));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastError = ex.Message;
        }
    }

    /// <summary>
    /// 攒够约 100 ms 就降采样到 16 kHz 问一次检测器。没接检测器直接返回，不做任何计算。
    /// 这一步跑在采集线程上，但一次只有几毫秒（三窗推理），不会顶住音频。
    /// </summary>
    private void FeedVoiceWake(ReadOnlySpan<float> mono)
    {
        var detector = _voiceWake;
        if (detector is null)
        {
            return;
        }

        var frame = Math.Clamp(_sampleRate / 10, 160, _wakePending.Length);
        for (var i = 0; i < mono.Length; i++)
        {
            _wakePending[_wakeCount++] = mono[i];
            if (_wakeCount < frame)
            {
                continue;
            }

            var window = _wakePending.AsSpan(0, _wakeCount);
            _wakeCount = 0;
            var down = Dsp.Resampler.Downsample(window, _sampleRate, PeaceWpf.Core.AutoFlavor.SileroVoiceDetector.SampleRate);
            _voiceWakeGate.Push(detector.DetectScore(down));
            if (_voiceWakeGate.Confirmed)
            {
                RaiseVoiceWake();
            }
        }
    }

    /// <summary>3 秒内只报一次：一句话会连续命中很多个窗，没必要每次都触发切换。</summary>
    private void RaiseVoiceWake()
    {
        var now = Environment.TickCount64;
        if (now - _lastVoiceWakeAt < 3000)
        {
            return;
        }

        _lastVoiceWakeAt = now;
        VoiceWakeDetected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>取这一块的峰值；顺便记下时间，界面靠它判断是不是还在出声。</summary>
    private void UpdatePeak(float[] buffer, int count)
    {
        var peak = 0f;
        for (var i = 0; i < count; i++)
        {
            var value = Math.Abs(buffer[i]);
            if (value > peak)
            {
                peak = value;
            }
        }

        _blockPeakDb = peak > 1e-6f ? (float)(20 * Math.Log10(peak)) : -90f;
        Interlocked.Exchange(ref _lastBlockAt, Environment.TickCount64);
    }

    private void EnsureMonoBuffer(int frames)
    {
        if (_mono.Length < frames)
        {
            _mono = new float[frames];
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            LastError = e.Exception.Message;
        }

        bool shouldRetry;
        lock (_gate)
        {
            shouldRetry = !_disposed && ReferenceEquals(sender, _capture);
        }

        if (!shouldRetry)
        {
            return;
        }

        // 设备被拔掉或被独占：等两秒再试一次。再失败就安静地放着，界面只是没有频谱。
        _ = Task.Run(() =>
        {
            Thread.Sleep(2000);

            if (_disposed)
            {
                return;
            }

            Stop();
            Start();
        });
    }

    /// <summary>默认输出设备换了就重连。没换就什么都不做，静音时也不会被误判成掉了。</summary>
    private void Watch()
    {
        try
        {
            string? deviceId;
            lock (_gate)
            {
                if (_disposed || _capture is null)
                {
                    return;
                }

                deviceId = _deviceId;
            }

            var current = TryGetDefaultDeviceId();
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(current)
                || string.Equals(current, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Stop();
            Start();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastError = ex.Message;
        }
    }

    private static string? TryGetDefaultDeviceId()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
