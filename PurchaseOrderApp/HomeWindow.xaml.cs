using System.Windows;
using PurchaseOrderApp.ViewModels;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for HomeWindow.xaml
/// </summary>
public partial class HomeWindow : Window
{
    private readonly HomeWindowViewModel viewModel = new();

    public HomeWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnWindowLoaded;
    }

    private void OnOpenPurchaseOrders(object sender, RoutedEventArgs e)
    {
        var purchaseOrdersWindow = new MainWindow();
        purchaseOrdersWindow.Show();
    }

    private void OnOpenWialonUnits(object sender, RoutedEventArgs e)
    {
        var wialonWindow = new TrackingWindow();
        wialonWindow.Show();
    }

    private void OnOpenJobCards(object sender, RoutedEventArgs e)
    {
        var jobCardWindow = new WialonJobCardWindow();
        jobCardWindow.Show();
    }

    private async void OnOpenConnectivitySettings(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new ConnectivitySettingsWindow
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
        await viewModel.InitializeAsync();
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
    }
}
