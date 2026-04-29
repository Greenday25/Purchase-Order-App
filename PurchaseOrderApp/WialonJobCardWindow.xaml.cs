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

    private void OnDeleteSelectedJobCard(object sender, RoutedEventArgs e)
    {
        if (JobCardRegisterGrid.SelectedItem is not JobCardHistoryItem jobCard)
        {
            MessageBox.Show(
                this,
                "Select a job card entry to delete from the local register.",
                "Delete Job Card Entry",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Delete local job card entry {jobCard.JobCardNumber}?\n\nThis will only remove it from this app's register. It will not delete or change anything in Wialon.",
            "Delete Job Card Entry",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            viewModel.DeleteJobCardEntry(jobCard.JobCardRecordId);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"I couldn't delete that local job card entry: {ex.Message}",
                "Delete Job Card Entry",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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
