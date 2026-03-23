using System.Collections.ObjectModel;

namespace PurchaseOrderApp.ViewModels
{
    public sealed class OrderDetailsViewModel
    {
        public int PurchaseOrderId { get; init; }
        public string OrderNumber { get; init; } = string.Empty;
        public DateTime Date { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public string BillTo { get; init; } = string.Empty;
        public string BillToAddress { get; init; } = string.Empty;
        public string Reference { get; init; } = string.Empty;
        public string OrderStatus { get; init; } = string.Empty;
        public string ApprovalStatus { get; init; } = "Pending";
        public string RejectionStatus { get; init; } = "Active";
        public decimal TotalAmount { get; init; }
        public ObservableCollection<OrderDetailsLineItem> Lines { get; init; } = [];
        public string SignedOrderFileName { get; init; } = "Not Uploaded";
        public string InvoiceFileName { get; init; } = "Not Uploaded";
        public bool IsApproved { get; init; }
        public bool IsRejected { get; init; }
        public bool IsCompleted { get; init; }
        public bool HasSignedOrder => !string.Equals(SignedOrderFileName, "Not Uploaded", StringComparison.OrdinalIgnoreCase);
        public bool HasInvoice => !string.Equals(InvoiceFileName, "Not Uploaded", StringComparison.OrdinalIgnoreCase);
        public bool CanDelete => !IsCompleted;
    }
}
