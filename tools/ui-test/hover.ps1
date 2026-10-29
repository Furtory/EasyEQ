<#
.SYNOPSIS
    展开一个下拉，逐行悬停，读出悬浮气泡窗口的真实位置——用数字判断「气泡到底动没动」。

.DESCRIPTION
    比截图可靠：截图上「看着差不多」和「真的一样」分不出来，坐标能。
    2026-10-30 那个「气泡固定不动」的 bug 就是靠这个定住的：
    气泡每一行都落在同一 y，因为窗口底边被单位混用算小了一截（150% 缩放，1292 被当成 861）。

    做法：UIA 读列表项的物理坐标 → SetCursorPos 移鼠标 → EnumWindows 找气泡窗口 → 比位置。

    气泡靠尺寸筛：宽 300..600、高 120..400。主窗口和下拉弹层都不在这个范围里。
    窗口枚举放在 C# 里：PowerShell 5.1 下委托闭包读不到脚本变量。

.PARAMETER Combo
    哪个下拉。数字 = 第几个 ComboBox（0 起）；字符串 = AutomationId（如 MainPresetCombo）。

.PARAMETER Rows
    要悬停的行号（0 起），逗号分隔。-1 表示最后一行。
    默认「0,-1」——首行和末行最能暴露夹紧问题。
    注意这里收字符串：powershell -File 传不了数组参数。

.PARAMETER KeepOpen
    测完不收起下拉，方便你接着手动看。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/ui-test/hover.ps1 -Combo MainPresetCombo

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/ui-test/hover.ps1 -Combo 2 -Rows "0,3,7"
#>
param(
    [string]$Combo = '0',
    [string]$Rows = '0,-1',
    [string]$WindowTitle = '电竞调音台',
    [switch]$KeepOpen
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class UiHover {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    // 找出所有「像气泡」的可见窗口：left,top,bottom,宽x高
    public static List<string> ListTips(int pid) {
        var rows = new List<string>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            int owner;
            GetWindowThreadProcessId(h, out owner);
            if (owner != pid || !IsWindowVisible(h)) return true;
            RECT r;
            GetWindowRect(h, out r);
            int w = r.Right - r.Left;
            int hh = r.Bottom - r.Top;
            if (w >= 300 && w <= 600 && hh >= 120 && hh <= 400) {
                rows.Add(r.Left + "," + r.Top + "," + r.Bottom + "," + w + "x" + hh);
            }
            return true;
        }, IntPtr.Zero);
        return rows;
    }
}
"@

[UiHover]::SetProcessDPIAware() | Out-Null

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $WindowTitle)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { throw "没找到窗口「$WindowTitle」，先启动程序。" }

$comboCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ComboBox)
$combos = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $comboCond)

$target = $null
if ($Combo -match '^\d+$') {
    $idx = [int]$Combo
    if ($idx -ge $combos.Count) { throw "只有 $($combos.Count) 个下拉，取不到第 $idx 个。" }
    $target = $combos[$idx]
}
else {
    foreach ($c in $combos) { if ($c.Current.AutomationId -eq $Combo) { $target = $c } }
    if (-not $target) { throw "没有 AutomationId 为 $Combo 的下拉。" }
}

Write-Output ("combo id={0} @ {1}" -f $target.Current.AutomationId, $target.Current.BoundingRectangle)

$target.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
Start-Sleep -Milliseconds 900

$items = $target.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.Condition]::TrueCondition)
$list = @()
foreach ($i in $items) {
    if ($i.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') {
        $list += [pscustomobject]@{ Name = [string]$i.Current.Name; Rect = $i.Current.BoundingRectangle }
    }
}
if ($list.Count -eq 0) { throw '下拉展开了但读不到列表项。' }
Write-Output ("rows: {0}" -f $list.Count)

$rowIndexes = @()
foreach ($part in $Rows.Split(',')) {
    if ($part.Trim() -ne '') { $rowIndexes += [int]$part.Trim() }
}

$procId = (Get-Process -Name 'EasyEQ' -ErrorAction SilentlyContinue | Select-Object -First 1).Id
$previousTop = $null

# 先悬停一次预热：第一次弹出要建 Popup，会慢半拍。
$pre = $list[0].Rect
[void][UiHover]::SetCursorPos([int]($pre.X + 60), [int]($pre.Y + $pre.Height / 2))
Start-Sleep -Milliseconds 700

# 移开一下：同一个位置不会再触发 MouseMove，第一行会读不到气泡。
[void][UiHover]::SetCursorPos(10, 10)
Start-Sleep -Milliseconds 300

foreach ($row in $rowIndexes) {
    $index = if ($row -lt 0) { $list.Count + $row } else { $row }
    if ($index -lt 0 -or $index -ge $list.Count) { continue }

    # 每次先移开再进入：同一位置不会重复触发 MouseMove，首行会读不到气泡。
    [void][UiHover]::SetCursorPos(10, 10)
    Start-Sleep -Milliseconds 200

    $rect = $list[$index].Rect
    $x = [int]($rect.X + 60)
    $y = [int]($rect.Y + $rect.Height / 2)
    [void][UiHover]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 900

    $name = $list[$index].Name
    if ($name.Length -gt 24) { $name = $name.Substring(0, 24) + '...' }

    $tips = [UiHover]::ListTips($procId)
    if ($tips.Count -eq 0) {
        Write-Output ("row{0,-2} [{1}] tip=<没有气泡>" -f $index, $name)
        continue
    }

    foreach ($tip in $tips) {
        $parts = $tip.Split(',')
        $top = [int]$parts[1]
        $move = if ($null -eq $previousTop) { '' } elseif ($top -eq $previousTop) { '  <- 和上一行相同' } else { '  <- 移动了' }
        $left = $parts[0]
        $topText = $parts[1]
        $bottomText = $parts[2]
        $size = $parts[3]
        Write-Output ("row{0} [{1}] tip={2},{3}-{4} ({5}){6}" -f $index, $name, $left, $topText, $bottomText, $size, $move)
        $previousTop = $top
    }
}

if (-not $KeepOpen) {
    try { $target.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch { }
}
