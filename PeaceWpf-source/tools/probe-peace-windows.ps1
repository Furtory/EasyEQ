Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public static List<string> List(uint target) {
        var found = new List<string>();
        EnumWindows((h, l) => {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != target) return true;
            var t = new StringBuilder(512); GetWindowTextW(h, t, 512);
            var c = new StringBuilder(256); GetClassNameW(h, c, 256);
            found.Add(h.ToInt64() + " visible=" + IsWindowVisible(h) + " class=[" + c + "] title=[" + t + "]");
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$p = Get-Process -Name Peace -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'Peace not running'; exit 1 }
Write-Output ("pid=" + $p.Id)
foreach ($line in [W]::List([uint32]$p.Id)) { Write-Output $line }
