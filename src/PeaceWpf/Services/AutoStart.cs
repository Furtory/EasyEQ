using System.IO;
using Microsoft.Win32;

namespace PeaceWpf;

/// <summary>
/// 开机自启：只写当前用户的一条 Run 项，值名固定，不碰别的启动项。
/// 卸载/关闭时删掉同一条，不留垃圾。
/// </summary>
public static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "EasyEQ";

    /// <summary>改名之前用的值名。读到它就搬过来，别让用户的开机自启悄悄掉线。</summary>
    private const string LegacyValueName = "PeaceWpf";

    /// <summary>
    /// 现在开没开。读不到注册表就当没开，不抛。
    /// 顺带自检路径：程序改过文件名（PeaceWpf.exe → EasyEQ.exe）之后，旧条目会指向一个不存在的 exe，
    /// 表现为「设置里明明是开的，开机却起不来」。这里读到旧路径就悄悄改成当前 exe。
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                if (key is null)
                {
                    return false;
                }

                if (key.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value))
                {
                    // 只剩旧值名（PeaceWpf）：说明是改名前的条目，搬过来。
                    if (key.GetValue(LegacyValueName) is string legacy && !string.IsNullOrWhiteSpace(legacy))
                    {
                        TryRepoint();
                        return true;
                    }

                    return false;
                }

                if (!value.Contains(ExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    TryRepoint();
                }

                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    /// <summary>把启动项改写成当前 exe，并清掉改名前的旧值名。修不了就算了，不能因为自启影响别的功能。</summary>
    private static void TryRepoint()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue(ValueName, '"' + ExecutablePath + "\" --startup", RegistryValueKind.String);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
        catch
        {
            // 权限不够之类，静默放弃。
        }
    }

    /// <summary>本程序 exe 的路径，写进启动项时用。</summary>
    public static string ExecutablePath =>
        Environment.ProcessPath ?? throw new InvalidOperationException("拿不到当前程序的路径。");

    /// <summary>开或关。失败时 <paramref name="error"/> 给原因，UI 照原样显示。</summary>
    public static bool SetEnabled(bool enabled, out string? error)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "打不开注册表的启动项（可能被管理策略锁了）。";
                return false;
            }

            if (enabled)
            {
                // 带上 --startup：程序能分辨“开机拉起”和“用户手动打开”，
                // 于是“启动时不显示界面”只在开机自启时生效。
                key.SetValue(ValueName, '"' + ExecutablePath + "\" --startup", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            // 不管开还是关，旧值名都不该留：开着就搬过来，关了更得清掉。
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

            error = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException
            or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }
}
