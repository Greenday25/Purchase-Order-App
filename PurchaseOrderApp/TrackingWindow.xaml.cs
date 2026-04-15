using PurchaseOrderApp.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for TrackingWindow.xaml
/// </summary>
public partial class TrackingWindow : Window
{
    public TrackingWindow()
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

    private void OnUnitDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not WialonTrackingViewModel viewModel)
        {
            return;
        }

        if (sender is not DataGrid)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not DataGridRow row ||
            row.Item is not WialonUnitSummary selectedUnit)
        {
            return;
        }

        var detailsWindow = new WialonUnitDetailsWindow(
            viewModel.ApiHost,
            viewModel.AccessToken,
            viewModel.CurrentSessionId,
            selectedUnit)
        {
            Owner = this
        };

        e.Handled = true;
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
