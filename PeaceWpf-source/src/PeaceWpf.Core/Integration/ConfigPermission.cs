using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace PeaceWpf.Core.Integration;

/// <summary>配置目录写不进去时，问题到底出在哪一类。</summary>
public enum ConfigProblem
{
    /// <summary>能写，没问题。</summary>
    None,

    /// <summary>找不到 EqualizerAPO 的 config 目录。</summary>
    DirectoryMissing,

    /// <summary>目录权限只给了管理员，而当前进程不是管理员——重新提权就能解决。</summary>
    NotElevated,

    /// <summary>进程跑在低完整性令牌下，写 Program Files 一定被拒。这一层比 ACL 更早、更硬。</summary>
    LowIntegrity,

    /// <summary>提了权也写不了：ACL 里没有给普通用户的写权限，要显式加一条。</summary>
    AclNeedsGrant,

    /// <summary>目标文件带只读位，覆盖写会被拒。</summary>
    ReadOnlyFile,

    /// <summary>文件被别的进程占着（EqualizerAPO 编辑器、Peace 之类）。</summary>
    FileLocked,

    /// <summary>Windows Defender 的受控文件夹访问把写入拦了。</summary>
    ControlledFolderAccess,

    /// <summary>能查的都查了，还是不知道。</summary>
    Unknown,
}

/// <summary>一次诊断的结果。Summary 给人看结论，Action 给人看下一步，Details 给愿意细看的人。</summary>
public sealed record ConfigDiagnosis(
    ConfigProblem Problem,
    string? Directory,
    bool DirectoryExists,
    bool Writable,
    bool Elevated,
    string Summary,
    string Action,
    IReadOnlyList<string> Details);

/// <summary>修一次的结果。改之前备份原 ACL 的路径在 BackupPath。</summary>
public sealed record ConfigRepairResult(bool Ok, string Message, string? BackupPath);

/// <summary>
/// 配置目录写权限的诊断与修复。
///
/// 不能只报「不可写」——那等于什么都没说。这里按顺序排掉几类常见原因：
/// 目录不存在、文件只读、文件被占用、没提权、受控文件夹访问、ACL 缺写权限。
/// 只有最后一类才动 ACL，动之前备份成文本，动完再实际写一次验证。
/// </summary>
public static class ConfigPermission
{
    /// <summary>给「Users」组授写权限用的 SID，避免不同语言的组名（Users / 用户）对不上。</summary>
    private const string UsersSid = "*S-1-5-32-545";

    /// <summary>当前进程是不是管理员。分得清「没提权」和「提了权仍被拒」。</summary>
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>诊断一个 config 目录。不抛异常，最差也返回 <see cref="ConfigProblem.Unknown"/>。</summary>
    public static ConfigDiagnosis Diagnose(string? directory)
    {
        var details = new List<string>();
        var elevated = IsElevated();

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return new ConfigDiagnosis(
                ConfigProblem.DirectoryMissing,
                directory,
                false,
                false,
                elevated,
                "找不到 EqualizerAPO 的 config 目录。",
                "确认 EqualizerAPO 装在默认位置；装在别处的话，用设置里的配置目录指过去。",
                details);
        }

        details.Add($"配置目录：{directory}");
        details.Add(elevated ? "当前进程：管理员。" : "当前进程：普通用户。");
        details.Add($"完整性级别：{IntegrityLevelName()}。");

        // 顺序有讲究：只读位和占用跟「是不是管理员」无关，先排掉。
        // 它们正是「提了权仍写不进去」最常见的两个原因，比 ACL 常见得多。
        var readOnly = FindReadOnlyTargets(directory);
        if (readOnly.Count > 0)
        {
            details.Add("带只读位的文件：" + string.Join("、", readOnly.Select(Path.GetFileName)));
            return new ConfigDiagnosis(
                ConfigProblem.ReadOnlyFile,
                directory,
                true,
                false,
                elevated,
                "配置目录里有文件带只读位，覆盖写会被拒。",
                "点「修一下」把只读位清掉再试。",
                details);
        }

        var locked = FindLockedTargets(directory);
        if (locked.Count > 0)
        {
            details.Add("正被以写方式打开的文件：" + string.Join("、", locked));
            return new ConfigDiagnosis(
                ConfigProblem.FileLocked,
                directory,
                true,
                false,
                elevated,
                "配置文件被别的程序占着，写不进去。",
                "关掉 Peace、EqualizerAPO 的配置文件编辑器（或者别的在写 config 的程序），再点「修一下」重测。",
                details);
        }

