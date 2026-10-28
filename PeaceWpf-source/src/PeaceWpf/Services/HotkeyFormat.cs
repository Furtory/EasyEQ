using System.Windows.Input;
using PeaceWpf.Core.Model;

namespace PeaceWpf.Services;

/// <summary>
/// 热键的显示名和 WPF / Win32 两套键位的换算。
///
/// 存盘只存 Win32 虚拟键码 + MOD_* 修饰位，因为 <see cref="RegisterHotKey"/> 只认这个；
/// WPF 那一套装在界面里录键时用，出了录键框就不再往外传。
/// </summary>
public static class HotkeyFormat
{
    private const string Unbound = "未绑定";

    /// <summary>存盘的键位 → 显示名；没绑定时返回「未绑定」。</summary>
    public static string Format(HotkeyModifiers modifiers, int virtualKey)
    {
        if (virtualKey <= 0)
        {
            return Unbound;
        }

        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return Format(ToModifierKeys(modifiers), key);
    }

    /// <summary>录键框里的组合键 → 显示名。</summary>
    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(5);

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    /// <summary>WPF 的修饰键位 → 存盘 / 注册用的 MOD_* 位。</summary>
    public static HotkeyModifiers ToHotkeyModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifiers.Win;
        }

        return result;
    }

    public static ModifierKeys ToModifierKeys(HotkeyModifiers modifiers)
    {
        var result = ModifierKeys.None;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            result |= ModifierKeys.Alt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            result |= ModifierKeys.Control;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            result |= ModifierKeys.Shift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Win))
        {
            result |= ModifierKeys.Windows;
        }

        return result;
    }

    /// <summary>修饰键本身：按下时先只更新「Ctrl + …」，等真正的键到了才算抓完。</summary>
    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;

    /// <summary>只显示修饰键的时候用（还没按到实键）。</summary>
    public static string DescribeModifiers(ModifierKeys modifiers)
    {
        var parts = new List<string>(4);

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        return parts.Count == 0 ? string.Empty : string.Join("+", parts) + " + …";
    }

    /// <summary>比 Key.ToString() 更适合给人看的几个名字；其余照原样。</summary>
    public static string KeyName(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)key - (int)Key.D0).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return "小键盘" + ((int)key - (int)Key.NumPad0);
        }

        return key switch
        {
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Space => "空格",
            Key.Back => "退格",
            Key.Delete => "Del",
            Key.Insert => "Ins",
            Key.Tab => "Tab",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.Left => "←",
            Key.Right => "→",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemTilde => "`",
            Key.PrintScreen => "PrtSc",
            Key.Capital => "CapsLock",
            Key.Next => "PgDn",
            Key.Prior => "PgUp",
            _ => key.ToString(),
        };
    }
}
