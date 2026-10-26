using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PeaceWpf.Core.Audio;

/// <summary>一个录音设备。</summary>
/// <param name="Id">设备 Id，重启后仍然有效，用它来记住上次选的设备。</param>
/// <param name="Name">给人看的名字。</param>
public sealed record CaptureDevice(string Id, string Name);

/// <summary>
/// 抓麦克风（WASAPI capture），把采样攒起来给扫频分析用。
///
/// 和 <see cref="LoopbackCaptureService"/> 是一对：那个抓系统输出画频谱，这个抓输入做校准。
/// 这里不做实时处理——校准只要一段完整录音，一次性分析比逐块处理简单得多。
/// </summary>
public sealed class MicrophoneCaptureService : IDisposable
{
    private readonly object _gate = new();
    private readonly List<float> _samples = new();

    private WasapiCapture? _capture;
    private float[] _mono = Array.Empty<float>();
    private int _channels = 1;
    private bool _floatSamples = true;
    private bool _disposed;

    /// <summary>录音采样率。没在录时是 0。</summary>
    public int SampleRate { get; private set; }

    /// <summary>正在录的时候是 true。</summary>
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

    /// <summary>当前设备名，界面上显示。</summary>
    public string? DeviceName { get; private set; }

    /// <summary>列出所有能用的录音设备。</summary>
    public static IReadOnlyList<CaptureDevice> ListDevices()
    {
        var result = new List<CaptureDevice>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                try
                {
                    result.Add(new CaptureDevice(device.ID, device.FriendlyName));
                }
                finally
                {
                    device.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 没有录音设备、或者音频服务没起来：返回空表，界面自己提示。
        }

        return result;
    }

    /// <summary>
    /// 开始录。deviceId 为 null 时用系统默认录音设备。
    /// </summary>
    public bool Start(string? deviceId)
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

            WasapiCapture? capture = null;
            try
            {
                var device = TryFindDevice(deviceId);
                capture = device is null ? new WasapiCapture() : new WasapiCapture(device);

                var format = capture.WaveFormat;
                _channels = Math.Max(1, format.Channels);
                _floatSamples = format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32;
                SampleRate = format.SampleRate;
                DeviceName = device?.FriendlyName ?? "默认录音设备";

                _samples.Clear();
                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;
                capture.StartRecording();

                _capture = capture;
                LastError = null;
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LastError = ex.Message;
                Cleanup(capture);
                return false;
            }
        }
    }

    /// <summary>停止录音。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            var capture = _capture;
            _capture = null;
            if (capture is null)
            {
                return;
            }

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
                Cleanup(capture);
            }
        }
    }

    /// <summary>
    /// 最近一段采样的电平（dBFS），只看不算、不清空缓冲。
    /// 界面用它显示「麦克风有没有在响」，不能用 <see cref="TakeSamples"/> 代替——
    /// 那个会把要分析的数据取走。
    ///
    /// 一个样本都没有时返回 null：那表示「还没开始送数据」，不是「安静」。
    /// 设备在推数据但全是 0（端点没接信号）才返回 -90，两者在界面上是两句话。
    /// </summary>
    public double? RecentLevelDb(int windowMilliseconds = 50)
    {
        lock (_gate)
        {
            if (_samples.Count == 0)
            {
                return null;
            }

            // 按时间算窗口，不按样本数：192 kHz 的声卡上固定 4800 个样本只有 25 ms，读数会乱跳。
            var window = Math.Max(1, (int)(Math.Max(1, SampleRate) * Math.Max(1, windowMilliseconds) / 1000.0));
            var start = Math.Max(0, _samples.Count - window);
            var sum = 0.0;
            for (var i = start; i < _samples.Count; i++)
            {
                var value = _samples[i];
                sum += value * value;
            }

            var rms = Math.Sqrt(sum / (_samples.Count - start));
            return rms > 1e-9 ? Math.Max(-90, 20 * Math.Log10(rms)) : -90;
        }
    }

    /// <summary>
    /// 系统默认录音设备的 Id；取不到就返回 null（调用方退回第一个设备）。
    /// </summary>
    public static string? DefaultDeviceId()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            return device.ID;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>已经录到多少个样本。实时曲线要按位置切段，不需要把缓冲整个复制出来。</summary>
    public int SampleCount
    {
        get
        {
            lock (_gate)
            {
                return _samples.Count;
            }
        }
    }

    /// <summary>
    /// 某个窗口的 RMS 电平（dBFS），直接从缓冲读，不复制。
    /// 实时曲线靠它一段一段地量，代价只有读那点样本。
    /// </summary>
    public double WindowRmsDb(int start, int length)
    {
        lock (_gate)
        {
            start = Math.Clamp(start, 0, _samples.Count);
            length = Math.Clamp(length, 0, _samples.Count - start);
            if (length <= 0)
            {
                return -140;
            }

            var sum = 0.0;
            for (var i = start; i < start + length; i++)
            {
                sum += _samples[i] * (double)_samples[i];
            }

            var rms = Math.Sqrt(sum / length);
            return rms > 1e-9 ? Math.Max(-140, 20 * Math.Log10(rms)) : -140;
        }
    }

    /// <summary>取走已经录到的全部采样（单声道），并把缓冲清空。</summary>
    public float[] TakeSamples()
    {
        lock (_gate)
        {
            var result = _samples.ToArray();
            _samples.Clear();
            return result;
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
            Stop();
        }
    }

    private static MMDevice? TryFindDevice(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            if (string.Equals(device.ID, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }

            device.Dispose();
        }

        return null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        try
        {
            var bytes = e.Buffer.AsSpan(0, e.BytesRecorded);
            var channels = Math.Max(1, _channels);

            if (_floatSamples)
            {
                var input = MemoryMarshal.Cast<byte, float>(bytes);
                AppendMono(input, channels);
            }
            else
            {
                var input = MemoryMarshal.Cast<byte, short>(bytes);
                AppendMono(input, channels);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastError = ex.Message;
        }
    }

    private void AppendMono(ReadOnlySpan<float> input, int channels)
    {
        var frames = input.Length / channels;
        if (frames <= 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_mono.Length < frames)
            {
                _mono = new float[frames];
            }

            for (var i = 0; i < frames; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    sum += input[(i * channels) + c];
                }

                _mono[i] = sum / channels;
            }

            _samples.AddRange(_mono.AsSpan(0, frames));
        }
    }

    private void AppendMono(ReadOnlySpan<short> input, int channels)
    {
        var frames = input.Length / channels;
        if (frames <= 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_mono.Length < frames)
            {
                _mono = new float[frames];
            }

            for (var i = 0; i < frames; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    sum += input[(i * channels) + c] / 32768f;
                }

                _mono[i] = sum / channels;
            }

            _samples.AddRange(_mono.AsSpan(0, frames));
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            LastError = e.Exception.Message;
        }
    }

    private static void Cleanup(WasapiCapture? capture)
    {
        if (capture is null)
        {
            return;
        }

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
