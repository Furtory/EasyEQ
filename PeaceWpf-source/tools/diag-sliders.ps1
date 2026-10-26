param(
    [string]$ConfigDir = 'C:\Program Files\EqualizerAPO\config',
    [int]$Pid2 = 0
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ErrorActionPreference = 'Stop'

$p = if ($Pid2 -ne 0) { Get-Process -Id $Pid2 } else { Get-Process PeaceWpf | Select-Object -First 1 }
Write-Output ("pid=" + $p.Id + " hwnd=" + $p.MainWindowHandle)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function Find-ById($id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Get-Text($id) {
    $e = Find-ById $id
    if (-not $e) { return '<none>' }
    try { return $e.GetCurrentPropertyValue([System.Windows.Automation.AutomationElement]::NameProperty) } catch { return '<err>' }
}

function Get-Range($id) {
    $e = Find-ById $id
    if (-not $e) { return $null }
    return $e.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
}

function Set-Range($id, $value) {
    $r = Get-Range $id
    if (-not $r) { Write-Output "no $id"; return $false }
    $r.SetValue($value)
    return $true
}

function Show-File([string]$tag) {
    $path = Join-Path $ConfigDir 'peace.txt'
    $lines = Get-Content $path
    $pre = ($lines | Where-Object { $_ -like 'Preamp:*' }) -join ' | '
    $ch = ($lines | Where-Object { $_ -like 'Channel:*' }) -join ' | '
    $stamp = (Get-Item $path).LastWriteTime.ToString('HH:mm:ss.fff')
    Write-Output ("[$tag] mtime=$stamp preamp=[$pre] channel=[$ch]")
}

Write-Output ("enabled(preamp)=" + (Find-ById 'PreampSlider').Current.IsEnabled)
Write-Output ("slider preamp=" + (Get-Range 'PreampSlider').Value + " label=" + (Get-Text 'PreampValue'))
Write-Output ("slider balance=" + (Get-Range 'BalanceSlider').Value + " label=" + (Get-Text 'BalanceSummary'))
Write-Output ("status=" + (Get-Text 'StatusText'))
Show-File 'before'

$pre0 = (Get-Range 'PreampSlider').Value
$bal0 = (Get-Range 'BalanceSlider').Value

Write-Output '--- set preamp -20 ---'
Set-Range 'PreampSlider' -20 | Out-Null
Start-Sleep -Milliseconds 1200
Write-Output ("slider preamp=" + (Get-Range 'PreampSlider').Value + " label=" + (Get-Text 'PreampValue'))
Write-Output ("status=" + (Get-Text 'StatusText'))
Show-File 'after-preamp'

Write-Output '--- set balance 0.6 ---'
Set-Range 'BalanceSlider' 0.6 | Out-Null
Start-Sleep -Milliseconds 1200
Write-Output ("slider balance=" + (Get-Range 'BalanceSlider').Value + " label=" + (Get-Text 'BalanceSummary'))
Write-Output ("status=" + (Get-Text 'StatusText'))
Show-File 'after-balance'

Write-Output '--- restore ---'
Set-Range 'PreampSlider' $pre0 | Out-Null
Set-Range 'BalanceSlider' $bal0 | Out-Null
Start-Sleep -Milliseconds 1500
Write-Output ("status=" + (Get-Text 'StatusText'))
Show-File 'restored'
