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
        public string CreatedByDisplayName { get; init; } = string.Empty;
        public string AssignedManagerDisplayName { get; init; } = string.Empty;
        public bool IsCreatedByCurrentUser { get; init; }
        public decimal TotalAmount { get; init; }
        public DateTime? ManagerApprovedAt { get; init; }
        public DateTime? DirectorApprovedAt { get; init; }
        public DateTime? SupplierCopySentAt { get; init; }
        public DateTime? RejectedAt { get; init; }
        public string? SignedOrderFileName { get; init; }
        public string? InvoiceFileName { get; init; }

        public string OrderStatus => GetOrderStatus(ManagerApprovedAt, DirectorApprovedAt, RejectedAt, SignedOrderFileName, InvoiceFileName);
        public bool IsPendingManagerApproval => string.Equals(OrderStatus, "Pending Manager Approval", StringComparison.OrdinalIgnoreCase);
        public bool IsPendingDirectorApproval => string.Equals(OrderStatus, "Pending Director Approval", StringComparison.OrdinalIgnoreCase);
        public bool IsPendingApproval => IsPendingManagerApproval || IsPendingDirectorApproval;
        public double ApprovalAgeHours => Math.Max(0, (DateTime.Now - Date).TotalHours);
        public int ApprovalUrgencyLevel => !IsPendingApproval ? 0 :
            ApprovalAgeHours >= 48 ? 2 :
            ApprovalAgeHours >= 24 ? 1 :
            0;
        public bool HasApprovalUrgency => ApprovalUrgencyLevel > 0;
        public string ApprovalUrgencyText => ApprovalUrgencyLevel switch
        {
            2 => "Over 48h",
            1 => "Over 24h",
            _ => "On track"
        };
        public string ApprovalUrgencyBackground => ApprovalUrgencyLevel switch
        {
            2 => "#FCEAEA",
            1 => "#FFF3D9",
            _ => "#EAF7F9"
        };
        public string ApprovalUrgencyBorder => ApprovalUrgencyLevel switch
        {
            2 => "#D86B6B",
            1 => "#E3A533",
            _ => "#9ED3DE"
        };
        public string ApprovalUrgencyForeground => ApprovalUrgencyLevel switch
        {
            2 => "#8C2F2F",
            1 => "#805300",
            _ => "#0D6980"
        };
        public string RowBackground => ApprovalUrgencyLevel switch
        {
            2 => "#FFF4F4",
            1 => "#FFF9EA",
            _ => "#FFFFFF"
        };
        public string RowAccent => ApprovalUrgencyLevel switch
        {
            2 => "#C84D4D",
            1 => "#D99112",
            _ => "#0D6980"
        };

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
        public bool CanDelete => IsCreatedByCurrentUser && !IsCompleted;
        public string DeleteRestrictionMessage => IsCompleted
            ? "Completed orders cannot be deleted."
            : IsCreatedByCurrentUser
                ? "Delete is available before completion."
                : "Only the user who created this purchase order can delete it.";

        private static string FormatStatus(DateTime? value, string fallback)
        {
            return value.HasValue ? value.Value.ToString("dd/MM/yyyy HH:mm") : fallback;
        }
    }
}
