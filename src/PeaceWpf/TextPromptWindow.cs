using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PeaceWpf;

/// <summary>
/// 只问一行字的小窗：重命名、复制预设时用。
///
/// 不用 Windows 自带的 InputBox（那要为了一个对话框拖进 Microsoft.VisualBasic），
/// 也不必为这么点事再开一个 XAML 文件 —— 一个 TextBox 加两个按钮，代码里搭完。
/// </summary>
public sealed class TextPromptWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly TextBox _input;

    public TextPromptWindow(string title, string message, string initial)
    {
        Title = title;
        Width = 460;
        MinWidth = 360;
        MinHeight = 0;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        // 尺寸钉死之后没有可拉的边框，顶部留一条能按住拖窗口
        WindowDragHelper.Enable(this);

        _input = new TextBox
        {
            Text = initial,
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(6, 4, 6, 4),
            FontSize = 13,
        };
        _input.PreviewKeyDown += OnInputPreviewKeyDown;

        var ok = new Button
        {
            Content = "确定",
            Padding = new Thickness(16, 4, 16, 4),
            Margin = new Thickness(0, 0, 8, 0),
        };
        ok.Click += (_, _) => Accept();

        var cancel = new Button
        {
            Content = "取消",
            Padding = new Thickness(16, 4, 16, 4),
            IsCancel = true,
        };
        cancel.Click += (_, _) => DialogResult = false;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });
        panel.Children.Add(_input);
        panel.Children.Add(buttons);

        Content = panel;

        Loaded += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };
    }

    /// <summary>用户输入的名字；点取消是 null。</summary>
    public string? Value { get; private set; }

    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        Accept();
        e.Handled = true;
    }

    private void Accept()
    {
        Value = _input.Text.Trim();
        DialogResult = true;
    }
}
