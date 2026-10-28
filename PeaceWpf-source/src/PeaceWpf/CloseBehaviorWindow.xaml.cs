using System.Windows;
using System.Windows.Input;
using PeaceWpf.Core.Model;

namespace PeaceWpf;

/// <summary>一次关闭询问的结果。Remember 为 false 时只对这次有效。</summary>
public readonly record struct CloseBehaviorChoice(CloseAction Action, bool Remember);

/// <summary>
/// 第一次点关闭时问一次：这次怎么做、要不要记住。
///
/// 只收集选择，不写配置 —— 写不写由调用方决定，这样「仅这一次」天然不会落盘。
/// 外观和 DialogWindow 一致：深色圆角、无系统边框，Esc 等同取消关闭。
/// </summary>
public partial class CloseBehaviorWindow : Window
{
    private CloseBehaviorWindow(CloseAction initial)
    {
        InitializeComponent();

        // 默认最小化到托盘；初始值只是把上次记住的选择显示出来（没记住时就是默认值）。
        MinimizeRadio.IsChecked = initial == CloseAction.MinimizeToTray;
        ExitRadio.IsChecked = initial == CloseAction.Exit;

        // 回车落在「仅这一次」：少付一次承诺，选错了下次还能改。
        Loaded += (_, _) => OnceButton.Focus();
    }

    /// <summary>点了「记住选择」。</summary>
    public bool Remember { get; private set; }

    private CloseAction Action => MinimizeRadio.IsChecked == true
        ? CloseAction.MinimizeToTray
        : CloseAction.Exit;

    /// <summary>问一次。返回 null 表示这次不关了（Esc、或对话框被直接关掉）。</summary>
    public static CloseBehaviorChoice? Ask(Window? owner, CloseAction initial)
    {
        var dialog = new CloseBehaviorWindow(initial);

        if (owner is { IsVisible: true })
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true
            ? new CloseBehaviorChoice(dialog.Action, dialog.Remember)
            : null;
    }

    private void OnOnce(object sender, RoutedEventArgs e)
    {
        Remember = false;
        DialogResult = true;
    }

    private void OnRemember(object sender, RoutedEventArgs e)
    {
        Remember = true;
        DialogResult = true;
    }

    /// <summary>拖动整块面板（WindowStyle=None 之后系统不再帮忙拖）。</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 鼠标已经松开了，忽略。
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
