using System.IO;
using System.Windows;
using System.Windows.Threading;
using PeaceWpf.Core.Model;
using PeaceWpf.ViewModels;

namespace PeaceWpf;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteLog(args.ExceptionObject as Exception);

        try
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow { DataContext = viewModel };

            MainWindow = window;
            window.Show();

            // 开机自启（注册表里带 --startup）且用户关掉「启动时显示界面」时，最小化启动。
            // 手动双击打开永远显示界面，免得用户以为程序没起来。
            var launchedByAutoStart = e.Args.Any(arg =>
                string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase));

            if (launchedByAutoStart && !UiStateStore.Load().ShowWindowOnStart)
            {
                window.WindowState = WindowState.Minimized;
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
