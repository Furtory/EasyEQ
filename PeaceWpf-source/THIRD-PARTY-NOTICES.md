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
