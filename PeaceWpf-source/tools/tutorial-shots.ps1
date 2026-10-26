param([switch]$TestAudio)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class TutShot {
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$ErrorActionPreference = 'Stop'
[TutShot]::SetProcessDPIAware() | Out-Null

$p = Get-Process PeaceWpf -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } |
    Select-Object -First 1
if (-not $p) { throw 'PeaceWpf 没在运行' }

$main = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
if (-not $main) { throw '拿不到主窗口的 UIA 根' }

function Find-ById($root, [string]$id) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-ById($root, [string]$id, [string]$label) {
    $el = Find-ById $root $id
    if (-not $el) { throw "找不到 $label（$id）" }
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Wait-Enabled($root, [string]$id, [int]$timeoutMs = 5000) {
    $until = (Get-Date).AddMilliseconds($timeoutMs)
    do {
        $el = Find-ById $root $id
        if ($el -and $el.Current.IsEnabled) { return $el }
        Start-Sleep -Milliseconds 120
    } while ((Get-Date) -lt $until)

    return $null
}

function Find-Window([int]$processId, [string]$titlePrefix) {
    $script:found = [IntPtr]::Zero
    $cb = [TutShot+EnumProc]{
        param($h, $l)
        $owner = 0
        [TutShot]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -ne $processId -or -not [TutShot]::IsWindowVisible($h)) { return $true }
        $sb = New-Object System.Text.StringBuilder 512
        [TutShot]::GetWindowText($h, $sb, 512) | Out-Null
        if ($sb.ToString().StartsWith($titlePrefix)) {
            $script:found = $h
            return $false
        }
        return $true
    }
    [TutShot]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $script:found
}

function Wait-Window([int]$processId, [string]$titlePrefix, [int]$timeoutMs = 3500) {
    $until = (Get-Date).AddMilliseconds($timeoutMs)
    do {
        $h = Find-Window $processId $titlePrefix
        if ($h -ne [IntPtr]::Zero) { return $h }
        Start-Sleep -Milliseconds 120
    } while ((Get-Date) -lt $until)

    return [IntPtr]::Zero
}

function Wait-NoWindow([int]$processId, [string]$titlePrefix, [int]$timeoutMs = 2500) {
    $until = (Get-Date).AddMilliseconds($timeoutMs)
    do {
        if ((Find-Window $processId $titlePrefix) -eq [IntPtr]::Zero) { return $true }
        Start-Sleep -Milliseconds 120
    } while ((Get-Date) -lt $until)

    return $false
}

function Shot([IntPtr]$h, [string]$path) {
    $r = New-Object TutShot+RECT
    [TutShot]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left
    $ht = $r.Bottom - $r.Top
    if ($w -le 0 -or $ht -le 0) { throw "窗口尺寸不对：$w x $ht" }
    $bmp = New-Object System.Drawing.Bitmap($w, $ht)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    [TutShot]::PrintWindow($h, $dc, 2) | Out-Null
    $g.ReleaseHdc($dc)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output ("saved " + $path + " (" + $w + "x" + $ht + ")")
}

