using CommunityToolkit.Mvvm.ComponentModel;

namespace PurchaseOrderApp.ViewModels;

public partial class WialonUnitSummary : ObservableObject
{
    [ObservableProperty]
    private long unitId;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UniqueIdDisplay))]
    private string? uniqueId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PhoneNumberDisplay))]
    private string? phoneNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountDisplay))]
    private long? accountId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountDisplay))]
    private string? accountLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HardwareTypeDisplay))]
    private long? hardwareTypeId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HardwareTypeDisplay))]
    private string? hardwareTypeName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastMessageDisplay))]
    [NotifyPropertyChangedFor(nameof(IsInactiveOver14Days))]
    private DateTimeOffset? lastMessageAt;

    [ObservableProperty]
    private double? latitude;

    [ObservableProperty]
    private double? longitude;

    [ObservableProperty]
    private long accessRights;

    public string LastMessageDisplay =>
        LastMessageAt.HasValue
            ? LastMessageAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "No messages";

    public bool IsInactiveOver14Days =>
        !LastMessageAt.HasValue ||
        LastMessageAt.Value < DateTimeOffset.UtcNow.AddDays(-14);

    public string UniqueIdDisplay =>
        string.IsNullOrWhiteSpace(UniqueId) ? "N/A" : UniqueId;

    public string PhoneNumberDisplay =>
        string.IsNullOrWhiteSpace(PhoneNumber) ? "N/A" : PhoneNumber;

    public string AccountDisplay =>
        !string.IsNullOrWhiteSpace(AccountLabel)
            ? AccountLabel
            : "N/A";

    public string HardwareTypeDisplay =>
        !string.IsNullOrWhiteSpace(HardwareTypeName)
            ? HardwareTypeName
            : HardwareTypeId.HasValue
                ? HardwareTypeId.Value.ToString()
                : "N/A";

    public string LocationDisplay =>
        Latitude.HasValue && Longitude.HasValue
            ? $"{Latitude.Value:0.######}, {Longitude.Value:0.######}"
            : "Unknown";
}
