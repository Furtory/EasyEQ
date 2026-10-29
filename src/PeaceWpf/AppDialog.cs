using System.Windows;

namespace PeaceWpf;

/// <summary>统一的对话框入口。外观见 <see cref="DialogWindow"/>。</summary>
public static class AppDialog
{
    /// <summary>给几个按钮就问几个，返回点的是第几个；Esc 或直接关掉返回 -1。</summary>
    public static int Choose(Window? owner, string title, string message, params string[] buttons)
    {
        var dialog = new DialogWindow(title, message, buttons);
        Attach(dialog, owner);
        dialog.ShowDialog();
        return dialog.SelectedIndex;
    }

    /// <summary>两个按钮的确认，返回是不是点了第一个。</summary>
    public static bool Confirm(
        Window? owner,
        string title,
        string message,
        string primary = "确定",
        string secondary = "取消") =>
        Choose(owner, title, message, primary, secondary) == 0;

    /// <summary>只是通知一声，只有一个按钮。</summary>
    public static void Info(Window? owner, string title, string message, string primary = "知道了") =>
        Choose(owner, title, message, primary);

    private static void Attach(Window dialog, Window? owner)
    {
        if (owner is { IsVisible: true })
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }
}
