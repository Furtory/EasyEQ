param([string[]]$Names = @('explorer','PeaceWpf','Peace','dsh','node'))

$sig = @'
using System;
using System.Runtime.InteropServices;
public static class Tok2 {
  [DllImport("advapi32.dll", SetLastError=true)] public static extern bool OpenProcessToken(IntPtr h, uint a, out IntPtr t);
  [DllImport("advapi32.dll", SetLastError=true)] public static extern bool GetTokenInformation(IntPtr t, int c, IntPtr buf, int len, out int need);
  [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr OpenProcess(uint a, bool i, int pid);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  public static string Info(int pid) {
    IntPtr hp = OpenProcess(0x0400, false, pid);
    if (hp == IntPtr.Zero) return "OpenProcess err " + Marshal.GetLastWin32Error();
    IntPtr t;
    if (!OpenProcessToken(hp, 0x0008, out t)) return "OpenToken err " + Marshal.GetLastWin32Error();
    string s = "";
    int need;
    IntPtr b4 = Marshal.AllocHGlobal(4);
    if (GetTokenInformation(t, 20, b4, 4, out need)) s += "Elevated=" + (Marshal.ReadInt32(b4) != 0) + " ";
    if (GetTokenInformation(t, 29, b4, 4, out need)) s += "AppContainer=" + (Marshal.ReadInt32(b4) != 0) + " ";
    GetTokenInformation(t, 25, IntPtr.Zero, 0, out need);
    IntPtr b = Marshal.AllocHGlobal(need);
    if (GetTokenInformation(t, 25, b, need, out need)) {
      IntPtr sid = Marshal.ReadIntPtr(b);
      int cnt = Marshal.ReadByte(sid, 1);
      IntPtr last = new IntPtr(sid.ToInt64() + 8 + 4*(cnt-1));
      uint rid = (uint)Marshal.ReadInt32(last) >> 12;
      s += "Integrity=" + rid + " (" + (rid==0?"Untrusted":rid==1?"Low":rid==2?"Medium":rid==3?"High":"Other") + ")";
    } else s += "IntegrityErr=" + Marshal.GetLastWin32Error();
    Marshal.FreeHGlobal(b4); Marshal.FreeHGlobal(b);
    CloseHandle(t); CloseHandle(hp);
    return s;
  }
}
'@
Add-Type -TypeDefinition $sig -Language CSharp

foreach ($n in $Names) {
  Get-Process $n -ErrorAction SilentlyContinue | ForEach-Object {
    "{0,-12} pid={1,-6} {2}" -f $_.ProcessName, $_.Id, [Tok2]::Info($_.Id)
  }
}
"self         pid=$PID   " + [Tok2]::Info($PID)
