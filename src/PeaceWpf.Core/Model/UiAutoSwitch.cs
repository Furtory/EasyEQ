using System.Text.Json.Serialization;

namespace PeaceWpf.Core.Model;

/// <summary>
/// 全局热键的修饰键。数值和 Win32 的 MOD_* 一致，注册时不用再换算。
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// 一组「自动切换」的组合：左右层用哪份预设、低音层用哪份、低音叠不叠加，
/// 外加可选的全局热键和一个前台进程绑定。
///
/// 为什么只存这两份预设名：增益和风味本来就跟左右预设走（<see cref="UiState.MainPreAmps"/>、
/// <see cref="UiState.MainFlavors"/>），切预设时它们会自己跟过来，这里再存一份只会对不上。
/// </summary>
public sealed class UiAutoSwitch
{
    /// <summary>稳定标识：界面里改行、删行、重排都不影响热键和进程绑定。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>左右层用的预设文件名（含 .peace）。</summary>
    [JsonPropertyName("mainPreset")]
    public string MainPreset { get; set; } = string.Empty;

    /// <summary>低音层用的预设文件名（含 .peace）。</summary>
    [JsonPropertyName("bassPreset")]
    public string BassPreset { get; set; } = string.Empty;

    /// <summary>这一组要不要把低音层叠加进左右；对应顶栏「叠加低音修正」。</summary>
    [JsonPropertyName("bassEnabled")]
    public bool BassEnabled { get; set; } = true;

    /// <summary>热键修饰键；<see cref="HotkeyVirtualKey"/> 为 0 时这一组没有热键。</summary>
    [JsonPropertyName("hotkeyModifiers")]
    public HotkeyModifiers HotkeyModifiers { get; set; }

    /// <summary>热键的 Win32 虚拟键码；0 表示不绑定。默认不绑定。</summary>
    [JsonPropertyName("hotkeyVirtualKey")]
    public int HotkeyVirtualKey { get; set; }

    /// <summary>绑定进程的进程名（不带 .exe）；null 表示不绑定。</summary>
    [JsonPropertyName("processName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessName { get; set; }

    [JsonIgnore]
    public bool HasHotkey => HotkeyVirtualKey > 0;

    [JsonIgnore]
    public bool HasProcess => !string.IsNullOrWhiteSpace(ProcessName);

    /// <summary>
    /// 「左右 + 低音 + 叠加」三元组。语音唤醒按这个认组合：
    /// 同三元组的多行（复制出来的、只差叠加开关的）算同一个，只能绑定一次。
    /// </summary>
    [JsonIgnore]
    public string ComboKey => MakeComboKey(MainPreset, BassPreset, BassEnabled);

    /// <summary>拼三元组键；比较时用小写，预设名大小写不该造成「两个组合」。</summary>
    public static string MakeComboKey(string? main, string? bass, bool bassEnabled) =>
        (main ?? string.Empty).Trim().ToLowerInvariant()
        + "|"
        + (bass ?? string.Empty).Trim().ToLowerInvariant()
        + "|"
        + (bassEnabled ? "1" : "0");

    /// <summary>撤销热键绑定。</summary>
    public void ClearHotkey()
    {
        HotkeyModifiers = HotkeyModifiers.None;
        HotkeyVirtualKey = 0;
    }

    /// <summary>撤销进程绑定。</summary>
    public void ClearProcess() => ProcessName = null;

    public UiAutoSwitch Clone() => new()
    {
        Id = Id,
        MainPreset = MainPreset,
        BassPreset = BassPreset,
        BassEnabled = BassEnabled,
        HotkeyModifiers = HotkeyModifiers,
        HotkeyVirtualKey = HotkeyVirtualKey,
        ProcessName = ProcessName,
    };

    /// <summary>
    /// 旧数据或手改过的 JSON 里可能出现空 id：补一个，免得热键和进程绑定找不到自己的行。
    /// </summary>
    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }
    }
}
