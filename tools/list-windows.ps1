Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$p = Get-Process PeaceWpf -ErrorAction SilentlyContinue | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::RootElement
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($w in $all) {
  if ($w.Current.ProcessId -eq $p.Id) {
    'window: ' + $w.Current.Name + ' class=' + $w.Current.ClassName + ' hwnd=' + $w.Current.NativeWindowHandle
  }
}
