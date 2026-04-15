using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models;

public static class JobCardWorkflowStatuses
{
    public const string Created = "Created";
    public const string CreatedWithWarnings = "Created With Warnings";
    public const string Amended = "Amended";
    public const string AwaitingInstallationPhotos = "Awaiting Installation Photos";
    public const string InstallationCompleted = "Installation Completed";
}

public class JobCardRecord
{
    public int JobCardRecordId { get; set; }

    public int SequenceNumber { get; set; }

    [Required]
    public string JobCardNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    [Required]
    public string WorkflowStatus { get; set; } = JobCardWorkflowStatuses.Created;

    public string? StatusNotes { get; set; }

    public DateTime? DetailsConfirmedAt { get; set; }

    public DateTime? LastAmendedAt { get; set; }

    public string? AmendmentNotes { get; set; }

    public DateTime? EvidenceReceivedAt { get; set; }

    public bool HasVehiclePhoto { get; set; }

    public bool HasRegistrationPhoto { get; set; }

    public bool HasVinPhoto { get; set; }

    public bool HasTrackingUnitPhoto { get; set; }

    public bool UseCustomBillingSystem { get; set; }

    public string? CustomBillingSystemName { get; set; }

    public string? SystemPriceExVat { get; set; }

    public bool HasPanicButton { get; set; }

    public string? PanicButtonPriceExVat { get; set; }

    public bool HasEarlyWarningSystem { get; set; }

    public string? EarlyWarningSystemPriceExVat { get; set; }

    public string? BleSensorQuantity { get; set; }

    public string? BleSensorUnitPriceExVat { get; set; }

    public bool HasLvCanAdaptor { get; set; }

    public string? LvCanAdaptorPriceExVat { get; set; }

    public string? OtherHardwareDescription { get; set; }

    public string? OtherHardwarePriceExVat { get; set; }

    public string? BillingNotes { get; set; }

    public long? WialonUnitId { get; set; }

    public string? WialonUnitName { get; set; }

    public long? WialonAccountId { get; set; }

    public string? WialonAccountName { get; set; }

    public long? WialonCreatorId { get; set; }

    public string? WialonCreatorName { get; set; }

    public long? WialonHardwareTypeId { get; set; }

    public string? WialonHardwareTypeName { get; set; }

    [Required]
    public string JobCardName { get; set; } = string.Empty;

    [Required]
    public string UniqueId { get; set; } = string.Empty;

    [Required]
    public string Iccid { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    [Required]
    public string Brand { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = string.Empty;

    [Required]
    public string Year { get; set; } = string.Empty;

    [Required]
    public string Colour { get; set; } = string.Empty;

    [Required]
    public string VehicleClass { get; set; } = string.Empty;

    [Required]
    public string VehicleType { get; set; } = string.Empty;

    [Required]
    public string RegistrationPlate { get; set; } = string.Empty;

    [Required]
    public string Vin { get; set; } = string.Empty;

    [Required]
    public string Client { get; set; } = string.Empty;

    [Required]
    public string Contact1 { get; set; } = string.Empty;

    [Required]
    public string Contact2 { get; set; } = string.Empty;

    [Required]
    public string MakeAndModel { get; set; } = string.Empty;

    [Required]
    public string RegistrationFleet { get; set; } = string.Empty;
}
