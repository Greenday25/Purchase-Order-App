using PurchaseOrderApp.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for TrackingCertificateView.xaml
/// </summary>
public partial class TrackingCertificateView : UserControl
{
    public event EventHandler? BackRequested;

    public TrackingCertificateView()
    {
        InitializeComponent();
        Loaded += OnViewLoaded;
    }

    private async void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WialonTrackingViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void OnBackToMenu(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenCertificateBuilder(object sender, RoutedEventArgs e)
    {
        OpenCertificateBuilder();
    }

    private void OnUnitDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not DataGridRow row ||
            row.Item is not WialonUnitSummary)
        {
            return;
        }

        e.Handled = true;
        OpenCertificateBuilder();
    }

    private void OpenCertificateBuilder()
    {
        if (DataContext is not WialonTrackingViewModel viewModel || viewModel.SelectedUnit is null)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                "Select a Wialon unit first, then open the certificate builder.",
                "Tracking Certificates",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var detailsWindow = new WialonUnitDetailsWindow(
            viewModel.ApiHost,
            viewModel.AccessToken,
            viewModel.CurrentSessionId,
            viewModel.SelectedUnit)
        {
            Owner = Window.GetWindow(this)
        };

        detailsWindow.ShowDialog();
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
