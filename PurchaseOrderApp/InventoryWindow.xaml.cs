using System.Windows;

namespace PurchaseOrderApp;

public partial class InventoryWindow : Window
{
    public InventoryWindow()
    {
        InitializeComponent();
        InventoryView.BackRequested += OnInventoryBackRequested;
    }

    private void OnInventoryBackRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
