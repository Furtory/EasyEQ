using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.Protocol;

/// <summary>
/// 面向业务的高层封装：把"发哪个消息、wParam 用多少、单位怎么换算"藏起来。
/// 界面层只跟这个类打交道。
/// </summary>
public sealed class PeaceClient
{
    private readonly IPeaceTransport _transport;

    public PeaceClient(IPeaceTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public IPeaceTransport Transport => _transport;

    /// <summary>连接后端，失败抛异常。</summary>
    public void Attach()
    {
        if (!_transport.TryConnect())
        {
            throw new PeaceBridgeException(_transport.LastError ?? "连不上 Peace 后端。");
        }
    }

    /// <summary>后端是否处于可以接收遥控的状态。对应 WM_APP+1 的"查状态"。</summary>
    public bool IsReady()
    {
        try
        {
            var state = _transport.Send(PeaceMessages.Interface, 3, 0);
            return state is 1 or 2;
        }
        catch (PeaceBridgeException)
        {
            return false;
        }
    }

    /// <summary>Peace 当前配置的 Id；0 或负数表示没有 Id / 读不到。</summary>
    public int GetConfigurationId() => _transport.Send(PeaceMessages.Configuration, 5, 0);

    /// <summary>
    /// 按 Id 切换 Peace 的当前配置。Peace 只认 Id &gt; 0 的预设；
    /// 已经在该配置上时返回 -6（它按「没找到」处理），调用方当成幂等结果即可。
    /// </summary>
    public PeaceResult SwitchConfiguration(int id) =>
        new(_transport.Send(PeaceMessages.Configuration, 0, id));

    public PeaceResult ShowInterface() => new(_transport.Send(PeaceMessages.Interface, 0, 0));

    public PeaceResult HideInterface() => new(_transport.Send(PeaceMessages.Interface, 1, 0));

    /// <summary>后端报告的推子数量。</summary>
    public int GetBandCount()
    {
        var count = _transport.Send(PeaceMessages.EqualizationSliders, 13, 0);
        if (count is < PeaceConstants.MinBands or > PeaceConstants.MaxBands)
        {
            throw new PeaceBridgeException(
                $"后端报告的推子数量不合理：{count}（合法范围 {PeaceConstants.MinBands}–{PeaceConstants.MaxBands}）。");
        }

        return count;
    }

    /// <summary>读当前声道组合的 preamp，单位 dB。</summary>
    public double GetPreAmpDb() => _transport.Send(PeaceMessages.PreAmplifying, 5, 0) / 1000.0;

    /// <summary>设置 preamp，单位 dB。</summary>
    public PeaceResult SetPreAmpDb(double db) =>
        new(_transport.Send(PeaceMessages.PreAmplifying, 1, ToMilli(db)));

    /// <summary>读某段增益，单位 dB。</summary>
    public double GetGainDb(int band) =>
        _transport.Send(PeaceMessages.Slider(band), (int)SliderAction.GetGainMilliDb, 0) / 1000.0;

    /// <summary>写某段增益，单位 dB。用毫 dB 版本避免小数被截断。</summary>
    public PeaceResult SetGainDb(int band, double db) =>
        new(_transport.Send(PeaceMessages.Slider(band), (int)SliderAction.SetGainMilliDb, ToMilli(db)));

    /// <summary>读某段中心频率，单位 Hz。</summary>
    public double GetFrequencyHz(int band) =>
        _transport.Send(PeaceMessages.Slider(band), (int)SliderAction.GetFrequencyHz, 0);

    /// <summary>写某段中心频率，单位 Hz。</summary>
    public PeaceResult SetFrequencyHz(int band, double hz) =>
        new(_transport.Send(PeaceMessages.Slider(band), (int)SliderAction.SetFrequencyHz, (int)Math.Round(hz)));

    /// <summary>读某段 Q 值。</summary>
    public double GetQuality(int band) =>
        _transport.Send(PeaceMessages.Slider(band), (int)SliderAction.GetQualityMilli, 0) / 1000.0;

    /// <summary>写某段 Q 值。</summary>
    public PeaceResult SetQuality(int band, double q) =>
        new(_transport.Send(PeaceMessages.Slider(band), (int)SliderAction.SetQualityMilli, ToMilli(q)));

    /// <summary>读某段滤波器类型序号，0 表示无。</summary>
    public int GetFilterType(int band) =>
        _transport.Send(PeaceMessages.Slider(band), (int)SliderAction.GetFilterType, 0) - 1;

    /// <summary>写某段滤波器类型，lParam 用 1 基序号。</summary>
    public PeaceResult SetFilterType(int band, int filterType) =>
        new(_transport.Send(PeaceMessages.Slider(band), (int)SliderAction.SetFilterType, filterType + 1));

    /// <summary>读某段是否启用。后端返回 1=启用，2=停用。</summary>
    public bool GetEnabled(int band) =>
        _transport.Send(PeaceMessages.Slider(band), (int)SliderAction.GetEnabled, 0) == 1;

    /// <summary>启用某段。</summary>
    public PeaceResult EnableBand(int band) =>
        new(_transport.Send(PeaceMessages.Slider(band), (int)SliderAction.Enable, 0));

    /// <summary>停用某段。</summary>
    public PeaceResult DisableBand(int band) =>
        new(_transport.Send(PeaceMessages.Slider(band), (int)SliderAction.Disable, 0));

    /// <summary>切换某段启用状态。</summary>
    public PeaceResult ToggleBand(int band) =>
        new(_transport.Send(PeaceMessages.Slider(band), (int)SliderAction.ToggleEnabled, 0));

    /// <summary>系统主音量，0–100。</summary>
    public int GetMainVolume() => _transport.Send(PeaceMessages.MainRenderDevice, 2, 0);

    /// <summary>设置系统主音量，0–100。</summary>
    public PeaceResult SetMainVolume(int volume) =>
        new(_transport.Send(PeaceMessages.MainRenderDevice, 0, Math.Clamp(volume, 0, 100)));

    /// <summary>总体峰值，0–100。</summary>
    public int GetPeak() => _transport.Send(PeaceMessages.MainRenderDevice, 3, 0);

    /// <summary>某声道的峰值（1 基声道号），0–100。</summary>
    public int GetChannelPeak(int channel) =>
        _transport.Send(PeaceMessages.MainRenderDevice, 4, channel);

    /// <summary>
    /// 读低音增益（WM_APP+6 的 8）。返回 dB，范围 -20..20。
    /// 读操作不把负数当错误。
    /// </summary>
    public double GetBassGainDb() => _transport.Send(PeaceMessages.EffectsPanel, 8, 0);

    /// <summary>写低音增益（WM_APP+6 的 5，毫 dB）。后端会自己夹到 -20..20。</summary>
    public PeaceResult SetBassGainDb(double db) =>
        new(_transport.Send(PeaceMessages.EffectsPanel, 5, ToMilli(db)));

    /// <summary>
    /// 把后端的段数加到至少 target 个，返回加完之后的实际段数。
    ///
    /// 后端一次只能加一个段（WM_APP+22 的 14），而且加完只是把 $RestartGUI 置位；
    /// 真正的重建要等主窗口被显示时才发生 —— 所以隐藏状态下这里会短暂把后端窗口叫出来，
    /// 等段数真的变了再收回去。段数没变就停下来，不无限重试。
    /// </summary>
    /// <summary>界面现在是不是收在托盘里（状态 2）。</summary>
    public bool IsInterfaceHidden() => _transport.Send(PeaceMessages.Interface, 3, 0) == 2;

    /// <summary>
    /// 把后端段数调整到 target（7–31）：少了加、多了减，返回调整后的实际段数。
    ///
    /// 后端一次只改一个段（WM_APP+22 的 14 加 / 15 减），而且改完只是置了 RestartGUI；
    /// 真正的重建要等主窗口显示时才发生。所以需要变动时临时把窗口叫出来，
    /// 等段数真的变了再按原状收回去（本来就在托盘里才收）。
    /// </summary>
    public int EnsureBandCount(int target, TimeSpan? waitPerStep = null)
    {
        var wanted = Math.Clamp(target, PeaceConstants.MinBands, PeaceConstants.MaxBands);
        var count = GetBandCount();
        if (count == wanted)
        {
            return count;
        }

        var wait = waitPerStep ?? TimeSpan.FromSeconds(2);
        var wasHidden = IsInterfaceHidden();
        var shown = false;

        for (var attempt = 0; count != wanted && attempt < PeaceConstants.MaxBands; attempt++)
        {
            var action = count < wanted ? 14 : 15;
            var result = new PeaceResult(_transport.Send(PeaceMessages.EqualizationSliders, action, 0));

            // -5 表示已经顶到 7 / 31 的极限；其他负数按错误抛出去。
            if (result.Code == -5)
            {
                break;
            }

            if (result.Failed && result.Code != -4)
            {
                throw new PeaceBridgeException($"调整段数失败：{result.Describe()}");
            }

            // 加减命令只置 RestartGUI，窗口不显示就不会重建。
            if (!shown)
            {
                ShowInterface();
                shown = true;
            }

            var changed = WaitForBandCountChange(count, wait);
            if (changed == count)
            {
                break;
            }

            count = changed;
        }

        if (shown && wasHidden)
        {
            try
            {
                HideInterface();
            }
            catch (PeaceBridgeException)
            {
                // 收不回去不影响已经调好的段数。
            }
        }

        return count;
    }

    /// <summary>
    /// 全量写回点列表：前 M 个槽位按顺序写点，剩下的槽位统一写成 PK、0 dB，
    /// 保证上一次留下的增益不会继续发声。
    ///
    /// 每个点先开段、再写类型/频率/增益/Q。后端对“上一条还没处理完”会返回 -4，
    /// 所以每次写都带重试；连续失败才抛异常。
    /// </summary>
    public void WriteAllPoints(IReadOnlyList<EqPoint> points, int bandCount)
    {
        ArgumentNullException.ThrowIfNull(points);

        var total = Math.Clamp(Math.Max(bandCount, points.Count), PeaceConstants.MinBands, PeaceConstants.MaxBands);
        if (points.Count > total)
        {
            throw new PeaceBridgeException($"点太多：{points.Count} 个点，后端只有 {total} 个段槽位。");
        }

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var index = i;

            Write(() => EnableBand(index));
            Write(() => SetFilterType(index, point.FilterType));
            Write(() => SetFrequencyHz(index, point.FrequencyHz));
            Write(() => SetGainDb(index, point.GainDb));
            Write(() => SetQuality(index, point.Quality));
        }

        for (var i = points.Count; i < total; i++)
        {
            var index = i;
            Write(() => SetFilterType(index, PeaceConstants.PeakingFilter));
            Write(() => SetGainDb(index, 0));
        }
    }

