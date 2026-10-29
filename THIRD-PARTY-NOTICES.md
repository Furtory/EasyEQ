# 第三方资源与许可

## Lucide 图标（风味图标）

`src/PeaceWpf/Theme/FlavorIcons.cs` 里的八个图标取自开源图标库 **Lucide**（<https://lucide.dev>），
版本 `lucide-static@1.61.0`，许可是 ISC。

用到的图标（Lucide 原名 → 用在哪）：

| Lucide | 用在哪 |
|---|---|
| `bomb` | 爆炸/载具 |
| `mars` | 男声 |
| `venus` | 女声 |
| `footprints` | 脚步 |
| `crosshair` | 枪声 |
| `wrench` | 换弹/机械 |
| `wind` | 齿音 |
| `sparkles` | 空间/细节 |

源文件是 24×24 视框的纯描边图标（`stroke-width="2"`、圆头圆角）。仓库里只保留了几何路径，
`circle` / `line` 手写成了等价的 WPF 圆弧与直线，线条粗细由代码里的 `FlavorIcons.StrokeWidth` 给。

### ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

### 其中 `crosshair` 来自 Feather（MIT）

Lucide 的 `crosshair` 沿用自 Feather 项目，所以下面这段也一并保留：

The MIT License (MIT)

Copyright (c) 2013-present Cole Bemis

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Breeze 光标（按住曲线时的张开手 / 握拳）

`src/PeaceWpf/Assets/cursor-open.cur`（对应 `openhand`）和 `cursor-grab.cur`（对应 `grabbing`）
取自 **KDE Breeze** 光标主题的 Windows 移植
[FelisDiligens/Breeze-Cursors-for-Windows](https://github.com/FelisDiligens/Breeze-Cursors-for-Windows)
的 `Breeze_Snow/` 目录（白色版本，配合深色界面）。

- 上游：<https://invent.kde.org/plasma/breeze>（KDE Plasma 的 cursors，经 win2xcur 转成 .cur）
- 许可：**LGPL-3.0-or-later**（该仓库的 `COPYING-ICONS`）
- Copyright (C) 2014 Uri Herrera <kaisergreymon99@gmail.com> and others
- 本地没有改动图形，两个文件原样放进资源集（32 / 48 / 64 三档，热点居中）。

这两个光标文件按 LGPL-3.0-or-later 分发；许可证全文见
<https://www.gnu.org/licenses/lgpl-3.0.html>，对应源码即上述 KDE Breeze 仓库。

## Silero VAD（人声检测模型）

`src/PeaceWpf/Assets/silero_vad.onnx`（2 327 524 字节）取自
[snakers4/silero-vad](https://github.com/snakers4/silero-vad) 的
`src/silero_vad/data/silero_vad.onnx`，用于自动风味判断「这段录音里有没有人说话」。

- 上游：<https://github.com/snakers4/silero-vad>
- 许可：**MIT**（见上游 `LICENSE`）
- Copyright (c) Silero Team
- 本地没有改模型；只按官方 ONNX 接口调用（input / state / sr → output / stateN）。
- 推理运行时：**Microsoft.ML.OnnxRuntime**（NuGet），许可 MIT。

The MIT License (MIT)

Copyright (c) Silero Team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
