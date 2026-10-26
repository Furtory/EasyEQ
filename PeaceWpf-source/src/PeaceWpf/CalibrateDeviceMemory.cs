using System.IO;
using PeaceWpf.Core.Model;

namespace PeaceWpf;

/// <summary>
/// 记住自动EQ 上次用的录音设备。
///
/// 不放进 ui-state.json：那个文件由主窗口整份读写，这里改一笔会把主窗口内存里的状态冲掉。
/// 单独一个小文件，坏了大不了读回默认设备。
/// </summary>
internal static class CalibrateDeviceMemory
{
    private static string FilePath => System.IO.Path.Combine(UiState.Directory, "calibrate-device.txt");

    public static string? Load()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        try
        {
            System.IO.Directory.CreateDirectory(UiState.Directory);
            File.WriteAllText(FilePath, deviceId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 记不住设备不该挡住测量。
        }
    }
}
