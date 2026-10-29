using System.Text.Json;
using System.Text.Json.Serialization;
using PeaceWpf.Core.AutoFlavor;

namespace PeaceWpf.Core.Model;

/// <summary>
/// WPF 侧自己的界面状态，存成 JSON，不复用 Peace 的 ini。
///
/// 后端只有一套段槽位，两层 EQ 合并后写进去之后就分不开了，
/// 所以两层曲线、低音开关、左右平衡都必须在这里各存一份。
/// 音频参数仍然以后端为准：后端读回来的永远当作主层的最新状态。
/// </summary>
public sealed class UiState
{
    public const string FileName = "ui-state.json";

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Dark";

    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 1180;

    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 720;

    [JsonPropertyName("showGrid")]
    public bool ShowGrid { get; set; } = true;

    [JsonPropertyName("gainRangeDb")]
    public double GainRangeDb { get; set; } = 15;

    /// <summary>主层（“左右”曲线）。启动时先用它把界面画出来，连上后端后再以后端为准。</summary>
    [JsonPropertyName("mainPoints")]
    public List<UiPoint> MainPoints { get; set; } = new();

    /// <summary>低音层。后端读不回来，只能靠这里保存。</summary>
    [JsonPropertyName("bassPoints")]
    public List<UiPoint> BassPoints { get; set; } = new();

    /// <summary>低音层是否参与合并。默认开。</summary>
    [JsonPropertyName("bassLayerEnabled")]
    public bool BassLayerEnabled { get; set; } = true;

    /// <summary>左右平衡，-1（全左）到 1（全右），0 是居中。</summary>
    [JsonPropertyName("balance")]
    public double Balance { get; set; }

    /// <summary>曲线底部的实时频谱开关。抓不到音频时不报错，只是不显示。</summary>
    [JsonPropertyName("spectrumEnabled")]
    public bool SpectrumEnabled { get; set; } = true;

    /// <summary>无延迟防爆音开关：挂一个 ClipOnly3 在链路末尾，只在快破音时削峰。</summary>
    [JsonPropertyName("peakGuardEnabled")]
    public bool PeakGuardEnabled { get; set; }

    /// <summary>主层当前对应的 Peace 预设文件名。</summary>
    [JsonPropertyName("mainConfig")]
    public string MainConfig { get; set; } = "EsportsMain.peace";

    /// <summary>低音层当前对应的 Peace 预设文件名。</summary>
    [JsonPropertyName("bassConfig")]
    public string BassConfig { get; set; } = "EsportsBass.peace";

    /// <summary>
    /// 「左右」每个预设各自记住的预增益（dB），键是 .peace 文件名。
    ///
    /// 增益只属于左右这条曲线：低音层没有自己的增益，切到低音也不会动它。
    /// 不写进 .peace，是因为 Peace 的预设格式里没有这一项，硬塞一个它不认识的键
    /// 换来的只是文件更难解释；这个值只在软件自己的配置里跟着预设走。
    /// </summary>
    [JsonPropertyName("mainPreAmps")]
    public Dictionary<string, double> MainPreAmps { get; set; } = new();

    /// <summary>
    /// 「左右」每个预设各自记住的一套风味，键是 .peace 文件名。
    ///
    /// 风味是八条固定滤波器，叠加在左右曲线上，只进 peace.txt、不进 .peace：
    /// Peace 的预设格式没有这一项，硬塞只会让文件更难解释。
    /// 所以它和预增益一样，只在软件自己的配置里跟着预设走。
    /// </summary>
    [JsonPropertyName("mainFlavors")]
    public Dictionary<string, List<UiFlavor>> MainFlavors { get; set; } = new();

    /// <summary>自动风味选的方向。只记选择，不记录制结果。</summary>
    [JsonPropertyName("autoFlavorDirection")]
    public AutoFlavorDirection AutoFlavorDirection { get; set; } = AutoFlavorDirection.Competitive;

