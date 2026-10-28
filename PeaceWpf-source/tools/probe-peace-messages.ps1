Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class N {
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
}
"@

$h = [N]::FindWindow($null, 'Peace window messages')
Write-Output ("hwnd=" + $h + " (zero=" + ($h -eq [IntPtr]::Zero) + ")")

if ($h -ne [IntPtr]::Zero) {
    $r = [IntPtr]::Zero
    $ok = [N]::SendMessageTimeout($h, 0x8001, [IntPtr]3, [IntPtr]::Zero, 3, 2000, [ref]$r)
    $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    Write-Output ("interface-state: ok=" + $ok + " err=" + $err + " result=" + $r.ToInt64())

    $r2 = [IntPtr]::Zero
    $ok2 = [N]::SendMessageTimeout($h, 0x8016, [IntPtr]13, [IntPtr]::Zero, 3, 2000, [ref]$r2)
    $err2 = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    Write-Output ("slider-count: ok=" + $ok2 + " err=" + $err2 + " result=" + $r2.ToInt64())
}

$p = Get-Process -Name Peace -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) {
    Write-Output ("peace pid=" + $p.Id + " path=" + $p.Path + " title=" + $p.MainWindowTitle)
}
