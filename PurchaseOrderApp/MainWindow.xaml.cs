using PurchaseOrderApp.Models;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(AppUser? signedInUser)
    {
        InitializeComponent();
        _ = PurchaseOrdersView.SetSignedInUserAsync(signedInUser);
        PurchaseOrdersView.BackRequested += OnPurchaseOrdersBackRequested;
    }

    private void OnPurchaseOrdersBackRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
