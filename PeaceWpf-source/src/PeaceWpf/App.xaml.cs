using System.IO;
using System.Windows;
using System.Windows.Threading;
using PeaceWpf.Core.Model;
using PeaceWpf.Services;
using PeaceWpf.ViewModels;

namespace PeaceWpf;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 只允许一个实例：第二个实例先把已经在跑的主窗口叫出来，再自己退出。
        // 两个实例会同时写 peace.txt / .peace / EsportsMix，还会一起抢 Peace 的权限。
        if (!SingleInstance.TryAcquire())
        {
            SingleInstance.ActivateExisting();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteLog(args.ExceptionObject as Exception);

        try
        {
            // 老版本数据在 %LOCALAPPDATA%\PeaceWpf，现在是 %LOCALAPPDATA%\EasyEQ。
            // 必须在读任何设置之前跑，否则先读到空设置就会把初值写进新目录。
            PeaceWpf.Core.AppPaths.MigrateLegacyData();

            var viewModel = new MainViewModel();
            var window = new MainWindow { DataContext = viewModel };

            // 开机自启（注册表里带 --startup）且用户关掉「启动时显示界面」时，最小化启动。
            // 手动双击打开永远显示界面，免得用户以为程序没起来。
            // 要在 Show 之前定状态：窗口加载时就靠它决定播不播启动动画。
            var launchedByAutoStart = e.Args.Any(arg =>
                string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase));

            if (launchedByAutoStart && !UiStateStore.Load().ShowWindowOnStart)
            {
                window.WindowState = WindowState.Minimized;
            }

            MainWindow = window;
            window.Show();

            // 第二个实例不再开新窗，只发一个信号；由这个实例自己把窗口从托盘叫回来。
            SingleInstance.ListenForActivation(
                () => window.Dispatcher.BeginInvoke(new Action(window.ShowFromTray)));

            // 系统注销 / 关机时别让「关闭窗口」那套询问挡住关机。
            SessionEnding += (_, _) => window.AllowSessionEnding();

            // 两个低权限症状一起处理：
            //   1) EqualizerAPO 的 .peace 预设只允许管理员写 —— 保存会直接失败、保存按钮不消失；
            //   2) Peace 以管理员运行时，普通权限的程序发给它的窗口消息会被 UIPI 丢掉。
            // 任一命中就引导一次提权；--elevated 是重启后的自己，别让它再问一遍。
            var elevatedRetry = e.Args.Any(arg =>
                string.Equals(arg, "--elevated", StringComparison.OrdinalIgnoreCase));

            if (!elevatedRetry && !Elevation.IsElevated())
            {
                var state = UiStateStore.Load();
                var configDirectory = PeaceWpf.Core.Integration.EapoConfig.FindConfigDirectory(state.ConfigDirectoryOverride);
                var mainPreset = configDirectory is null
                    ? null
                    : Path.Combine(
                        configDirectory,
                        string.IsNullOrWhiteSpace(state.MainConfig) ? "EsportsMain.peace" : state.MainConfig);

                var presetWritable = mainPreset is null || Elevation.CanWriteFile(mainPreset);
                var peaceRunning = Elevation.IsPeaceRunning();

                if (!presetWritable || peaceRunning)
                {
                    var restart = AppDialog.Confirm(
                        window,
                        "需要管理员权限",
                        (presetWritable
                                ? "Peace 正在以管理员身份运行，普通权限的程序控制不了它，推子不会跟着动。\n\n"
                                : "EqualizerAPO 的配置在 Program Files 下，保存预设（.peace）需要管理员权限，\n"
                                    + "现在点保存会失败、按钮也不会消失。\n\n")
                            + "以管理员身份重启本程序后，保存和同步都会正常。",
                        "以管理员身份重启",
                        "先这样用");

                    if (restart && Elevation.TryRestartElevated())
                    {
                        Shutdown();
                        return;
                    }
                }
            }

            _ = viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            MessageBox.Show(ex.ToString(), "Peace WPF 启动失败");
            Shutdown(1);
        }
    }

    /// <summary>用 runas 重新拉起自己；成功返回 true（调用方该退出了）。</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog(e.Exception);
        MessageBox.Show(e.Exception.Message, "Peace WPF 出错");
        e.Handled = true;
    }

    private static void WriteLog(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:O}] {ex}\r\n\r\n");
        }
        catch
        {
            // 日志都写不进去就算了，不要因为它再抛一次。
        }
    }
}
