using System.Text;

namespace PeaceWpf.Core.Integration;

/// <summary>
/// EqualizerAPO 的入口配置（<c>config.txt</c>）。
///
/// 引擎只读 <c>config.txt</c>，别的文件都要靠 <c>Include:</c> 挂上去。以前这一行由 Peace 维护，
/// 现在程序自己跑，就得自己确认：没挂上时由用户点一下接上，接的时候先备份、再追加，别的东西一律不动。
/// </summary>
public static class ApoSetup
{
    /// <summary>EqualizerAPO 的入口文件。</summary>
    public const string ConfigFileName = "config.txt";

    /// <summary>把文件挂进引擎的关键字。</summary>
    public const string IncludeKeyword = "Include:";

    /// <summary>入口文件的完整路径。</summary>
    public static string ConfigFilePath(string configDirectory) =>
        Path.Combine(configDirectory, ConfigFileName);

    /// <summary>
    /// 入口文件里有没有加载 <paramref name="includeFileName"/>。
    /// 只认 <c>Include:</c> 开头的行，跳过空行与 <c>#</c> 注释；比较时只看文件名，大小写不敏感。
    /// </summary>
    public static bool IsIncluded(string configText, string includeFileName)
    {
        ArgumentNullException.ThrowIfNull(configText);
        if (string.IsNullOrWhiteSpace(includeFileName))
        {
            return false;
        }

        foreach (var rawLine in configText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (!line.StartsWith(IncludeKeyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = line[IncludeKeyword.Length..].Trim().Trim('"');
            if (target.Length == 0)
            {
                continue;
            }

            // APO 里的路径可以是相对 config 目录的子目录，这里只比最后一段。
            var name = target.Replace('\\', '/');
            var slash = name.LastIndexOf('/');
            if (slash >= 0)
            {
                name = name[(slash + 1)..];
            }

            if (string.Equals(name, includeFileName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把 <paramref name="includeFileName"/> 挂进入口文件。已经挂上就什么都不做。
    /// 改之前先把原文件备份到 <paramref name="backupDirectory"/>；是只读位就先清掉。
    /// </summary>
    /// <param name="configDirectory">EqualizerAPO 的 config 目录。</param>
    /// <param name="includeFileName">要挂进去的文件名，例如 <c>peace.txt</c>。</param>
    /// <param name="backupDirectory">备份目录，通常是 <c>%LOCALAPPDATA%\PeaceWpf</c>。</param>
    public static ApoIncludeResult EnsureInclude(
        string configDirectory,
        string includeFileName,
        string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory) || !Directory.Exists(configDirectory))
        {
            return new ApoIncludeResult(false, "找不到 EqualizerAPO 的 config 目录。", null);
        }

        if (string.IsNullOrWhiteSpace(includeFileName))
        {
            return new ApoIncludeResult(false, "要挂进去的文件名是空的。", null);
        }

        var path = ConfigFilePath(configDirectory);
        string? backupPath = null;

        try
        {
            var existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            if (IsIncluded(existing, includeFileName))
            {
                return new ApoIncludeResult(false, null, null);
            }

            if (File.Exists(path))
            {
                backupPath = Backup(path, backupDirectory);
                EapoConfig.ClearReadOnly(path);
            }

            var builder = new StringBuilder(existing);
            if (existing.Length > 0 && !existing.EndsWith('\n'))
            {
                builder.AppendLine();
            }

            builder.AppendLine(IncludeKeyword + " " + includeFileName);
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));

            return new ApoIncludeResult(true, null, backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ApoIncludeResult(false, ex.Message, backupPath);
        }
    }

    /// <summary>读入口文件的文本；读不了返回 null，不抛。</summary>
    public static string? TryRead(string configDirectory)
    {
        try
        {
            var path = ConfigFilePath(configDirectory);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>复制一份带时间戳的备份，返回备份路径；备份失败返回 null（不阻止改动）。</summary>
    private static string? Backup(string path, string backupDirectory)
    {
        try
        {
            Directory.CreateDirectory(backupDirectory);
            var target = Path.Combine(
                backupDirectory,
                $"{ConfigFileName}.{DateTime.Now:yyyyMMdd-HHmmss}.bak");
            File.Copy(path, target, overwrite: true);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>挂载结果。</summary>
/// <param name="Changed">这次是不是真的改了文件。</param>
/// <param name="Error">出错原因；成功时是 null。</param>
/// <param name="BackupPath">改之前备份到哪；没备份或没改动时是 null。</param>
public sealed record ApoIncludeResult(bool Changed, string? Error, string? BackupPath);
