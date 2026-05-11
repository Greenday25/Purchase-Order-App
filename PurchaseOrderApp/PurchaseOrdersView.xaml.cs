using Microsoft.Win32;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for PurchaseOrdersView.xaml
/// </summary>
public partial class PurchaseOrdersView : UserControl
{
    private AppUser? signedInUser;
    private readonly DispatcherTimer historyRefreshTimer = new();
    private bool isHistoryRefreshRunning;
    private bool isDetailsDialogOpen;
    private int historyRefreshCycleCount;

    public event EventHandler? BackRequested;

    public PurchaseOrdersView()
        : this(null)
    {
    }

    public PurchaseOrdersView(AppUser? signedInUser)
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        historyRefreshTimer.Interval = TimeSpan.FromSeconds(5);
        historyRefreshTimer.Tick += OnHistoryRefreshTimerTick;
        _ = SetSignedInUserAsync(signedInUser);
    }

    public async Task SetSignedInUserAsync(AppUser? user)
    {
        signedInUser = user;
        if (GetViewModel() is MainViewModel vm)
        {
            await vm.SetSignedInUserAsync(user);
        }
    }

    private void OnBackToMenu(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        historyRefreshTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        historyRefreshTimer.Stop();
    }

    private async void OnHistoryRefreshTimerTick(object? sender, EventArgs e)
    {
        if (isHistoryRefreshRunning || HistoryPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        if (GetViewModel() is not MainViewModel vm)
        {
            return;
        }

        try
        {
            isHistoryRefreshRunning = true;
            historyRefreshCycleCount++;
            var forceFullRefresh = historyRefreshCycleCount % 12 == 0;
            await vm.LoadOrderHistoryAsync(forceFullRefresh);
        }
        finally
        {
            isHistoryRefreshRunning = false;
        }
    }

    private void OnNewOrder(object sender, RoutedEventArgs e)
    {
        if (GetViewModel() is { CanCreatePurchaseOrders: false })
        {
            MessageBox.Show("Managers and executives cannot create purchase orders.", "Create restricted", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editorViewModel = new MainViewModel();
        _ = editorViewModel.SetSignedInUserAsync(signedInUser);

        InlineOrderEditor.DataContext = editorViewModel;
        HistoryPanel.Visibility = Visibility.Collapsed;
        InlineOrderEditor.Visibility = Visibility.Visible;
        InlineOrderEditor.Focus();
    }

    private async void OnInlineOrderEditorCloseRequested(object sender, EventArgs e)
    {
        InlineOrderEditor.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Visible;
        InlineOrderEditor.DataContext = new MainViewModel();
        if (GetViewModel() is MainViewModel vm)
        {
            await vm.LoadOrderHistoryAsync(forceFullRefresh: true);
        }
    }

    private void OnHistoryRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (isDetailsDialogOpen)
        {
            return;
        }

        var vm = GetViewModel();
        var order = vm?.SelectedOrderHistoryItem;
        if (vm == null || order == null)
        {
            return;
        }

        var detailsWindow = new OrderDetailsWindow(vm, order.PurchaseOrderId)
        {
            Owner = GetHostWindow()
        };

        if (!detailsWindow.HasLoadedOrder)
        {
            _ = vm.LoadOrderHistoryAsync(forceFullRefresh: true);
            return;
        }

        try
        {
            isDetailsDialogOpen = true;
            detailsWindow.ShowDialog();
        }
        finally
        {
            isDetailsDialogOpen = false;
            _ = vm.LoadOrderHistoryAsync(forceFullRefresh: true);
        }
    }

    private void OnAllOrdersTabClick(object sender, RoutedEventArgs e) => SetHistoryScope(MainViewModel.OrderHistoryScope.All);

    private void OnMyOrdersTabClick(object sender, RoutedEventArgs e) => SetHistoryScope(MainViewModel.OrderHistoryScope.MyOrders);

    private void OnAwaitingManagerTabClick(object sender, RoutedEventArgs e) => SetHistoryScope(MainViewModel.OrderHistoryScope.AwaitingManager);

    private void OnAwaitingExecutiveTabClick(object sender, RoutedEventArgs e) => SetHistoryScope(MainViewModel.OrderHistoryScope.AwaitingExecutive);

    private void OnAwaitingInvoiceTabClick(object sender, RoutedEventArgs e) => SetHistoryScope(MainViewModel.OrderHistoryScope.AwaitingInvoice);

    private void OnCompletedTabClick(object sender, RoutedEventArgs e) => SetHistoryScope(MainViewModel.OrderHistoryScope.Completed);

    private void SetHistoryScope(MainViewModel.OrderHistoryScope scope)
    {
        if (GetViewModel() is MainViewModel vm)
        {
            vm.SelectedHistoryScope = scope;
        }
    }

    private async void OnMarkRejected(object sender, RoutedEventArgs e)
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

        if (!await GetViewModel()!.MarkRejectedAsync(order.PurchaseOrderId))
        {
            MessageBox.Show("I couldn't update the rejected status for that order.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnDeleteOrder(object sender, RoutedEventArgs e)
    {
        var order = GetSelectedOrder();
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

        if (!await GetViewModel()!.DeleteOrderAsync(order.PurchaseOrderId))
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

    private async void UploadDocument(string documentLabel, bool isInvoice)
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

        if (isInvoice && !order.IsApproved)
        {
            MessageBox.Show("Manager and director approval are required before the invoice is added.", "Approval required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"Upload {documentLabel} for {order.OrderNumber}",
            Filter = "Documents|*.pdf;*.png;*.jpg;*.jpeg;*.doc;*.docx;*.xls;*.xlsx|All Files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(GetHostWindow()) != true)
        {
            return;
        }

        var fileBytes = File.ReadAllBytes(dialog.FileName);
        var fileName = Path.GetFileName(dialog.FileName);
        var vm = GetViewModel()!;

        var wasSaved = isInvoice
            ? await vm.SaveInvoiceDocumentAsync(order.PurchaseOrderId, fileName, fileBytes)
            : await vm.SaveSignedOrderDocumentAsync(order.PurchaseOrderId, fileName, fileBytes);

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

    private Window? GetHostWindow()
    {
        return Window.GetWindow(this);
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
