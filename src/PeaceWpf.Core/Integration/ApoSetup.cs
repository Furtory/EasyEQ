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

    /// <summary>
    /// 切换入口文件里的 Include：保证挂上 <paramref name="includeFileName"/>，
    /// 同时把指向 <paramref name="excludeFileName"/> 的那一行去掉，别的行一个不动
    /// （`protect.txt`、注释、空行都原样留着）。
    ///
    /// 为什么必须二选一：两个文件都挂上，两份 EQ 会叠加，声音就不对了。
    /// 改之前同样先备份；文件不存在时就新建。
    /// </summary>
    public static ApoIncludeResult SwitchInclude(
        string configDirectory,
        string includeFileName,
        string excludeFileName,
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
            var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = existing.Length == 0
                ? new List<string>()
                : existing.Split(newline).ToList();

            while (lines.Count > 0 && lines[^1].Trim().Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            var kept = new List<string>(lines.Count + 1);
            var removed = 0;
            var alreadyIncluded = false;

            foreach (var line in lines)
            {
                if (TryReadIncludeName(line, out var name))
                {
                    if (string.Equals(name, excludeFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        removed++;
                        continue;
                    }

                    if (string.Equals(name, includeFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyIncluded = true;
                    }
                }

                kept.Add(line);
            }

            if (alreadyIncluded && removed == 0)
            {
                return new ApoIncludeResult(false, null, null);
            }

            if (!alreadyIncluded)
            {
                kept.Add(IncludeKeyword + " " + includeFileName);
            }

            if (File.Exists(path))
            {
                backupPath = Backup(path, backupDirectory);
                EapoConfig.ClearReadOnly(path);
            }

            File.WriteAllText(path, string.Join(newline, kept) + newline, new UTF8Encoding(false));
            return new ApoIncludeResult(true, null, backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ApoIncludeResult(false, ex.Message, backupPath);
        }
    }

    /// <summary>
    /// 把一行里的 Include 目标文件名读出来（`Include: 子目录\文件.txt` 只取最后一段），
    /// 不是 Include 行返回 false。空行和 `#` 注释也算不是。
    /// </summary>
    private static bool TryReadIncludeName(string rawLine, out string name)
    {
        name = string.Empty;

        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            return false;
        }

        if (!line.StartsWith(IncludeKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var target = line[IncludeKeyword.Length..].Trim().Trim('"');
        if (target.Length == 0)
        {
            return false;
        }

        var normalized = target.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        if (slash >= 0)
        {
            normalized = normalized[(slash + 1)..];
        }

        name = normalized;
        return true;
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
