namespace PeaceWpf.Core.Audio;

/// <summary>
/// 语音唤醒的判定闸门：每 100 ms 往这里塞一块「这段有多像人声」的分数，它回答「现在算不算有人在说话」。
///
/// 为什么不是一块高分就算：音乐从暂停里恢复、枪声、技能音效这些瞬态都会亮一下，
/// 单块 100 ms 的判定几乎没有信息量。真说话是连续的一串，而且模型分数压得比音乐片段高。
///
/// 判据三条，同时满足才算：
///   · 最近 <see cref="Window"/> 块里至少 <see cref="VoicedNeeded"/> 块过 <see cref="ChunkThreshold"/>；
///   · 最新一块过 <see cref="NowThreshold"/>；
///   · 这 <see cref="Window"/> 块里有一块到过 <see cref="PeakThreshold"/>。
///
/// 一句话大约 0.5 秒触发；音乐起播那种一下子的过不去。
/// 这些数字是估计值，真出现漏判/误判就调这里，别散到调用方。
/// </summary>
public sealed class VoiceWakeGate
{
    /// <summary>看最近多少块。一块 100 ms，6 块 = 0.6 秒。</summary>
    public const int Window = 6;

    /// <summary>这 6 块里至少要有几块像人声。</summary>
    public const int VoicedNeeded = 4;

    /// <summary>一块的分数到多少算「这块像人声」。</summary>
    public const double ChunkThreshold = 0.5;

    /// <summary>最新一块要过多少分，才算「这一刻有人说」。</summary>
    public const double NowThreshold = 0.7;

    /// <summary>这 0.6 秒里至少有一块到过多少分。真语音一般能压到 0.9 以上，音乐片段多在 0.6~0.75。</summary>
    public const double PeakThreshold = 0.8;

    private readonly double[] _scores = new double[Window];
    private int _head;
    private int _count;

    /// <summary>刚推进来的那块分数。</summary>
    public double LastScore => _count == 0 ? 0 : _scores[(_head - 1 + Window) % Window];

    /// <summary>攒够一整窗之前不判，免得开机头几块乱报。</summary>
    public bool IsReady => _count >= Window;

    /// <summary>推进一块 0..1 的分数。</summary>
    public void Push(double score)
    {
        _scores[_head] = double.IsFinite(score) ? Math.Clamp(score, 0, 1) : 0;
        _head = (_head + 1) % Window;
        if (_count < Window)
        {
            _count++;
        }
    }

    /// <summary>现在算不算有人在说话。</summary>
    public bool Confirmed
    {
        get
        {
            if (!IsReady || ScoreAt(0) < NowThreshold)
            {
                return false;
            }

            var voiced = 0;
            var peak = 0.0;
            for (var i = 0; i < Window; i++)
            {
                var score = ScoreAt(i);
                if (score >= ChunkThreshold)
                {
                    voiced++;
                }

                peak = Math.Max(peak, score);
            }

            return voiced >= VoicedNeeded && peak >= PeakThreshold;
        }
    }

    /// <summary>清空历史：换检测器、重新开采集时调。</summary>
    public void Reset()
    {
        Array.Clear(_scores);
        _head = 0;
        _count = 0;
    }

    private double ScoreAt(int back)
    {
        var index = (_head - 1 - back) % Window;
        return _scores[index < 0 ? index + Window : index];
    }
}
