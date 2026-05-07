using PurchaseOrderApp.ViewModels;
using System.Windows;

namespace PurchaseOrderApp;

public partial class InventoryWindow : Window
{
    private readonly InventoryViewModel viewModel = new();

    public InventoryWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnWindowLoaded;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOpenReceiveStock(object sender, RoutedEventArgs e)
    {
        var receiveStockWindow = new ReceiveStockWindow
        {
            Owner = this
        };

        var result = receiveStockWindow.ShowDialog();
        if (result == true)
        {
            viewModel.ReloadInventory(receiveStockWindow.ReceivedInventoryItemId);
        }
    }

    private void OnOpenRecentMovements(object sender, RoutedEventArgs e)
    {
        var movementsWindow = new InventoryMovementsWindow
        {
            Owner = this
        };

        movementsWindow.ShowDialog();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.Initialize();
    }
}
