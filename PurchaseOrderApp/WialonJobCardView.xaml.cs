using PurchaseOrderApp.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for WialonJobCardView.xaml
/// </summary>
public partial class WialonJobCardView : UserControl
{
    private readonly WialonJobCardViewModel viewModel;

    public event EventHandler? BackRequested;

    public WialonJobCardView()
    {
        InitializeComponent();
        viewModel = new WialonJobCardViewModel();
        DataContext = viewModel;
        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenEditorWindow(object sender, RoutedEventArgs e)
    {
        var editorWindow = new WialonJobCardEditorWindow(viewModel)
        {
            Owner = Window.GetWindow(this)
        };

        editorWindow.ShowDialog();
    }

    private void OnDeleteSelectedJobCard(object sender, RoutedEventArgs e)
    {
        if (JobCardRegisterGrid.SelectedItem is not JobCardHistoryItem jobCard)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                "Select a job card entry to delete from the local register.",
                "Delete Job Card Entry",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            Window.GetWindow(this),
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
                Window.GetWindow(this),
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
            Owner = Window.GetWindow(this)
        };

        e.Handled = true;
        detailsWindow.ShowDialog();
        viewModel.RefreshJobCardRegister();
    }

    private async void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
    }

    private void OnViewUnloaded(object sender, RoutedEventArgs e)
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
