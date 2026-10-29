using System.Diagnostics;
using PeaceWpf.Core.Model;
using PeaceWpf.Core.Protocol;

namespace PeaceWpf.Core.Integration;

/// <summary>
/// 非独立模式下的「Peace 同步」：把当前滤波器按段推给正在运行的 Peace，
/// 让它的推子跟着动。
///
/// 为什么要推：Peace 不监视配置文件，只写文件它不会刷新界面；
/// 而它没有「重新加载」消息，只有推子级遥控，所以只能按段写。
///
/// 代价与边界：
///  - Peace 只显示前 31 段，超出的点只在文件里生效（会通过 <see cref="LastNote"/> 说出来）；
///  - 每条消息都是同步的 SendMessageTimeout，所以整个推送放在后台线程，
///    并且同一时刻只跑一轮，后续请求合并成「推最新的一份」；
///  - Peace 没在跑、窗口找不到、权限不一致——一律静默跳过，不影响本程序自己的写入。
/// </summary>
public sealed class PeaceSync
{
    /// <summary>
    /// Peace 里的专用混音配置。Peace 只有一条曲线，所以同步都写到这里，
    /// 不去动用户自己的 EsportsMain / EsportsBass。
    /// </summary>
    public const string MixPresetName = "EsportsMix.peace";

    /// <summary>EsportsMix 的配置 Id；Peace 切换配置时按它找文件。</summary>
    public const int MixPresetId = 99;

    private static readonly object LogGate = new();

    /// <summary>
    /// 同步日志：每次尝试、权限级别、配置 Id、段数、错误码都写一行。
    /// 没有它，失败只能靠猜（UIPI 拦消息时连异常都到不了界面）。
    /// 路径是 %LOCALAPPDATA%\EasyEQ\peace-sync.log。
    /// </summary>
    private static readonly string LogPath =
        Path.Combine(AppPaths.DataDirectory, "peace-sync.log");

    /// <summary>写一行同步日志；日志本身绝不抛异常。</summary>
    public static void Log(string message)
    {
        try
        {
            lock (LogGate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 512 * 1024)
                {
                    info.Delete();
                }

                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 日志写不进去就算了，别因为它影响同步或界面。
        }
    }

    /// <summary>Peace 的可执行名（64 位优先）。</summary>
    private static readonly string[] ExecutableNames = { "Peace64", "Peace" };

    private readonly object _gate = new();
    private (EqPoint[] Points, double PreAmpDb)? _pending;
    private bool _running;
    private bool _stopped;

    private DateTime _lastProcessCheckAt = DateTime.MinValue;
    private bool _lastProcessCheck;

    /// <summary>最近一次推送的补充说明（例如被截到 31 段）；没话说时为 null。</summary>
    public string? LastNote { get; private set; }

    /// <summary>最近一次真的推成功的时间；从没成功过是 null。</summary>
    public DateTime? LastPushedAt { get; private set; }

    /// <summary>
    /// 排一次同步。调用方不必等；连续调用只会保留最新的一份。
    /// 独立模式下不要调它。
    /// </summary>
    public void Queue(IReadOnlyList<EqPoint> points, double preAmpDb)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (_stopped)
        {
            return;
        }

        lock (_gate)
        {
            _pending = (points.ToArray(), preAmpDb);
            if (_running)
            {
                return;
            }

            _running = true;
        }