    /// <summary>自动风味的强度（0–1），默认 60%。</summary>
    [JsonPropertyName("autoFlavorStrength")]
    public double AutoFlavorStrength { get; set; } = 0.6;

    /// <summary>风味命名视角：false 用游戏名，true 用乐器名。旧档里的全局值，读不到按条目时兜底用。</summary>
    [JsonPropertyName("instrumentNaming")]
    public bool InstrumentNaming { get; set; }

    /// <summary>
    /// 「左右」每个预设各自的命名视角：跟着预设走，切预设就跟着换。
    /// 键是 .peace 文件名，和 mainFlavors / mainPreAmps 一样。
    /// </summary>
    [JsonPropertyName("mainInstrumentNaming")]
    public Dictionary<string, bool> MainInstrumentNaming { get; set; } = new();

    /// <summary>
    /// 下拉栏里预设的顺序，用户拖出来的。文件是按名字排的，这里记的是人排的；
    /// 不在这张表里的名字按文件名的本地化顺序补在后面。
    /// </summary>
    [JsonPropertyName("presetOrder")]
    public List<string> PresetOrder { get; set; } = new();

    /// <summary>
    /// 「自动切换」的组合：每行一组左右预设 + 低音预设 + 叠加开关 + 热键 + 进程绑定。
    /// 这个页面上的改动立即写盘，没有撤销；存这里才能跟着设置备份一起走。
    /// </summary>
    [JsonPropertyName("autoSwitchProfiles")]
    public List<UiAutoSwitch> AutoSwitchProfiles { get; set; } = new();