# 全屏遮罩是独立窗口，PrintWindow 只能拍到它自己。这里按主窗口 + 功能窗口的并集截屏，
# 这样气泡、目标高亮和被打开的设置 / 预设EQ / 自动EQ 会一起进图。
function Shot-Screen([IntPtr]$mainHandle, [string]$path) {
    $left = 0; $top = 0; $right = 0; $bottom = 0
    $has = $false

    $rects = @()
    $mr = New-Object TutShot+RECT
    [TutShot]::GetWindowRect($mainHandle, [ref]$mr) | Out-Null
    $rects += $mr

    $cb = [TutShot+EnumProc]{
        param($h, $l)
        $owner = 0
        [TutShot]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -ne $p.Id -or -not [TutShot]::IsWindowVisible($h)) { return $true }
        $sb = New-Object System.Text.StringBuilder 512
        [TutShot]::GetWindowText($h, $sb, 512) | Out-Null
        $title = $sb.ToString()
        if ($title -eq '教程 · 引导' -or $title -eq '教程 · 选一个') { return $true }
        $r = New-Object TutShot+RECT
        [TutShot]::GetWindowRect($h, [ref]$r) | Out-Null
        if ($r.Right -gt $r.Left -and $r.Bottom -gt $r.Top) {
            $script:extraRects += $r
        }
        return $true
    }
    $script:extraRects = @()
    [TutShot]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    $rects += $script:extraRects

    foreach ($r in $rects) {
        if (-not $has) {
            $left = $r.Left; $top = $r.Top; $right = $r.Right; $bottom = $r.Bottom
            $has = $true
        } else {
            $left = [Math]::Min($left, $r.Left)
            $top = [Math]::Min($top, $r.Top)
            $right = [Math]::Max($right, $r.Right)
            $bottom = [Math]::Max($bottom, $r.Bottom)
        }
    }

    $left -= 0; $top -= 0; $right += 0; $bottom += 0
    $w = $right - $left
    $ht = $bottom - $top
    if ($w -le 0 -or $ht -le 0) { throw "截屏尺寸不对：$w x $ht" }

    $bmp = New-Object System.Drawing.Bitmap($w, $ht)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($left, $top, 0, 0, (New-Object System.Drawing.Size($w, $ht)))
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output ("saved " + $path + " (" + $w + "x" + $ht + ")")
}

