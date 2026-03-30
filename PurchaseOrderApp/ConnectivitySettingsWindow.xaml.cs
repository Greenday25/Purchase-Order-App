using System.Windows;
using PurchaseOrderApp.ViewModels;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for ConnectivitySettingsWindow.xaml
/// </summary>
public partial class ConnectivitySettingsWindow : Window
{
    private readonly HomeWindowViewModel viewModel = new();

    public ConnectivitySettingsWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        viewModel.SaveConnectionSettingsCommand.Execute(null);
        DialogResult = true;
        Close();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
