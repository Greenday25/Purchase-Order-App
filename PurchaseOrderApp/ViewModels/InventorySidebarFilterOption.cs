namespace PurchaseOrderApp.ViewModels;

public sealed class InventorySidebarFilterOption
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int Count { get; init; }

    public string CountDisplay => Count.ToString();
}
