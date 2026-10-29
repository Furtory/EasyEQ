Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$p = Get-Process PeaceWpf -ErrorAction SilentlyContinue | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
foreach ($b in $items) {
  'btn id=[' + $b.Current.AutomationId + '] name=[' + $b.Current.Name + '] offscreen=' + $b.Current.IsOffscreen
}
