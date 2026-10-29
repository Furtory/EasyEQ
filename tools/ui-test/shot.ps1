<#
.SYNOPSIS
    截 EasyEQ 主窗口（默认）或整个主屏，存成 PNG。

.DESCRIPTION
    用 CopyFromScreen 而不是 PrintWindow：WPF 走 DirectX，PrintWindow 经常返回全黑。
    CopyFromScreen 抓的是屏幕上真实合成后的画面，所见即所得。

.PARAMETER Out
    输出路径。默认 tools/ui-test/out/shot.png（目录会自动建）。

.PARAMETER FullScreen
    截整个主屏，而不是应用窗口。多显示器时只截主屏。

.EXAMPLE
    pwsh -NoProfile -ExecutionPolicy Bypass -File tools/ui-test/shot.ps1

.EXAMPLE
    pwsh -NoProfile -ExecutionPolicy Bypass -File tools/ui-test/shot.ps1 -Out docs/shots/a.png -FullScreen
#>
param(
    [string]$Out = (Join-Path $PSScriptRoot 'out\shot.png'),
    [switch]$FullScreen,
    [string]$ProcessName = 'EasyEQ'
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class UiShot {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

# 先声明 DPI 感知，否则高缩放下 GetWindowRect 给的是逻辑坐标，截出来会偏移/缺块。
[UiShot]::SetProcessDPIAware() | Out-Null

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

if ($FullScreen) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
}
else {
    $p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { throw "没找到进程 $ProcessName，先启动程序。" }

    $r = New-Object UiShot+RECT
    [UiShot]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
    $bounds = New-Object System.Drawing.Rectangle $r.Left, $r.Top, ($r.Right - $r.Left), ($r.Bottom - $r.Top)
}

$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output ("{0}  {1}x{2}" -f $Out, $bounds.Width, $bounds.Height)
