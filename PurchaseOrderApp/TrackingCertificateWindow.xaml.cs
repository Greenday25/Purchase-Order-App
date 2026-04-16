using PurchaseOrderApp.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for TrackingCertificateWindow.xaml
/// </summary>
public partial class TrackingCertificateWindow : Window
{
    public TrackingCertificateWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WialonTrackingViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void OnBackToMenu(object sender, RoutedEventArgs e)
    {
        Close();
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
            Owner = this
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
