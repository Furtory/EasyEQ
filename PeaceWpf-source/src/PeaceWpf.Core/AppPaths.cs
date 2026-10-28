namespace PeaceWpf.Core;

/// <summary>
/// 程序自己的名字和运行时目录。集中一处，别再让字面量散在各文件里。
/// 数据放 %LOCALAPPDATA%\EasyEQ：设置、保存备份、诊断输出、测量记录都在这里。
/// </summary>
public static class AppPaths
{
    /// <summary>产品名，同时就是数据目录名。改这里之前想清楚，会导致所有人丢设置。</summary>
    public const string Name = "EasyEQ";

    /// <summary>改名之前用的数据目录名，只给一次性迁移用。</summary>
    private const string LegacyName = "PeaceWpf";

    /// <summary>设置、备份、诊断落到这个目录。</summary>
    public static string DataDirectory => Path.Combine(LocalAppData, Name);

    /// <summary>老版本的数据目录，迁移时读它。</summary>
    public static string LegacyDataDirectory => Path.Combine(LocalAppData, LegacyName);

    private static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// 老版本把数据放在 %LOCALAPPDATA%\PeaceWpf。换了目录不能让人白调一遍：
    /// 新目录里还没有 ui-state.json 时，把旧目录里的文件补过来（只补缺的，不覆盖）。
    /// 旧目录原样留着，想回退或者自己清理都行。任何失败都吞掉，不能因为它起不来。
    /// </summary>
    public static void MigrateLegacyData()
    {
        try
        {
            var legacy = LegacyDataDirectory;
            if (!Directory.Exists(legacy))
            {
                return;
            }

            // 新目录已经有设置就不动：用户可能已经在新版里调过了，别拿旧的盖掉。
            if (File.Exists(Path.Combine(DataDirectory, "ui-state.json")))
            {
                return;
            }

            CopyMissing(legacy, DataDirectory);
        }
        catch
        {
            // 复制失败就当没有老数据，照常启动。
        }
    }

    /// <summary>递归复制，目标已存在的文件一律跳过。</summary>
    private static void CopyMissing(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(file));
            if (!File.Exists(destination))
            {
                File.Copy(file, destination);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            // app\ 是 run.cmd 放的调试副本，几十兆的 dll，不是用户数据，别搬。
            if (string.Equals(Path.GetFileName(directory), "app", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyMissing(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
