using Microsoft.Win32;
using PurchaseOrderApp.ViewModels;
using System.IO;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for OrderDetailsWindow.xaml
/// </summary>
public partial class OrderDetailsWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly int _purchaseOrderId;

    public OrderDetailsWindow(MainViewModel mainViewModel, int purchaseOrderId)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        _purchaseOrderId = purchaseOrderId;

        LoadOrderDetails();
    }

    private OrderDetailsViewModel? CurrentOrderDetails => DataContext as OrderDetailsViewModel;

    private void OnMarkApproved(object sender, RoutedEventArgs e)
    {
        var order = GetCurrentOrderDetails();
        if (order == null)
        {
            return;
        }

        if (order.IsRejected)
        {
            MessageBox.Show("Rejected orders cannot be approved.", "Workflow order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (order.IsCompleted)
        {
            MessageBox.Show("Completed orders do not need another approval step.", "Already completed", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (order.IsApproved)
        {
            MessageBox.Show("Approval is already marked for this order.", "Already updated", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_mainViewModel.MarkApprovalsCompleted(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't update the approval status for that order.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadOrderDetails();
    }

    private void OnMarkRejected(object sender, RoutedEventArgs e)
    {
        var order = GetCurrentOrderDetails();
        if (order == null)
        {
            return;
        }

        if (order.IsRejected)
        {
            MessageBox.Show("This order is already marked as rejected.", "Already updated", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (order.IsCompleted)
        {
            MessageBox.Show("Completed orders cannot be rejected.", "Workflow order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_mainViewModel.MarkRejected(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't update the rejected status for that order.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadOrderDetails();
    }

    private void OnUploadSignedOrder(object sender, RoutedEventArgs e)
    {
        UploadDocument("signed order", isInvoice: false);
    }

    private void OnUploadInvoice(object sender, RoutedEventArgs e)
    {
        UploadDocument("invoice", isInvoice: true);
    }

    private void OnOpenSignedOrder(object sender, RoutedEventArgs e)
    {
        OpenDocument(isInvoice: false);
    }

    private void OnOpenInvoice(object sender, RoutedEventArgs e)
    {
        OpenDocument(isInvoice: true);
    }

    private void OnDeleteOrder(object sender, RoutedEventArgs e)
    {
        var order = GetCurrentOrderDetails();
        if (order == null)
        {
            return;
        }

        if (order.IsCompleted)
        {
            MessageBox.Show("Completed orders cannot be deleted.", "Delete blocked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmation = MessageBox.Show(
            $"Delete order {order.OrderNumber}?",
            "Delete order",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        if (!_mainViewModel.DeleteOrder(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't delete that order.", "Delete failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Close();
    }

    private void OnExportPdf(object sender, RoutedEventArgs e)
    {
        var exportViewModel = new MainViewModel();
        if (!exportViewModel.LoadExistingOrder(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't load that order for PDF export.", "Export failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var exportWindow = new OrderEditorWindow(exportViewModel)
        {
            Owner = this
        };

        exportWindow.ExportCurrentOrderToPdf();
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UploadDocument(string documentLabel, bool isInvoice)
    {
        var order = GetCurrentOrderDetails();
        if (order == null)
        {
            return;
        }

        if (order.IsRejected)
        {
            MessageBox.Show("Rejected orders cannot receive new documents.", "Workflow order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!isInvoice && !order.IsApproved)
        {
            MessageBox.Show("Approve the order first before uploading the signed order.", "Approval required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (isInvoice && !order.HasSignedOrder)
        {
            MessageBox.Show("Upload the signed order first so the order can move into the closed state before the invoice is added.", "Signed order required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"Upload {documentLabel} for {order.OrderNumber}",
            Filter = "Documents|*.pdf;*.png;*.jpg;*.jpeg;*.doc;*.docx;*.xls;*.xlsx|All Files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var fileBytes = File.ReadAllBytes(dialog.FileName);
        var fileName = Path.GetFileName(dialog.FileName);
        var wasSaved = isInvoice
            ? _mainViewModel.SaveInvoiceDocument(_purchaseOrderId, fileName, fileBytes)
            : _mainViewModel.SaveSignedOrderDocument(_purchaseOrderId, fileName, fileBytes);

        if (!wasSaved)
        {
            MessageBox.Show($"I couldn't store that {documentLabel} on the selected order.", "Upload failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadOrderDetails();
    }

    private void OpenDocument(bool isInvoice)
    {
        var storedDocument = isInvoice
            ? _mainViewModel.GetInvoiceDocument(_purchaseOrderId)
            : _mainViewModel.GetSignedOrderDocument(_purchaseOrderId);

        if (storedDocument == null)
        {
            MessageBox.Show(
                $"There is no {(isInvoice ? "invoice" : "signed order")} uploaded for this order yet.",
                "Document not found",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OrderDocumentLauncher.OpenStoredDocument(storedDocument);
    }

    private OrderDetailsViewModel? GetCurrentOrderDetails()
    {
        if (CurrentOrderDetails != null)
        {
            return CurrentOrderDetails;
        }

        MessageBox.Show("I couldn't load that order's details.", "Order not found", MessageBoxButton.OK, MessageBoxImage.Warning);
        return null;
    }

    private void LoadOrderDetails()
    {
        var orderDetails = _mainViewModel.GetOrderDetails(_purchaseOrderId);
        if (orderDetails == null)
        {
            MessageBox.Show("I couldn't load that order's details.", "Order not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
            return;
        }

        DataContext = orderDetails;
    }
}
