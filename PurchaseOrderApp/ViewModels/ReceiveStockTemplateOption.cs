namespace PurchaseOrderApp.ViewModels;

public sealed class ReceiveStockTemplateOption
{
    public string DisplayText { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsTrackingUnit { get; init; }
}
