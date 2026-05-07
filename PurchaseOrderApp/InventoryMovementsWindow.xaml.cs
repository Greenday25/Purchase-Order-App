using PurchaseOrderApp.ViewModels;
using System.Windows;

namespace PurchaseOrderApp;

public partial class InventoryMovementsWindow : Window
{
    private readonly InventoryMovementsViewModel viewModel = new();

    public InventoryMovementsWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnWindowLoaded;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.Initialize();
    }
}
