namespace PurchaseOrderApp.ViewModels;

public sealed class ReceiptPurchaseOrderOptionViewModel
{
    public int PurchaseOrderId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string SupplierName { get; init; } = string.Empty;

    public DateTime Date { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string DisplayText =>
        string.IsNullOrWhiteSpace(SupplierName)
            ? $"{OrderNumber} - {Date:dd/MM/yyyy}"
            : $"{OrderNumber} - {SupplierName} - {Date:dd/MM/yyyy}";
}
