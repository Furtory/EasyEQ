Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$p = Get-Process -Name EasyEQ -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'EasyEQ not running'; exit 1 }

$out = Join-Path $env:TEMP 'easyeq-status-text.txt'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("pid=" + $p.Id + " title=" + $p.MainWindowTitle)

$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Text)

foreach ($item in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
    $name = $item.Current.Name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    $lines.Add("[" + $item.Current.AutomationId + "] " + $name)
}

[System.IO.File]::WriteAllLines($out, $lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("written: " + $out + " lines=" + $lines.Count)
