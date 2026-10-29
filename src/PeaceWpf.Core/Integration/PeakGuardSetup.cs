using System.Text;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.Integration;

/// <summary>
/// 无延迟防爆音：把内置的 ClipOnly3（Airwindows，MIT）放进 APO 读得到的位置，
/// 再用一个独立的 <c>protect.txt</c> 把它挂在 <c>peace.txt</c> 之后。
///
/// 开关只改 <c>protect.txt</c> 的内容，不碰 <c>peace.txt</c>，也不改 EQ 的任何一行。
/// 关掉时文件里只剩一行注释——APO 读到空配置，链路等于没有它。
/// </summary>
public static class PeakGuardSetup
{
    /// <summary>随程序分发的 VST。</summary>
    public const string VstFileName = "ClipOnly364.dll";

    /// <summary>承载开关的配置文件，靠 <c>Include</c> 挂在 <c>config.txt</c> 末尾。</summary>
    public const string ProtectFileName = "protect.txt";

    /// <summary>优先放这里：config 目录自己管得到，audiodg 也读得到。</summary>
    public const string VstSubDirectory = "VST";

    private const string DisabledText =
        "# 无延迟防爆音：关\r\n";

    /// <summary>程序自带的那份 DLL 在输出目录里的位置。</summary>
    public static string SourcePath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", VstFileName);

    /// <summary>
    /// 开或关。打开时依次做三件事：放 DLL、确保 <c>config.txt</c> 挂了 <c>protect.txt</c>、写 <c>protect.txt</c>。
    /// 任何一步失败都返回原因，调用方据此把开关弹回去。
    /// </summary>
    public static PeakGuardResult Apply(
        string? configDirectory,
        bool enabled,
        string? backupDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(configDirectory) || !Directory.Exists(configDirectory))
        {
            return new PeakGuardResult(false, "找不到 EqualizerAPO 的 config 目录。", null);
        }

        if (!enabled)
        {
            return TryWrite(configDirectory, DisabledText, out var offError)
                ? new PeakGuardResult(true, null, null)
                : new PeakGuardResult(false, offError, null);
        }

        var dll = Install(configDirectory, out var installError);
        if (dll is null)
        {
            return new PeakGuardResult(false, installError, null);
        }

        var include = ApoSetup.EnsureInclude(
            configDirectory,
            ProtectFileName,
            backupDirectory ?? UiState.Directory);
        if (include.Error is not null)
        {
            return new PeakGuardResult(false, include.Error, dll);
        }

        return TryWrite(configDirectory, BuildEnabledText(dll), out var writeError)
            ? new PeakGuardResult(true, null, dll)
            : new PeakGuardResult(false, writeError, dll);
    }

    /// <summary>现在是不是真的挂着 VST。</summary>
    public static bool IsApplied(string? configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory) || !Directory.Exists(configDirectory))
        {
            return false;
        }

        try
        {
            var path = Path.Combine(configDirectory, ProtectFileName);
            if (!File.Exists(path))
            {
                return false;
            }

            return File.ReadAllText(path)
                .Contains("VSTPlugin:", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>生成打开状态的文件内容。单独拿出来是为了能直接测。</summary>
    public static string BuildEnabledText(string dllPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dllPath);
        return "# 由「电竞调音台」自动生成，勿手改；关掉开关会清空这里。\r\n"
            + $"VSTPlugin: Library \"{dllPath}\"\r\n";
    }

    /// <summary>生成关闭状态的文件内容。</summary>
    public static string BuildDisabledText() => DisabledText;

    /// <summary>把 DLL 放到第一个写得进去的位置，返回最终路径。</summary>
    private static string? Install(string configDirectory, out string? error)
    {
        error = null;
        var source = SourcePath;
        if (!File.Exists(source))
        {
            error = $"程序里缺少内置的 {VstFileName}。";
            return null;
        }

        // 先试 config\VST（和 config.txt 同权限），不行再试安装目录的 VSTPlugins。
        var candidates = new[]
        {
            Path.Combine(configDirectory, VstSubDirectory),
            Path.GetFullPath(Path.Combine(configDirectory, "..", "VSTPlugins")),
        };

        var failures = new List<string>();
        foreach (var directory in candidates)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var target = Path.Combine(directory, VstFileName);
                var sourceLength = new FileInfo(source).Length;
                var targetInfo = new FileInfo(target);
                if (!targetInfo.Exists || targetInfo.Length != sourceLength)
                {
                    File.Copy(source, target, overwrite: true);
                }

                return target;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{directory}：{ex.Message}");
            }
        }

        error = "放不进 APO 目录。" + string.Join("；", failures);
        return null;
    }

    private static bool TryWrite(string configDirectory, string text, out string? error)
    {
        error = null;
        try
        {
            var path = Path.Combine(configDirectory, ProtectFileName);
            EapoConfig.ClearReadOnly(path);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"写不进 {ProtectFileName}：{ex.Message}";
            return false;
        }
    }
}

/// <summary>开关处理结果。</summary>
/// <param name="Changed">配置有没有真的动。</param>
/// <param name="Error">失败原因；成功时是 null。</param>
/// <param name="DllPath">DLL 最终放在哪；关掉时是 null。</param>
public sealed record PeakGuardResult(bool Changed, string? Error, string? DllPath);
