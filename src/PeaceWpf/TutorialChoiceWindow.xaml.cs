using System.Windows;
using Wpf.Ui.Controls;

namespace PeaceWpf;

/// <summary>
/// 点「教程」后先问一句：基础还是进阶。选完就关掉，由主窗口铺气泡引导。
/// 这里不承担教学内容，避免一打开就是一大段文字。
/// </summary>
public partial class TutorialChoiceWindow : FluentWindow
{
    public TutorialChoiceWindow()
    {
        InitializeComponent();

        // 尺寸钉死之后没有可拉的边框，顶部留一条能按住拖窗口
        WindowDragHelper.Enable(this);
    }

    /// <summary>用户选的路线；取消或直接关窗口时是 null。</summary>
    public TutorialCourse? Chosen { get; private set; }

    private void OnBasic(object sender, RoutedEventArgs e)
    {
        Chosen = TutorialCourse.Basic;
        DialogResult = true;
    }

    private void OnAdvanced(object sender, RoutedEventArgs e)
    {
        Chosen = TutorialCourse.Advanced;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Chosen = null;
        DialogResult = false;
    }
}
