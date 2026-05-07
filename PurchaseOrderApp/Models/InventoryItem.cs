using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models;

public class InventoryItem
{
    public int InventoryItemId { get; set; }

    [Required]
    public string ItemCode { get; set; } = string.Empty;

    [Required]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsTrackingUnit { get; set; }

    public int QuantityOnHand { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<InventoryTransaction> Transactions { get; set; } = [];

    public ICollection<InventoryTrackingUnit> TrackingUnits { get; set; } = [];
}
