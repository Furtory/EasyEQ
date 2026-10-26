using System.Diagnostics;
using System.IO;
using System.Windows;
using PeaceWpf.Core.Model;
using PeaceWpf.ViewModels;
using Wpf.Ui.Controls;

namespace PeaceWpf;

/// <summary>
/// 设置窗口：三块仪表盘面板——运行环境、常规设置、关于软件。
/// 只做“看状态”和“显式动作”，不在打开时自动改任何东西。
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private ConfigPermissionWindow? _permissionWindow;

    /// <summary>初始化开关时，别把「设值」当成「用户点击」。</summary>
    private bool _loadingAutoStart;

    private bool _loadingShowOnStart;

    public SettingsWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;

        // 尺寸钉死之后没有可拉的边框，顶部留一条能按住拖窗口
        WindowDragHelper.Enable(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionText.Text = "v" + AppInfo.Version;
        UpdateStatusText.Text = "点一下看看有没有新版本。";
        RefreshApoInstallText();

        _loadingAutoStart = true;
        AutoStartSwitch.IsChecked = AutoStart.IsEnabled;
        _loadingAutoStart = false;

        _loadingShowOnStart = true;
        ShowOnStartSwitch.IsChecked = _viewModel.ShowWindowOnStart;
        _loadingShowOnStart = false;
    }

    /// <summary>安装状态那一行：能读到版本就报版本，读不到就是未检测到。</summary>
    private void RefreshApoInstallText()
    {
        var version = ApoProbe.TryGetVersion(_viewModel.ConfigDirectory);
        ApoInstallText.Text = version is null
            ? "未检测到 EqualizerAPO"
            : $"EqualizerAPO {version}";
    }

    // ---------------- 运行环境 ----------------

    /// <summary>
    /// 安装 EqualizerAPO：下载官方安装包，再以管理员身份拉起安装程序。
    /// 下载失败就退到下载页，不让用户卡在“什么也没发生”。
    /// </summary>
    private async void OnInstallApo(object sender, RoutedEventArgs e)
    {
        var confirmed = AppDialog.Confirm(
            this,
            "安装 EqualizerAPO",
            "会从 EqualizerAPO 官方页面下载最新安装包，下载完以管理员身份启动安装程序。\n\n"
                + "安装时记得勾选你正在用的声卡/耳机。继续吗？",
            "下载并安装",
            "取消");

        if (!confirmed)
        {
            return;
        }

        _viewModel.StatusText = "正在下载 EqualizerAPO…";
        var progress = new Progress<double>(p =>
            _viewModel.StatusText = $"正在下载 EqualizerAPO… {p:P0}");

        var download = await ApoInstaller.DownloadAsync(progress, CancellationToken.None);
        if (download.Path is null)
        {
            var openPage = AppDialog.Confirm(
                this,
                "安装 EqualizerAPO",
                "自动下载失败：" + download.Error + "\n\n要打开下载页手动装吗？",
                "打开下载页",
                "关闭");

            if (openPage && !ApoInstaller.OpenDownloadPage(out var pageError))
            {
                AppDialog.Info(this, "安装 EqualizerAPO", "打不开下载页：" + pageError);
            }

            _viewModel.StatusText = "安装 EqualizerAPO：下载没有完成。";
            return;
        }

        _viewModel.StatusText = "下载完成，正在启动安装程序…";
        if (!ApoInstaller.LaunchInstaller(download.Path, out var launchError))
        {
            AppDialog.Info(this, "安装 EqualizerAPO", launchError ?? "安装程序没有启动。");
            _viewModel.StatusText = "安装 EqualizerAPO：安装程序没有启动。";
            return;
        }

        _viewModel.StatusText = "安装向导已打开：按它走完，再回来点「重新检查并连接」。";
    }

    /// <summary>重新检查；发现找到目录但没接上时，顺手接上。</summary>
    private async void OnRecheckApo(object sender, RoutedEventArgs e)
    {
        _viewModel.RecheckApoConnection();
        RefreshApoInstallText();

        if (_viewModel.HasApoWarning)
        {
            await _viewModel.ConnectApoCommand.ExecuteAsync(null);
            RefreshApoInstallText();
        }
    }

    /// <summary>手动改 EqualizerAPO 的 config 文件夹；选完立即生效。</summary>
    private void OnPickConfigFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择 EqualizerAPO 的 config 文件夹",
            Multiselect = false,
        };

        var current = _viewModel.ConfigDirectory;
        if (Directory.Exists(current))
        {
            dialog.InitialDirectory = current;
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!_viewModel.TrySetConfigDirectory(dialog.FolderName, out var error))
        {
            AppDialog.Info(this, "配置文件夹", error ?? "这个文件夹不能用。");
            return;
        }

        RefreshApoInstallText();
    }

    // ---------------- 常规设置 ----------------

    private void OnAutoStartChecked(object sender, RoutedEventArgs e)
    {
        if (_loadingAutoStart)
        {
            return;
        }

        if (!AutoStart.SetEnabled(true, out var error))
        {
            _loadingAutoStart = true;
            AutoStartSwitch.IsChecked = false;
            _loadingAutoStart = false;

            AppDialog.Info(this, "开机自启", "没设置成功：" + error);
            return;
        }

        _viewModel.StatusText = "已开启开机自启。";
    }

    private void OnAutoStartUnchecked(object sender, RoutedEventArgs e)
    {
        if (_loadingAutoStart)
        {
            return;
        }

        if (!AutoStart.SetEnabled(false, out var error))
        {
            _loadingAutoStart = true;
            AutoStartSwitch.IsChecked = true;
            _loadingAutoStart = false;

            AppDialog.Info(this, "开机自启", "没关掉：" + error);
            return;
        }

        _viewModel.StatusText = "已关闭开机自启。";
    }

    private void OnShowOnStartChecked(object sender, RoutedEventArgs e)
    {
        if (_loadingShowOnStart)
        {
            return;
        }

        _viewModel.ShowWindowOnStart = true;
    }

    private void OnShowOnStartUnchecked(object sender, RoutedEventArgs e)
    {
        if (_loadingShowOnStart)
        {
            return;
        }

        _viewModel.ShowWindowOnStart = false;
    }

    // ---------------- 关于软件 ----------------

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = "正在检查…";

        var result = await UpdateChecker.CheckAsync(AppInfo.Version);

        if (result.Error is not null)
        {
            UpdateStatusText.Text = result.Error;
            return;
        }

        if (!result.HasUpdate)
        {
            UpdateStatusText.Text = $"已经是最新版本（{AppInfo.Version}）。";
            return;
        }

        UpdateStatusText.Text = $"发现新版本 {result.LatestVersion}。";
        var open = AppDialog.Confirm(
            this,
            "检查更新",
            $"线上有新版本 {result.LatestVersion}，当前是 {AppInfo.Version}。\n\n现在去发布页看看？",
            "去看看",
            "以后再说");

        if (open)
        {
            OpenUrl(result.ReleaseUrl, "打开发布页");
        }
    }

    private void OnOpenBilibili(object sender, RoutedEventArgs e) =>
        OpenUrl(AppInfo.BilibiliUrl, "打开 B 站主页");

    private void OnOpenGitHub(object sender, RoutedEventArgs e) =>
        OpenUrl(AppInfo.GitHubUrl, "打开 GitHub 主页");

    private void OpenUrl(string url, string title)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppDialog.Info(this, title, "打不开：" + ex.Message);
        }
    }

    // ---------------- 其余动作 ----------------

    private void OnCheckPermission(object sender, RoutedEventArgs e)
    {
        if (_permissionWindow is { IsVisible: true })
        {
            _permissionWindow.Activate();
            return;
        }

        var window = new ConfigPermissionWindow(_viewModel.ConfigDirectory, UiState.Directory)
        {
            Owner = this,
        };

        window.Repaired += _viewModel.RecheckConfigWritable;
        _permissionWindow = window;
        window.Closed += (_, _) => _permissionWindow = null;
        window.Show();
    }

    /// <summary>
    /// 把软件里的全部设置导出成一个 JSON。
    /// 默认名字带时间戳（文件名不能有冒号，用短横），默认落在桌面。
    /// </summary>
    private void OnExportSettings(object sender, RoutedEventArgs e)
    {
        var backup = _viewModel.CaptureBackup();
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var name = SettingsBackup.SuggestFileName(backup.ExportedAt);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出设置",
            FileName = string.IsNullOrWhiteSpace(desktop)
                ? name
                : System.IO.Path.Combine(desktop, name),
            DefaultExt = ".json",
            AddExtension = true,
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            System.IO.File.WriteAllText(dialog.FileName, backup.ToJson());
            _viewModel.StatusText = $"设置已导出：{dialog.FileName}";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            AppDialog.Info(this, "导出设置", "写文件失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 读一份之前导出的 JSON，把两层曲线和各项设置换回来。
    /// 会覆盖当前状态，所以先弹一次确认，默认落在「否」。
    /// </summary>
    private void OnImportSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入设置",
            DefaultExt = ".json",
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SettingsBackup? backup;
        try
        {
            backup = SettingsBackup.FromJson(System.IO.File.ReadAllText(dialog.FileName));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            AppDialog.Info(this, "导入设置", "读文件失败：" + ex.Message);
            return;
        }

        if (backup is null)
        {
            AppDialog.Info(this, "导入设置", "这个文件不是本程序导出的设置备份。");
            return;
        }

        var confirmed = AppDialog.Confirm(
            this,
            "导入设置",
            "导入会覆盖现在的两层曲线、预设选择、平衡和预增益。\n\n"
                + $"导出时间：{backup.ExportedAt:yyyy-MM-dd HH:mm}\n"
                + $"左右 {backup.UiState.MainPoints.Count} 点 · 低音 {backup.UiState.BassPoints.Count} 点",
            "导入",
            "取消");

        if (!confirmed)
        {
            return;
        }

        _viewModel.StatusText = _viewModel.ImportBackup(backup);
    }

    private void OnCopyStatus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.StatusText))
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(_viewModel.StatusText);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // 剪贴板被别的程序占着，点第二次就好；不弹窗打断。
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
