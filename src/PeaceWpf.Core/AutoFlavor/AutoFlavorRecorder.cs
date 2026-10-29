namespace PeaceWpf.Core.AutoFlavor;

using System.Numerics;
using PeaceWpf.Core.Dsp;

/// <summary>
/// 自动风味的录音器：把回环采到的声音切成 100 ms 一帧，每帧存 120 条 1/12 倍频程的 dB 值。
///
/// 存包络不存波形：同样 1 GB，48 kHz 立体声裸波形只够 1.5 小时，包络能放五十多个小时。
/// 数据只在内存里，退出自动风味就清空——没有留在磁盘上的理由。
///
/// 这个类只管「采」和「存」，不做判断；怎么理解这批数据是 <see cref="AutoFlavorAnalyzer"/> 的事。
/// 分开是为了让自测不用声卡也能跑。
/// </summary>
public sealed class AutoFlavorRecorder
{
    /// <summary>频带数：20 Hz–20 kHz 按 1/12 倍频程切。</summary>
    public const int BandCount = 120;

    /// <summary>一帧的时长。事件时长也就按这个粒度算。</summary>
    public const int FrameMs = 100;

    /// <summary>FFT 点数，和显示用的频谱一致：48 kHz 上 5.9 Hz 一格。</summary>
    public const int FftSize = 8192;

    public const double MinFrequencyHz = 20;
    public const double MaxFrequencyHz = 20000;

    /// <summary>没有声音时的底噪，也是存值的下边界。</summary>
    public const double FloorDb = -90;

    /// <summary>峰值低于这个数的帧根本不算「有声音」，直接不存。</summary>
    public const double SilencePeakDb = -45;

    /// <summary>峰值高于这个数的帧已经削波失真，分析时整帧丢掉。</summary>
    public const double ClipPeakDb = -0.5;

    /// <summary>一块放多少帧。4096 帧＝约 1.97 MB，避免大对象堆碎片。</summary>
    private const int FramesPerChunk = 4096;

    /// <summary>默认内存上限 1 GB。</summary>
    public const long DefaultMaxBytes = 1L << 30;

    /// <summary>系统可用内存低于这个数就停，别把机器拖死。</summary>
    public const long ReserveBytes = 2L << 30;

    private int _sampleRate;
    private readonly double[] _window;
    private readonly double[] _ring;
    private readonly double[] _windowed;
    private readonly Complex[] _fft;
    private readonly float[] _bands;
    private int[] _windowStart;
    private double[][] _windowWeights;

    private readonly List<float[]> _chunks = new();
    private readonly List<long> _times = new();
    private readonly List<float> _peaks = new();

    /// <summary>每帧一位：这一帧里有没有人声。和 <see cref="_peaks"/> 一一对应。</summary>
    private readonly List<bool> _voice = new();

    private readonly object _gate = new();
    private int _write;
    private bool _filled;
    private int _count;
    private int _voiceCount;
    private long _lastMemoryCheckAt;
    private bool _limitReached;