    /// <summary>
    /// 一次性读出完整快照。
    /// 先确认后端就绪，避免把 -1/-2 之类的状态码当成数据。
    /// </summary>
    public PeaceSnapshot ReadSnapshot()
    {
        if (!IsReady())
        {
            throw new PeaceBridgeException("后端还没进入主界面，读不到参数。");
        }

        var count = GetBandCount();
        var bands = new List<SliderBand>(count);

        for (var i = 0; i < count; i++)
        {
            var gain = GetGainDb(i);
            var frequency = GetFrequencyHz(i);
            var quality = GetQuality(i);
            var filter = GetFilterType(i);
            var enabled = GetEnabled(i);

            bands.Add(new SliderBand(
                i,
                Clamp(frequency, PeaceConstants.MinFrequencyHz, PeaceConstants.MaxFrequencyHz),
                Clamp(gain, PeaceConstants.MinGainDb, PeaceConstants.MaxGainDb),
                quality,
                filter,
                enabled));
        }

        return new PeaceSnapshot(bands, GetPreAmpDb(), count);
    }

    /// <summary>
    /// 写一条消息。后端返回 -4 表示上一条遥控请求还没处理完，稍等重试；
    /// -1 / -2 是后端状态不对，重试没有意义，直接抛。
    /// </summary>
    private PeaceResult Write(Func<PeaceResult> action)
    {
        var result = default(PeaceResult);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            result = action();
            if (!result.Failed)
            {
                return result;
            }

            if (result.Code is -1 or -2)
            {
                throw new PeaceBridgeException($"后端现在写不进去：{result.Describe()}");
            }

            Thread.Sleep(40);
        }

        throw new PeaceBridgeException($"写入失败：{result.Describe()}");
    }

    /// <summary>轮询段数，直到它和 current 不同或者超时。</summary>
    private int WaitForBandCountChange(int current, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var latest = current;

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(120);
            try
            {
                latest = GetBandCount();
            }
            catch (PeaceBridgeException)
            {
                continue;
            }

            if (latest != current)
            {
                return latest;
            }
        }

        return latest;
    }

    private static int ToMilli(double value) => (int)Math.Round(value * 1000.0);

    private static double Clamp(double value, double min, double max) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : min;
}
