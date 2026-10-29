using System.Windows;
using PeaceWpf.Core.Calibration;
using Wpf.Ui.Controls;

namespace PeaceWpf;

/// <summary>
/// 点「自动EQ」之后的第一步：问清楚校准到哪条曲线。
///
/// 单独一个窗口而不是 MessageBox，是因为 MessageBox 的按钮文字只能是「是 / 否 / 取消」，
/// 而这里要让人一眼看出两条曲线的区别——选错了，后面整套 EQ 都是错的。
/// </summary>
public partial class CalibrationTargetWindow : FluentWindow
{
    private readonly bool _interactive;

    /// <param name="interactive">
    /// 教程里只把它当展示窗打开，别让误点把窗口关掉或触发 DialogResult（非模态窗口设它会抛异常）。
    /// </param>
    public CalibrationTargetWindow(bool interactive = true)
    {
        InitializeComponent();
        _interactive = interactive;

        // 尺寸钉死之后没有可拉的边框，顶部留一条能按住拖窗口
        WindowDragHelper.Enable(this);
    }

    /// <summary>选中的目标；取消或不选时是 null。</summary>
    public CalibrationTargetKind? Chosen { get; private set; }

    private void OnFlat(object sender, RoutedEventArgs e) => Choose(CalibrationTargetKind.Flat);

    private void OnHarman(object sender, RoutedEventArgs e) => Choose(CalibrationTargetKind.HarmanInEar);

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Chosen = null;
        if (_interactive)
        {
            DialogResult = false;
        }
    }

    private void Choose(CalibrationTargetKind kind)
    {
        Chosen = kind;
        if (_interactive)
        {
            DialogResult = true;
        }
    }
}
