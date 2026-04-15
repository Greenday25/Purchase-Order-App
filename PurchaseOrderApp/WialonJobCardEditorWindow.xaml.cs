using PurchaseOrderApp.ViewModels;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for WialonJobCardEditorWindow.xaml
/// </summary>
public partial class WialonJobCardEditorWindow : Window
{
    public WialonJobCardEditorWindow(WialonJobCardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
