namespace PeaceWpf.Core.AutoEq;

/// <summary>一个随程序分发的 AutoEQ 结果库。</summary>
/// <param name="Key">稳定标识，用来记住用户上次选了哪个库。</param>
/// <param name="DisplayName">下拉框里的名字。</param>
/// <param name="FileName">数据文件名。</param>
public sealed record AutoEqSource(string Key, string DisplayName, string FileName);

/// <summary>
/// AutoEQ 结果库的位置与懒加载。
///
/// 数据来自 Peace 自带的 <c>AutoEQCompressed5.7z</c>，解开后是 4 份文本，跟着程序放在
/// <c>AutoEqData</c> 目录里。这里只负责找文件、读一次、缓存住。
/// </summary>
public static class AutoEqCatalog
{
    /// <summary>指到别处的数据目录时用的环境变量（自测和开发时用）。</summary>
    public const string DataDirectoryVariable = "PEACEWPF_AUTOEQ_DIR";

    /// <summary>跟着程序发布的相对目录。</summary>
    public const string DataFolderName = "AutoEqData";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, AutoEqDatabase> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Peace 里那四个库，顺序一致。</summary>
    public static IReadOnlyList<AutoEqSource> Sources { get; } = new[]
    {
        new AutoEqSource("Harman", "Harman · 原始 AutoEQ 项目", "AutoEQCompressedHarman5.txt"),
        new AutoEqSource("IEF", "IEF · Ian Walton 分支", "AutoEQCompressedIEF5.txt"),
        new AutoEqSource("IEFBass", "IEF + 低音 · Ian Walton 分支", "AutoEQCompressedIEFBass5.txt"),
        new AutoEqSource("OPRA", "OPRA · Roon Labs 等", "OPRAEQCompressed5.txt"),
    };

    /// <summary>找数据目录：环境变量优先，其次是程序旁边的 <c>AutoEqData</c>。</summary>
    public static string? FindDataDirectory(string? overrideDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideDirectory) && Directory.Exists(overrideDirectory))
        {
            return overrideDirectory;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        var beside = Path.Combine(AppContext.BaseDirectory, DataFolderName);
        return Directory.Exists(beside) ? beside : null;
    }

    /// <summary>某个库的数据文件在哪；找不到返回 null。</summary>
    public static string? FindFile(AutoEqSource source, string? dataDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var directory = FindDataDirectory(dataDirectory);
        if (directory is null)
        {
            return null;
        }

        var path = Path.Combine(directory, source.FileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>有没有装数据。</summary>
    public static bool IsAvailable(string? dataDirectory = null) =>
        Sources.Any(source => FindFile(source, dataDirectory) is not null);

    /// <summary>
    /// 读一个库，读过的缓存住。出错时返回 null，并给出给人看的原因。
    /// </summary>
    public static AutoEqDatabase? TryLoad(AutoEqSource source, out string error, string? dataDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        error = string.Empty;

        var path = FindFile(source, dataDirectory);
        if (path is null)
        {
            error = $"没找到 {source.FileName}（应该在 {DataFolderName} 目录里，或用环境变量 "
                + $"{DataDirectoryVariable} 指过去）";
            return null;
        }

        lock (Gate)
        {
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            try
            {
                var database = AutoEqDatabase.Load(path);
                Cache[path] = database;
                return database;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
            {
                error = $"读不了 {source.FileName}：{ex.GetType().Name} {ex.Message}";
                return null;
            }
        }
    }
}
