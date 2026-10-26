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
    private string? _deviceId;
    private bool _disposed;
    private float _blockPeakDb = -90f;
    private long _lastBlockAt;

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
                _floatSamples = format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32;
                _analyzer = new SpectrumAnalyzer(format.SampleRate);

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
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastError = ex.Message;
        }
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
