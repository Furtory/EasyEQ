using System.Diagnostics;
using System.IO;

namespace PeaceWpf;

/// <summary>
/// 翻 EqualizerAPO 的安装版本。config 目录的父目录就是安装目录；
/// 找不到程序文件就返回 null，界面上显示“未检测到”。
/// </summary>
public static class ApoProbe
{
    private static readonly string[] CandidateFiles =
    {
        "EqualizerAPO.exe",
        "EqualizerAPO64.exe",
        "EqualizerAPO.dll",
        "EqualizerAPO64.dll",
    };

    /// <summary>从 config 目录反推安装目录，读主程序的文件版本。</summary>
    public static string? TryGetVersion(string? configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return null;
        }

        var root = Path.GetDirectoryName(configDirectory);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        foreach (var name in CandidateFiles)
        {
            var path = Path.Combine(root, name);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                var version = info.FileVersion ?? info.ProductVersion;
                if (!string.IsNullOrWhiteSpace(version))
                {
                    // 文件版本常带四段（1.4.2.0），显示到第三段就够。
                    var parts = version.Split('.');
                    return parts.Length >= 3 ? string.Join('.', parts.Take(3)) : version;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 读不到就换下一个候选。
            }
        }

        return null;
    }
}
