using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private Forms.NotifyIcon? trayIcon;
    private bool isExitRequested;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var loginWindow = new LoginWindow();
        var loginResult = loginWindow.ShowDialog();
        if (loginResult != true || loginWindow.AuthenticatedUser is null)
        {
            Shutdown();
            return;
        }

        var homeWindow = new HomeWindow(loginWindow.AuthenticatedUser);
        MainWindow = homeWindow;
        RegisterMainWindow(homeWindow);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        homeWindow.Show();
    }

    public void RegisterMainWindow(Window window)
    {
        InitializeTrayIcon();
        window.StateChanged -= OnMainWindowStateChanged;
        window.StateChanged += OnMainWindowStateChanged;
    }

    public void ShowTrayNotification(string title, string message)
    {
        InitializeTrayIcon();
        trayIcon?.ShowBalloonTip(7000, title, message, Forms.ToolTipIcon.Info);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIcon?.Dispose();
        trayIcon = null;
        base.OnExit(e);
    }

    private void InitializeTrayIcon()
    {
        if (trayIcon != null)
        {
            return;
        }

        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (_, _) => RestoreMainWindowFromTray());
        contextMenu.Items.Add("Exit", null, (_, _) =>
        {
            isExitRequested = true;
            Shutdown();
        });

        trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Capital Air (Pty) Ltd",
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => RestoreMainWindowFromTray();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app.ico");
        return System.IO.File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Application;
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (isExitRequested || sender is not Window window || window.WindowState != WindowState.Minimized)
        {
            return;
        }

        window.Hide();
        window.ShowInTaskbar = false;
        ShowTrayNotification("Capital Air is still running", "Double-click the tray icon to restore the app.");
    }

    private void RestoreMainWindowFromTray()
    {
        if (MainWindow == null)
        {
            return;
        }

        MainWindow.ShowInTaskbar = true;
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
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
