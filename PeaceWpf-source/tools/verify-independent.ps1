# 独立模式冒烟测试：用一个临时的 config 目录跑，不碰真实的 EqualizerAPO 配置。
# 步骤：启动 → 打开设置 → 拨开「独立模式」→ 看 config.txt / easyeq.txt → 拨回去 → 关程序 → 恢复 ui-state。
param(
    [string]$ConfigDir = "$env:TEMP\easyeq-independent-test",
    [string]$Exe = "$PSScriptRoot\..\src\PeaceWpf\bin\Debug\net10.0-windows\EasyEQ.exe",
    [string]$ShotDir = "$PSScriptRoot\..\docs\shots"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[W]::SetProcessDPIAware() | Out-Null

$uiState = Join-Path $env:LOCALAPPDATA 'EasyEQ\ui-state.json'
$uiBackup = Join-Path $env:TEMP 'easyeq-ui-state-backup.json'
if (Test-Path $uiState) { Copy-Item $uiState $uiBackup -Force; Write-Output "ui-state 已备份：$uiBackup" }

if (Test-Path $ConfigDir) { Remove-Item $ConfigDir -Recurse -Force }
New-Item -ItemType Directory $ConfigDir | Out-Null
Set-Content -Path (Join-Path $ConfigDir 'config.txt') -Value "Include: peace.txt`r`nInclude: protect.txt`r`n" -NoNewline
Set-Content -Path (Join-Path $ConfigDir 'peace.txt') -Value "Device: all`r`nPreamp: 0.0 dB`r`nFilter 1: ON PK Fc 1000 Hz Gain 3.0 dB Q 1.00`r`n" -NoNewline

function Show-Entry {
    param([string]$Dir)
    $p = Join-Path $Dir 'config.txt'
    Write-Output "--- config.txt ---"
    if (Test-Path $p) { Get-Content $p -Raw } else { Write-Output '(不存在)' }
}

$env:PEACEWPF_CONFIG_DIR = $ConfigDir
$proc = Start-Process -FilePath $Exe -PassThru
Write-Output "已启动 pid=$($proc.Id)，config 目录=$ConfigDir"

try {
    $main = $null
    for ($i = 0; $i -lt 60 -and -not $main; $i++) {
        Start-Sleep -Milliseconds 250
        $proc.Refresh()
        if ($proc.MainWindowHandle -ne 0) { $main = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle) }
    }
    if (-not $main) { throw '主窗口没出来' }
    Write-Output "主窗口：$($main.Current.Name)"

    $settings = $main.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'SettingsButton')))
    if (-not $settings) { throw '没找到设置按钮' }
    $settings.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1200

    $dlg = $null
    for ($i = 0; $i -lt 30 -and -not $dlg; $i++) {
        Start-Sleep -Milliseconds 200
        $dlg = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))
    }
    if (-not $dlg) { throw '设置窗口没出来' }
    Write-Output "设置窗口：$($dlg.Current.Name)"

    $toggle = $dlg.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'IndependentModeSwitch')))
    if (-not $toggle) { throw '没找到 IndependentModeSwitch' }
    Write-Output "开关：#$($toggle.Current.Name) 当前=$($toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState)"

    Show-Entry $ConfigDir

    # 打开独立模式
    $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 1800
    Write-Output "== 打开独立模式后 =="
    Show-Entry $ConfigDir
    Write-Output "easyeq.txt 存在=$(Test-Path (Join-Path $ConfigDir 'easyeq.txt'))"

    # 截图设置窗口
    $h = [IntPtr]$dlg.Current.NativeWindowHandle
    $r = New-Object W+RECT
    [W]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $hh = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $hh)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc(); [W]::PrintWindow($h, $dc, 2) | Out-Null; $g.ReleaseHdc($dc); $g.Dispose()
    $shot = Join-Path $ShotDir 'settings-independent-mode.png'
    $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Output "截图：$shot ($w x $hh)"

    # 拨回关闭
    $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 1800
    Write-Output "== 拨回关闭后 =="
    Show-Entry $ConfigDir
    Write-Output "开关现在=$($toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState)"
}
finally {
    if (-not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 800; if (-not $proc.HasExited) { $proc.Kill() } }
    if (Test-Path $uiBackup) { Copy-Item $uiBackup $uiState -Force; Remove-Item $uiBackup -Force; Write-Output 'ui-state 已恢复' }
    Remove-Item Env:\PEACEWPF_CONFIG_DIR -ErrorAction SilentlyContinue
}
