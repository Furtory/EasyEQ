using System.Windows;
using System.Windows.Media;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Theme;

/// <summary>
/// 风味的配色：按彩虹顺序（红→橙→黄→黄绿→绿→青→蓝→紫），
/// 顺序必须和 <see cref="ToneBands.All"/> 一致。
///
/// 下拉里的文字、曲线上的手柄、每条风味自己的那条曲线都从这里取色，只有这一份，别处不要另抄一套。
/// </summary>
public static class TonePalette
{
    /// <summary>八种颜色，按彩虹顺序排。</summary>
    private static readonly Color[] Colors =
    {
        Color.FromRgb(0xF4, 0x58, 0x5C), // 爆炸/载具：红
        Color.FromRgb(0xF7, 0x8E, 0x3D), // 男声：橙
        Color.FromRgb(0xF6, 0xC9, 0x45), // 女声：黄
        Color.FromRgb(0xA8, 0xD4, 0x5A), // 脚步：黄绿
        Color.FromRgb(0x5C, 0xC4, 0x6A), // 枪声：绿
        Color.FromRgb(0x3F, 0xC9, 0xC1), // 换弹/机械：青
        Color.FromRgb(0x4A, 0x9D, 0xF0), // 齿音：蓝
        Color.FromRgb(0xA4, 0x6B, 0xE0), // 空间/细节：紫
    };

    /// <summary>八条风味的画刷，顺序同 <see cref="ToneBands.All"/>。</summary>
    public static IReadOnlyList<Brush> Brushes { get; } =
        Colors.Select(color => Freeze(new SolidColorBrush(color))).ToArray();

    private static IReadOnlyList<Pen> NormalPens { get; } =
        Brushes.Select(brush => Freeze(new Pen(brush, 1.3))).ToArray();

    private static IReadOnlyList<Pen> SelectedPens { get; } =
        Brushes.Select(brush => Freeze(new Pen(brush, 2.4))).ToArray();

    /// <summary>按类型的 Key 取画刷；不认识的 Key 给第一条的颜色。</summary>
    public static Brush BrushFor(string? key) => Brushes[IndexOrFirst(key)];

    /// <summary>
    /// 取某条风味的曲线笔。选中那条画粗一点，其余 1.3 px。
    /// 笔按颜色预生成，每次重绘不会产生新对象。
    /// </summary>
    public static Pen CurvePenFor(string? key, bool selected) =>
        (selected ? SelectedPens : NormalPens)[IndexOrFirst(key)];

    private static int IndexOrFirst(string? key)
    {
        var index = ToneBands.IndexOf(key);
        return index >= 0 ? index : 0;
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }
}
