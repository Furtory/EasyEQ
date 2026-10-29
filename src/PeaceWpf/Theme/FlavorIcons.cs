using System.Windows;
using System.Windows.Media;
using PeaceWpf.Core.AutoFlavor;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Theme;

/// <summary>
/// 风味图标。取自开源图标库 Lucide（https://lucide.dev），ISC 许可；
/// 许可原文见仓库根目录的 THIRD-PARTY-NOTICES.md。
///
/// 原文件是 24×24 视框的纯描边图标（stroke-width 2、圆头圆角），这里只保留几何路径：
/// 颜色和最终大小由调用方给，下拉里的 Path 和曲线上的绘制共用这一份。
/// 路径是 SVG 的 d 属性直接搬过来的（WPF 支持同名命令与圆弧），
/// 只有 circle / line 手写成了等价的圆弧和直线。
/// </summary>
public static class FlavorIcons
{
    /// <summary>Lucide 的视框边长；画的时候按「目标尺寸 / 24」缩放。</summary>
    public const double ViewBox = 24;

    /// <summary>原图标的描边宽度（在 24×24 空间里），缩放时跟着一起缩。</summary>
    public const double StrokeWidth = 2;

    private static readonly Dictionary<string, Geometry> ByKey = new(StringComparer.Ordinal)
    {
        // 爆炸：bomb
        ["boom"] = Geometry.Parse(
            "M2,13 A9,9 0 1 0 20,13 A9,9 0 1 0 2,13 " +
            "M14.35,4.65 L16.3,2.7 a2.41,2.41 0 0 1 3.4,0 l1.6,1.6 a2.4,2.4 0 0 1 0,3.4 l-1.95,1.95 " +
            "M22,2 l-1.5,1.5"),

        // 男声：mars
        ["voiceM"] = Geometry.Parse(
            "M16,3 h5 v5 " +
            "M21,3 l-6.75,6.75 " +
            "M4,14 A6,6 0 1 0 16,14 A6,6 0 1 0 4,14"),

        // 女声：venus
        ["voiceF"] = Geometry.Parse(
            "M12,15 v7 " +
            "M9,19 h6 " +
            "M6,9 A6,6 0 1 0 18,9 A6,6 0 1 0 6,9"),

        // 脚步：footprints
        ["step"] = Geometry.Parse(
            "M4,16 v-2.38 C4,11.5 2.97,10.5 3,8 c.03,-2.72 1.49,-6 4.5,-6 " +
            "C9.37,2 10,3.8 10,5.5 c0,3.11 -2,5.66 -2,8.68 V16 a2,2 0 1 1 -4,0 Z " +
            "M20,20 v-2.38 c0,-2.12 1.03,-3.12 1,-5.62 c-.03,-2.72 -1.49,-6 -4.5,-6 " +
            "C14.63,6 14,7.8 14,9.5 c0,3.11 2,5.66 2,8.68 V20 a2,2 0 1 0 4,0 Z " +
            "M16,17 h4 " +
            "M4,13 h4"),

        // 枪声：crosshair
        ["gun"] = Geometry.Parse(
            "M2,12 A10,10 0 1 0 22,12 A10,10 0 1 0 2,12 " +
            "M22,12 L18,12 " +
            "M6,12 L2,12 " +
            "M12,6 L12,2 " +
            "M12,22 L12,18"),

        // 余震：waves（低频余韵）
        ["aftershock"] = Geometry.Parse(
            "M2,6 c.6,.5 1.2,1 2.5,1 C7,7 7,5 9.5,5 c2.6,0 2.4,2 5,2 1.3,0 1.9,-.5 2.5,-1 " +
            "M2,12 c.6,.5 1.2,1 2.5,1 2.5,0 2.5,-2 5,-2 2.6,0 2.4,2 5,2 1.3,0 1.9,-.5 2.5,-1 " +
            "M2,18 c.6,.5 1.2,1 2.5,1 2.5,0 2.5,-2 5,-2 2.6,0 2.4,2 5,2 1.3,0 1.9,-.5 2.5,-1"),

        // 齿音：wind（气声/嘶声）
        ["sibil"] = Geometry.Parse(
            "M12.8,19.6 A2,2 0 1 0 14,16 H2 " +
            "M17.5,8 a2.5,2.5 0 1 1 2,4 H2 " +
            "M9.8,4.4 A2,2 0 1 1 11,8 H2"),

        // 空间：sparkles
        ["air"] = Geometry.Parse(
            "M11.017,2.814 a1,1 0 0 1 1.966,0 l1.051,5.558 a2,2 0 0 0 1.594,1.594 " +
            "l5.558,1.051 a1,1 0 0 1 0,1.966 l-5.558,1.051 a2,2 0 0 0 -1.594,1.594 " +
            "l-1.051,5.558 a1,1 0 0 1 -1.966,0 l-1.051,-5.558 a2,2 0 0 0 -1.594,-1.594 " +
            "l-5.558,-1.051 a1,1 0 0 1 0,-1.966 l5.558,-1.051 a2,2 0 0 0 1.594,-1.594 z " +
            "M20,2 v4 " +
            "M22,4 h-4 " +
            "M2,20 A2,2 0 1 0 6,20 A2,2 0 1 0 2,20"),

        // 尖啸：zap（极高频的刺）
        ["shriek"] = Geometry.Parse(
            "M4,14 a1,1 0 0 1 -.78,-1.63 l9.9,-10.2 a.5,.5 0 0 1 .86,.46 l-1.92,6.02 " +
            "A1,1 0 0 0 13,10 h7 a1,1 0 0 1 .78,1.63 l-9.9,10.2 a.5,.5 0 0 1 -.86,-.46 " +
            "l1.92,-6.02 A1,1 0 0 0 11,14 z"),

        // 全部：三条叠在一起的横线
        ["all"] = Geometry.Parse(
            "M4,7 L20,7 M4,12 L20,12 M4,17 L20,17"),

        // ---- 乐器视角：同一条频段换成一个乐器剪影，风格保持同样的纯描边 ----
        ["inst.boom"] = Geometry.Parse(
            "M12,8 L12,2 M10,2 L14,2 " +
            "M12,22 A7,7 0 1 0 12,8 A7,7 0 1 0 12,22"),
        ["inst.aftershock"] = Geometry.Parse(
            "M12,10 L12,4 M10,4 L14,4 " +
            "M12,22 A6,6 0 1 0 12,10 A6,6 0 1 0 12,22 " +
            "M3,21 L21,3"),
        ["inst.voiceM"] = Geometry.Parse(
            "M12,12 L12,5 M10,5 L14,5 " +
            "M12,22 A5,5 0 1 0 12,12 A5,5 0 1 0 12,22 " +
            "M3,21 L21,3"),
        ["inst.voiceF"] = Geometry.Parse(
            "M12,13 L12,6 M10,6 L14,6 " +
            "M12,22 A4.5,4.5 0 1 0 12,13 A4.5,4.5 0 1 0 12,22 " +
            "M3,21 L21,3"),
        ["inst.step"] = Geometry.Parse(
            "M3,9 L20,9 M3,12 L21,12 M3,15 L20,15"),
        ["inst.gun"] = Geometry.Parse(
            "M3,12 L14,12 M14,12 A6,6 0 0 1 20,18 " +
            "M8,8 L8,16 M12,8 L12,16"),
        ["inst.sibil"] = Geometry.Parse(
            "M10,3 L10,9 M7,9 L19,19 M19,19 L17,21"),
        ["inst.air"] = Geometry.Parse(
            "M4,7 L20,11 M4,11 L20,7 M12,11 L12,21 M8,21 L16,21"),
        ["inst.shriek"] = Geometry.Parse(
            "M12,4 L5,19 L19,19 Z M2,22 L13,12"),
    };

