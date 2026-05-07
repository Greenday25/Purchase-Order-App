using CommunityToolkit.Mvvm.ComponentModel;

namespace PurchaseOrderApp.ViewModels;

public partial class TrackingUnitIdentityEntryViewModel : ObservableObject
{
    public TrackingUnitIdentityEntryViewModel(int unitNumber)
    {
        UnitNumber = unitNumber;
    }

    public int UnitNumber { get; }

    public string UnitLabel => $"Unit {UnitNumber}";

    [ObservableProperty]
    private string serialNumber = string.Empty;

    [ObservableProperty]
    private string imeiNumber = string.Empty;
}
