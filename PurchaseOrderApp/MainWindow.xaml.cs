using Microsoft.Win32;
using PurchaseOrderApp.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnBackToMenu(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnNewOrder(object sender, RoutedEventArgs e)
    {
        var editorWindow = new OrderEditorWindow
        {
            Owner = this
        };

        editorWindow.ShowDialog();

        GetViewModel()?.LoadOrderHistory();
    }

    private void OnHistoryRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var vm = GetViewModel();
        var order = vm?.SelectedOrderHistoryItem;
        if (vm == null || order == null)
        {
            return;
        }

        var detailsWindow = new OrderDetailsWindow(vm, order.PurchaseOrderId)
        {
            Owner = this
        };

        detailsWindow.ShowDialog();
    }

    private void OnMarkApproved(object sender, RoutedEventArgs e)
    {
        var order = GetSelectedOrder();
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

        if (order.ManagerApprovedAt.HasValue || order.DirectorApprovedAt.HasValue)
        {
            MessageBox.Show("Approval is already marked for this order.", "Already updated", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!GetViewModel()!.MarkApprovalsCompleted(order.PurchaseOrderId))
        {
            MessageBox.Show("I couldn't update the approval status for that order.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnMarkRejected(object sender, RoutedEventArgs e)
    {
        var order = GetSelectedOrder();
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

        if (!GetViewModel()!.MarkRejected(order.PurchaseOrderId))
        {
            MessageBox.Show("I couldn't update the rejected status for that order.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnDeleteOrder(object sender, RoutedEventArgs e)
    {
        var order = GetSelectedOrder();
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

        if (!GetViewModel()!.DeleteOrder(order.PurchaseOrderId))
        {
            MessageBox.Show("I couldn't delete that order.", "Delete failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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

    private void UploadDocument(string documentLabel, bool isInvoice)
    {
        var order = GetSelectedOrder();
        if (order == null)
        {
            return;
        }

        if (order.IsRejected)
        {
            MessageBox.Show("Rejected orders cannot receive new documents.", "Workflow order", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!isInvoice && !(order.ManagerApprovedAt.HasValue || order.DirectorApprovedAt.HasValue))
        {
            MessageBox.Show("Approve the order first before uploading the signed order.", "Approval required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (isInvoice && string.IsNullOrWhiteSpace(order.SignedOrderFileName))
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
        var vm = GetViewModel()!;

        var wasSaved = isInvoice
            ? vm.SaveInvoiceDocument(order.PurchaseOrderId, fileName, fileBytes)
            : vm.SaveSignedOrderDocument(order.PurchaseOrderId, fileName, fileBytes);

        if (!wasSaved)
        {
            MessageBox.Show($"I couldn't store that {documentLabel} on the selected order.", "Upload failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenDocument(bool isInvoice)
    {
        var order = GetSelectedOrder();
        if (order == null)
        {
            return;
        }

        var vm = GetViewModel()!;
        var storedDocument = isInvoice
            ? vm.GetInvoiceDocument(order.PurchaseOrderId)
            : vm.GetSignedOrderDocument(order.PurchaseOrderId);

        if (storedDocument == null)
        {
            MessageBox.Show($"There is no {(isInvoice ? "invoice" : "signed order")} uploaded for this order yet.", "Document not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OrderDocumentLauncher.OpenStoredDocument(storedDocument);
    }

    private MainViewModel? GetViewModel()
    {
        return DataContext as MainViewModel;
    }

    private OrderHistoryItem? GetSelectedOrder()
    {
        var order = GetViewModel()?.SelectedOrderHistoryItem;
        if (order != null)
        {
            return order;
        }

        MessageBox.Show("Select an order from the history table first.", "Order required", MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
    }
}
