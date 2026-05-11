using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for TrackingCertificateWindow.xaml
/// </summary>
public partial class TrackingCertificateWindow : Window
{
    public TrackingCertificateWindow()
    {
        InitializeComponent();
        TrackingCertificateView.BackRequested += OnCertificatesBackRequested;
    }

    private void OnCertificatesBackRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