        var probe = ProbeWrite(directory);
        if (probe.Ok)
        {
            details.Add("写测试：通过（在目录里建了个临时文件又删掉）。");
            return new ConfigDiagnosis(
                ConfigProblem.None,
                directory,
                true,
                true,
                elevated,
                "配置目录可写，保存不会失败。",
                "什么都不用做。",
                details);
        }

        details.Add("写测试：失败 —— " + probe.Error);

        // 写不进去才轮到完整性。低完整性是这一层的硬墙：Program Files 对它只读，
        // 而且这跟 ACL 写没写对无关，加 ACE、重启电脑都不解决。
        if (IsLowIntegrity())
        {
            details.Add("低完整性令牌写 Program Files 一定被拒，跟 ACL 有没有写权限无关。");
            details.Add("常见原因：UAC 被关掉（EnableLUA=0）时 manifest 里的 requireAdministrator 不生效，进程沿用了启动者的完整性级别；runas 也不会提升，只是换一次同样的令牌。");
            details.Add("可行的解法：把 config 目录的完整性标签降到「低」，低完整性进程就能写它。这条要管理员跑一次。");
            return new ConfigDiagnosis(
                ConfigProblem.LowIntegrity,
                directory,
                true,
                false,
                elevated,
                "程序跑在低完整性下，写 Program Files 必被拒；这不是 ACL 的问题，加权限没用。",
                "用管理员 PowerShell 跑一次把目录标签降下来，之后普通启动就写得进：" + IntegrityFixCommand(directory),
                details);
        }

        if (!elevated)
        {
            details.Add("没有管理员权限，Program Files 下的目录默认只允许管理员写。");
            return new ConfigDiagnosis(
                ConfigProblem.NotElevated,
                directory,
                true,
                false,
                elevated,
                "当前不是管理员，而这个目录只有管理员能写。",
                "关掉程序，右键「以管理员身份运行」再保存。",
                details);
        }

        if (IsControlledFolderAccessOn(out var cfaDetail))
        {
            details.Add("Windows Defender 受控文件夹访问：已开启。");
            if (cfaDetail is not null)
            {
                details.Add(cfaDetail);
            }

            return new ConfigDiagnosis(
                ConfigProblem.ControlledFolderAccess,
                directory,
                true,
                false,
                elevated,
                "已经是管理员，但 Windows Defender 的受控文件夹访问挡住了写入。",
                "到「Windows 安全中心 → 病毒和威胁防护 → 勒索软件防护」里关掉受控文件夹访问，或者把程序加进允许列表。",
                details);
        }

