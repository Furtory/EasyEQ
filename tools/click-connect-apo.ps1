Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$p = Get-Process PeaceWpf -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'no process'; exit 1 }
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function Find-ById($id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

$b = Find-ById 'ConnectApoButton'
if (-not $b) { Write-Output 'button not found'; exit 2 }
Write-Output ('found: ' + $b.Current.Name + ' offscreen=' + $b.Current.IsOffscreen)
$b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 1500

$hint = Find-ById 'ConnectApoButton'
Write-Output ('after: button exists=' + [bool]$hint)
$status = Find-ById 'CopyStatusButton'
Write-Output ('status-button=' + [bool]$status)
