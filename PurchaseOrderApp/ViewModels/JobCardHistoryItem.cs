namespace PurchaseOrderApp.ViewModels;

public sealed class JobCardHistoryItem
{
    public int JobCardRecordId { get; init; }

    public string JobCardNumber { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string WorkflowStatus { get; init; } = string.Empty;

    public string VehicleDisplay { get; init; } = string.Empty;

    public string Client { get; init; } = string.Empty;

    public string? WialonUnitName { get; init; }

    public long? WialonUnitId { get; init; }

    public string StatusNotes { get; init; } = string.Empty;

    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string UnitDisplay =>
        WialonUnitId.HasValue
            ? string.IsNullOrWhiteSpace(WialonUnitName)
                ? WialonUnitId.Value.ToString()
                : $"{WialonUnitName} ({WialonUnitId.Value})"
            : string.IsNullOrWhiteSpace(WialonUnitName)
                ? "Pending"
                : WialonUnitName;
}
