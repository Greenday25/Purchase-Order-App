namespace PurchaseOrderApp.ViewModels;

public sealed class InventoryItemRow
{
    public int InventoryItemId { get; init; }

    public string ItemCode { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsTrackingUnit { get; init; }

    public int QuantityOnHand { get; init; }

    public DateTime UpdatedAt { get; init; }

    public string ProductDisplay => string.IsNullOrWhiteSpace(ItemCode)
        ? ItemName
        : $"[{ItemCode}] {ItemName}";

    public string TrackingTypeDisplay => IsTrackingUnit ? "Tracking Unit" : "General Stock";

    public string StockStateDisplay => QuantityOnHand <= 0 ? "Out of Stock" : "Available";

    public string LastUpdatedDisplay => UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