        details.Add("已经是管理员，也没只读、没占用、没被 Defender 拦——那就是目录的 ACL 没给普通用户写权限。");
        return new ConfigDiagnosis(
            ConfigProblem.AclNeedsGrant,
            directory,
            true,
            false,
            elevated,
            "已经是管理员，但仍写不进去；ACL 里没有给普通账户写权限。",
            "点「授予写权限」给当前用户加一条修改权限，改之前会把原 ACL 备份成文本。",
            details);
    }

    /// <summary>
    /// 尽量把目录修到可写。只做与诊断结论对应的那一件事，不扫全盘：
    /// 只读位就清掉；缺 ACL 就加一条 Users 的修改权限。改完都实际写一次验证。
    /// </summary>
    public static ConfigRepairResult Repair(ConfigDiagnosis diagnosis, string backupDirectory)
    {
        ArgumentNullException.ThrowIfNull(diagnosis);

        if (diagnosis.Problem == ConfigProblem.None)
        {
            return new ConfigRepairResult(true, "本来就写得了，没改任何东西。", null);
        }

        if (diagnosis.Directory is not { } directory || !Directory.Exists(directory))
        {
            return new ConfigRepairResult(false, "目录不存在，没东西可修。", null);
        }

        switch (diagnosis.Problem)
        {
            case ConfigProblem.ReadOnlyFile:
                return ClearReadOnly(directory);

            case ConfigProblem.FileLocked:
            {
                // 占用没法由我们「清掉」，只能重测一次看对方是不是已经放手了。
                var recheck = Diagnose(directory);
                return recheck.Problem == ConfigProblem.None
                    ? new ConfigRepairResult(true, "文件已经不被占了，现在写得了。", null)
                    : new ConfigRepairResult(false, "文件还是被别的进程占着。先把它关掉再试。", null);
            }

            case ConfigProblem.NotElevated:
                return new ConfigRepairResult(
                    false,
                    "这个要重新以管理员身份启动程序，程序自己改不了自己。关掉，右键「以管理员身份运行」。",
                    null);

            case ConfigProblem.LowIntegrity:
                return new ConfigRepairResult(
                    false,
                    "这条程序自己执行不了（改完整性标签要管理员）。把下面这条复制到管理员 PowerShell 里跑一次，之后普通启动就写得进："
                        + IntegrityFixCommand(directory),
                    null);

            case ConfigProblem.ControlledFolderAccess:
                return new ConfigRepairResult(
                    false,
                    "受控文件夹访问只能由你在 Windows 安全中心里关掉或加白名单，程序不动这个系统开关。",
                    null);

            case ConfigProblem.AclNeedsGrant:
                return GrantUsersWrite(directory, backupDirectory);

            default:
                return new ConfigRepairResult(false, "没认出是哪一类问题，没敢乱改。", null);
        }
    }

    /// <summary>清掉目录里带只读位的目标文件，然后复测。</summary>
    public static ConfigRepairResult ClearReadOnly(string directory)
    {
        var cleared = new List<string>();
        foreach (var path in FindReadOnlyTargets(directory))
        {
            try
            {
                var info = new FileInfo(path);
                info.IsReadOnly = false;
                cleared.Add(Path.GetFileName(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new ConfigRepairResult(false, $"清只读位失败：{Path.GetFileName(path)} · {ex.Message}", null);
            }
        }

        var ok = EapoConfig.CanWrite(directory);
        var message = ok
            ? $"清掉了 {cleared.Count} 个文件的只读位（{string.Join("、", cleared)}），现在写得了。"
            : $"清掉了只读位但还是写不了（{string.Join("、", cleared)}）。再诊断一次看看剩哪一类原因。";

        return new ConfigRepairResult(ok, message, null);
    }

    /// <summary>
    /// 给目录加一条 Users 的修改权限（可继承到子文件）。改之前把原 ACL 的文本存到
    /// <paramref name="backupDirectory"/>，改完用真实的写测试验证，不以 icacls 的返回码为准。
    /// </summary>
    public static ConfigRepairResult GrantUsersWrite(string directory, string backupDirectory)
    {
        if (!IsElevated())
        {
            return new ConfigRepairResult(
                false,
                "改目录 ACL 要管理员权限。先关掉程序，右键「以管理员身份运行」。",
                null);
        }

        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, $"acl-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var dump = Run("icacls", [directory]);
        if (dump.Ok)
        {
            try
            {
                File.WriteAllText(backupPath, dump.Output);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                backupPath = null;
            }
        }

        var grant = Run("icacls", [directory, "/grant", $"{UsersSid}:(OI)(CI)M", "/T", "/C"]);
        var writable = EapoConfig.CanWrite(directory);

        if (!writable)
        {
            var reason = grant.Ok
                ? "icacls 说改成了，但实际还是写不进去。"
                : "icacls 失败：" + FirstLine(grant.Output);
            var detail = Squash(grant.Output);
            return new ConfigRepairResult(
                false,
                reason
                    + (detail.Length == 0 ? string.Empty : " 原始输出：" + detail)
                    + " 再诊断一次，看看是不是别的原因。",
                backupPath);
        }

        var where = backupPath is null ? "（原 ACL 没备份成功，但已经改了）" : $"（原 ACL 备份在 {backupPath}）";
        return new ConfigRepairResult(
            true,
            $"给 Users 加了一条对 {directory} 的修改权限（含子文件），写测试通过 {where}",
            backupPath);
    }

    /// <summary>目标文件清单：peace.txt、config.txt，以及目录里所有 .peace 预设。</summary>
    private static IEnumerable<string> TargetFiles(string directory)
    {
        yield return Path.Combine(directory, EapoConfig.FileName);
        yield return Path.Combine(directory, ApoSetup.ConfigFileName);

        string[] presets;
        try
        {
            presets = Directory.GetFiles(directory, "*.peace");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            presets = [];
        }

        foreach (var preset in presets)
        {
            yield return preset;
        }
    }

    private static List<string> FindReadOnlyTargets(string directory)
    {
        var list = new List<string>();
        foreach (var path in TargetFiles(directory))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.IsReadOnly)
                {
                    list.Add(info.FullName);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 查不了就当它不是只读，别在这里误报。
            }
        }

        return list;
    }

    private static List<string> FindLockedTargets(string directory)
    {
        var list = new List<string>();
        foreach (var path in TargetFiles(directory))
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                // 只测「有没有别人以写方式占着」：允许共享读，免得把 EqualizerAPO 的正常读挡在外头，
                // 也免得我们自己短暂独占把引擎的读取卡一下。
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            }
            catch (FileNotFoundException)
            {
                // 不存在不是占用。
            }
            catch (DirectoryNotFoundException)
            {
                // 同上。
            }
            catch (IOException)
            {
                list.Add(Path.GetFileName(path));
            }            catch (UnauthorizedAccessException)
            {
                // 没权限不等于被占；这一类交给 ACL 分支去判断。
            }
        }

        return list;
    }

    /// <summary>读 Defender 受控文件夹访问的开关。0 关，1 开，2 只审计。</summary>
    private static bool IsControlledFolderAccessOn(out string? detail)
    {
        detail = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access");
            if (key?.GetValue("EnableControlledFolderAccess") is not int value)
            {
                return false;
            }

            if (value == 0)
            {
                return false;
            }

            detail = value switch
            {
                1 => "受控文件夹访问：已开启（会拦截非白名单程序写受保护目录）。",
                2 => "受控文件夹访问：只审计（不拦，但会记日志）。",
                _ => $"受控文件夹访问：EnableControlledFolderAccess={value}。",
            };
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private readonly record struct RunResult(bool Ok, string Output);

    /// <summary>跑一个控制台工具并把输出接回来。参数走 ArgumentList，不拼命令行字符串。</summary>
    private static RunResult Run(string fileName, IEnumerable<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new RunResult(false, "启动 " + fileName + " 失败。");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);
            return new RunResult(process.ExitCode == 0, stdout + stderr);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new RunResult(false, ex.Message);
        }
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        return string.IsNullOrWhiteSpace(line) ? "（没有输出）" : line;
    }

    /// <summary>
    /// 写测试：建个临时文件再删掉。和 <c>EapoConfig.CanWrite</c> 同一个动作，
    /// 但把失败的原话留下来——「写不进去」和「系统为什么不让写」是两件事，后者才指向病根。
    /// </summary>
    private static (bool Ok, string Error) ProbeWrite(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return (true, string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (false, ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// 当前进程的令牌完整性级别。UAC 提权会把「中」抬到「高」；
    /// 低的时候，ACL 有没有写权限都不重要，因为这一层就先被挡了。
    /// </summary>
    public static string IntegrityLevelName() => CurrentIntegrityRid() switch
    {
        0 or 1 => "低（Low）",
        2 => "中（Medium，未提权）",
        3 => "高（High，已提权）",
        4 => "系统（System）",
        var rid => "读不出来（" + rid + "）",
    };

    /// <summary>程序是不是跑在低完整性下。低完整性写 Program Files 必被拒。</summary>
    public static bool IsLowIntegrity() => CurrentIntegrityRid() is 0 or 1;

    /// <summary>
    /// 把目录的完整性标签降到「低」的命令。低完整性进程写不了带「中」标签的 Program Files，
    /// 降标签之后就能写——不需要每次提权，也不动 ACL。
    /// </summary>
    public static string IntegrityFixCommand(string directory) =>
        $"icacls \"{directory}\" /setintegritylevel (OI)(CI)L";

    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevelInformation = 25;

    private static int CurrentIntegrityRid()
    {
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out var token))
            {
                return -1;
            }

            try
            {
                GetTokenInformation(token, TokenIntegrityLevelInformation, IntPtr.Zero, 0, out var needed);
                if (needed <= 0)
                {
                    return -1;
                }

                var buffer = Marshal.AllocHGlobal(needed);
                try
                {
                    if (!GetTokenInformation(token, TokenIntegrityLevelInformation, buffer, needed, out _))
                    {
                        return -1;
                    }

                    // TOKEN_MANDATORY_LABEL { SID_AND_ATTRIBUTES { PSID Sid; DWORD Attributes } }
                    var sid = Marshal.ReadIntPtr(buffer);
                    var count = Marshal.ReadByte(sid, 1);
                    var last = (IntPtr)((long)sid + 8 + (4 * (count - 1)));
                    return Marshal.ReadInt32(last) >> 12;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ArgumentException)
        {
            return -1;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr buffer, int length, out int needed);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>把多行输出压成一行，截短——修复失败时它要能塞进状态条，又得留住关键那句。</summary>
    private static string Squash(string text)
    {
        var flat = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (flat.Contains("  ", StringComparison.Ordinal))
        {
            flat = flat.Replace("  ", " ", StringComparison.Ordinal);
        }

        return flat.Length <= 600 ? flat : flat[..600] + "…";
    }
}
