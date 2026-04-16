namespace PurchaseOrderApp.Models;

public sealed class TrackingCertificateData
{
    public long UnitId { get; init; }

    public string UnitName { get; init; } = string.Empty;

    public string CustomerClient { get; init; } = string.Empty;

    public string RegistrationNumber { get; init; } = string.Empty;

    public string Vin { get; init; } = string.Empty;

    public string VehicleType { get; init; } = string.Empty;

    public string Colour { get; init; } = string.Empty;

    public string SystemName { get; init; } = string.Empty;

    public string SerialNumber { get; init; } = string.Empty;

    public string TypeOfSystem { get; init; } = string.Empty;

    public string VesaSaiaNumber { get; init; } = string.Empty;

    public string InstallationDate { get; init; } = string.Empty;

    public DateTimeOffset? UnitCreatedAt { get; init; }
}
