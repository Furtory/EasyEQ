Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class M {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    public static IntPtr Find(string title) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => {
            var t = new StringBuilder(512); GetWindowTextW(h, t, 512);
            if (t.ToString() == title) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$h = [M]::Find('Peace window messages')
Write-Output ("hwnd=" + $h)
if ($h -eq [IntPtr]::Zero) { exit 1 }

function Send($msg, $w, $label) {
    $r = [IntPtr]::Zero
    $ok = [M]::SendMessageTimeout($h, [uint32]$msg, [IntPtr]$w, [IntPtr]::Zero, 3, 2000, [ref]$r)
    $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    Write-Output ($label + ": ok=" + $ok + " err=" + $err + " result=" + $r.ToInt64())
}

Send 0x8001 3 'interface-state'
Send 0x8016 13 'slider-count'
Send 0x8002 5 'config-id'
