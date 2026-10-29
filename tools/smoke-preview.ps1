param([int]$TargetPid, [string]$ConfigDir)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ErrorActionPreference = 'Stop'

$p = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
if (-not $p) { Write-Output 'no process'; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function Find-ById($id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function PeaceFile([string]$dir) { Join-Path $dir 'peace.txt' }

$mode = Find-ById 'ModeMain'
if (-not $mode) { Write-Output 'no ModeMain'; exit 2 }
$mode.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 700

$slider = Find-ById 'PreampSlider'
if (-not $slider) { Write-Output 'no PreampSlider'; exit 3 }
$slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(-6.0)
Write-Output 'set preamp to -6'
Start-Sleep -Milliseconds 900

$text = Get-Content (PeaceFile $ConfigDir) -Raw
Write-Output ('after set: ' + (($text -split "`n") | Where-Object { $_ -like 'Preamp:*' }) -join ' | ')

$cancel = Find-ById 'CancelButton'
if (-not $cancel) { Write-Output 'no CancelButton (dirty flag off?)'; exit 4 }
$cancel.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'clicked cancel'
Start-Sleep -Milliseconds 1200

$text2 = Get-Content (PeaceFile $ConfigDir) -Raw
Write-Output ('after cancel: ' + (($text2 -split "`n") | Where-Object { $_ -like 'Preamp:*' }) -join ' | ')
