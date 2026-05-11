using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for WialonJobCardWindow.xaml
/// </summary>
public partial class WialonJobCardWindow : Window
{
    public WialonJobCardWindow()
    {
        InitializeComponent();
        WialonJobCardView.BackRequested += OnJobCardsBackRequested;
    }

    private void OnJobCardsBackRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
