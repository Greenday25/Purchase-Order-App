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

    public bool HasLoadedOrder { get; private set; }

    private OrderDetailsViewModel? CurrentOrderDetails => DataContext as OrderDetailsViewModel;

    private async void OnMarkManagerApproved(object sender, RoutedEventArgs e)
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

        if (order.IsManagerApproved)
        {
            MessageBox.Show("Manager approval is already marked for this order.", "Already updated", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_mainViewModel.CanManagerApprovePurchaseOrders)
        {
            MessageBox.Show("Only manager approvers can complete manager approval.", "Approval Restricted", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!await _mainViewModel.MarkManagerApprovedAsync(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't update the approval status for that order.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Close();
    }

    private async void OnMarkDirectorApproved(object sender, RoutedEventArgs e)
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

        if (!order.IsManagerApproved)
        {
            MessageBox.Show("Manager approval is required before director approval.", "Manager approval required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (order.IsDirectorApproved)
        {
            MessageBox.Show("Director approval is already marked for this order.", "Already updated", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_mainViewModel.CanApprovePurchaseOrders)
        {
            MessageBox.Show("Only executive users can complete director approval.", "Approval Restricted", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!await _mainViewModel.MarkDirectorApprovedAsync(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't update the approval status for that order.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Close();
    }

    private async void OnAmendOrder(object sender, RoutedEventArgs e)
    {
        var order = GetCurrentOrderDetails();
        if (order == null)
        {
            return;
        }

        if (!order.CanAmend)
        {
            MessageBox.Show("Only pending approval orders can be amended.", "Amend blocked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editViewModel = new MainViewModel();
        editViewModel.CopyAccessContextFrom(_mainViewModel);
        if (!editViewModel.LoadExistingOrder(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't load that order for amendment.", "Amend failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var editorWindow = new OrderEditorWindow(editViewModel)
        {
            Owner = this
        };

        editorWindow.ShowDialog();
        await _mainViewModel.LoadOrderHistoryAsync();
        LoadOrderDetails();
    }

    private async void OnMarkRejected(object sender, RoutedEventArgs e)
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

        if (!await _mainViewModel.MarkRejectedAsync(_purchaseOrderId))
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

    private async void OnDeleteOrder(object sender, RoutedEventArgs e)
    {
        var order = GetCurrentOrderDetails();
        if (order == null)
        {
            return;
        }

        if (!order.CanDelete)
        {
            MessageBox.Show(order.DeleteRestrictionMessage, "Delete blocked", MessageBoxButton.OK, MessageBoxImage.Information);
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

        if (!await _mainViewModel.DeleteOrderAsync(_purchaseOrderId))
        {
            MessageBox.Show("I couldn't delete that order.", "Delete failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Close();
    }

    private void OnExportPdf(object sender, RoutedEventArgs e)
    {
        var exportViewModel = new MainViewModel();
        exportViewModel.CopyAccessContextFrom(_mainViewModel);
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

    private async void UploadDocument(string documentLabel, bool isInvoice)
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

        if (isInvoice && !order.IsApproved)
        {
            MessageBox.Show("Manager and director approval are required before the invoice is added.", "Approval required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (isInvoice && !order.CanUploadInvoice)
        {
            MessageBox.Show("Executive users can upload invoices only for purchase orders they created.", "Invoice restricted", MessageBoxButton.OK, MessageBoxImage.Information);
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
            ? await _mainViewModel.SaveInvoiceDocumentAsync(_purchaseOrderId, fileName, fileBytes)
            : await _mainViewModel.SaveSignedOrderDocumentAsync(_purchaseOrderId, fileName, fileBytes);

        if (!wasSaved)
        {
            MessageBox.Show($"I couldn't store that {documentLabel} on the selected order.", "Upload failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadOrderDetails();
    }

    private void OpenDocument(bool isInvoice)
    {
        var order = GetCurrentOrderDetails();
        if (order == null)
        {
            return;
        }

        if (isInvoice && !order.CanOpenInvoice)
        {
            MessageBox.Show("Invoices are visible only to the user who created the purchase order.", "Invoice restricted", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

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
            HasLoadedOrder = false;
            MessageBox.Show("I couldn't load that order's details.", "Order not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (IsVisible)
            {
                Close();
            }
            return;
        }

        HasLoadedOrder = true;
        DataContext = orderDetails;
    }
}
