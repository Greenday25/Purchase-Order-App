using PurchaseOrderApp.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

    private void OnOpenEditorWindow(object sender, RoutedEventArgs e)
    {
        var editorWindow = new WialonJobCardEditorWindow(viewModel)
        {
            Owner = this
        };

        editorWindow.ShowDialog();
    }

    private void OnOpenHistoricalWindow(object sender, RoutedEventArgs e)
    {
        var historicalWindow = new WialonHistoricalJobCardWindow(viewModel)
        {
            Owner = this
        };

        historicalWindow.ShowDialog();
    }

    private void OnJobCardDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not DataGridRow row ||
            row.Item is not JobCardHistoryItem jobCard)
        {
            return;
        }

        var detailsWindow = new JobCardDetailsWindow(jobCard.JobCardRecordId)
        {
            Owner = this
        };

        e.Handled = true;
        detailsWindow.ShowDialog();
        viewModel.RefreshJobCardRegister();
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        viewModel.SaveCredentials();
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
