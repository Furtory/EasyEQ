namespace PeaceWpf.Core.Dsp;

/// <summary>
/// 降到更低采样率的简单重采样。给 VAD 用：模型只吃 8 kHz / 16 kHz，回环抓的是 44.1/48 kHz。
///
/// 不追求高保真：整数倍用盒式平均（顺带压掉一部分高频混叠），非整数倍用线性插值。
/// 这两种在 16 kHz 以下的形状上都够用——VAD 只看语音带的包络，不看通带纹波。
/// </summary>
public static class Resampler
{
    /// <summary>从 <paramref name="fromRate"/> 降到 <paramref name="toRate"/>。</summary>
    public static float[] Downsample(ReadOnlySpan<float> input, int fromRate, int toRate)
    {
        if (input.Length == 0 || fromRate <= 0 || toRate <= 0 || toRate >= fromRate)
        {
            return input.ToArray();
        }

        if (fromRate % toRate == 0)
        {
            var factor = fromRate / toRate;
            var output = new float[input.Length / factor];
            for (var i = 0; i < output.Length; i++)
            {
                var sum = 0.0;
                var start = i * factor;
                for (var j = 0; j < factor && start + j < input.Length; j++)
                {
                    sum += input[start + j];
                }

                output[i] = (float)(sum / factor);
            }

            return output;
        }

        var ratio = (double)fromRate / toRate;
        var length = Math.Max(1, (int)(input.Length / ratio));
        var resampled = new float[length];
        for (var i = 0; i < resampled.Length; i++)
        {
            var position = i * ratio;
            var index = (int)position;
            var fraction = position - index;
            var a = input[Math.Min(index, input.Length - 1)];
            var b = input[Math.Min(index + 1, input.Length - 1)];
            resampled[i] = (float)(a + ((b - a) * fraction));
        }

        return resampled;
    }
}
