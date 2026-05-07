namespace PurchaseOrderApp.ViewModels;

public sealed class JobCardInventoryOption
{
    public int JobCardRecordId { get; init; }

    public string JobCardNumber { get; init; } = string.Empty;

    public string VehicleDisplay { get; init; } = string.Empty;

    public string Client { get; init; } = string.Empty;

    public string WorkflowStatus { get; init; } = string.Empty;

    public string DisplayText => $"{JobCardNumber} | {VehicleDisplay} | {Client}";
}
