using PurchaseOrderApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PurchaseOrderApp;

public partial class InventoryView : UserControl
{
    private readonly InventoryViewModel viewModel = new();

    public event EventHandler? BackRequested;

    public InventoryView()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnViewLoaded;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenReceiveStock(object sender, RoutedEventArgs e)
    {
        InlineReceiveStockView.Reset();
        InventoryDashboard.Visibility = Visibility.Collapsed;
        InlineIssueOutView.Visibility = Visibility.Collapsed;
        InlineReceiveStockView.Visibility = Visibility.Visible;
        InlineReceiveStockView.Focus();
    }

    private void OnInlineReceiveStockCloseRequested(object sender, EventArgs e)
    {
        InlineReceiveStockView.Visibility = Visibility.Collapsed;
        InventoryDashboard.Visibility = Visibility.Visible;
    }

    private void OnInlineStockReceived(object sender, int? receivedInventoryItemId)
    {
        viewModel.ReloadInventory(receivedInventoryItemId);
    }

    private void OnOpenIssueOut(object sender, RoutedEventArgs e)
    {
        if (!viewModel.OpenIssueOutPanelCommand.CanExecute(null))
        {
            viewModel.StatusMessage = "Select an inventory row first, then use Issue Out.";
            return;
        }

        viewModel.OpenIssueOutPanelCommand.Execute(null);
        viewModel.IsMovementPanelOpen = false;
        InlineReceiveStockView.Visibility = Visibility.Collapsed;
        InventoryDashboard.Visibility = Visibility.Collapsed;
        InlineIssueOutView.Visibility = Visibility.Visible;
        InlineIssueOutView.Focus();
    }

    private void OnCloseIssueOut(object sender, RoutedEventArgs e)
    {
        InlineIssueOutView.Visibility = Visibility.Collapsed;
        InventoryDashboard.Visibility = Visibility.Visible;
    }

    private void OnRecordIssueOut(object sender, RoutedEventArgs e)
    {
        if (!viewModel.RecordMovementCommand.CanExecute(null))
        {
            return;
        }

        viewModel.RecordMovementCommand.Execute(null);
        InlineIssueOutView.Visibility = Visibility.Collapsed;
        InventoryDashboard.Visibility = Visibility.Visible;
    }

    private void OnOpenRecentMovements(object sender, RoutedEventArgs e)
    {
        var movementsWindow = new InventoryMovementsWindow
        {
            Owner = Window.GetWindow(this)
        };

        movementsWindow.ShowDialog();
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.Initialize();
    }
}
