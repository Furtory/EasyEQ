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
    private const string ValueName = "PeaceWpf";

    /// <summary>现在开没开。读不到注册表就当没开，不抛。</summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
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