    public AutoFlavorRecorder(int sampleRate = 48000)
    {
        if (sampleRate < 8000)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "采样率至少要 8 kHz。");
        }

        _sampleRate = sampleRate;
        _window = Audio.SpectrumAnalyzer.BuildHannWindow(FftSize);
        _ring = new double[FftSize];
        _windowed = new double[FftSize];
        _fft = new Complex[FftSize];
        _bands = new float[BandCount];
        _windowStart = new int[BandCount];
        _windowWeights = new double[BandCount][];
        BuildBandWindows(sampleRate, out _windowStart, out _windowWeights);
    }

    /// <summary>正在录（会往盘…不，往内存里加帧）。</summary>
    public bool IsRecording { get; private set; }

    /// <summary>
    /// 分析用的采样率。默认 48 kHz，接上回环设备后要设成设备的真实采样率——
    /// 不改的话 44.1k 的机器会让所有频段整体偏低约 8%，96k 的整体高一倍。
    /// 改它只重建频带窗口；环形缓冲里的旧数据会丢弃重来（混着算会错位）。
    /// </summary>
    public int SampleRate
    {
        get
        {
            lock (_gate)
            {
                return _sampleRate;
            }
        }

        set
        {
            if (value < 8000)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "采样率至少要 8 kHz。");
            }

            lock (_gate)
            {
                if (value == _sampleRate)
                {
                    return;
                }

                _sampleRate = value;
                BuildBandWindows(value, out _windowStart, out _windowWeights);
                _write = 0;
                _filled = false;
            }
        }
    }

    /// <summary>已经录到的帧数。</summary>
    public int FrameCount
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>当前占用的大致字节数（频带值 + 时间戳 + 峰值）。</summary>
    public long BytesUsed
    {
        get
        {
            lock (_gate)
            {
                return (long)_count * ((BandCount * sizeof(float)) + sizeof(long) + sizeof(float) + sizeof(bool));
            }
        }
    }

    /// <summary>内存上限，默认 1 GB。</summary>
    public long MaxBytes { get; set; } = DefaultMaxBytes;

    /// <summary>
    /// 系统可用内存的读数。界面接上之后每次检查都问一次；自测可以不设。
    /// 空着就只按 <see cref="MaxBytes"/> 判断。
    /// </summary>
    public Func<long>? AvailableBytes { get; set; }

    /// <summary>
    /// 人声判定器。接上之后每次取帧都顺带问一句「这 100 ms 里有没有人说话」，
    /// 结果记进帧标签；不接就全部记成「没有」，行为与加这个功能之前完全一样。
    /// </summary>
    public IVoiceDetector? VoiceDetector { get; set; }

    /// <summary>人声判定最后一次失败的原因；正常为 null，方便界面说明而不是静默失效。</summary>
    public string? VoiceError { get; private set; }

    /// <summary>已存的帧里有多少帧带人声。</summary>
    public int VoiceFrameCount
    {
        get
        {
            lock (_gate)
            {
                return _voiceCount;
            }
        }
    }

    /// <summary>碰到上限停下来了。界面读到它就把录制状态收掉并说明原因。</summary>
    public bool LimitReached
    {
        get
        {
            lock (_gate)
            {
                return _limitReached;
            }
        }
    }

    /// <summary>开始录。已经录着就什么都不做（继续录制不清数据）。</summary>
    public void Start() => IsRecording = true;

    /// <summary>暂停：停止加帧，但不丢已有数据。</summary>
    public void Pause() => IsRecording = false;

    /// <summary>清空：取消、应用之后调用。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            IsRecording = false;
            _chunks.Clear();
            _times.Clear();
            _peaks.Clear();
            _voice.Clear();
            _voiceCount = 0;
            _count = 0;
            _limitReached = false;
            VoiceDetector?.Reset();
        }
    }

    /// <summary>采集线程往这里灌单声道采样。多线程调用是安全的。</summary>
    public void Feed(ReadOnlySpan<float> mono)
    {
        lock (_gate)
        {
            foreach (var sample in mono)
            {
                _ring[_write] = sample;
                _write++;

                if (_write == FftSize)
                {
                    _write = 0;
                    _filled = true;
                }
            }
        }
    }

    /// <summary>
    /// 取一帧。界面每 100 ms 调一次；没在录、还没攒够样本、或者这一段太安静，都返回 false。
    /// </summary>
    /// <param name="timeMs">这一帧的时间戳（相对录制开始，调用方给）。</param>
    public bool CaptureFrame(long timeMs)
    {
        if (!IsRecording)
        {
            return false;
        }

        float[] bands;
        float peakDb;
        float[]? voiceSamples = null;
        var detector = VoiceDetector;
        lock (_gate)
        {
            if (!_filled)
            {
                return false;
            }

            // 峰值取原始样本：Hann 窗边缘会压到 0，拿加窗后的数据量峰值会低估最多 6 dB。
            var raw = new double[FftSize];
            for (var i = 0; i < FftSize; i++)
            {
                raw[i] = _ring[(_write + i) % FftSize];
            }

            peakDb = PeakOf(raw);
            for (var i = 0; i < FftSize; i++)
            {
                _windowed[i] = raw[i] * _window[i];
            }

            for (var i = 0; i < FftSize; i++)
            {
                _fft[i] = new Complex(_windowed[i], 0);
            }

            Audio.SpectrumAnalyzer.Fft(_fft);

            const double scale = FftSize / 4.0;
            for (var band = 0; band < BandCount; band++)
            {
                var start = _windowStart[band];
                var weights = _windowWeights[band];
                var energy = 0.0;

                for (var i = 0; i < weights.Length; i++)
                {
                    var magnitude = _fft[start + i].Magnitude / scale;
                    energy += magnitude * magnitude * weights[i];
                }

                _bands[band] = energy > 1e-18 ? (float)Math.Max(FloorDb, 10 * Math.Log10(energy)) : (float)FloorDb;
            }

            bands = (float[])_bands.Clone();

            // 给 VAD 的那一段：最近一帧的时长。静音帧也要喂——模型有时间记忆，
            // 跳帧会让它的上下文断掉；这里只是取出来，推理放到锁外。
            if (detector is not null)
            {
                var length = Math.Min(FftSize, Math.Max(1, (_sampleRate * FrameMs) / 1000));
                voiceSamples = new float[length];
                var start = (_write + FftSize - length) % FftSize;
                for (var i = 0; i < length; i++)
                {
                    voiceSamples[i] = (float)_ring[(start + i) % FftSize];
                }
            }
        }

        var voice = false;
        if (detector is not null && voiceSamples is not null)
        {
            try
            {
                var down = Resampler.Downsample(voiceSamples, _sampleRate, 16000);
                voice = detector.Detect(down);
                VoiceError = null;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // 附加功能出错不能把整次录制废掉：记下原因，这一帧按「没有人声」处理。
                VoiceError = ex.Message;
            }
        }

        if (peakDb < SilencePeakDb)
        {
            return false;
        }

        return AppendFrame(bands, peakDb, timeMs, voice);
    }

    /// <summary>存一帧。自测与回放也走这里：直接给 120 条 dB 值，不经过声卡。</summary>
    public bool AppendFrame(ReadOnlySpan<float> bands, float peakDb, long timeMs, bool voice = false)
    {
        if (bands.Length < BandCount || !double.IsFinite(peakDb))
        {
            return false;
        }

        lock (_gate)
        {
            if (WouldExceedLimitLocked())
            {
                _limitReached = true;
                IsRecording = false;
                return false;
            }

            var frame = _count;
            var chunk = frame / FramesPerChunk;
            while (_chunks.Count <= chunk)
            {
                _chunks.Add(new float[FramesPerChunk * BandCount]);
            }

            var offset = (frame % FramesPerChunk) * BandCount;
            var target = _chunks[chunk];
            for (var i = 0; i < BandCount; i++)
            {
                target[offset + i] = bands[i];
            }

            _times.Add(timeMs);
            _peaks.Add(peakDb);
            _voice.Add(voice);
            if (voice)
            {
                _voiceCount++;
            }

            _count++;
            return true;
        }
    }

    /// <summary>第 frame 帧、第 band 条带的值（dB）。越界返回底噪。</summary>
    public float BandAt(int frame, int band)
    {
        lock (_gate)
        {
            if (frame < 0 || frame >= _count || band < 0 || band >= BandCount)
            {
                return (float)FloorDb;
            }

            var chunk = _chunks[frame / FramesPerChunk];
            return chunk[((frame % FramesPerChunk) * BandCount) + band];
        }
    }

    /// <summary>第 frame 帧的峰值（dBFS）。</summary>
    public float PeakAt(int frame)
    {
        lock (_gate)
        {
            return frame >= 0 && frame < _count ? _peaks[frame] : (float)FloorDb;
        }
    }

    /// <summary>第 frame 帧有没有人声。越界返回 false。</summary>
    public bool VoiceAt(int frame)
    {
        lock (_gate)
        {
            return frame >= 0 && frame < _count && _voice[frame];
        }
    }

    /// <summary>第 frame 帧的时间戳（毫秒，调用方给的）。</summary>
    public long TimeAt(int frame)
    {
        lock (_gate)
        {
            return frame >= 0 && frame < _count ? _times[frame] : 0;
        }
    }

    /// <summary>第 index 条带的中心频率。</summary>
    public static double BandCenter(int index)
    {
        var t = (Math.Clamp(index, 0, BandCount - 1) + 0.5) / BandCount;
        return MinFrequencyHz * Math.Exp(t * Math.Log(MaxFrequencyHz / MinFrequencyHz));
    }

    /// <summary>某个频率落在第几条带（四舍五入）。</summary>
    public static int BandIndexFor(double frequencyHz)
    {
        if (!double.IsFinite(frequencyHz) || frequencyHz <= 0)
        {
            return 0;
        }

        var t = Math.Log(Math.Clamp(frequencyHz, MinFrequencyHz, MaxFrequencyHz) / MinFrequencyHz)
            / Math.Log(MaxFrequencyHz / MinFrequencyHz);
        return Math.Clamp((int)Math.Round((t * BandCount) - 0.5), 0, BandCount - 1);
    }

    private bool WouldExceedLimitLocked()
    {
        var next = (long)(_count + 1) * ((BandCount * sizeof(float)) + sizeof(long) + sizeof(float));
        if (next > MaxBytes)
        {
            return true;
        }

        if (AvailableBytes is null)
        {
            return false;
        }

        // 问系统可用内存不便宜，最多一秒一次。
        var now = Environment.TickCount64;
        if (now - _lastMemoryCheckAt < 1000)
        {
            return false;
        }

        _lastMemoryCheckAt = now;
        try
        {
            return AvailableBytes() < ReserveBytes;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static float PeakOf(double[] samples)
    {
        var peak = 0.0;
        foreach (var sample in samples)
        {
            var value = Math.Abs(sample);
            if (value > peak)
            {
                peak = value;
            }
        }

        return peak > 1e-6 ? (float)(20 * Math.Log10(peak)) : (float)FloorDb;
    }

    /// <summary>
    /// 1/12 倍频程的带窗：以带中心为顶点、半宽为 1/24 倍频程的三角窗，按峰值归一到 1。
    /// 低频的带比一格 FFT 还窄，所以半宽至少取一格，宁可几条带共用一格也不读到底噪。
    /// </summary>
    private static void BuildBandWindows(int sampleRate, out int[] starts, out double[][] weightsByBand)
    {
        var binHz = (double)sampleRate / FftSize;
        var maxBin = FftSize / 2;
        var halfWidthRatio = Math.Pow(2, (1.0 / 12.0) / 2) - 1;

        starts = new int[BandCount];
        weightsByBand = new double[BandCount][];

        for (var band = 0; band < BandCount; band++)
        {
            var center = BandCenter(band);
            var halfWidth = Math.Max(center * halfWidthRatio, binHz);

            var from = (int)Math.Floor((center - halfWidth) / binHz);
            var to = (int)Math.Ceiling((center + halfWidth) / binHz);
            from = Math.Clamp(from, 1, maxBin - 1);
            to = Math.Clamp(to, from + 1, maxBin);

            var weights = new double[to - from];
            for (var bin = from; bin < to; bin++)
            {
                var distance = Math.Abs((bin * binHz) - center);
                weights[bin - from] = Math.Max(0, 1 - (distance / halfWidth));
            }

            var peak = 0.0;
            foreach (var weight in weights)
            {
                peak = Math.Max(peak, weight);
            }

            if (peak > 0)
            {
                for (var i = 0; i < weights.Length; i++)
                {
                    weights[i] /= peak;
                }
            }
            else
            {
                weights[0] = 1;
            }

            starts[band] = from;
            weightsByBand[band] = weights;
        }
    }
}
