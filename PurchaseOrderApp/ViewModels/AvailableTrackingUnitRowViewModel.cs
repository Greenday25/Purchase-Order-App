using CommunityToolkit.Mvvm.ComponentModel;

namespace PurchaseOrderApp.ViewModels;

public partial class AvailableTrackingUnitRowViewModel : ObservableObject
{
    public int InventoryTrackingUnitId { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public string ImeiNumber { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string ReceivedAtDisplay => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    [ObservableProperty]
    private bool isSelected;
}
