using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models;

public class InventoryTrackingUnit
{
    public int InventoryTrackingUnitId { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;

    [Required]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    public string ImeiNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public int? ReceivedInventoryTransactionId { get; set; }

    public InventoryTransaction? ReceivedInventoryTransaction { get; set; }

    public bool IsIssued { get; set; }

    public DateTime? IssuedAt { get; set; }

    public int? IssuedInventoryTransactionId { get; set; }

    public InventoryTransaction? IssuedInventoryTransaction { get; set; }

    public int? IssuedJobCardRecordId { get; set; }

    public string? IssuedJobCardNumber { get; set; }
}
