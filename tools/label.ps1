param([string[]]$Paths)

if (-not $Paths) {
  $exe = Get-ChildItem 'D:\AHK' -Recurse -Filter 'EasyEQ.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
  $Paths = @(
    $exe.FullName,
    (Split-Path $exe.FullName -Parent),
    "$env:LOCALAPPDATA\EasyEQ",
    "$env:LOCALAPPDATA\EasyEQ\peace.txt.bak",
    'C:\Program Files\EqualizerAPO\config',
    'C:\Program Files\EqualizerAPO\config\peace.txt'
  )
}

$src = @'
using System;
using System.Runtime.InteropServices;

public static class Label {
  [DllImport("advapi32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
  static extern uint GetNamedSecurityInfo(string name, int type, int info,
      out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr sd);

  [DllImport("kernel32.dll")]
  static extern IntPtr LocalFree(IntPtr p);

  public static string Read(string path) {
    IntPtr owner, group, dacl, sacl, sd;
    uint err = GetNamedSecurityInfo(path, 1, 0x10, out owner, out group, out dacl, out sacl, out sd);
    if (err != 0) return "error " + err;
    try {
      if (sacl == IntPtr.Zero) return "no label (medium by default)";
      ushort aceCount = (ushort)Marshal.ReadInt16(sacl, 4);
      IntPtr ace = new IntPtr(sacl.ToInt64() + 8);
      for (int i = 0; i < aceCount; i++) {
        byte aceType = Marshal.ReadByte(ace, 0);
        ushort aceSize = (ushort)Marshal.ReadInt16(ace, 2);
        if (aceType == 0x11) {
          IntPtr sid = new IntPtr(ace.ToInt64() + 8);
          int count = Marshal.ReadByte(sid, 1);
          IntPtr last = new IntPtr(sid.ToInt64() + 8 + 4 * (count - 1));
          uint rid = (uint)Marshal.ReadInt32(last) >> 12;
          return "label RID=" + rid + " (" + Name(rid) + ")";
        }
        ace = new IntPtr(ace.ToInt64() + aceSize);
      }
      return "no mandatory label ace";
    } finally { LocalFree(sd); }
  }

  static string Name(uint rid) {
    if (rid == 0) return "Untrusted";
    if (rid == 1) return "Low";
    if (rid == 2) return "Medium";
    if (rid == 3) return "High";
    if (rid == 4) return "System";
    return "?";
  }
}
'@
Add-Type -TypeDefinition $src -Language CSharp

foreach ($p in $Paths) {
  if (-not (Test-Path -LiteralPath $p)) { "missing  $p"; continue }
  $t = if (Test-Path -LiteralPath $p -PathType Container) { 'dir ' } else { 'file' }
  '{0} {1}  ->  {2}' -f $t, $p, [Label]::Read($p)
}
