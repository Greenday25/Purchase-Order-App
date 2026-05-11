using System.Windows;
using System.Windows.Controls;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;
using PurchaseOrderApp.ViewModels;
using System.Windows.Threading;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for HomeWindow.xaml
/// </summary>
public partial class HomeWindow : Window
{
    private readonly HomeWindowViewModel viewModel = new();
    private readonly AppUser? signedInUser;
    private readonly DispatcherTimer approvalNotificationTimer = new();
    private PurchaseOrderApprovalNotificationService? approvalNotificationService;
    private object? menuContent;
    private readonly double menuMinHeight;
    private readonly double menuMinWidth;

    public HomeWindow()
        : this(null)
    {
    }

    public HomeWindow(AppUser? signedInUser)
    {
        InitializeComponent();
        this.signedInUser = signedInUser;
        menuContent = Content;
        menuMinHeight = MinHeight;
        menuMinWidth = MinWidth;
        DataContext = viewModel;
        if (signedInUser is not null)
        {
            viewModel.SetSignedInUser(signedInUser);
        }

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
    }

    private void OnOpenPurchaseOrders(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Purchase Orders"))
        {
            return;
        }

        var purchaseOrdersView = new PurchaseOrdersView(viewModel.SelectedUser);
        ShowWorkspace(purchaseOrdersView, "Purchase Orders", 760, 1200, 1400, 820);
    }

    private void OnPurchaseOrdersBackRequested(object? sender, EventArgs e)
    {
        if (sender is PurchaseOrdersView purchaseOrdersView)
        {
            purchaseOrdersView.BackRequested -= OnPurchaseOrdersBackRequested;
        }

        ShowMenu();
    }

    private void OnOpenWialonUnits(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Wialon Units"))
        {
            return;
        }

        var trackingView = new TrackingView();
        ShowWorkspace(trackingView, "Wialon Unit List", 900, 1280, 1540, 980);
    }

    private void OnOpenJobCards(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Job Cards"))
        {
            return;
        }

        var jobCardView = new WialonJobCardView();
        ShowWorkspace(jobCardView, "Job Card Register", 860, 1280, 1480, 960);
    }

    private void OnOpenTrackingCertificates(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Tracking Certificates"))
        {
            return;
        }

        var trackingCertificateView = new TrackingCertificateView();
        ShowWorkspace(trackingCertificateView, "Tracking Certificates", 900, 1280, 1540, 980);
    }

    private void OnOpenInventory(object sender, RoutedEventArgs e)
    {
        if (!CanOpenWorkspace("Stock Inventory"))
        {
            return;
        }

        var inventoryView = new InventoryView();
        ShowWorkspace(inventoryView, "Stock Inventory", 820, 1320, 1560, 940);
    }

    private void ShowWorkspace(UserControl workspaceView, string title, double minHeight, double minWidth, double width, double height)
    {
        SubscribeBackRequested(workspaceView);

        Title = title;
        MinHeight = minHeight;
        MinWidth = minWidth;
        Width = Math.Max(Width, width);
        Height = Math.Max(Height, height);
        Content = workspaceView;
    }

    private void OnWorkspaceBackRequested(object? sender, EventArgs e)
    {
        if (sender is UserControl workspaceView)
        {
            UnsubscribeBackRequested(workspaceView);
        }

        ShowMenu();
    }

    private void ShowMenu()
    {
        Title = "Capital Air (Pty) Ltd";
        MinHeight = menuMinHeight;
        MinWidth = menuMinWidth;
        Content = menuContent;
    }

    private void SubscribeBackRequested(UserControl workspaceView)
    {
        switch (workspaceView)
        {
            case PurchaseOrdersView purchaseOrdersView:
                purchaseOrdersView.BackRequested += OnPurchaseOrdersBackRequested;
                break;
            case TrackingView trackingView:
                trackingView.BackRequested += OnWorkspaceBackRequested;
                break;
            case WialonJobCardView jobCardView:
                jobCardView.BackRequested += OnWorkspaceBackRequested;
                break;
            case TrackingCertificateView trackingCertificateView:
                trackingCertificateView.BackRequested += OnWorkspaceBackRequested;
                break;
            case InventoryView inventoryView:
                inventoryView.BackRequested += OnWorkspaceBackRequested;
                break;
        }
    }

    private void UnsubscribeBackRequested(UserControl workspaceView)
    {
        switch (workspaceView)
        {
            case PurchaseOrdersView purchaseOrdersView:
                purchaseOrdersView.BackRequested -= OnPurchaseOrdersBackRequested;
                break;
            case TrackingView trackingView:
                trackingView.BackRequested -= OnWorkspaceBackRequested;
                break;
            case WialonJobCardView jobCardView:
                jobCardView.BackRequested -= OnWorkspaceBackRequested;
                break;
            case TrackingCertificateView trackingCertificateView:
                trackingCertificateView.BackRequested -= OnWorkspaceBackRequested;
                break;
            case InventoryView inventoryView:
                inventoryView.BackRequested -= OnWorkspaceBackRequested;
                break;
        }
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
            (app as App)?.RegisterMainWindow(nextHomeWindow);
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
        await StartApprovalNotificationsAsync();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        approvalNotificationTimer.Stop();
        approvalNotificationTimer.Tick -= OnApprovalNotificationTimerTick;
    }

    private async Task StartApprovalNotificationsAsync()
    {
        if (signedInUser == null)
        {
            return;
        }

        approvalNotificationService = new PurchaseOrderApprovalNotificationService(signedInUser);
        await approvalNotificationService.InitializeAsync();

        approvalNotificationTimer.Stop();
        approvalNotificationTimer.Tick -= OnApprovalNotificationTimerTick;
        approvalNotificationTimer.Interval = TimeSpan.FromSeconds(30);
        approvalNotificationTimer.Tick += OnApprovalNotificationTimerTick;
        approvalNotificationTimer.Start();
    }

    private async void OnApprovalNotificationTimerTick(object? sender, EventArgs e)
    {
        if (approvalNotificationService == null)
        {
            return;
        }

        try
        {
            var notifications = await approvalNotificationService.GetNewNotificationsAsync();
            foreach (var notification in notifications)
            {
                (Application.Current as App)?.ShowTrayNotification(notification.Title, notification.Message);
            }
        }
        catch
        {
            // Notification polling should never interrupt purchase order work.
        }
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
