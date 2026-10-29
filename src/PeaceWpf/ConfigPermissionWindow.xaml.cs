using System.Windows;
using PeaceWpf.Core.Integration;
using Wpf.Ui.Controls;

namespace PeaceWpf;

/// <summary>
/// 配置目录写不进去时开的窗口：先把原因查清楚，只对能修的那一类给按钮。
/// 不显示「一键授权」这种话——没诊断出来之前，授权本身可能就是错的。
/// </summary>
public partial class ConfigPermissionWindow : FluentWindow
{
    private readonly string? _directory;
    private readonly string _backupDirectory;
    private ConfigDiagnosis? _diagnosis;

    /// <summary>修好了以后通知主窗口重查可写状态。</summary>
    public event Action? Repaired;

    public ConfigPermissionWindow(string? directory, string backupDirectory)
    {
        _directory = directory;
        _backupDirectory = backupDirectory;
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        WindowDragHelper.Enable(this);
    }

    private void Refresh()
    {
        _diagnosis = ConfigPermission.Diagnose(_directory);

        SummaryText.Text = _diagnosis.Summary;
        ActionText.Text = _diagnosis.Action;
        DetailsList.ItemsSource = _diagnosis.Details;

        var repairable = _diagnosis.Problem is ConfigProblem.ReadOnlyFile
            or ConfigProblem.AclNeedsGrant
            or ConfigProblem.FileLocked
            or ConfigProblem.LowIntegrity;

        RepairButton.IsEnabled = repairable;
        RepairButton.Content = _diagnosis.Problem switch
        {
            ConfigProblem.AclNeedsGrant => "授予写权限",
            ConfigProblem.LowIntegrity => "复制修复命令",
            _ => "修一下",
        };
    }

    /// <summary>
    /// 修复动作放到后台线程：icacls 要改 ACL、要递归，走 UI 线程会把窗口挂死
    /// （Windows 五秒没响应就判 AppHang，直接关掉程序）。
    /// </summary>
    private async void OnRepair(object sender, RoutedEventArgs e)
    {
        if (_diagnosis is null)
        {
            return;
        }

        var diagnosis = _diagnosis;

        // 低完整性这条程序自己执行不了：改完整性标签要管理员权限。
        // 能做的是把命令原样递到剪贴板，让用户拿去管理员 PowerShell 跑一次。
        if (diagnosis.Problem == ConfigProblem.LowIntegrity)
        {
            CopyIntegrityFix(diagnosis);
            return;
        }

        RepairButton.IsEnabled = false;
        LogText.Text = "正在修…";

        var result = await Task
            .Run(() => ConfigPermission.Repair(diagnosis, _backupDirectory))
            .ConfigureAwait(true);

        LogText.Text = result.Message;
        Refresh();

        if (result.Ok)
        {
            Repaired?.Invoke();
        }
    }

    private void CopyIntegrityFix(ConfigDiagnosis diagnosis)
    {
        if (diagnosis.Directory is not { } directory)
        {
            return;
        }

        var command = ConfigPermission.IntegrityFixCommand(directory);

        try
        {
            Clipboard.SetText(command);
            LogText.Text = "已复制，粘贴到管理员 PowerShell 里跑一次：" + command;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            LogText.Text = "剪贴板被占着，手动复制这条：" + command;
        }
    }

    private void OnRecheck(object sender, RoutedEventArgs e)
    {
        LogText.Text = string.Empty;
        Refresh();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