    /// <summary>
    /// 语音唤醒绑定的组合键，格式见 <see cref="UiAutoSwitch.MakeComboKey"/>。
    /// 全局只能有一个；null / 空表示没开。存三元组而不是行 id：
    /// 同三元组的多行（复制出来的、只差叠加开关的）算同一个组合，切换任一行都认。
    /// </summary>
    [JsonPropertyName("voiceWakeKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VoiceWakeKey { get; set; }

    /// <summary>
    /// 用户手动选的 EqualizerAPO config 目录。null 表示按默认位置找。
    /// 机器相关的路径，导入备份时不跟着走。
    /// </summary>
    [JsonPropertyName("configDirectoryOverride")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConfigDirectoryOverride { get; set; }

    /// <summary>
    /// 自动EQ 上次导入的麦克风矫正曲线文件。null 表示不矫正。
    /// 机器相关的路径，和 <see cref="ConfigDirectoryOverride"/> 一样：导入备份时不跟着走。
    /// </summary>
    [JsonPropertyName("micCalibrationFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MicCalibrationFile { get; set; }

    /// <summary>开机自启时是否把主界面显示出来；false 就是最小化启动。</summary>
    [JsonPropertyName("showWindowOnStart")]
    public bool ShowWindowOnStart { get; set; } = true;

    /// <summary>
    /// 组合切换时是否在主屏顶部滑下一条曲线通知（软件不在前台才弹）。
    /// 默认开：切了组合却看不见，最容易让人以为「没生效」。
    /// </summary>
    [JsonPropertyName("showSwitchOsd")]
    public bool ShowSwitchOsd { get; set; } = true;

    /// <summary>
    /// 点关闭按钮时做什么：默认收进托盘，程序继续在后台跑。
    /// 只有用户明确记住过选择才用这个值；没记住时每次关闭都问。
    /// </summary>
    [JsonPropertyName("closeAction")]
    [JsonConverter(typeof(JsonStringEnumConverter<CloseAction>))]
    public CloseAction CloseAction { get; set; } = CloseAction.MinimizeToTray;

    /// <summary>
    /// 关闭行为是不是用户记住过的。false 表示还是默认值（问过但只选了「仅这一次」，
    /// 或者根本没问过）；下次关闭要继续问。
    /// </summary>
    [JsonPropertyName("closeActionRemembered")]
    public bool CloseActionRemembered { get; set; }

    /// <summary>
    /// 独立模式：打开时本程序写自己的 easyeq.txt，Peace 写它的 peace.txt，互不覆盖；
    /// 关闭（默认）时写 peace.txt，并把推子同步给正在运行的 Peace。
    /// </summary>
    [JsonPropertyName("independentMode")]
    public bool IndependentMode { get; set; }

    /// <summary>旧版本只有一条曲线，读进来之后升级成主层；写回时不再输出这个字段。</summary>
    [JsonPropertyName("points")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UiPoint>? LegacyPoints { get; set; }

    /// <summary>所在目录：%LOCALAPPDATA%\EasyEQ。</summary>
    public static string Directory => AppPaths.DataDirectory;

    public static string FilePath => System.IO.Path.Combine(Directory, FileName);

    /// <summary>
    /// 把另一份状态整体抄进来（导入备份用）。字段级拷贝，换的是内容不是对象，
    /// 因为 MainViewModel 里拿着这个实例的引用，换对象会把正在跑的东西甩掉。
    /// </summary>
    public void CopyFrom(UiState other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Theme = other.Theme;
        WindowWidth = other.WindowWidth;
        WindowHeight = other.WindowHeight;
        ShowGrid = other.ShowGrid;
        GainRangeDb = other.GainRangeDb;
        MainPoints = other.MainPoints?.ToList() ?? new List<UiPoint>();
        BassPoints = other.BassPoints?.ToList() ?? new List<UiPoint>();
        BassLayerEnabled = other.BassLayerEnabled;
        Balance = other.Balance;
        SpectrumEnabled = other.SpectrumEnabled;
        PeakGuardEnabled = other.PeakGuardEnabled;
        MainConfig = other.MainConfig;
        BassConfig = other.BassConfig;
        MainPreAmps = other.MainPreAmps is null
            ? new Dictionary<string, double>()
            : new Dictionary<string, double>(other.MainPreAmps);
        MainFlavors = other.MainFlavors is null
            ? new Dictionary<string, List<UiFlavor>>()
            : other.MainFlavors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.Select(item => item.Clone()).ToList() ?? new List<UiFlavor>());
        PresetOrder = other.PresetOrder?.ToList() ?? new List<string>();
        AutoFlavorDirection = other.AutoFlavorDirection;
        AutoFlavorStrength = other.AutoFlavorStrength;
        InstrumentNaming = other.InstrumentNaming;
        MainInstrumentNaming = other.MainInstrumentNaming is null
            ? new Dictionary<string, bool>()
            : new Dictionary<string, bool>(other.MainInstrumentNaming);
        AutoSwitchProfiles = other.AutoSwitchProfiles is null
            ? new List<UiAutoSwitch>()
            : other.AutoSwitchProfiles
                .Where(item => item is not null)
                .Select(item => item.Clone())
                .ToList();
        VoiceWakeKey = other.VoiceWakeKey;
        IndependentMode = other.IndependentMode;
        ShowSwitchOsd = other.ShowSwitchOsd;
        CloseAction = other.CloseAction;
        CloseActionRemembered = other.CloseActionRemembered;
        LegacyPoints = null;
    }

    /// <summary>取某个左右预设记住的预增益；这个预设还没记过就返回 false。</summary>
    public bool TryGetMainPreAmp(string preset, out double preAmpDb)
    {
        preAmpDb = 0;
        if (MainPreAmps is null || string.IsNullOrWhiteSpace(preset))
        {
            return false;
        }

        if (MainPreAmps.TryGetValue(preset, out preAmpDb))
        {
            return true;
        }

        // 文件名在 Windows 上不分大小写，词典却分；找不到精确键时补一遍忽略大小写的查找。
        foreach (var pair in MainPreAmps)
        {
            if (string.Equals(pair.Key, preset, StringComparison.OrdinalIgnoreCase))
            {
                preAmpDb = pair.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>记下某个左右预设的预增益；同一个预设不区分大小写只留一份。</summary>
    public void SetMainPreAmp(string preset, double preAmpDb)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        MainPreAmps ??= new Dictionary<string, double>();
        foreach (var key in MainPreAmps.Keys
                     .Where(key => !string.Equals(key, preset, StringComparison.Ordinal)
                         && string.Equals(key, preset, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            MainPreAmps.Remove(key);
        }

        MainPreAmps[preset] = preAmpDb;
    }

    /// <summary>
    /// 取某个左右预设记住的风味。没记过、或者条目读不出来时返回 false，
    /// 由调用方决定用默认一套（八条全平）——不要在这里悄悄造一份带增益的。
    /// </summary>
    public bool TryGetMainFlavors(string preset, out FlavorSet flavors)
    {
        flavors = FlavorSet.CreateDefault();
        if (MainFlavors is null || string.IsNullOrWhiteSpace(preset))
        {
            return false;
        }

        List<UiFlavor>? stored = null;
        if (MainFlavors.TryGetValue(preset, out var exact))
        {
            stored = exact;
        }
        else
        {
            // 文件名在 Windows 上不分大小写，词典却分；补一遍忽略大小写的查找。
            foreach (var pair in MainFlavors)
            {
                if (string.Equals(pair.Key, preset, StringComparison.OrdinalIgnoreCase))
                {
                    stored = pair.Value;
                    break;
                }
            }
        }

        if (stored is null)
        {
            return false;
        }

        flavors = FlavorSet.FromBands(stored
            .Where(item => item is not null)
            .Select(item => new FlavorBand(item.Key, item.FrequencyHz, item.GainDb, item.Quality, item.FilterType)));
        return true;
    }

    /// <summary>记下某个左右预设的风味；同一个预设不区分大小写只留一份。</summary>
    public void SetMainFlavors(string preset, FlavorSet flavors)
    {
        ArgumentNullException.ThrowIfNull(flavors);
        if (string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        MainFlavors ??= new Dictionary<string, List<UiFlavor>>();
        foreach (var key in MainFlavors.Keys
                     .Where(key => !string.Equals(key, preset, StringComparison.Ordinal)
                         && string.Equals(key, preset, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            MainFlavors.Remove(key);
        }

        MainFlavors[preset] = flavors.Bands
            .Select(band => new UiFlavor
            {
                Key = band.Key,
                FrequencyHz = band.FrequencyHz,
                GainDb = band.GainDb,
                Quality = band.Quality,
                FilterType = band.FilterType,
            })
            .ToList();
    }

    /// <summary>
    /// 取某个左右预设的命名视角（false = 游戏，true = 乐器）。
    /// 没记过时退回旧档的全局值，再没有就是游戏视角。
    /// </summary>
    public bool GetInstrumentNaming(string preset)
    {
        if (MainInstrumentNaming is not null && !string.IsNullOrWhiteSpace(preset))
        {
            if (MainInstrumentNaming.TryGetValue(preset, out var exact))
            {
                return exact;
            }

            foreach (var pair in MainInstrumentNaming)
            {
                if (string.Equals(pair.Key, preset, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }
        }

        return InstrumentNaming;
    }

    /// <summary>记下某个左右预设的命名视角；同一个预设不区分大小写只留一份。</summary>
    public void SetInstrumentNaming(string preset, bool instrument)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        MainInstrumentNaming ??= new Dictionary<string, bool>();
        foreach (var key in MainInstrumentNaming.Keys
                     .Where(key => !string.Equals(key, preset, StringComparison.Ordinal)
                         && string.Equals(key, preset, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            MainInstrumentNaming.Remove(key);
        }

        MainInstrumentNaming[preset] = instrument;
    }

    /// <summary>把缓存的两层读出来；一份都没有时返回 null。</summary>
    public EqLayerSet? ToLayerSet()
    {
        var mainSource = MainPoints is { Count: > 0 } ? MainPoints : LegacyPoints;
        var main = mainSource is { Count: > 0 } ? ToList(mainSource) : null;
        var bass = BassPoints is { Count: > 0 } ? ToList(BassPoints) : null;

        if (main is null && bass is null)
        {
            return null;
        }

        return new EqLayerSet(main ?? EqPointList.CreateDefault(), bass ?? EqPointList.CreateDefault());
    }

    /// <summary>把两层写进缓存。只存四个数值，Id 是会话内的东西，不落盘。</summary>
    public void SetLayers(EqLayerSet layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        MainPoints = ToUiPoints(layers.Main);
        BassPoints = ToUiPoints(layers.Bass);
        LegacyPoints = null;
    }

    private static EqPointList ToList(IEnumerable<UiPoint> points) =>
        EqPointList.FromPoints(points.Select(point => new EqPoint(
            0,
            point.FrequencyHz,
            point.GainDb,
            point.Quality,
            point.Endpoint,
            EqLimits.ClampFilter(point.FilterType))));

    private static List<UiPoint> ToUiPoints(EqPointList list) =>
        list.Points.Select(point => new UiPoint
        {
            FrequencyHz = point.FrequencyHz,
            GainDb = point.GainDb,
            Quality = point.Quality,
            Endpoint = point.IsEndpoint,
            FilterType = point.FilterType,
        }).ToList();
}

/// <summary>落盘用的点。故意不和 EqPoint 共用一个类型：文件格式要能单独演进。</summary>
public sealed class UiPoint
{
    [JsonPropertyName("frequencyHz")]
    public double FrequencyHz { get; set; }

    [JsonPropertyName("gainDb")]
    public double GainDb { get; set; }

    [JsonPropertyName("quality")]
    public double Quality { get; set; } = 1.41;

    [JsonPropertyName("endpoint")]
    public bool Endpoint { get; set; }

    /// <summary>滤波器类型序号；0 是 PK。旧版本没有这个字段，读进来就是 0。</summary>
    [JsonPropertyName("filterType")]
    public int FilterType { get; set; }
}

/// <summary>落盘用的风味条目。和 <see cref="FlavorBand"/> 一样故意分开：文件格式要能单独演进。</summary>
public sealed class UiFlavor
{
    /// <summary>对应 <see cref="ToneBands"/> 里的 Key；靠它认人，不靠下标。</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("frequencyHz")]
    public double FrequencyHz { get; set; }

    [JsonPropertyName("gainDb")]
    public double GainDb { get; set; }

    [JsonPropertyName("quality")]
    public double Quality { get; set; } = 1.41;

    /// <summary>滤波器类型序号；0 是 PK。</summary>
    [JsonPropertyName("filterType")]
    public int FilterType { get; set; }

    /// <summary>导入备份时深拷用。</summary>
    public UiFlavor Clone() => new()
    {
        Key = Key,
        FrequencyHz = FrequencyHz,
        GainDb = GainDb,
        Quality = Quality,
        FilterType = FilterType,
    };
}

/// <summary>点窗口关闭按钮时的行为。</summary>
public enum CloseAction
{
    /// <summary>收进托盘，程序继续在后台跑。默认。</summary>
    MinimizeToTray = 0,

    /// <summary>直接退出软件。</summary>
    Exit = 1,
}

/// <summary>界面状态的读写，出错时静默回退到默认值。</summary>
public static class UiStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static UiState Load()
    {
        try
        {
            if (!File.Exists(UiState.FilePath))
            {
                return new UiState();
            }

            var json = File.ReadAllText(UiState.FilePath);
            return JsonSerializer.Deserialize<UiState>(json) ?? new UiState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UiState();
        }
    }

    public static void Save(UiState state)
    {
        try
        {
            System.IO.Directory.CreateDirectory(UiState.Directory);
            var json = JsonSerializer.Serialize(state, Options);
            File.WriteAllText(UiState.FilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 界面状态存不下来不该影响正在调音。
            System.Diagnostics.Debug.WriteLine($"UiState 保存失败：{ex.Message}");
        }
    }
}
