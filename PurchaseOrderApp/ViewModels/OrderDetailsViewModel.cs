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
        public string CreatedByDisplayName { get; init; } = string.Empty;
        public string AssignedManagerDisplayName { get; init; } = string.Empty;
        public string OrderStatus { get; init; } = string.Empty;
        public string ApprovalStatus { get; init; } = "Pending";
        public string ManagerApprovalStatus { get; init; } = "Pending";
        public string DirectorApprovalStatus { get; init; } = "Pending";
        public string RejectionStatus { get; init; } = "Active";
        public decimal TotalAmount { get; init; }
        public ObservableCollection<OrderDetailsLineItem> Lines { get; init; } = [];
        public ObservableCollection<OrderReceiptItemViewModel> LinkedReceipts { get; init; } = [];
        public string SignedOrderFileName { get; init; } = "Not Uploaded";
        public string InvoiceFileName { get; init; } = "Not Uploaded";
        public bool IsApproved { get; init; }
        public bool IsManagerApproved { get; init; }
        public bool IsDirectorApproved { get; init; }
        public bool IsRejected { get; init; }
        public bool IsCompleted { get; init; }
        public bool CanManagerApprove { get; init; }
        public bool CanDirectorApprove { get; init; }
        public bool CanUploadInvoice { get; init; }
        public bool CanOpenInvoice { get; init; }
        public bool CanAmend { get; init; }
        public bool CanDelete { get; init; }
        public string DeleteRestrictionMessage { get; init; } = string.Empty;
        public int LinkedReceiptCount => LinkedReceipts.Count;
        public int LinkedReceiptQuantity => LinkedReceipts.Sum(item => item.Quantity);
        public bool HasLinkedReceipts => LinkedReceipts.Count > 0;
        public bool HasSignedOrder => !string.Equals(SignedOrderFileName, "Not Uploaded", StringComparison.OrdinalIgnoreCase);
        public bool HasInvoice => CanOpenInvoice;
    }
}
