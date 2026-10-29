# UI 自动化：截图与坐标测量

给「这个控件到底动没动」这类问题用。截图给人看，坐标给判断用。

脚本在 `tools/ui-test/`：

| 脚本 | 干什么 |
|---|---|
| `shot.ps1` | 截主窗口或整个主屏，存 PNG |
| `dump.ps1` | 打印窗口、按钮、下拉框、展开中的列表项（含坐标） |
| `hover.ps1` | 展开一个下拉，逐行悬停，读出悬浮气泡的真实位置 |

---

## 怎么跑

这台机器已经装了 PowerShell 7.6.9（`dotnet tool install --global PowerShell`，装在 `%USERPROFILE%\.dotnet\tools\pwsh.exe`，已在 PATH）。
本地脚本的执行策略是 RemoteSigned，直接跑就行：

```bat
pwsh -NoProfile -File tools\ui-test\dump.ps1
pwsh -NoProfile -File tools\ui-test\shot.ps1
pwsh -NoProfile -File tools\ui-test\hover.ps1 -Combo MainPresetCombo -Rows "0,4,8"
```

如果哪天 `pwsh` 不在了，退路是 Windows PowerShell 5.1 —— 要加 `-ExecutionPolicy Bypass`，并且注意下面第 2 条的编码问题：

```bat
powershell -NoProfile -ExecutionPolicy Bypass -File tools\ui-test\dump.ps1
```

先启动 EasyEQ，脚本自己去找进程和窗口。

常见参数：

- `-ProcessName`（默认 `EasyEQ`）
- `-WindowTitle`（默认 `电竞调音台`）
- `shot.ps1 -FullScreen`：截整个主屏而不是窗口
- `shot.ps1 -Out <路径>`：默认 `tools/ui-test/out/shot.png`
- `hover.ps1 -Combo`：数字 = 第几个 ComboBox（0 起），字符串 = AutomationId
- `hover.ps1 -Rows`：逗号分隔的行号，`-1` 是最后一行
- `hover.ps1 -KeepOpen`：测完不收起下拉

---

## 怎么读输出

`hover.ps1` 的输出长这样：

```
combo id=MainPresetCombo @ 5021,581,252,60
rows: 23
row4  [EsportsMain 5.peace] tip=5276,679-901 (348x222)
row8  [EsportsMain 9.peace] tip=5276,823-1045 (348x222)  <- 移动了
row22 [Last Configuration - 副本 ...] tip=5276,1327-1549 (348x222)  <- 移动了
```

判断规则只有一条：**tip 的 y 有没有跟着行变**。

- 每行 y 都不同 → 跟得上。
- 每行 y 一样 → 钉死了，这就是 2026-10-30 那个 bug 的样子。
- `tip=<没有气泡>` → 那一行没弹出来。

括号里是气泡尺寸，顺手能看出是不是在正确的那个气泡（预设和风味的都是 `348x222`，150% 缩放下的 232×148）。

---

## 三个坑

### 1. 执行策略（只在 5.1 下才有）

用 pwsh 7 不受影响（RemoteSigned 允许本地脚本）。只有在 Windows PowerShell 5.1 下，`& script.ps1` 会被拒：

```
无法加载文件 ... 因为在此系统上禁止运行脚本
```

那时用 `powershell -ExecutionPolicy Bypass -File`。

### 2. 编码：5.1 需要 BOM，pwsh 不需要

Windows PowerShell 5.1 按 ANSI 读 `.ps1`。UTF-8 无 BOM 的中文会被读成乱码，然后**解析器直接崩**，报一堆 `Unexpected token '{'`。

pwsh 7 默认按 UTF-8 读，没有这个问题。当前三个脚本都带 BOM，两边都能跑。

如果改了脚本又想给 5.1 用，补一下 BOM：

```bash
printf '\xEF\xBB\xBF' | cat - tools/ui-test/hover.ps1 > tmp && mv tmp tools/ui-test/hover.ps1
```

### 3. 两个坐标系，别混

这台机器是 150% 缩放（屏幕 7800×1440）：

| 来源 | 单位 | 例子 |
|---|---|---|
| UI Automation `BoundingRectangle` | **物理像素** | 主窗口 `4890,510 2297x1292` |
| Win32 `GetWindowRect` | **逻辑像素** | 主窗口 `3260,340 1531x861` |
| WPF `PointToScreen` / `CustomPopupPlacement` | **物理像素** | — |

比例就是 1.5。所以：

- 用 UIA 读坐标、用 `SetCursorPos` 移鼠标 → 都是物理，直接对得上。
- 写 WPF 定位代码时，`PointToScreen`（物理）不能和 `ActualWidth/Height`（DIP）混算。混了就会在 150% 下把窗口底边算小 431 px，竖直夹紧把靠下的行全夹成同一个位置。

`dump.ps1` 两种坐标都打，就是为了让你一眼看出当前比例。

---

## 已知现象

**预设下拉的前两行悬停时不弹气泡。**

`hover.ps1 -Rows "0,1"` 会得到 `tip=<没有气泡>`，从第 3 行起正常。这不是脚本的问题，是产品当前的行为——列表顶部那两行的气泡位置落在「立即可见」的范围之外。测跟随性用第 3 行往后就行。

---

## 一个实际例子

排查「风味方向下拉的缩略图不跟随」时的顺序：

1. `dump.ps1` —— 确认窗口在跑、下拉框和列表项都读得到，顺带看到主窗口物理坐标 2297×1292。
2. `hover.ps1 -Combo MainPresetCombo` —— 确认**预设**下拉的气泡是跟随的（679 → 823），说明测量方法本身没问题。
3. `hover.ps1 -Combo 2`（自动风味要先点「左右」再点「自动风味」，方向下拉才是第 3 个）—— 确认**风味**的气泡钉死在 1149。
4. 在 `PlacePresetTip` 里临时加日志，发现 `targetTop=1295 / win=510..1371`：窗口底边被算成 1371，真实是 1802。
5. 改成全程物理像素，复测：`1289 / 1448 / 1554 / 1580-1802`，跟着走且贴住窗口底不再出界。

第 4 步的日志是临时加的，用完删掉了。要复现可以在 `PlacePresetTip` 里把 `target.Top`、`window.Top`、`window.Bottom`、`popupSize.Height` 写进临时文件——**先看这几个数，再改代码**，比盯着屏幕猜快得多。