    /// <summary>笔按画刷缓存，重绘时不再 new。</summary>
    private static readonly Dictionary<Brush, Pen> Pens = new();

    /// <summary>
    /// 按类型的 Key 取图标；不认识的 Key 给「脚步」那个。
    /// <paramref name="instrument"/> 为真时用乐器视角的图标；<paramref name="overrideKey"/> 是
    /// 「保留原样」的那条（自动风味选男声/女声时），它仍用游戏视角的图标。
    /// </summary>
    public static Geometry IconFor(string? key, bool instrument = false, string? overrideKey = null)
    {
        if (key is null)
        {
            return ByKey["step"];
        }

        if (instrument && !FlavorNaming.KeepsOwnName(key, overrideKey))
        {
            var instrumentKey = "inst." + key;
            if (ByKey.TryGetValue(instrumentKey, out var instrumentGeometry))
            {
                return instrumentGeometry;
            }
        }

        return ByKey.TryGetValue(key, out var geometry) ? geometry : ByKey["step"];
    }

    /// <summary>在 center 处画一个 size×size 的图标。</summary>
    public static void Draw(
        DrawingContext dc,
        string? key,
        Point center,
        double size,
        Brush brush,
        bool instrument = false,
        string? overrideKey = null)
    {
        var scale = size / ViewBox;
        dc.PushTransform(new TranslateTransform(center.X - (size / 2), center.Y - (size / 2)));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(null, PenFor(brush), IconFor(key, instrument, overrideKey));
        dc.Pop();
        dc.Pop();
    }

    private static Pen PenFor(Brush brush)
    {
        if (Pens.TryGetValue(brush, out var cached))
        {
            return cached;
        }

        var pen = new Pen(brush, StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        Pens[brush] = pen;
        return pen;
    }
}
