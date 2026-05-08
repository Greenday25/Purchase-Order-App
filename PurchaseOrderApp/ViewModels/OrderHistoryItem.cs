namespace PurchaseOrderApp.ViewModels
{
    public sealed class OrderHistoryItem
    {
        public static string GetOrderStatus(DateTime? managerApprovedAt, DateTime? directorApprovedAt, DateTime? rejectedAt, string? signedOrderFileName, string? invoiceFileName)
        {
            return rejectedAt.HasValue ? "Order Rejected" :
                !string.IsNullOrWhiteSpace(invoiceFileName) ? "Order Completed" :
                (managerApprovedAt.HasValue && directorApprovedAt.HasValue) || !string.IsNullOrWhiteSpace(signedOrderFileName) ? "Pending Invoice" :
                managerApprovedAt.HasValue ? "Pending Director Approval" :
                "Pending Manager Approval";
        }

        public int PurchaseOrderId { get; init; }
        public string OrderNumber { get; init; } = string.Empty;
        public DateTime Date { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public string BillTo { get; init; } = string.Empty;
        public string Reference { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public DateTime? ManagerApprovedAt { get; init; }
        public DateTime? DirectorApprovedAt { get; init; }
        public DateTime? SupplierCopySentAt { get; init; }
        public DateTime? RejectedAt { get; init; }
        public string? SignedOrderFileName { get; init; }
        public string? InvoiceFileName { get; init; }

        public string OrderStatus => GetOrderStatus(ManagerApprovedAt, DirectorApprovedAt, RejectedAt, SignedOrderFileName, InvoiceFileName);

        public string ApprovalStatus => IsApproved ? FormatStatus(DirectorApprovedAt ?? ManagerApprovedAt, "Pending") : OrderStatus;
        public string RejectionStatus => FormatStatus(RejectedAt, "Active");
        public string ManagerApprovalStatus => FormatStatus(ManagerApprovedAt, "Pending");
        public string DirectorApprovalStatus => FormatStatus(DirectorApprovedAt, "Pending");
        public string SupplierCopyStatus => FormatStatus(SupplierCopySentAt, "Pending");
        public string SignedOrderStatus => string.IsNullOrWhiteSpace(SignedOrderFileName) ? "Not Uploaded" : SignedOrderFileName;
        public string InvoiceStatus => string.IsNullOrWhiteSpace(InvoiceFileName) ? "Not Uploaded" : InvoiceFileName;
        public bool IsCompleted => string.Equals(OrderStatus, "Order Completed", StringComparison.OrdinalIgnoreCase);
        public bool IsManagerApproved => ManagerApprovedAt.HasValue || !string.IsNullOrWhiteSpace(SignedOrderFileName);
        public bool IsDirectorApproved => DirectorApprovedAt.HasValue || !string.IsNullOrWhiteSpace(SignedOrderFileName);
        public bool IsApproved => (ManagerApprovedAt.HasValue && DirectorApprovedAt.HasValue) || !string.IsNullOrWhiteSpace(SignedOrderFileName);
        public bool IsRejected => RejectedAt.HasValue;

        private static string FormatStatus(DateTime? value, string fallback)
        {
            return value.HasValue ? value.Value.ToString("dd/MM/yyyy HH:mm") : fallback;
        }
    }
}
