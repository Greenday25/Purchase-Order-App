using PurchaseOrderApp.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for WialonJobCardWindow.xaml
/// </summary>
public partial class WialonJobCardWindow : Window
{
    private readonly WialonJobCardViewModel viewModel;

    public WialonJobCardWindow()
    {
        InitializeComponent();
        viewModel = new WialonJobCardViewModel();
        DataContext = viewModel;
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        viewModel.SaveCredentials();
    }
}
