using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace PeaceWpf.Services;

/// <summary>
/// 权限相关的小工具。
///
/// 为什么需要它：Peace 装在 Program Files 下，通常以管理员身份运行；
/// 本程序从普通位置启动时是普通权限。Windows 的 UIPI 会直接丢掉
/// 「普通进程发给高权限窗口」的自定义消息 —— 消息发不出去、也不报错，
/// 表现就是「调了没反应、保存也没反应」。所以两边权限必须一致。
/// </summary>
public static class Elevation
{
    /// <summary>当前进程是不是管理员权限。</summary>
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Peace 是否在运行（64 位和 32 位都算）。</summary>
    public static bool IsPeaceRunning()
    {
        foreach (var name in new[] { "Peace", "Peace64" })
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length == 0)
                {
                    continue;
                }

                foreach (var process in processes)
                {
                    process.Dispose();
                }

                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // 拿不到进程列表就当没运行。
            }
        }

        return false;
    }

    /// <summary>
    /// 文件能不能以写方式打开（不写内容、不截断）。用来判断预设是不是被 ACL 挡住——
    /// EqualizerAPO 装在 Program Files 下时，.peace 预设往往只允许管理员写，
    /// 而程序自己建的 peace.txt 反而可写，所以「能出声、保存失败」是常见组合。
    /// </summary>
    public static bool CanWriteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return stream.CanWrite;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 用 runas 重新拉起自己，带上 --elevated 防止再次弹同一个询问。
    /// 用户取消 UAC 时返回 false。
    /// </summary>
    public static bool TryRestartElevated()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
            };
            startInfo.ArgumentList.Add("--elevated");

            return Process.Start(startInfo) is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // 1223 = 用户在 UAC 里点了「否」。
            return false;
        }
    }
}
