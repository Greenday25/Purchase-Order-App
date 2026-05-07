using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models;

public class InventoryReceiptLine
{
    public int InventoryReceiptLineId { get; set; }

    public int InventoryReceiptId { get; set; }

    public InventoryReceipt InventoryReceipt { get; set; } = null!;

    public int LineNumber { get; set; }

    [Required]
    public string ReceiptNumber { get; set; } = string.Empty;

    public int InventoryItemId { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;

    public int InventoryTransactionId { get; set; }

    public InventoryTransaction InventoryTransaction { get; set; } = null!;

    [Required]
    public string SupplierName { get; set; } = string.Empty;

    public int? PurchaseOrderId { get; set; }

    public string? PurchaseOrderNumber { get; set; }

    public int QuantityReceived { get; set; }

    [Required]
    public string ItemCodeSnapshot { get; set; } = string.Empty;

    [Required]
    public string ItemNameSnapshot { get; set; } = string.Empty;

    public string? CategorySnapshot { get; set; }

    public bool IsTrackingUnit { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}
