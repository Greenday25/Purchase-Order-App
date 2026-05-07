namespace PurchaseOrderApp.ViewModels;

public sealed class OrderReceiptItemViewModel
{
    public string ReceiptNumber { get; init; } = string.Empty;

    public DateTime ReceivedAt { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string ItemCode { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public string Notes { get; init; } = string.Empty;

    public string ItemDisplay =>
        string.IsNullOrWhiteSpace(ItemCode)
            ? ItemName
            : string.IsNullOrWhiteSpace(ItemName)
                ? ItemCode
                : $"{ItemCode} - {ItemName}";

    public string ReceivedAtDisplay => ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
