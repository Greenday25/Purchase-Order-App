using PurchaseOrderApp.ViewModels;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for WialonHistoricalJobCardWindow.xaml
/// </summary>
public partial class WialonHistoricalJobCardWindow : Window
{
    private readonly WialonJobCardViewModel viewModel;

    public WialonHistoricalJobCardWindow(WialonJobCardViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOpenEditorWindow(object sender, RoutedEventArgs e)
    {
        var editorWindow = new WialonJobCardEditorWindow(viewModel)
        {
            Owner = this
        };

        editorWindow.ShowDialog();
    }
}
