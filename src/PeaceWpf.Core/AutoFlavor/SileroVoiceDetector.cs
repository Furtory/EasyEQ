namespace PeaceWpf.Core.AutoFlavor;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

/// <summary>
/// Silero VAD 的 ONNX 实现：判断一小段 16 kHz 单声道里有没有人说话。
///
/// 模型 2.3 MB，窗口 512 样本（32 ms），单线程一窗不到 1 ms。
/// 有状态：模型内部带一层记忆，必须按顺序喂；<see cref="Reset"/> 回到初始。
///
/// 输入输出已按模型实测对齐：input(1,512) + state(2,1,128) + sr(标量) → output(1,1) + stateN。
/// output 超过 <see cref="SpeechThreshold"/> 就算这一段有人声。
/// </summary>
public sealed class SileroVoiceDetector : IVoiceDetector, IDisposable
{
    /// <summary>模型要的采样率。</summary>
    public const int SampleRate = 16000;

    /// <summary>一窗多少样本：16 kHz 上 32 ms。</summary>
    public const int WindowSize = 512;

    /// <summary>
    /// 模型输入其实是「上一窗的尾巴 64 个 + 这一窗 512 个」= 576。
    /// 官方 OnnxWrapper 每次把上一窗最后 <c>context_size</c> 个样本拼在前面再喂；
    /// 直接喂 512 不会报错，但内部少一段上下文，输出会恒在 0 附近。
    /// </summary>
    public const int ContextSize = 64;

    /// <summary>判「有人声」的门槛。</summary>
    public const double SpeechThreshold = 0.5;

    private readonly InferenceSession _session;
    private readonly DenseTensor<long> _sampleRateTensor;
    private readonly object _gate = new();

    private DenseTensor<float> _state = NewState();
    private readonly float[] _context = new float[ContextSize];
    private bool _disposed;

    /// <summary>加载模型。路径不存在或模型损坏会抛异常，调用方负责退回 <see cref="NullVoiceDetector"/>。</summary>
    public SileroVoiceDetector(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        _session = new InferenceSession(modelPath);

        // sr 是 0 维标量；DenseTensor 用空维度数组构造，索引也用空数组。
        _sampleRateTensor = new DenseTensor<long>(Array.Empty<int>());
        _sampleRateTensor[Array.Empty<int>()] = SampleRate;
    }

    public bool Detect(ReadOnlySpan<float> samples16k) => DetectScore(samples16k) >= SpeechThreshold;

    /// <summary>
    /// 喂一段 16 kHz 单声道，返回这一段里最像人声的一小窗的分数（0..1）。
    ///
    /// 语音唤醒要的是分数而不是「有没有」：音乐起播这类瞬态会亮一下 0.5，
    /// 但很少能连着几百毫秒都高。拿分数才能做「持续多久、最高多少」这类判据。
    /// </summary>
    public double DetectScore(ReadOnlySpan<float> samples16k)
    {
        if (samples16k.Length == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var best = 0.0;
            for (var offset = 0; offset < samples16k.Length; offset += WindowSize)
            {
                var count = Math.Min(WindowSize, samples16k.Length - offset);
                if (count < WindowSize / 2)
                {
                    // 尾巴太短就不喂：模型按整窗训练，硬补零反而容易误判。
                    break;
                }

                var window = new float[WindowSize];
                for (var i = 0; i < count; i++)
                {
                    window[i] = samples16k[offset + i];
                }

                best = Math.Max(best, RunWindow(window));
            }

            return best;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _state = NewState();
            Array.Clear(_context);
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
            _session.Dispose();
        }
    }

    private double RunWindow(ReadOnlySpan<float> window)
    {
        // 官方做法：上一窗尾巴 64 + 这一窗 512 = 576。
        var input = new DenseTensor<float>(new[] { 1, ContextSize + WindowSize });
        for (var i = 0; i < ContextSize; i++)
        {
            input[0, i] = _context[i];
        }

        for (var i = 0; i < WindowSize; i++)
        {
            input[0, ContextSize + i] = window[i];
        }

        using var results = _session.Run(new[]
        {
            NamedOnnxValue.CreateFromTensor("input", input),
            NamedOnnxValue.CreateFromTensor("state", _state),
            NamedOnnxValue.CreateFromTensor("sr", _sampleRateTensor),
        });

        var speech = 0.0;
        foreach (var result in results)
        {
            if (result.Name == "output")
            {
                speech = result.AsTensor<float>()[0, 0];
            }
            else if (result.Name == "stateN")
            {
                // 复制出来：result 释放后底下的内存就没了。
                _state = result.AsTensor<float>().ToDenseTensor();
            }
        }

        // 下一次的上下文就是这次拼好输入的最后 64 个，正好是这一窗的尾巴。
        for (var i = 0; i < ContextSize; i++)
        {
            _context[i] = input[0, WindowSize + i];
        }

        return speech;
    }

    private static DenseTensor<float> NewState() => new(new[] { 2, 1, 128 });
}
