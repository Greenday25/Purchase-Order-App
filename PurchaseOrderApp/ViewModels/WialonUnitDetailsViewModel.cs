using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;

namespace PurchaseOrderApp.ViewModels;

public partial class WialonUnitDetailsViewModel : ObservableObject
{
    private const long ViewConnectivitySettingsRight = 0x0004000000L;
    private readonly string apiHost;
    private readonly string accessToken;
    private readonly TrackingCertificateDataBuilder trackingCertificateDataBuilder = new();
    private readonly TrackingCertificatePdfService trackingCertificatePdfService = new();

    public WialonUnitDetailsViewModel(
        string apiHost,
        string accessToken,
        string? sessionId,
        WialonUnitSummary unit)
    {
        this.apiHost = apiHost;
        this.accessToken = accessToken;
        SessionId = sessionId;

        UnitId = unit.UnitId;
        Name = unit.Name;
        UniqueId = unit.UniqueId;
        PhoneNumber = unit.PhoneNumber;
        AccountId = unit.AccountId;
        AccountLabel = unit.AccountLabel;
        HardwareTypeId = unit.HardwareTypeId;
        HardwareTypeName = unit.HardwareTypeName;
        LastMessageAt = unit.LastMessageAt;
        TrackingCertificate = null;
        Latitude = unit.Latitude;
        Longitude = unit.Longitude;
        StatusMessage = "Loading Wialon profile data...";
    }

    public string ApiHost => apiHost;

    public string AccessToken => accessToken;

    public long? CreatorId { get; private set; }

