using PurchaseOrderApp.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for TrackingView.xaml
/// </summary>
public partial class TrackingView : UserControl
{
    public event EventHandler? BackRequested;

    public TrackingView()
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
            Owner = Window.GetWindow(this)
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
