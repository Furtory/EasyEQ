using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PeaceWpf.Core.Audio;

/// <summary>
/// 播放扫频：一串步进正弦，从 20 Hz 一路走到 20 kHz。
///
/// 用的是 WASAPI 共享模式的默认输出设备，所以它和系统里正在放的音乐走同一条路，
/// 也就意味着 EqualizerAPO 的滤镜同样会作用在它身上——校准前必须先把当前层归平
/// （界面上那一步是自动的），不然测到的是「EQ 之后」的声音，补偿会叠两遍。
///
/// 信号幅度压得比较低（三分之一满幅以下）：校准是为了听清楚，不是为了响。
/// 每个频点淡入淡出几毫秒，避免切换频率时的「咔哒」被当成测量内容。
/// </summary>
public sealed class ToneSweepPlayer : IDisposable
{
    /// <summary>
    /// 输出幅度。0.5 约等于 -6 dBFS。
    ///
    /// 原来是 0.18（-15 dBFS），实测下来太轻：音箱放出来、麦克风隔一两米收，
    /// 每一段的电平只比底噪高几个 dB，分段的静音切口根本看不出来。
    /// 校准是为了让麦克风听清楚，不是为了小声。
    /// </summary>
    public const float Amplitude = 0.5f;

    /// <summary>测量期间把系统音量至少抬到这个值，测完还原。音量太小时放大幅度也白搭。</summary>
    private const float BoostVolume = 0.9f;

    private readonly object _gate = new();

    private WasapiOut? _output;
    private SweepSignalProvider? _provider;
    private MMDevice? _volumeDevice;
    private float? _savedVolume;
    private bool _disposed;
    private long _startedAt;

    /// <summary>正在播的时候是 true。</summary>
    public bool IsPlaying
    {
        get
        {
            lock (_gate)
            {
                return _output is not null && !(_provider?.IsFinished ?? true);
            }
        }
    }

    /// <summary>已经播了多久（毫秒）。没开始是 0。</summary>
    public double ElapsedMs
    {
        get
        {
            var start = Interlocked.Read(ref _startedAt);
            return start == 0 ? 0 : Environment.TickCount64 - start;
        }
    }

    /// <summary>总时长（毫秒）。</summary>
    private static double DefaultTotalMs => SweepPlan.TotalMs;

    /// <summary>最近一次失败的原因；正常为 null。</summary>
    public string? LastError { get; private set; }

    /// <summary>这次播放的总时长（毫秒）。没开始播放时是默认频点表的时长。</summary>
    public double DurationMs { get; private set; } = SweepPlan.TotalMs;

    /// <summary>开始播放（旧入口）。已经在播就直接返回 true。</summary>
    /// <param name="frequencies">要播哪些频点；null 用默认的 61 点表。</param>
    public bool Start(IReadOnlyList<double>? frequencies = null) =>
        Start(SweepSchedule.Build(frequencies ?? SweepPlan.Frequencies));

