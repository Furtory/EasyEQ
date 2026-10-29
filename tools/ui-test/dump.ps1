<#
.SYNOPSIS
    把 EasyEQ 的窗口和控件结构打出来：顶层窗口、按钮、下拉框、展开中的列表项。

.DESCRIPTION
    两个坐标系，读数时务必分清（这台机器是 150% 缩放）：

    - UI Automation 的 BoundingRectangle 是物理像素（0..7800 那种）。
    - Win32 GetWindowRect 是逻辑像素（0..5200 那种），比值就是缩放系数。
    - WPF 的 PointToScreen / CustomPopupPlacement 也是物理像素。

    所以「UIA 坐标」直接用鼠标 API 的物理坐标就能对齐，「Win32 坐标」要乘缩放系数。

    窗口枚举写成 C# 静态方法：PowerShell 5.1 下委托闭包读不到脚本变量，
    放在 C# 里就没有作用域问题（踩过两次）。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/ui-test/dump.ps1
#>
param(
    [string]$ProcessName = 'EasyEQ',
    [string]$WindowTitle = '电竞调音台'
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class UiDump {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    public static List<string> ListWindows(int pid) {
        var rows = new List<string>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            int owner;
            GetWindowThreadProcessId(h, out owner);
            if (owner != pid || !IsWindowVisible(h)) return true;
            RECT r;
            GetWindowRect(h, out r);
            int w = r.Right - r.Left;
            int hh = r.Bottom - r.Top;
            if (w > 40 && hh > 40) rows.Add(r.Left + "," + r.Top + " " + w + "x" + hh);
            return true;
        }, IntPtr.Zero);
        return rows;
    }
}
"@

[UiDump]::SetProcessDPIAware() | Out-Null

$p = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { throw "没找到进程 $ProcessName，先启动程序。" }
Write-Output ("pid={0}" -f $p.Id)

Write-Output ''
Write-Output '== 顶层窗口（Win32 逻辑坐标，含 Popup）=='
foreach ($row in [UiDump]::ListWindows($p.Id)) { Write-Output ("  " + $row) }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $WindowTitle)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { throw "没找到窗口「$WindowTitle」。" }

Write-Output ''
Write-Output '== 主窗口（UIA 物理坐标）=='
Write-Output ("  " + $win.Current.BoundingRectangle)

Write-Output ''
Write-Output '== 按钮 =='
$btnCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Button)
foreach ($b in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)) {
    if ($b.Current.Name) {
        Write-Output ("  [{0}] id={1} @ {2}" -f $b.Current.Name, $b.Current.AutomationId, $b.Current.BoundingRectangle)
    }
}

Write-Output ''
Write-Output '== 下拉框 =='
$comboCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ComboBox)
foreach ($c in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $comboCond)) {
    Write-Output ("  id={0} @ {1}" -f $c.Current.AutomationId, $c.Current.BoundingRectangle)
    $items = $c.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($i in $items) {
        if ($i.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') {
            $name = [string]$i.Current.Name
            if ($name.Length -gt 40) { $name = $name.Substring(0, 40) + '...' }
            Write-Output ("    item [{0}] @ {1}" -f $name, $i.Current.BoundingRectangle)
        }
    }
}
