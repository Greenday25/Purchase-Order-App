using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show($"Unhandled UI exception: {e.Exception}", "Unhandled exception", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogException(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        MessageBox.Show($"Unhandled domain exception: {e.ExceptionObject}", "Unhandled exception", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show($"Unobserved task exception: {e.Exception}", "Unhandled exception", MessageBoxButton.OK, MessageBoxImage.Error);
        e.SetObserved();
    }

    private void LogException(Exception ex)
    {
        try
        {
            var log = $"[{DateTime.Now:O}] {ex}\n\n";
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "exception.log"), log);
        }
        catch
        {
            // ignore logging errors
        }
    }
}


