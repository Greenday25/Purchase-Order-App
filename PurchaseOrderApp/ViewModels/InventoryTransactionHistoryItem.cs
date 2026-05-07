namespace PurchaseOrderApp.ViewModels;

public sealed class InventoryTransactionHistoryItem
{
    public int InventoryTransactionId { get; init; }

    public string TransactionType { get; init; } = string.Empty;

    public string IssueOutNumber { get; init; } = string.Empty;

    public string ItemCode { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public int QuantityAfterTransaction { get; init; }

    public DateTime CreatedAt { get; init; }

    public string JobCardNumber { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string ItemDisplay => string.IsNullOrWhiteSpace(ItemCode)
        ? ItemName
        : $"{ItemCode} - {ItemName}";

    public string QuantityDisplay =>
        string.Equals(TransactionType, Models.InventoryTransactionTypes.StockOut, StringComparison.OrdinalIgnoreCase)
            ? $"-{Quantity}"
            : $"+{Quantity}";

    public string JobCardDisplay => string.IsNullOrWhiteSpace(JobCardNumber) ? "Not linked" : JobCardNumber;

    public string IssueOutNumberDisplay => string.IsNullOrWhiteSpace(IssueOutNumber) ? "-" : IssueOutNumber;
}
