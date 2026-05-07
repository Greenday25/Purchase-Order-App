using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models;

public class InventoryReceipt
{
    public int InventoryReceiptId { get; set; }

    [Required]
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? Notes { get; set; }

    public ICollection<InventoryReceiptLine> Lines { get; set; } = [];
}
