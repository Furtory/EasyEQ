Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class T {
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("advapi32.dll", SetLastError = true)] public static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] public static extern bool GetTokenInformation(IntPtr token, int cls, IntPtr info, uint len, out uint needed);
    [DllImport("advapi32.dll")] public static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

    public static string Integrity(uint pid) {
        IntPtr proc = OpenProcess(0x1000, false, pid);
        if (proc == IntPtr.Zero) return "OpenProcess failed: " + Marshal.GetLastWin32Error();
        IntPtr token;
        if (!OpenProcessToken(proc, 0x0008, out token)) { CloseHandle(proc); return "OpenProcessToken failed: " + Marshal.GetLastWin32Error(); }
        uint needed;
        GetTokenInformation(token, 25, IntPtr.Zero, 0, out needed);
        IntPtr buf = Marshal.AllocHGlobal((int)needed);
        string result;
        if (GetTokenInformation(token, 25, buf, needed, out needed)) {
            IntPtr sid = Marshal.ReadIntPtr(buf);
            int rid = Marshal.ReadInt32(GetSidSubAuthority(sid, 0));
            if (rid == 0x1000) result = "Medium";
            else if (rid == 0x2000) result = "High";
            else if (rid == 0x3000) result = "System";
            else result = "0x" + rid.ToString("X");
        } else {
            result = "GetTokenInformation failed: " + Marshal.GetLastWin32Error();
        }
        Marshal.FreeHGlobal(buf);
        CloseHandle(token);
        CloseHandle(proc);
        return result;
    }
}
"@

foreach ($name in @('EasyEQ', 'Peace')) {
    $p = Get-Process -Name $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { Write-Output ($name + " pid=" + $p.Id + " integrity=" + [T]::Integrity([uint32]$p.Id)) }
    else { Write-Output ($name + " not running") }
}
