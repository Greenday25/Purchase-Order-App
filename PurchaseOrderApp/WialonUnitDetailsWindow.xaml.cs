using PurchaseOrderApp.ViewModels;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for WialonUnitDetailsWindow.xaml
/// </summary>
public partial class WialonUnitDetailsWindow : Window
{
    private readonly WialonUnitDetailsViewModel _viewModel;

    public WialonUnitDetailsWindow(
        string apiHost,
        string accessToken,
        string? sessionId,
        WialonUnitSummary unit)
    {
        _viewModel = new WialonUnitDetailsViewModel(apiHost, accessToken, sessionId, unit);
        DataContext = _viewModel;
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.LoadAsync();
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
