using System.Windows;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.ViewModels;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for HomeWindow.xaml
/// </summary>
public partial class HomeWindow : Window
{
    private readonly HomeWindowViewModel viewModel = new();

    public HomeWindow()
        : this(null)
    {
    }

    public HomeWindow(AppUser? signedInUser)
    {
        InitializeComponent();
        DataContext = viewModel;
        if (signedInUser is not null)
        {
            viewModel.SetSignedInUser(signedInUser);
        }

        Loaded += OnWindowLoaded;
    }

    private void OnOpenPurchaseOrders(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Purchase Orders"))
        {
            return;
        }

        var purchaseOrdersWindow = new MainWindow(viewModel.SelectedUser);
        purchaseOrdersWindow.Show();
    }

    private void OnOpenWialonUnits(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Wialon Units"))
        {
            return;
        }

        var wialonWindow = new TrackingWindow();
        wialonWindow.Show();
    }

    private void OnOpenJobCards(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Job Cards"))
        {
            return;
        }

        var jobCardWindow = new WialonJobCardWindow();
        jobCardWindow.Show();
    }

    private void OnOpenTrackingCertificates(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Tracking Certificates"))
        {
            return;
        }

        var trackingCertificateWindow = new TrackingCertificateWindow();
        trackingCertificateWindow.Show();
    }

    private void OnOpenInventory(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Stock Inventory"))
        {
            return;
        }

        var inventoryWindow = new InventoryWindow();
        inventoryWindow.Show();
    }

    private async void OnOpenConnectivitySettings(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Connectivity Settings"))
        {
            return;
        }

        var settingsWindow = new ConnectivitySettingsWindow
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
        await viewModel.InitializeAsync();
    }

    private void OnOpenUserManagement(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("User Management"))
        {
            return;
        }

        var userManagementWindow = new UserManagementWindow
        {
            Owner = this
        };

        userManagementWindow.ShowDialog();
        viewModel.RefreshUsers();
    }

    private void OnLogout(object sender, RoutedEventArgs e)
    {
        var app = Application.Current;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Hide();

        var loginWindow = new LoginWindow();
        var loginResult = loginWindow.ShowDialog();
        if (loginResult == true && loginWindow.AuthenticatedUser is not null)
        {
            var nextHomeWindow = new HomeWindow(loginWindow.AuthenticatedUser);
            app.MainWindow = nextHomeWindow;
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            nextHomeWindow.Show();
            Close();
            return;
        }

        app.Shutdown();
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
    }

    private bool CanOpenWorkspace(string workspaceName)
    {
        if (viewModel.CanOpenWorkspace(workspaceName))
        {
            return true;
        }

        MessageBox.Show(
            $"{viewModel.SelectedUser?.DisplayName ?? "The selected user"} does not have access to {workspaceName}.",
            "Access Restricted",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return false;
    }
}
