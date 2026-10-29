Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$p = Get-Process PeaceWpf -ErrorAction SilentlyContinue | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'SettingsButton')
$b = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
$b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900
$wins = Get-Process PeaceWpf | ForEach-Object { $_.MainWindowTitle }
$wins -join ' | '
