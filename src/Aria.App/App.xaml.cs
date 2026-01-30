using System.Configuration;
using System.Data;

namespace Aria.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI 线程未捕获异常
        this.DispatcherUnhandledException += (s, args) =>
        {
            System.Windows.MessageBox.Show(
                $"程序发生未捕获异常 (UI):\n{args.Exception.Message}\n\n堆栈:\n{args.Exception.StackTrace}", 
                "Aria 错误", 
                System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Error);
            args.Handled = true;
            Current.Shutdown();
        };

        // 非 UI 线程未捕获异常
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.Windows.MessageBox.Show(
                $"程序发生严重错误 (Domain):\n{ex?.Message ?? "Unknown Error"}\n\n堆栈:\n{ex?.StackTrace}", 
                "Aria 严重错误", 
                System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Error);
        };

        try
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
             System.Windows.MessageBox.Show(
                $"启动窗口创建失败:\n{ex.Message}\n\n堆栈:\n{ex.StackTrace}", 
                "启动失败", 
                System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Error);
             Shutdown();
        }
    }
}