    public string? SessionId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private long unitId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UniqueIdDisplay))]
    private string? uniqueId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SecondaryUniqueIdDisplay))]
    private string? secondaryUniqueId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PhoneNumberDisplay))]
    private string? phoneNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SecondaryPhoneNumberDisplay))]
    private string? secondaryPhoneNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuidDisplay))]
    private string? unitGuid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HardwareTypeDisplay))]
    private long? hardwareTypeId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HardwareTypeDisplay))]
    private string? hardwareTypeName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastMessageDisplay))]
    private DateTimeOffset? lastMessageAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationDisplay))]
    private double? latitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationDisplay))]
    private double? longitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountDisplay))]
    [NotifyPropertyChangedFor(nameof(CanViewConnectivitySettings))]
    private long accessRights;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountDisplay))]
    private long? accountId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountDisplay))]
    private string? accountLabel;

    [ObservableProperty]
    private ObservableCollection<WialonUnitDetailField> fields = [];

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportTrackingCertificateCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExportTrackingCertificate))]
    [NotifyPropertyChangedFor(nameof(CustomerClientDisplay))]
    [NotifyPropertyChangedFor(nameof(RegistrationNumberDisplay))]
    [NotifyPropertyChangedFor(nameof(CertificateVinDisplay))]
    [NotifyPropertyChangedFor(nameof(CertificateVehicleTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(CertificateColourDisplay))]
    [NotifyPropertyChangedFor(nameof(TypeOfSystemDisplay))]
    [NotifyPropertyChangedFor(nameof(VesaSaiaNumberDisplay))]
    [NotifyPropertyChangedFor(nameof(InstallationDateDisplay))]
    [NotifyPropertyChangedFor(nameof(CertificateStatusMessage))]
    private TrackingCertificateData? trackingCertificate;

    public string WindowTitle =>
        string.IsNullOrWhiteSpace(Name)
            ? $"Wialon Unit {UnitId}"
            : $"{Name} - Wialon Profile";

    public string UniqueIdDisplay => string.IsNullOrWhiteSpace(UniqueId) ? "N/A" : UniqueId;

    public string SecondaryUniqueIdDisplay => string.IsNullOrWhiteSpace(SecondaryUniqueId) ? "N/A" : SecondaryUniqueId;

    public string PhoneNumberDisplay => string.IsNullOrWhiteSpace(PhoneNumber) ? "N/A" : PhoneNumber;

    public string SecondaryPhoneNumberDisplay => string.IsNullOrWhiteSpace(SecondaryPhoneNumber) ? "N/A" : SecondaryPhoneNumber;

    public string GuidDisplay => string.IsNullOrWhiteSpace(UnitGuid) ? "N/A" : UnitGuid;

    public string HardwareTypeDisplay =>
        !string.IsNullOrWhiteSpace(HardwareTypeName)
            ? HardwareTypeName
            : HardwareTypeId.HasValue
                ? HardwareTypeId.Value.ToString()
                : "N/A";

    public string LastMessageDisplay =>
        LastMessageAt.HasValue
            ? LastMessageAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "No messages";

    public string LocationDisplay =>
        Latitude.HasValue && Longitude.HasValue
            ? $"{Latitude.Value:0.######}, {Longitude.Value:0.######}"
            : "Unknown";

    public string AccountDisplay =>
        !string.IsNullOrWhiteSpace(AccountLabel)
            ? AccountLabel
            : "N/A";

    public bool CanViewConnectivitySettings => (AccessRights & ViewConnectivitySettingsRight) != 0;

    public bool CanExportTrackingCertificate => TrackingCertificate is not null && !IsBusy;

    public string CustomerClientDisplay => FormatCertificateValue(TrackingCertificate?.CustomerClient);

    public string RegistrationNumberDisplay => FormatCertificateValue(TrackingCertificate?.RegistrationNumber);

    public string CertificateVinDisplay => FormatCertificateValue(TrackingCertificate?.Vin);

    public string CertificateVehicleTypeDisplay => FormatCertificateValue(TrackingCertificate?.VehicleType);

    public string CertificateColourDisplay => FormatCertificateValue(TrackingCertificate?.Colour);

    public string TypeOfSystemDisplay => FormatCertificateValue(TrackingCertificate?.TypeOfSystem);

    public string VesaSaiaNumberDisplay => FormatCertificateValue(TrackingCertificate?.VesaSaiaNumber);

    public string InstallationDateDisplay => FormatCertificateValue(TrackingCertificate?.InstallationDate);

    public string CertificateStatusMessage =>
        TrackingCertificate is null
            ? "Load the unit details to prepare the tracking certificate."
            : "Certificate values are resolved from the selected Wialon unit. Any missing field remains marked as Pending until that value exists on Wialon.";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            StatusMessage = "A Wialon token is required to load unit details.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading Wialon profile data...";

            var client = new WialonApiClient(apiHost);
            var details = await LoadDetailsAsync(client, cancellationToken).ConfigureAwait(true);
            ApplyDetails(details);

            if (string.IsNullOrWhiteSpace(HardwareTypeName) && HardwareTypeId.HasValue)
            {
                var resolvedHardwareTypeName = await ResolveHardwareTypeNameAsync(
                    client,
                    SessionId,
                    HardwareTypeId,
                    cancellationToken).ConfigureAwait(true);

                if (!string.IsNullOrWhiteSpace(resolvedHardwareTypeName))
                {
                    HardwareTypeName = resolvedHardwareTypeName;
                    TrackingCertificate = BuildTrackingCertificate(details);
                }
            }

            var profileMessage = details.Fields.Count == 0
                ? "Loaded the unit, but no profile, custom, or admin fields were returned."
                : $"Loaded {details.Fields.Count} field(s) from Wialon.";

            var connectivityMessage = CanViewConnectivitySettings
                ? string.IsNullOrWhiteSpace(UniqueId) && string.IsNullOrWhiteSpace(PhoneNumber)
                    ? "Wialon returned the unit, but no connectivity values were included."
                    : "Connectivity settings loaded from Wialon."
                : "This token may not have View connectivity settings rights, so Wialon can hide the device type, phone, and unique ID.";

            StatusMessage = $"{profileMessage} {connectivityMessage}";
            ExportTrackingCertificateCommand.NotifyCanExecuteChanged();
        }
        catch (WialonApiException ex)
        {
            StatusMessage = ex.ErrorCode == 7
                ? "This unit could not be found or is inactive for the current token."
                : ex.Message;
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Unable to reach Wialon: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error while loading unit details: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportTrackingCertificate))]
    private void ExportTrackingCertificate()
    {
        if (TrackingCertificate is null)
        {
            StatusMessage = "Load the unit details before exporting the tracking certificate.";
            return;
        }

        try
        {
            var outputPath = trackingCertificatePdfService.ExportCertificate(TrackingCertificate);
            StatusMessage = $"Tracking certificate exported to {outputPath}";

            Process.Start(new ProcessStartInfo(outputPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to export the tracking certificate: {ex.Message}";
        }
    }

    private static async Task<string?> ResolveHardwareTypeNameAsync(
        WialonApiClient client,
        string? sessionId,
        long? hardwareTypeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !hardwareTypeId.HasValue)
        {
            return null;
        }

        try
        {
            var hardwareTypes = await client.GetHardwareTypeNamesAsync(sessionId, cancellationToken).ConfigureAwait(true);
            return hardwareTypes.TryGetValue(hardwareTypeId.Value, out var hardwareTypeName)
                ? hardwareTypeName
                : null;
        }
        catch (WialonApiException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<WialonUnitDetails> LoadDetailsAsync(WialonApiClient client, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(SessionId))
        {
            try
            {
                return await client.GetUnitDetailsAsync(SessionId, UnitId, cancellationToken).ConfigureAwait(true);
            }
            catch (WialonApiException ex) when (ex.ErrorCode == 6)
            {
                SessionId = null;
            }
        }

        var session = await client.LoginAsync(accessToken, cancellationToken).ConfigureAwait(true);
        SessionId = session.SessionId;
        CreatorId = session.CreatorId;
        return await client.GetUnitDetailsAsync(SessionId, UnitId, cancellationToken).ConfigureAwait(true);
    }

    private void ApplyDetails(WialonUnitDetails details)
    {
        UnitId = details.UnitId;
        Name = details.Name;
        UniqueId = details.UniqueId;
        SecondaryUniqueId = details.UniqueId2;
        PhoneNumber = details.PhoneNumber;
        SecondaryPhoneNumber = details.PhoneNumber2;
        UnitGuid = details.Guid;
        HardwareTypeId = details.HardwareTypeId;
        HardwareTypeName = details.HardwareTypeName;
        LastMessageAt = details.LastMessageAt;
        Latitude = details.Latitude;
        Longitude = details.Longitude;
        AccessRights = details.AccessRights;
        AccountId = details.AccountId ?? AccountId;
        if (!string.IsNullOrWhiteSpace(details.AccountLabel))
        {
            AccountLabel = details.AccountLabel;
        }

        Fields = new ObservableCollection<WialonUnitDetailField>(
            details.Fields.Select(field => new WialonUnitDetailField
            {
                Category = field.Category,
                FieldId = field.FieldId,
                Name = field.Name,
                Value = field.Value
            }));

        TrackingCertificate = BuildTrackingCertificate(details);
    }

    private TrackingCertificateData BuildTrackingCertificate(WialonUnitDetails details)
    {
        return trackingCertificateDataBuilder.Build(details, HardwareTypeName, AccountLabel);
    }

    private static string FormatCertificateValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Pending" : value.Trim();
    }

}
