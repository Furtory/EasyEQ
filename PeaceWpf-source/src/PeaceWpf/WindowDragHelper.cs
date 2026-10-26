using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PeaceWpf;

/// <summary>
/// 给窗口补上「点住顶部那条拖动」。
///
/// 尺寸钉死的窗口没有可拖的边框，只剩系统标题栏那一条；窗口要是把内容画到标题栏附近，
/// 或者系统标题栏被去掉，就整块都没地方可抓。这里在最上面一条里接住按下，
/// 落在按钮、输入框、下拉、滑块这些控件上时不抢，照常交给控件自己处理。
/// </summary>
public static class WindowDragHelper
{
    /// <summary>顶部多高以内算可抓的标题条（DIP）。</summary>
    private const double GrabHeight = 40;

    public static void Enable(Window window)
    {
        window.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!ShouldDrag(window, e))
            {
                return;
            }

            try
            {
                window.DragMove();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
                // 鼠标已经松开时 DragMove 会抛，忽略。
            }
        };
    }

    private static bool ShouldDrag(Window window, MouseButtonEventArgs e)
    {
        // 最大化时拖动没有意义，交给系统
        if (window.WindowState != WindowState.Normal)
        {
            return false;
        }

        if (e.GetPosition(window).Y > GrabHeight)
        {
            return false;
        }

        return !IsInteractive(e.OriginalSource as DependencyObject);
    }

    private static bool IsInteractive(DependencyObject? node)
    {
        for (; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ButtonBase or TextBoxBase or ComboBox or Slider or ScrollBar)
            {
                return true;
            }

            if (node is Window)
            {
                return false;
            }
        }

        return false;
    }
}
