using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PeaceWpf;

/// <summary>
/// 跟主界面同一套配色的对话框：深色圆角、无系统边框，阴影自己画。
///
/// 不用系统 MessageBox 是因为它在深色主题里是白底灰框，跟整个程序对不上；
/// 也不用 FluentWindow——那个的圆角在 Win10 上不生效。
/// 按钮由调用方给，2 个或 3 个都行，结果用下标告诉外面。
/// </summary>
public partial class DialogWindow : Window
{
    /// <summary>用户点了第几个按钮；直接按 Esc 或关掉窗口是 -1。</summary>
    public int SelectedIndex { get; private set; } = -1;

    public DialogWindow(string title, string message, params string[] buttons)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = buttons[i],
                Padding = new Thickness(18, 6, 18, 6),
                MinWidth = 78,
                FontSize = 12,
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                // 回车落在最后一个按钮上：取消 / 继续编辑，都是最不伤的那个。
                IsDefault = i == buttons.Length - 1,
            };

            button.Click += (_, _) =>
            {
                SelectedIndex = index;
                DialogResult = index == 0;
            };

            ButtonRow.Children.Add(button);
        }

        Loaded += (_, _) =>
        {
            if (ButtonRow.Children.Count > 0 && ButtonRow.Children[^1] is Button last)
            {
                last.Focus();
            }
        };
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
            SelectedIndex = -1;
            DialogResult = false;
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
