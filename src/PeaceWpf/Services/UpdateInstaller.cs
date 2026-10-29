using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace PeaceWpf.Services;

/// <summary>
/// 软件内更新：下载新版本、解压，然后让新版本的 exe 以 <c>--apply-update</c> 模式
/// 等旧进程退出、覆盖安装目录、再把新版本拉起来。
///
/// 为什么拿新 exe 自己当更新器，而不是写一个 bat / ps1：
/// 安装路径带中文和空格时，批处理的编码很容易翻车；PowerShell 又可能被执行策略拦住。
/// 新版本本来就是完整可运行的程序，让它干这件事最稳，也不用额外依赖解压器。
/// </summary>
public static class UpdateInstaller
{
    public const string ApplyUpdateArg = "--apply-update";

    /// <summary>更新包在 Release 里的资产名，和 <see cref="UpdateChecker.UpdateAssetName"/> 是同一个。</summary>
    public const string PackageFileName = "EasyEQ.zip";

    private const string StageFolder = "EasyEQ-update";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static bool IsApplyUpdateMode(string[] args) =>
        args.Any(arg => string.Equals(arg, ApplyUpdateArg, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 只从 GitHub 的地址下载。更新包是本仓库的 Release 资产，但这个检查能挡住
    /// 被篡改的接口响应把人引到别处去执行 exe。
    /// </summary>
    public static bool IsTrustedDownloadUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.Host;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>下载更新包到临时目录，返回 zip 路径。进度是 0..1。</summary>
    public static async Task<string> DownloadAsync(
        string url,
        string version,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var directory = StageDirectory(version);
        Directory.CreateDirectory(directory);
        var zipPath = Path.Combine(directory, PackageFileName);
        var partial = zipPath + ".part";

        using (var response = await Http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            await using var input = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var output = new FileStream(
                partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                if (total > 0)
                {
                    progress?.Report(Math.Clamp((double)received / total, 0, 1));
                }
            }
        }

        File.Move(partial, zipPath, overwrite: true);
        return zipPath;
    }

    /// <summary>解压到临时目录，返回 <c>EasyEQ.exe</c> 所在目录。包不完整时抛异常。</summary>
    public static string Extract(string zipPath, string version)
    {
        var files = Path.Combine(StageDirectory(version), "files");
        if (Directory.Exists(files))
        {
            Directory.Delete(files, recursive: true);
        }

        Directory.CreateDirectory(files);
        ZipFile.ExtractToDirectory(zipPath, files, overwriteFiles: true);

        if (!File.Exists(Path.Combine(files, "EasyEQ.exe")))
        {
            throw new InvalidOperationException("更新包里没有 EasyEQ.exe，可能传错了资产。");
        }

        return files;
    }

    /// <summary>
    /// 启动解压目录里的新 exe 当更新器，参数里带上目标目录、当前进程号和临时目录。
    /// 调用方拿到 true 之后就该关掉自己了。
    /// </summary>
    public static bool TryLaunchApplier(string sourceDirectory, string targetDirectory, out string error)
    {
        error = string.Empty;

        var exe = Path.Combine(sourceDirectory, "EasyEQ.exe");
        if (!File.Exists(exe))
        {
            error = "解压后的更新器不见了。";
            return false;
        }

        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = sourceDirectory,
        };
        start.ArgumentList.Add(ApplyUpdateArg);
        start.ArgumentList.Add(targetDirectory);
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add(sourceDirectory);

        try
        {
            Process.Start(start);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            error = "启动更新器失败：" + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// <c>--apply-update</c> 模式的全部工作：等旧进程退出、把临时目录覆盖到安装目录、启动新版本。
    /// 返回进程退出码；App 在启动最早期调用它，此时还没有任何窗口。
    /// </summary>
    public static int RunApplyUpdate(string[] args)
    {
        var index = Array.FindIndex(args, arg =>
            string.Equals(arg, ApplyUpdateArg, StringComparison.OrdinalIgnoreCase));

        if (index < 0 || args.Length < index + 4)
        {
            return 2;
        }

        var target = args[index + 1];
        var source = args[index + 3];
        _ = int.TryParse(args[index + 2], out var waitingPid);

        try
        {
            WaitForExit(waitingPid);
            CopyTree(source, target);
        }
        catch (Exception ex)
        {
            WriteApplyLog("更新失败：" + ex);

            // 换失败就别把用户晾在那儿：原来的程序还在，拉起来还能用。
            StartTarget(target);
            return 1;
        }

        StartTarget(target);
        return 0;
    }

    private static string StageDirectory(string version) =>
        Path.Combine(Path.GetTempPath(), StageFolder, SafeName(version));

    private static string SafeName(string version)
    {
        var chars = version
            .Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_')
            .ToArray();
        var name = new string(chars);
        return name.Length == 0 ? "latest" : name;
    }

    /// <summary>等旧进程自己退出；最多等两分钟，超时也照常往下走（文件可能还锁着，让 robocopy 去撞）。</summary>
    private static void WaitForExit(int pid)
    {
        if (pid <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            process.WaitForExit(120_000);
        }
        catch (ArgumentException)
        {
            // 早就退出了，正常。
        }
        catch (InvalidOperationException)
        {
            // 也当已退出。
        }
    }

    /// <summary>用 robocopy 整目录覆盖：更新器在临时目录里跑，覆盖的是另一个目录，不会自己删自己。</summary>
    private static void CopyTree(string source, string target)
    {
        var start = new ProcessStartInfo("robocopy")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(source);
        start.ArgumentList.Add(target);
        foreach (var arg in new[] { "/MIR", "/R:5", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP" })
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("起不了 robocopy。");
        process.WaitForExit();

        // robocopy 的退出码：0–7 都算成功，8 及以上才是真失败。
        if (process.ExitCode >= 8)
        {
            throw new InvalidOperationException($"robocopy 返回 {process.ExitCode}。");
        }
    }

    private static void StartTarget(string target)
    {
        try
        {
            var exe = Path.Combine(target, "EasyEQ.exe");
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = target });
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            WriteApplyLog("拉起新版本失败：" + ex);
        }
    }

    private static void WriteApplyLog(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), StageFolder, "apply-error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:O}] {message}\r\n");
        }
        catch
        {
            // 日志写不进去就算了，更新流程本身不该因此再炸一次。
        }
    }
}
