using PurchaseOrderApp.ViewModels;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for JobCardBillingEditorWindow.xaml
/// </summary>
public partial class JobCardBillingEditorWindow : Window
{
    private readonly JobCardBillingEditorViewModel viewModel;

    public JobCardBillingEditorWindow(int jobCardRecordId)
    {
        InitializeComponent();
        viewModel = new JobCardBillingEditorViewModel(jobCardRecordId);
        DataContext = viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            viewModel.Save();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save billing", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
