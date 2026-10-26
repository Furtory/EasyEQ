using System.Diagnostics;

namespace PeaceWpf.Core.Hosting;

/// <summary>
/// 负责找到后端进程，必要时把它拉起来。
///
/// Peace 的启动参数见 Peace.au3 第 3460–3514 行：
///   Peace.exe &lt;预设名&gt; hide   —— 隐藏主界面启动，遥控照常可用
///   Peace.exe &lt;预设名&gt; stay   —— 启动但保持隐藏
///   Peace.exe &lt;预设名&gt; start  —— 开机自启
/// </summary>
public static class PeaceLauncher
{
    /// <summary>可能的后端文件名，64 位优先。</summary>
    public static IReadOnlyList<string> ExecutableNames { get; } = new[] { "Peace64.exe", "Peace.exe" };

    /// <summary>在后端所在目录里找可执行文件。</summary>
    public static string? FindExecutable(string? directory = null)
    {
        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(directory))
        {
            dirs.Add(directory);
        }

        // 常见安装位置：EqualizerAPO 的 config 目录。
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        dirs.Add(Path.Combine(programFiles, "EqualizerAPO", "config"));
        dirs.Add(Path.Combine(programFiles, "EqualizerAPO"));

        foreach (var dir in dirs)
        {
            foreach (var name in ExecutableNames)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>后端是否已经在跑。</summary>
    public static bool IsRunning()
    {
        foreach (var name in ExecutableNames)
        {
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name)).Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 启动后端。参数规则照抄 ReadCommandLine：hide/stay/start 只有跟在配置名后面才会被识别，
    /// 所以没有配置名时不能传 "hide"，否则它会被当成配置名。
    /// 没有配置名时启动后需要再发 WM_APP+1（隐藏）把界面收起来。
    /// </summary>
    public static bool StartHidden(string executablePath, string? configurationName = null)
    {
        if (!File.Exists(executablePath))
        {
            return false;
        }

        var arguments = string.IsNullOrWhiteSpace(configurationName)
            ? string.Empty
            : $"\"{configurationName}\" hide";

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return Process.Start(startInfo) is not null;
    }
}