function Choose-Course([string]$buttonId) {
    $handle = Wait-Window $p.Id '教程 · 选一个'
    if ($handle -eq [IntPtr]::Zero) { throw '选择基础还是进阶的窗口没出现' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    Invoke-ById $window $buttonId '课程按钮'
}

function Click-Next($root, [string]$label) {
    $next = Wait-Enabled $root 'TutorialNextButton'
    if (-not $next) { throw "下一步没出来：$label" }
    $next.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1400
}

$out = (Resolve-Path (Join-Path $PSScriptRoot '..\docs\shots')).Path

# 1. 教程按钮必须在设置左边
$tutorial = Find-ById $main 'TutorialButton'
$settings = Find-ById $main 'SettingsButton'
if (-not $tutorial -or -not $settings) { throw '找不到 TutorialButton / SettingsButton' }
$tr = $tutorial.Current.BoundingRectangle
$sr = $settings.Current.BoundingRectangle
if ($tr.Right -gt $sr.Left) {
    throw ("教程按钮不在设置左边：tutorial.Right={0}, settings.Left={1}" -f $tr.Right, $sr.Left)
}
Write-Output ("layout ok: tutorial.Right={0}, settings.Left={1}" -f $tr.Right, $sr.Left)

# 2. 基础课：选择窗口 + 主界面 + 设置窗口 + 预设EQ窗口 + 自动EQ窗口 + 保存
$tutorial.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
$choiceHandle = Wait-Window $p.Id '教程 · 选一个'
if ($choiceHandle -eq [IntPtr]::Zero) { throw '选择窗口没出现' }
Start-Sleep -Milliseconds 300
Shot $choiceHandle (Join-Path $out '17-tutorial-choice.png')

Choose-Course 'TutorialBasicChoiceButton'
$overlayHandle = Wait-Window $p.Id '教程 · 引导'
if ($overlayHandle -eq [IntPtr]::Zero) { throw '教程遮罩没出现' }
$overlay = [System.Windows.Automation.AutomationElement]::FromHandle($overlayHandle)
$next = Wait-Enabled $overlay 'TutorialNextButton'
if (-not $next) { throw '基础课气泡没出现，或下一步一直不可用' }
Start-Sleep -Milliseconds 300
Shot-Screen $p.MainWindowHandle (Join-Path $out '18-tutorial-basic.png')

# 第 2 步：设置窗口里的状态
Click-Next $overlay '打开设置'
$settingsHandle = Wait-Window $p.Id '设置'
if ($settingsHandle -eq [IntPtr]::Zero) { throw '教程没打开设置窗口' }
Start-Sleep -Milliseconds 300
Shot-Screen $p.MainWindowHandle (Join-Path $out '21-tutorial-settings.png')

# 第 3 步：设置里的配置目录；第 4 步回主界面；第 5 步打开预设EQ
Click-Next $overlay '设置第 3 步'
Click-Next $overlay '回主界面'
Click-Next $overlay '打开预设EQ'
$autoEqHandle = Wait-Window $p.Id '预设EQ'
if ($autoEqHandle -eq [IntPtr]::Zero) { throw '教程没打开预设EQ窗口' }
Start-Sleep -Milliseconds 300
Shot-Screen $p.MainWindowHandle (Join-Path $out '22-tutorial-presseq.png')

# 第 6 步：预设EQ 里只讲搜索；第 7 步回主界面点自动EQ，弹出「校准到哪条曲线」
Click-Next $overlay '预设EQ 搜索第 6 步'
Click-Next $overlay '打开自动EQ'
$targetHandle = Wait-Window $p.Id '自动EQ · 校准到哪条曲线'
if ($targetHandle -eq [IntPtr]::Zero) { throw '教程没打开校准目标窗口' }
Start-Sleep -Milliseconds 300
Shot-Screen $p.MainWindowHandle (Join-Path $out '25-tutorial-target.png')

# 第 8、9 步：平直 / 哈曼两张卡；选完窗口关掉，进测量窗口
Click-Next $overlay '平直曲线第 8 步'
Click-Next $overlay '哈曼曲线第 9 步'
if (-not (Wait-NoWindow $p.Id '自动EQ · 校准到哪条曲线')) { throw '选完目标后校准窗口没关' }
$calibrateHandle = Wait-Window $p.Id '自动EQ · 扫频校准'
if ($calibrateHandle -eq [IntPtr]::Zero) { throw '教程没打开自动EQ窗口' }
Start-Sleep -Milliseconds 300
Shot-Screen $p.MainWindowHandle (Join-Path $out '23-tutorial-calibrate.png')

# 第 10~13 步都在测量窗口里：测试模式 → 矫正起点 → 开始测量 → 套用
for ($i = 0; $i -lt 4; $i++) {
    Click-Next $overlay ("自动EQ 第 $($i + 10) 步")
    if ($i -eq 1) {
        $autoEqCurve = Find-ById $overlay 'DemoPanelTitle'
        if (-not $autoEqCurve -or $autoEqCurve.Current.Name -ne '模拟：拖黄线改矫正起点') {
            $name = if ($autoEqCurve) { $autoEqCurve.Current.Name } else { '找不到曲线标题' }
            throw ('自动EQ看曲线步骤没有显示模拟曲线：' + $name)
        }
        Shot-Screen $p.MainWindowHandle (Join-Path $out '28-tutorial-autoeq-curve.png')
        Write-Output 'AutoEQ simulated curve shown'
    }
}

Invoke-ById $overlay 'TutorialSkipButton' '跳过按钮'
if (-not (Wait-NoWindow $p.Id '教程 · 引导')) { throw '跳过之后遮罩没消失' }
if (-not (Wait-NoWindow $p.Id '设置')) { throw '跳过之后设置窗口没关' }
if (-not (Wait-NoWindow $p.Id '预设EQ')) { throw '跳过之后预设EQ窗口没关' }
if (-not (Wait-NoWindow $p.Id '自动EQ')) { throw '跳过之后自动EQ窗口没关' }
Write-Output 'basic tour closed'

# 3. 进阶课：第一屏
$tutorial.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Choose-Course 'TutorialAdvancedChoiceButton'
$overlayHandle = Wait-Window $p.Id '教程 · 引导'
if ($overlayHandle -eq [IntPtr]::Zero) { throw '进阶课遮罩没出现' }
$overlay = [System.Windows.Automation.AutomationElement]::FromHandle($overlayHandle)
$next = Wait-Enabled $overlay 'TutorialNextButton'
if (-not $next) { throw '进阶课气泡没出现，或下一步一直不可用' }
Start-Sleep -Milliseconds 300
Shot-Screen $p.MainWindowHandle (Join-Path $out '20-tutorial-advanced.png')

# 第 2 步：点左右，涟漪点击后图表进入编辑态
Click-Next $overlay '进阶课第 2 步'
Start-Sleep -Milliseconds 900
Shot-Screen $p.MainWindowHandle (Join-Path $out '31-tutorial-edit-mode.png')
Write-Output 'edit mode glow shown'

# 第 3 步：真实曲线上的演示（建点 → 拉增益 → 拖频率 → 右键删 → Ctrl+Z）
Click-Next $overlay '进阶课第 3 步'
$demo = Wait-Enabled $overlay 'TutorialDemoButton'
if (-not $demo) { throw '进阶课第 3 步没有曲线演示按钮' }
Start-Sleep -Seconds 9
Shot-Screen $p.MainWindowHandle (Join-Path $out '24-tutorial-curve.png')
Write-Output 'real curve demo shown'

# 第 4~6 步：预增益、试听左右曲线、进入低音编辑（都是真实数据）
for ($i = 0; $i -lt 3; $i++) {
    Click-Next $overlay ("进阶课第 $($i + 4) 步")
    Start-Sleep -Milliseconds 500
}
Shot-Screen $p.MainWindowHandle (Join-Path $out '30-tutorial-bass-edit.png')
Write-Output 'bass edit shown'

# 第 7 步叠加低音修正，第 8 步试听左右曲线和叠加低音层
for ($i = 0; $i -lt 2; $i++) {
    Click-Next $overlay ("进阶课第 $($i + 7) 步")
}
$audio = Wait-Enabled $overlay 'TutorialAudioButton'
if (-not $audio) { throw '进阶课第 8 步音频按钮不可用' }
$audio.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900
Shot-Screen $p.MainWindowHandle (Join-Path $out '27-tutorial-audio-flat.png')
Write-Output 'layer base audio shown'
Start-Sleep -Milliseconds 5500
$audioStatus = Find-ById $overlay 'TutorialAudioStatus'
if (-not $audioStatus) { throw '找不到音频状态' }
Shot-Screen $p.MainWindowHandle (Join-Path $out '29-tutorial-bass-boost.png')
Write-Output 'layer compare audio shown'
Invoke-ById $overlay 'TutorialAudioButton' '停止音频'
Start-Sleep -Milliseconds 400

# 第 9 步：风味曲线演示：选目标 → 拖动 → 右键还原
Click-Next $overlay '进阶课第 9 步'
$flavorTitle = $null
$flavorUntil = (Get-Date).AddSeconds(12)
do {
    $flavorTitle = Find-ById $overlay 'DemoPanelTitle'
    if ($flavorTitle -and $flavorTitle.Current.Name -like '模拟：脚步*') { break }
    Start-Sleep -Milliseconds 200
} while ((Get-Date) -lt $flavorUntil)
if (-not $flavorTitle -or $flavorTitle.Current.Name -notlike '模拟：脚步*') {
    $name = if ($flavorTitle) { $flavorTitle.Current.Name } else { '找不到曲线标题' }
    throw ('风味演示没有显示模拟曲线：' + $name)
}
Start-Sleep -Milliseconds 1600
Shot-Screen $p.MainWindowHandle (Join-Path $out '32-tutorial-flavor.png')
Write-Output 'flavor curve shown'

Invoke-ById $overlay 'TutorialSkipButton' '跳过按钮'
if (-not (Wait-NoWindow $p.Id '教程 · 引导')) { throw '进阶课跳过之后遮罩没消失' }
Write-Output 'advanced tour closed'

# 4. 教程关掉以后，设置仍能打开
Invoke-ById $main 'SettingsButton' '设置按钮'
$settingsHandle = Wait-Window $p.Id '设置'
if ($settingsHandle -eq [IntPtr]::Zero) { throw '设置窗口没出现' }
[TutShot]::PostMessage($settingsHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Milliseconds 400
Write-Output 'settings window opened'

Write-Output 'tutorial check passed'
