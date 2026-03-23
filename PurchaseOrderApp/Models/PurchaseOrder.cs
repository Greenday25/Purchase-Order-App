using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PurchaseOrderApp.Models
{
    public class PurchaseOrder
    {
        public int PurchaseOrderId { get; set; }

        [Required]
        public string OrderNumber { get; set; }

        public DateTime Date { get; set; }
        public string Reference { get; set; }

        public int VendorId { get; set; }
        public Vendor Vendor { get; set; }

        public string BillTo { get; set; }
        public string BillToAddress { get; set; }

        public decimal VATPercent { get; set; } = 15m;
        public DateTime? ManagerApprovedAt { get; set; }
        public DateTime? DirectorApprovedAt { get; set; }
        public DateTime? SupplierCopySentAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? SignedOrderFileName { get; set; }
        public byte[]? SignedOrderContent { get; set; }
        public string? InvoiceFileName { get; set; }
        public byte[]? InvoiceContent { get; set; }

        public IList<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();

        public decimal SubTotal => Math.Round(Lines == null ? 0 : (decimal) (Lines.Sum(x => x.Quantity * x.UnitPrice)), 2);
        public decimal VATAmount => Math.Round(SubTotal * VATPercent / 100, 2);
        public decimal Total => Math.Round(SubTotal + VATAmount, 2);
    }
}