    /// <summary>
    /// 按一份时间表播放。分析端拿同一份时间表去分段，两边不会各算各的。
    /// </summary>
    /// <param name="schedule">频点和每块的时长。</param>
    public bool Start(SweepSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        lock (_gate)
        {
            if (_output is not null)
            {
                return true;
            }

            if (_disposed)
            {
                LastError = "已经释放了。";
                return false;
            }

            try
            {
                var provider = new SweepSignalProvider(schedule, SweepPlan.PlaybackSampleRate, Amplitude);

                DurationMs = schedule.TotalMs;

                var device = TryGetDefaultDevice();
                var output = device is null
                    ? new WasapiOut(AudioClientShareMode.Shared, 100)
                    : new WasapiOut(device, AudioClientShareMode.Shared, false, 100);

                RaiseVolume(device);
                output.Init(provider);
                output.Play();

                _provider = provider;
                _output = output;
                Interlocked.Exchange(ref _startedAt, Environment.TickCount64);
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
        _output = null;
        _provider = null;
        Interlocked.Exchange(ref _startedAt, 0);

        RestoreVolume();

        if (output is null)
        {
            return;
        }

        try
        {
            output.Stop();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LastError = ex.Message;
        }
        finally
        {
            try
            {
                output.Dispose();
            }
            catch (Exception)
            {
                // 释放不干净也不该把界面拖崩。
            }
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

    /// <summary>
    /// 测量期间把系统音量抬到至少 <see cref="BoostVolume"/>，并记住原值。
    /// 已经比它响就不动——不能把用户调好的音量往下压。
    /// 只放大信号没用：系统音量若是 30%，放大器出来的东西还是被乘掉。
    /// </summary>
    private void RaiseVolume(MMDevice? device)
    {
        _volumeDevice = device;
        _savedVolume = null;

        if (device?.AudioEndpointVolume is null)
        {
            return;
        }

        try
        {
            var current = device.AudioEndpointVolume.MasterVolumeLevelScalar;
            if (current >= BoostVolume)
            {
                return;
            }

            _savedVolume = current;
            device.AudioEndpointVolume.MasterVolumeLevelScalar = BoostVolume;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _savedVolume = null;
        }
    }

    private void RestoreVolume()
    {
        var device = _volumeDevice;
        var saved = _savedVolume;
        _volumeDevice = null;
        _savedVolume = null;

        if (device is null || saved is null)
        {
            return;
        }

        try
        {
            device.AudioEndpointVolume.MasterVolumeLevelScalar = saved.Value;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 设备被拔了或者已经释放，恢复不了就算了。
        }
    }

    /// <summary>
    /// 按样本位置生成信号的 provider。样本数自己数，所以「现在第几个频点」永远和声音对得上。
    ///
    /// 每个频点 = 响声 + 静音。静音不只是给播放缓冲留余量，它还是分析时切段的标记
    /// （见 <see cref="SweepAnalyzer"/>）——改它之前先看那边是怎么分段的。
    /// </summary>
    private sealed class SweepSignalProvider : IWaveProvider
    {
        private const int Channels = 2;
        private const int FadeMs = 8;

        private readonly double[] _frequencies;
        private readonly int[] _blockStarts;
        private readonly int[] _blockTones;
        private readonly int _fadeSamples;
        private readonly float _amplitude;
        private readonly int _rate;
        private readonly int _syncSamples;
        private readonly int _leadSamples;
        private readonly int _endSamples;

        private long _written;

        public SweepSignalProvider(SweepSchedule schedule, int sampleRate, float amplitude)
        {
            _rate = sampleRate;
            _frequencies = schedule.Blocks.Select(block => block.FrequencyHz).ToArray();
            _blockStarts = schedule.Blocks.Select(block => block.StartSample(sampleRate)).ToArray();
            _blockTones = schedule.Blocks.Select(block => block.ToneSamples(sampleRate)).ToArray();
            _fadeSamples = sampleRate * FadeMs / 1000;
            _amplitude = amplitude;
            _syncSamples = sampleRate * SweepPlan.SyncMs / 1000;
            _leadSamples = _syncSamples + (sampleRate * SweepPlan.SyncGapMs / 1000);
            _endSamples = _blockStarts.Length == 0 ? 0 : _blockStarts[^1] + _blockTones[^1];

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, Channels);
        }

        public WaveFormat WaveFormat { get; }

        /// <summary>所有频点都写完了。写完之后输出静音。</summary>
        public bool IsFinished =>
            Interlocked.Read(ref _written) >= (long)_leadSamples + _endSamples;

        public int Read(byte[] buffer, int offset, int count)
        {
            var blockAlign = WaveFormat.BlockAlign;
            var frames = count / blockAlign;
            if (frames <= 0)
            {
                return 0;
            }

            var samples = new float[frames * Channels];
            var written = Interlocked.Read(ref _written);

            for (var frame = 0; frame < frames; frame++)
            {
                var position = written + frame;
                var value = ValueAt(position);
                for (var channel = 0; channel < Channels; channel++)
                {
                    samples[(frame * Channels) + channel] = value;
                }
            }

            Interlocked.Add(ref _written, frames);

            Buffer.BlockCopy(samples, 0, buffer, offset, samples.Length * sizeof(float));
            return frames * blockAlign;
        }

        private float ValueAt(long samplePosition)
        {
            if (samplePosition < 0 || _endSamples <= 0)
            {
                return 0;
            }

            // 前导：先放同步音，再留一段静音，然后才是第一个频点。
            if (samplePosition < _leadSamples)
            {
                if (samplePosition >= _syncSamples)
                {
                    return 0;
                }

                var syncPhase = (int)samplePosition;
                var syncEnvelope = Envelope(syncPhase, _syncSamples);
                if (syncEnvelope <= 0)
                {
                    return 0;
                }

                var syncAngle = 2 * Math.PI * SweepPlan.SyncHz * syncPhase / _rate;
                return (float)(Math.Sin(syncAngle) * syncEnvelope * _amplitude);
            }

            var position = (int)(samplePosition - _leadSamples);
            if (position >= _endSamples)
            {
                return 0;
            }

            var block = FindBlock(position);
            var phase = position - _blockStarts[block];
            if (phase < 0 || phase >= _blockTones[block])
            {
                return 0;
            }

            var frequency = _frequencies[block];
            var envelope = Envelope(phase, _blockTones[block]);
            if (envelope <= 0)
            {
                return 0;
            }

            var angle = 2 * Math.PI * frequency * phase / _rate;
            return (float)(Math.Sin(angle) * envelope * _amplitude);
        }

        /// <summary>这个位置落在第几块。块长不再相等，只能二分。</summary>
        private int FindBlock(int position)
        {
            var low = 0;
            var high = _blockStarts.Length - 1;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (_blockStarts[middle] <= position)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        /// <summary>两头各淡 <see cref="FadeMs"/> 毫秒，切换频点时不会有咔哒声。</summary>
        private float Envelope(int phase, int length)
        {
            if (_fadeSamples <= 0 || length <= 0)
            {
                return 1;
            }

            // 块再短也不能让两头淡入淡出碰在一起。
            var fade = Math.Min(_fadeSamples, Math.Max(1, length / 2));
            if (phase < fade)
            {
                return (float)phase / fade;
            }

            var remaining = length - phase;
            if (remaining < fade)
            {
                return (float)remaining / fade;
            }

            return 1;
        }
    }
}
