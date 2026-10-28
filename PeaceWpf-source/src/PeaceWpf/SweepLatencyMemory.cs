using System;
using System.Globalization;
using System.IO;
using PeaceWpf.Core;

namespace PeaceWpf;

/// <summary>
/// 扫频延迟（秒）：从「放出去」到「麦克风听到」的那一段。
///
/// 反卷积出来的曲线不受它影响（延迟只改相位），但**实时画的那条**受影响：
/// 扫到第 t 秒，麦克风听到的其实是 t−延迟 时刻的频率。不扣掉这段，
/// 高频端就会错位——20 kHz 附近扫得飞快，几十毫秒就是几百 Hz。
///
/// 所以：扫完一次就把测到的延迟存下来，下次实时曲线直接扣；
/// 每次扫频再重新测一次并覆盖——用户换声卡之后，第一次扫频就会把新延迟写进来。
/// </summary>
internal static class SweepLatencyMemory
{
    private const string FileName = "sweep-latency.txt";

    /// <summary>合理的延迟范围：负值允许一点（缓冲区起点比播放早），上限两秒。</summary>
    private const double MinSeconds = -0.2;
    private const double MaxSeconds = 2.0;

    private static bool _loaded;
    private static double _seconds;

    public static double Seconds
    {
        get
        {
            EnsureLoaded();
            return _seconds;
        }

        set
        {
            if (double.IsNaN(value) || value < MinSeconds || value > MaxSeconds)
            {
                return;
            }

            _seconds = value;
            _loaded = true;
            Save();
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            var path = Path.Combine(AppPaths.DataDirectory, FileName);
            if (File.Exists(path)
                && double.TryParse(File.ReadAllText(path).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && value >= MinSeconds
                && value <= MaxSeconds)
            {
                _seconds = value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            // 读不出来就按 0 处理：实时曲线错一点，不影响最终那条。
        }
    }

    private static void Save()
    {
        try
        {
            File.WriteAllText(
                Path.Combine(AppPaths.DataDirectory, FileName),
                _seconds.ToString("0.0000", CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 存不下就下次重测。
        }
    }
}
