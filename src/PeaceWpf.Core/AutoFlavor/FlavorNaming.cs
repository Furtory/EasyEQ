using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.AutoFlavor;

/// <summary>
/// 风味名称与图标的「视角」规则：默认两套（游戏 / 乐器），但有一个例外——
/// 自动风味的方向如果是男声或女声，那条风味保持自己的名字和图标，不换成乐器。
/// 理由：那时整个建议就是在说「这是男声 / 女声」，换成中提琴反而对不上。
/// </summary>
public static class FlavorNaming
{
    /// <summary>这一条要不要保留自己的名字（不跟着视角换）。</summary>
    public static bool KeepsOwnName(string? key, string? overrideKey) =>
        !string.IsNullOrEmpty(overrideKey)
        && string.Equals(key, overrideKey, StringComparison.Ordinal);

    /// <summary>界面上该显示的名字。</summary>
    public static string NameOf(ToneBand band, bool instrument, string? overrideKey)
    {
        ArgumentNullException.ThrowIfNull(band);
        return instrument && !KeepsOwnName(band.Key, overrideKey)
            ? string.Join(" ", band.InstrumentLabels)
            : band.Label;
    }

    /// <summary>这一条在乐器视角下覆盖的乐器（给下拉按需展开）。</summary>
    public static IReadOnlyList<string> InstrumentNamesOf(ToneBand band, string? overrideKey)
    {
        ArgumentNullException.ThrowIfNull(band);
        return KeepsOwnName(band.Key, overrideKey)
            ? new[] { band.Label }
            : band.InstrumentLabels;
    }

    /// <summary>图标查表用的键：保留原名时是自己的键，否则加 <c>inst.</c> 前缀。</summary>
    public static string IconKeyOf(ToneBand band, bool instrument, string? overrideKey)
    {
        ArgumentNullException.ThrowIfNull(band);
        return instrument && !KeepsOwnName(band.Key, overrideKey) ? "inst." + band.Key : band.Key;
    }

    /// <summary>方向对应的保留键：带「男声 / 女声」的方向各保留自己那条，其余返回空。</summary>
    public static string OverrideKeyFor(AutoFlavorDirection direction) => direction switch
    {
        AutoFlavorDirection.MaleFolk => "voiceM",
        AutoFlavorDirection.FemaleElectronic => "voiceF",
        _ => string.Empty,
    };

    /// <summary>
    /// 选这个方向时命名该用哪套视角：竞技定位用游戏叫法，其余用乐器叫法。
    /// 竞技讨论的是「脚步、枪声」这类线索，换成乐器名字反而听不懂。
    /// </summary>
    public static bool PrefersInstrumentNaming(AutoFlavorDirection direction) =>
        direction != AutoFlavorDirection.Competitive;
}
