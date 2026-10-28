using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeaceWpf.Core.Model;

/// <summary>
/// 导出给用户的一份完整备份：界面状态（含两层曲线、预设名、顺序、平衡）加上几项
/// 不进 ui-state.json 的运行时配置（预增益、当前模式、校准用的录音设备）。
///
/// 只负责写。回读牵涉新旧版本迁移和合并顺序，先不做——真要做的时候得单独设计。
/// </summary>
public sealed class SettingsBackup
{
    /// <summary>文件名前缀。后面跟时间，见 <see cref="SuggestFileName"/>。</summary>
    public const string ExportPrefix = "EQ备份";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [JsonPropertyName("app")]
    public string App { get; init; } = AppPaths.Name;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("exportedAt")]
    public DateTimeOffset ExportedAt { get; init; }

    [JsonPropertyName("configDirectory")]
    public string? ConfigDirectory { get; init; }

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = string.Empty;

    [JsonPropertyName("preAmpDb")]
    public double PreAmpDb { get; init; }

    [JsonPropertyName("calibrateDevice")]
    public string? CalibrateDevice { get; init; }

    [JsonPropertyName("uiState")]
    public UiState UiState { get; init; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>读一份导出的备份；不是这个格式就返回 null，不抛。</summary>
    public static SettingsBackup? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SettingsBackup>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>「EQ备份 2026-10-25 01-36.json」——文件名里不能有冒号，用短横。</summary>
    public static string SuggestFileName(DateTimeOffset now) =>
        $"{ExportPrefix} {now:yyyy-MM-dd HH-mm}.json";
}
