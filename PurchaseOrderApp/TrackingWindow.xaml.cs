using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for TrackingWindow.xaml
/// </summary>
public partial class TrackingWindow : Window
{
    public TrackingWindow()
    {
        InitializeComponent();
        TrackingView.BackRequested += OnTrackingBackRequested;
    }

    private void OnTrackingBackRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