        _ = Task.Run(PumpAsync);
    }

    /// <summary>程序退出时叫停，别把后台线程留在这里。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _stopped = true;
            _pending = null;
        }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            (EqPoint[] Points, double PreAmpDb) job;
            lock (_gate)
            {
                if (_stopped || _pending is null)
                {
                    _running = false;
                    return;
                }

                job = _pending.Value;
                _pending = null;
            }

            if (IsPeaceRunning())
            {
                try
                {
                    Push(job.Points, job.PreAmpDb, out var note);
                    LastNote = note;
                    LastPushedAt = DateTime.UtcNow;
                }
                catch (PeaceBridgeException ex)
                {
                    // Peace 关窗口、切设置界面、权限不一致都会走到这里。
                    // 文件已经写好了，声音不受影响；这里只记日志，不打扰界面。
                    LastNote = ex.Message;
                    Log("推送异常：" + ex.Message);
                }
                catch (Exception ex)
                {
                    // 遥控是锦上添花，任何意外都不该冒到界面线程。
                    Log("推送意外：" + ex.GetType().Name + " " + ex.Message);
                }
            }

            // 拖动时写文件是 150 ms 一次，这里慢一点：既让 Peace 跟得上，
            // 又不至于把它的消息队列塞满。
            await Task.Delay(800).ConfigureAwait(false);
        }
    }

    private void Push(IReadOnlyList<EqPoint> points, double preAmpDb, out string? note)
    {
        note = null;

        var wanted = Math.Clamp(points.Count, PeaceConstants.MinBands, PeaceConstants.MaxBands);
        var elevated = IsSelfElevated();
        Log($"开始 points={points.Count} wanted={wanted} elevated={elevated}");

        var client = new PeaceClient(new Win32PeaceTransport { TimeoutMs = 600 });

        try
        {
            client.Attach();
        }
        catch (PeaceBridgeException ex)
        {
            Log("attach 失败：" + ex.Message);
            note = ex.Message;
            return;
        }

        bool ready;
        try
        {
            ready = client.IsReady();
        }
        catch (PeaceBridgeException ex)
        {
            Log("IsReady 失败：" + ex.Message);
            note = ex.Message;
            return;
        }

        if (!ready)
        {
            Log("Peace 未就绪（可能停在设置界面，或主循环没运行）");
            note = "Peace 未就绪，这次不同步。";
            return;
        }

        // 只推 Mix 配置：切不过去就不推，免得写到用户的其它预设上。
        if (!EnsureMixConfiguration(client, wanted, out var switchError))
        {
            Log("切换 Mix 失败：" + switchError);
            note = switchError;
            return;
        }

        int bands;
        try
        {
            bands = client.GetBandCount();
        }
        catch (PeaceBridgeException ex)
        {
            Log("读段数失败：" + ex.Message);
            bands = wanted;
        }

        if (bands <= 0)
        {
            bands = wanted;
        }

        Log($"段数={bands} 目标={wanted}");

        if (bands != wanted)
        {
            try
            {
                bands = client.EnsureBandCount(wanted);
                Log($"增减后段数={bands}");
            }
            catch (PeaceBridgeException ex)
            {
                Log("增减段数失败：" + ex.Message + "（按当前段数继续）");
            }

            if (bands <= 0)
            {
                bands = wanted;
            }
        }

        var limit = Math.Min(bands, PeaceConstants.PeaceDisplayBands);
        if (points.Count > limit)
        {
            note = $"Peace 只显示前 {limit} 段，超出的 {points.Count - limit} 段只在配置文件里生效。";
        }

        var visible = points.Count <= limit ? points : points.Take(limit).ToArray();

        try
        {
            client.WriteAllPoints(visible, bands);
            client.SetPreAmpDb(preAmpDb);
            Log($"写入完成 {visible.Count}/{bands}");
        }
        catch (PeaceBridgeException ex)
        {
            Log("写推子失败：" + ex.Message);
            note = ex.Message;
        }
    }

    /// <summary>当前进程是不是管理员，写日志用（权限不一致是最常见的静默失败原因）。</summary>
    private static bool IsSelfElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 保证 Peace 停在自己的混音配置上，而且段数是 <paramref name="wanted"/>。
    /// 已经在 Mix 上、段数也对，就什么都不做；否则重切一次配置，
    /// Peace 会按 EsportsMix.peace 里的频率段数一次性重算，跨多段也不会错位。
    /// </summary>
    private static bool EnsureMixConfiguration(PeaceClient client, int wanted, out string? error)
    {
        error = null;

        var configId = -999;
        try
        {
            configId = client.GetConfigurationId();
        }
        catch (PeaceBridgeException ex)
        {
            Log("读配置 Id 失败：" + ex.Message);
        }

        var onMix = configId == MixPresetId;
        Log($"configId={configId} onMix={onMix}");

        if (onMix)
        {
            try
            {
                if (client.GetBandCount() == wanted)
                {
                    return true;
                }
            }
            catch (PeaceBridgeException ex)
            {
                Log("段数读取失败：" + ex.Message);
            }
        }

        var switched = client.SwitchConfiguration(MixPresetId);
        if (switched.Failed)
        {
            error = $"Peace 里还没出现 {MixPresetName}，这次不同步；重启 Peace 后会重新接上。";
            return false;
        }

        if (client.GetConfigurationId() != MixPresetId)
        {
            error = $"Peace 没能切到 {MixPresetName}，这次不同步。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 把 Peace 的段数调整到 wanted。Peace 刚切完配置时会重建界面，
    /// 读段数可能失败，重试几轮；始终读不到就返回 0，这一轮不同步。
    /// </summary>
    private static int EnsureBandCountQuietly(PeaceClient client, int wanted)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return client.EnsureBandCount(wanted);
            }
            catch (PeaceBridgeException)
            {
                Thread.Sleep(400);
            }
        }

        return 0;
    }

    /// <summary>
    /// 进程在不在。枚举进程表有成本，缓存两秒——从没见过两秒内切 Peace 的场景，
    /// 但足以避免拖动滑块时每秒枚举六次。
    /// </summary>
    private bool IsPeaceRunning()
    {
        if ((DateTime.UtcNow - _lastProcessCheckAt).TotalSeconds < 2)
        {
            return _lastProcessCheck;
        }

        _lastProcessCheckAt = DateTime.UtcNow;

        foreach (var name in ExecutableNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length == 0)
                {
                    continue;
                }

                foreach (var process in processes)
                {
                    process.Dispose();
                }

                _lastProcessCheck = true;
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // 拿不到进程列表就当它没跑，不要因此打断同步之外的任何事。
            }
        }

        _lastProcessCheck = false;
        return false;
    }
}
