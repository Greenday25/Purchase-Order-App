using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models;

public static class InventoryTransactionTypes
{
    public const string StockIn = "Stock In";
    public const string StockOut = "Stock Out";

    public static readonly IReadOnlyList<string> All =
    [
        StockIn,
        StockOut
    ];
}

public class InventoryTransaction
{
    public int InventoryTransactionId { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;

    [Required]
    public string TransactionType { get; set; } = InventoryTransactionTypes.StockIn;

    public int Quantity { get; set; }

    public int QuantityAfterTransaction { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? IssueOutNumber { get; set; }

    [Required]
    public string ItemCodeSnapshot { get; set; } = string.Empty;

    [Required]
    public string ItemNameSnapshot { get; set; } = string.Empty;

    public string? CategorySnapshot { get; set; }

    public bool IsTrackingUnit { get; set; }

    public int? JobCardRecordId { get; set; }

    public string? JobCardNumber { get; set; }

    public string? Notes { get; set; }
}
