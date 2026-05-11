using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Data;
using PurchaseOrderApp.Models;

namespace PurchaseOrderApp.Services;

internal sealed class PurchaseOrderApprovalNotificationService
{
    private readonly int signedInUserId;
    private readonly bool canReceiveManagerApprovals;
    private readonly bool canReceiveExecutiveApprovals;
    private readonly HashSet<int> knownManagerApprovalOrderIds = [];
    private readonly HashSet<int> knownExecutiveApprovalOrderIds = [];
    private readonly HashSet<string> knownOverdueApprovalKeys = [];

    public PurchaseOrderApprovalNotificationService(AppUser signedInUser)
    {
        signedInUserId = signedInUser.AppUserId;
        canReceiveManagerApprovals = signedInUser.Role?.CanManagerApprovePurchaseOrders == true;
        canReceiveExecutiveApprovals = signedInUser.Role?.CanApprovePurchaseOrders == true;
    }

    public async Task InitializeAsync()
    {
        knownManagerApprovalOrderIds.Clear();
        knownExecutiveApprovalOrderIds.Clear();
        knownOverdueApprovalKeys.Clear();

        foreach (var notification in await GetPendingManagerApprovalNotificationsAsync())
        {
            knownManagerApprovalOrderIds.Add(notification.PurchaseOrderId);
        }

        foreach (var notification in await GetPendingExecutiveApprovalNotificationsAsync())
        {
            knownExecutiveApprovalOrderIds.Add(notification.PurchaseOrderId);
        }
    }

    public async Task<IReadOnlyList<ApprovalNotification>> GetNewNotificationsAsync()
    {
        var notifications = new List<ApprovalNotification>();

        foreach (var notification in await GetPendingManagerApprovalNotificationsAsync())
        {
            if (knownManagerApprovalOrderIds.Add(notification.PurchaseOrderId))
            {
                notifications.Add(notification);
            }
        }

        foreach (var notification in await GetPendingExecutiveApprovalNotificationsAsync())
        {
            if (knownExecutiveApprovalOrderIds.Add(notification.PurchaseOrderId))
            {
                notifications.Add(notification);
            }
        }

        foreach (var notification in await GetOverdueApprovalNotificationsAsync())
        {
            if (notification.NotificationKey != null &&
                knownOverdueApprovalKeys.Add(notification.NotificationKey))
            {
                notifications.Add(notification);
            }
        }

        return notifications;
    }

    private async Task<List<ApprovalNotification>> GetPendingManagerApprovalNotificationsAsync()
    {
        if (!canReceiveManagerApprovals)
        {
            return [];
        }

        using var db = new PurchaseOrderContext();
        return await db.PurchaseOrders
            .AsNoTracking()
            .Where(order =>
                order.AssignedManagerAppUserId == signedInUserId &&
                !order.ManagerApprovedAt.HasValue &&
                !order.DirectorApprovedAt.HasValue &&
                !order.RejectedAt.HasValue &&
                string.IsNullOrWhiteSpace(order.InvoiceFileName))
            .OrderBy(order => order.PurchaseOrderId)
            .Select(order => new ApprovalNotification(
                order.PurchaseOrderId,
                "Manager approval required",
                $"Purchase order {order.OrderNumber} is ready for your approval."))
            .ToListAsync();
    }

    private async Task<List<ApprovalNotification>> GetPendingExecutiveApprovalNotificationsAsync()
    {
        if (!canReceiveExecutiveApprovals)
        {
            return [];
        }

        using var db = new PurchaseOrderContext();
        return await db.PurchaseOrders
            .AsNoTracking()
            .Where(order =>
                order.ManagerApprovedAt.HasValue &&
                !order.DirectorApprovedAt.HasValue &&
                !order.RejectedAt.HasValue &&
                string.IsNullOrWhiteSpace(order.InvoiceFileName))
            .OrderBy(order => order.PurchaseOrderId)
            .Select(order => new ApprovalNotification(
                order.PurchaseOrderId,
                "Executive approval required",
                $"Purchase order {order.OrderNumber} has manager approval and is ready for executive approval."))
            .ToListAsync();
    }

    private async Task<List<ApprovalNotification>> GetOverdueApprovalNotificationsAsync()
    {
        if (!canReceiveManagerApprovals && !canReceiveExecutiveApprovals)
        {
            return [];
        }

        using var db = new PurchaseOrderContext();
        var now = DateTime.Now;
        var pendingOrders = await db.PurchaseOrders
            .AsNoTracking()
            .Where(order =>
                !order.RejectedAt.HasValue &&
                string.IsNullOrWhiteSpace(order.InvoiceFileName) &&
                (!order.ManagerApprovedAt.HasValue || !order.DirectorApprovedAt.HasValue))
            .OrderBy(order => order.PurchaseOrderId)
            .Select(order => new
            {
                order.PurchaseOrderId,
                order.OrderNumber,
                order.Date,
                order.AssignedManagerAppUserId,
                order.ManagerApprovedAt,
                order.DirectorApprovedAt
            })
            .ToListAsync();

        var notifications = new List<ApprovalNotification>();
        foreach (var order in pendingOrders)
        {
            var ageHours = (now - order.Date).TotalHours;
            var threshold = ageHours >= 48 ? 48 : ageHours >= 24 ? 24 : 0;
            if (threshold == 0)
            {
                continue;
            }

            if (canReceiveManagerApprovals &&
                order.AssignedManagerAppUserId == signedInUserId &&
                !order.ManagerApprovedAt.HasValue)
            {
                notifications.Add(new ApprovalNotification(
                    order.PurchaseOrderId,
                    threshold == 48 ? "Purchase order approval overdue" : "Purchase order approval due",
                    $"Purchase order {order.OrderNumber} has been awaiting manager approval for over {threshold} hours.",
                    $"manager:{order.PurchaseOrderId}:{threshold}"));
            }

            if (canReceiveExecutiveApprovals &&
                order.ManagerApprovedAt.HasValue &&
                !order.DirectorApprovedAt.HasValue)
            {
                notifications.Add(new ApprovalNotification(
                    order.PurchaseOrderId,
                    threshold == 48 ? "Executive approval overdue" : "Executive approval due",
                    $"Purchase order {order.OrderNumber} has been awaiting executive approval and is over {threshold} hours old.",
                    $"executive:{order.PurchaseOrderId}:{threshold}"));
            }
        }

        return notifications;
    }
}

internal sealed record ApprovalNotification(int PurchaseOrderId, string Title, string Message, string? NotificationKey = null);
