using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models
{
    public class PurchaseOrderLine
    {
        public int PurchaseOrderLineId { get; set; }
        public int PurchaseOrderId { get; set; }

        public decimal Quantity { get; set; } = 1;
        public string PartNumber { get; set; }
        public string Description { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;

        public PurchaseOrder PurchaseOrder { get; set; }
    }
}
