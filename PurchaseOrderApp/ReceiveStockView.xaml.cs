using PurchaseOrderApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PurchaseOrderApp;

public partial class ReceiveStockView : UserControl
{
    private ReceiveStockViewModel viewModel = new();

    public event EventHandler? CloseRequested;
    public event EventHandler<int?>? StockReceived;

    public ReceiveStockView()
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public int? ReceivedInventoryItemId => viewModel.ReceivedInventoryItemId;

    public void Reset()
    {
        viewModel = new ReceiveStockViewModel();
        DataContext = viewModel;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnReceiveStock(object sender, RoutedEventArgs e)
    {
        if (!viewModel.TryReceiveStock())
        {
            return;
        }

        StockReceived?.Invoke(this, viewModel.ReceivedInventoryItemId);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
