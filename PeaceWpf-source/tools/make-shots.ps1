Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W5 {
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[W5]::SetProcessDPIAware() | Out-Null

$script:winList = @()
function Get-Windows([int]$procId) {
    $script:winList = @()
    $cb = [W5+EnumProc]{
        param($h, $l)
        $owner = 0
        [W5]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -eq $procId -and [W5]::IsWindowVisible($h)) {
            $r = New-Object W5+RECT
            [W5]::GetWindowRect($h, [ref]$r) | Out-Null
            $script:winList += [pscustomobject]@{ Handle = $h; W = $r.Right - $r.Left; H = $r.Bottom - $r.Top }
        }
        return $true
    }
    [W5]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
}

function Find-Id($handle, $id) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Click-Id($handle, $id) {
    $b = Find-Id $handle $id
    if (-not $b) { return $false }
    $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    return $true
}

function Shot($handle, $path) {
    $r = New-Object W5+RECT
    [W5]::GetWindowRect($handle, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    [W5]::PrintWindow($handle, $dc, 2) | Out-Null
    $g.ReleaseHdc($dc)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output ("saved " + $path)
}

$out = (Resolve-Path (Join-Path $PSScriptRoot '..\docs\shots')).Path
$p = Get-Process PeaceWpf -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'no process'; exit 1 }

Click-Id $p.MainWindowHandle 'AutoCalibrateButton' | Out-Null
Start-Sleep -Milliseconds 1600

Get-Windows $p.Id
$picker = $script:winList | Where-Object { $_.W -gt 300 -and $_.W -lt 1200 } | Sort-Object W | Select-Object -First 1
if (-not $picker) { Write-Output 'no picker'; exit 2 }
Start-Sleep -Milliseconds 400
Shot $picker.Handle ($out + '\12-calibrate-target.png')

Click-Id $picker.Handle 'CalibrateFlatButton' | Out-Null
Start-Sleep -Milliseconds 1800

Get-Windows $p.Id
$cal = $script:winList | Where-Object { $_.W -gt 1200 -and $_.W -lt 1600 } | Sort-Object W | Select-Object -First 1
if (-not $cal) { Write-Output 'no calibrate window'; exit 3 }
Start-Sleep -Milliseconds 600
Shot $cal.Handle ($out + '\13-autocalibrate-window.png')
