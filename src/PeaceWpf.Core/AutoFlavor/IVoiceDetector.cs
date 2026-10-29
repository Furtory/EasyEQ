namespace PeaceWpf.Core.AutoFlavor;

/// <summary>
/// 「这一小段里有没有人说话」的判定口。
///
/// 调用方一次喂一帧（100 ms）的 16 kHz 单声道采样，实现自己决定切多大的窗；
/// 有状态（模型有记忆），所以必须按顺序喂，不能跳。开始新一轮录制时 <see cref="Reset"/>。
/// </summary>
public interface IVoiceDetector
{
    /// <summary>喂一段 16 kHz 单声道，返回这一段里有没有人声。空输入返回 false。</summary>
    bool Detect(ReadOnlySpan<float> samples16k);

    /// <summary>清掉内部状态（记住的上下文）。每次开始录制前调一次。</summary>
    void Reset();
}

/// <summary>
/// 没有模型时的替身：永远说「没有」。
///
/// 三种情况用它：方向不是男声/女声、模型文件没打进包、ONNX 加载失败。
/// 结果是自动风味完全按曲线走，和没加这个功能时一样。
/// </summary>
public sealed class NullVoiceDetector : IVoiceDetector
{
    /// <summary>不用每次 new，它没有状态。</summary>
    public static NullVoiceDetector Instance { get; } = new();

    public bool Detect(ReadOnlySpan<float> samples16k) => false;

    public void Reset()
    {
    }
}
