namespace PurchaseOrderApp.ViewModels;

public sealed class PurchaseOrderSpendReportItem
{
    public string SupplierName { get; init; } = string.Empty;
    public string CreatedByDisplayName { get; init; } = string.Empty;
    public int OrderCount { get; init; }
    public decimal TotalAmount { get; init; }
}
