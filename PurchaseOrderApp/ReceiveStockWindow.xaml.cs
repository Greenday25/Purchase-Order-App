using PurchaseOrderApp.ViewModels;
using System.Windows;

namespace PurchaseOrderApp;

public partial class ReceiveStockWindow : Window
{
    private readonly ReceiveStockViewModel viewModel = new();

    public ReceiveStockWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public int? ReceivedInventoryItemId => viewModel.ReceivedInventoryItemId;

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnReceiveStock(object sender, RoutedEventArgs e)
    {
        if (!viewModel.TryReceiveStock())
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
