using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Data;
using PurchaseOrderApp.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PurchaseOrderApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public enum OrderHistoryScope
        {
            All,
            MyOrders,
            AwaitingManager,
            AwaitingExecutive,
            AwaitingInvoice,
            Completed
        }

        public sealed record StoredOrderDocument(string FileName, byte[] Content);
        private sealed record PurchaseOrderHistoryProjection(
            int PurchaseOrderId,
            string OrderNumber,
            DateTime Date,
            DateTime UpdatedAt,
            string CompanyName,
            string BillTo,
            string Reference,
            string CreatedByDisplayName,
            int? CreatedByAppUserId,
            string AssignedManagerDisplayName,
            int? AssignedManagerAppUserId,
            decimal TotalAmount,
            DateTime? ManagerApprovedAt,
            DateTime? DirectorApprovedAt,
            DateTime? SupplierCopySentAt,
            DateTime? RejectedAt,
            string? SignedOrderFileName,
            string? InvoiceFileName);

        private ObservableCollection<PurchaseOrderLine>? observedLines;
        private int? signedInUserAppUserId;
        private string signedInUserDisplayName = string.Empty;
        private string signedInUserRoleName = string.Empty;
        private DateTime? lastHistoryRefreshAt;
        private readonly Task initializationTask;

        private const string CapitalAirCompanyName = "CAPITAL AIR (Pty) Ltd";
        private const string SecurityOperationsCompanyName = "Capital Air Security Operations (Pty) Ltd";
        private const int OrderSequenceDigits = 5;
        private const int OrderSequenceStartingValue = 100;
        private const string ReactionServicesCompanyName = "Capital Air Reaction Services CC";
        private const string LegacyReactionServicesCompanyName = "Capital Air Reaction Services (Pty) Ltd";
        private static readonly Vendor[] DefaultCompanies =
        [
            new()
            {
                Name = CapitalAirCompanyName,
                Address = "P.O. BOX 18009, RAND AIRPORT 1419, GERMISTON, SOUTH AFRICA",
                Phone = "+27 11 827 0335 / 82 2634/2840",
                Email = "info@capitalair.com"
            },
            new()
            {
                Name = ReactionServicesCompanyName,
                Address = "P.O. BOX 18009, RAND AIRPORT 1419, GERMISTON, SOUTH AFRICA",
                Phone = "+27 11 827 0335 / 82 2634/2840",
                Email = "info@capitalair.com"
            },
            new()
            {
                Name = SecurityOperationsCompanyName,
                Address = "P.O. BOX 18009, RAND AIRPORT 1419, GERMISTON, SOUTH AFRICA",
                Phone = "+27 11 827 0335 / 82 2634/2840",
                Email = "info@capitalair.com"
            }
        ];

        public MainViewModel()
        {
            initializationTask = InitializeModelAsync();
        }

        public bool CanManagerApprovePurchaseOrders { get; private set; }

        public bool CanApprovePurchaseOrders { get; private set; }

        public bool CanCreatePurchaseOrders =>
            !string.Equals(signedInUserRoleName, "Manager", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(signedInUserRoleName, "Executive", StringComparison.OrdinalIgnoreCase);

        public bool IsExecutiveRole => CanApprovePurchaseOrders;

        public bool IsManagerRole => CanManagerApprovePurchaseOrders && !CanApprovePurchaseOrders;

        public bool ShowAwaitingManagerSummary =>
            !string.Equals(signedInUserRoleName, "Executive", StringComparison.OrdinalIgnoreCase);

        public int OrderHistorySummaryColumnCount => ShowAwaitingManagerSummary ? 6 : 5;

        public string RoleFocusedDashboardTitle => IsExecutiveRole
            ? "Executive Approval Dashboard"
            : IsManagerRole
                ? "Manager Approval Dashboard"
                : "Purchase Order Dashboard";

        public string RoleFocusedDashboardSubtitle => IsExecutiveRole
            ? "Review manager-approved orders and finish executive sign-off."
            : IsManagerRole
                ? "Review the orders assigned to you and complete manager approvals."
                : "Create and manage your purchase orders through to invoice completion.";

        public string ApprovalAccessStatus =>
            CanManagerApprovePurchaseOrders || CanApprovePurchaseOrders
                ? "Purchase order approval access enabled."
                : "Purchase order approval access required.";

        public async Task SetSignedInUserAsync(AppUser? user)
        {
            signedInUserAppUserId = user?.AppUserId;
            signedInUserDisplayName = user?.DisplayName ?? string.Empty;
            signedInUserRoleName = user?.Role?.Name ?? string.Empty;
            CanManagerApprovePurchaseOrders = user?.Role?.CanManagerApprovePurchaseOrders == true;
            CanApprovePurchaseOrders = user?.Role?.CanApprovePurchaseOrders == true;
            await initializationTask;
            await LoadManagerApproversAsync();
            OnPropertyChanged(nameof(CanManagerApprovePurchaseOrders));
            OnPropertyChanged(nameof(CanApprovePurchaseOrders));
            OnPropertyChanged(nameof(CanCreatePurchaseOrders));
            OnPropertyChanged(nameof(ApprovalAccessStatus));
            OnPropertyChanged(nameof(IsExecutiveRole));
            OnPropertyChanged(nameof(IsManagerRole));
            OnPropertyChanged(nameof(ShowAwaitingManagerSummary));
            OnPropertyChanged(nameof(OrderHistorySummaryColumnCount));
            OnPropertyChanged(nameof(RoleFocusedDashboardTitle));
            OnPropertyChanged(nameof(RoleFocusedDashboardSubtitle));
            SelectedHistoryScope = IsExecutiveRole
                ? OrderHistoryScope.AwaitingExecutive
                : IsManagerRole
                    ? OrderHistoryScope.AwaitingManager
                    : OrderHistoryScope.MyOrders;
            RefreshOrderNumber();
            lastHistoryRefreshAt = null;
            await LoadOrderHistoryAsync(forceFullRefresh: true);
        }

        public void CopyAccessContextFrom(MainViewModel source)
        {
            signedInUserAppUserId = source.signedInUserAppUserId;
            signedInUserDisplayName = source.signedInUserDisplayName;
            signedInUserRoleName = source.signedInUserRoleName;
            CanManagerApprovePurchaseOrders = source.CanManagerApprovePurchaseOrders;
            CanApprovePurchaseOrders = source.CanApprovePurchaseOrders;
            OnPropertyChanged(nameof(CanManagerApprovePurchaseOrders));
            OnPropertyChanged(nameof(CanApprovePurchaseOrders));
            OnPropertyChanged(nameof(CanCreatePurchaseOrders));
            OnPropertyChanged(nameof(ApprovalAccessStatus));
            OnPropertyChanged(nameof(IsExecutiveRole));
            OnPropertyChanged(nameof(IsManagerRole));
            OnPropertyChanged(nameof(ShowAwaitingManagerSummary));
            OnPropertyChanged(nameof(OrderHistorySummaryColumnCount));
            OnPropertyChanged(nameof(RoleFocusedDashboardTitle));
            OnPropertyChanged(nameof(RoleFocusedDashboardSubtitle));
        }

        private async Task InitializeModelAsync()
        {
            using var db = new PurchaseOrderContext();
            db.MigrateSafely();

            await EnsureDefaultCompaniesAsync(db);

            Vendors = new ObservableCollection<Vendor>(db.Vendors.ToList());
            SelectedVendor = Vendors.FirstOrDefault();

            CurrentOrder = new PurchaseOrder
            {
                OrderNumber = string.Empty,
                Date = DateTime.Today,
                Reference = string.Empty,
                BillTo = string.Empty,
                BillToAddress = "",
                IncludeVat = true,
                VATPercent = 15m,
                VendorId = Vendors[0]?.VendorId ?? 0,
                Vendor = Vendors[0],
                Lines = new ObservableCollection<PurchaseOrderLine>().ToList()
            };

            SetLinesCollection(new ObservableCollection<PurchaseOrderLine>(CurrentOrder.Lines));
            RefreshOrderNumber(db);
            await LoadOrderHistoryAsync();
        }

        private static async Task EnsureDefaultCompaniesAsync(PurchaseOrderContext db)
        {
            bool hasChanges = false;
            var legacyReactionVendor = db.Vendors.FirstOrDefault(v => v.Name == LegacyReactionServicesCompanyName);
            var currentReactionVendor = db.Vendors.FirstOrDefault(v => v.Name == ReactionServicesCompanyName);

            if (legacyReactionVendor != null)
            {
                if (currentReactionVendor == null)
                {
                    legacyReactionVendor.Name = ReactionServicesCompanyName;
                    currentReactionVendor = legacyReactionVendor;
                    hasChanges = true;
                }
                else if (legacyReactionVendor.VendorId != currentReactionVendor.VendorId)
                {
                    var legacyOrders = db.PurchaseOrders
                        .Where(order => order.VendorId == legacyReactionVendor.VendorId)
                        .ToList();

                    foreach (var order in legacyOrders)
                    {
                        order.VendorId = currentReactionVendor.VendorId;
                    }

                    db.Vendors.Remove(legacyReactionVendor);
                    hasChanges = true;
                }
            }

            foreach (var company in DefaultCompanies)
            {
                var existingVendor = db.Vendors.FirstOrDefault(v => v.Name == company.Name);
                if (existingVendor != null)
                {
                    if (existingVendor.Address != company.Address ||
                        existingVendor.Phone != company.Phone ||
                        existingVendor.Email != company.Email)
                    {
                        existingVendor.Address = company.Address;
                        existingVendor.Phone = company.Phone;
                        existingVendor.Email = company.Email;
                        hasChanges = true;
                    }

                    continue;
                }

                db.Vendors.Add(new Vendor
                {
                    Name = company.Name,
                    Address = company.Address,
                    Phone = company.Phone,
                    Email = company.Email
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                await db.SaveChangesAsync();
            }
        }

        [ObservableProperty]
        private ObservableCollection<Vendor> vendors;

        [ObservableProperty]
        private Vendor selectedVendor;

        [ObservableProperty]
        private ObservableCollection<AppUser> managerApprovers = [];

        [ObservableProperty]
        private AppUser? selectedManagerApprover;

        partial void OnSelectedManagerApproverChanged(AppUser? value)
        {
            if (CurrentOrder == null)
            {
                return;
            }

            CurrentOrder.AssignedManagerAppUserId = value?.AppUserId;
            CurrentOrder.AssignedManagerDisplayName = NormalizeText(value?.DisplayName);
        }

        partial void OnSelectedVendorChanged(Vendor value)
        {
            if (CurrentOrder != null && value != null)
            {
                CurrentOrder.Vendor = value;
                CurrentOrder.VendorId = value.VendorId;

                if (CurrentOrder.PurchaseOrderId <= 0 && !CurrentOrder.OrderNumberManuallyEdited)
                {
                    RefreshOrderNumber();
                }
            }
        }

        [ObservableProperty]
        private PurchaseOrder currentOrder;

        partial void OnCurrentOrderChanged(PurchaseOrder value)
        {
            OnPropertyChanged(nameof(OrderNumber));
            OnPropertyChanged(nameof(IsOrderNumberReadOnly));
        }

        public string OrderNumber
        {
            get => CurrentOrder?.OrderNumber ?? string.Empty;
            set
            {
                if (CurrentOrder == null)
                {
                    return;
                }

                value ??= string.Empty;

                if (string.Equals(CurrentOrder.OrderNumber, value, StringComparison.Ordinal))
                {
                    return;
                }

                CurrentOrder.OrderNumber = value;
                CurrentOrder.OrderNumberManuallyEdited = !string.IsNullOrWhiteSpace(value);
                OnPropertyChanged(nameof(OrderNumber));
            }
        }

        public bool IsOrderNumberReadOnly => CurrentOrder?.PurchaseOrderId > 0;

        [ObservableProperty]
        private ObservableCollection<PurchaseOrderLine> lines;

        [ObservableProperty]
        private decimal subTotal;

        [ObservableProperty]
        private decimal vatAmount;

        [ObservableProperty]
        private decimal totalAmount;

        public string VatDisplayLabel => (CurrentOrder?.IncludeVat ?? true)
            ? $"VAT ({(CurrentOrder?.VATPercent ?? 0m):0.##}%)"
            : "VAT Not Included";

        [ObservableProperty]
        private ObservableCollection<OrderHistoryItem> orderHistory = [];

        [ObservableProperty]
        private ObservableCollection<OrderHistoryItem> filteredOrderHistory = [];

        [ObservableProperty]
        private string lastSaveError = string.Empty;

        [ObservableProperty]
        private OrderHistoryItem? selectedOrderHistoryItem;

        [ObservableProperty]
        private string historySearchText = string.Empty;

        [ObservableProperty]
        private int totalOrderCount;

        [ObservableProperty]
        private int activeOrderCount;

        [ObservableProperty]
        private int completedOrderCount;

        [ObservableProperty]
        private int rejectedOrderCount;

        [ObservableProperty]
        private int visibleOrderCount;

        [ObservableProperty]
        private int myOrderCount;

        [ObservableProperty]
        private int awaitingManagerCount;

        [ObservableProperty]
        private int awaitingExecutiveCount;

        [ObservableProperty]
        private int awaitingInvoiceCount;

        [ObservableProperty]
        private OrderHistoryScope selectedHistoryScope = OrderHistoryScope.All;

        public bool HasVisibleOrders => FilteredOrderHistory.Count > 0;

        public bool HasNoVisibleOrders => !HasVisibleOrders;

        public string EmptyHistoryMessage => SelectedHistoryScope switch
        {
            OrderHistoryScope.MyOrders => "No purchase orders created by you yet.",
            OrderHistoryScope.AwaitingManager => "No purchase orders are awaiting manager approval.",
            OrderHistoryScope.AwaitingExecutive => "No purchase orders are awaiting executive approval.",
            OrderHistoryScope.AwaitingInvoice => "No approved purchase orders are awaiting invoice upload.",
            OrderHistoryScope.Completed => "No completed purchase orders yet.",
            _ => "No purchase orders match the current filters."
        };

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new PurchaseOrderLine { PartNumber = "", Description = "" });
            RefreshTotals();
        }

        [RelayCommand]
        private void RemoveLine(PurchaseOrderLine line)
        {
            if (line == null) return;
            Lines.Remove(line);
            RefreshTotals();
        }

        [RelayCommand]
        public void UpdateTotals()
        {
            RefreshTotals();
        }

        [RelayCommand]
        public async Task RefreshHistoryAsync()
        {
            await LoadOrderHistoryAsync();
        }

        public async Task<bool> MarkManagerApprovedAsync(int orderId)
        {
            if (!CanManagerApprovePurchaseOrders)
            {
                return false;
            }

            var didUpdate = false;
            return await UpdateOrderWorkflowAsync(orderId, order =>
            {
                if (!CanCurrentUserManagerApprove(order) ||
                    order.ManagerApprovedAt.HasValue ||
                    order.RejectedAt.HasValue ||
                    !string.IsNullOrWhiteSpace(order.InvoiceFileName))
                {
                    return;
                }

                order.ManagerApprovedAt = DateTime.Now;
                order.ManagerApprovedByAppUserId = signedInUserAppUserId;
                order.ManagerApprovedByDisplayName = NormalizeText(signedInUserDisplayName);
                didUpdate = true;
            }) && didUpdate;
        }

        public async Task<bool> MarkDirectorApprovedAsync(int orderId)
        {
            if (!CanApprovePurchaseOrders)
            {
                return false;
            }

            var didUpdate = false;
            return await UpdateOrderWorkflowAsync(orderId, order =>
            {
                if (!order.ManagerApprovedAt.HasValue ||
                    order.DirectorApprovedAt.HasValue ||
                    order.RejectedAt.HasValue ||
                    !string.IsNullOrWhiteSpace(order.InvoiceFileName))
                {
                    return;
                }

                order.DirectorApprovedAt = DateTime.Now;
                order.DirectorApprovedByAppUserId = signedInUserAppUserId;
                order.DirectorApprovedByDisplayName = NormalizeText(signedInUserDisplayName);
                didUpdate = true;
            }) && didUpdate;
        }

        public async Task<bool> MarkRejectedAsync(int orderId)
        {
            return await UpdateOrderWorkflowAsync(orderId, order => order.RejectedAt ??= DateTime.Now);
        }

        public async Task<bool> DeleteOrderAsync(int orderId)
        {
            using var db = new PurchaseOrderContext();

            var order = db.PurchaseOrders
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.PurchaseOrderId == orderId);

            if (order == null || !CanDeleteOrder(order))
            {
                return false;
            }

            db.PurchaseOrders.Remove(order);
            await db.SaveChangesAsync();
            await LoadOrderHistoryAsync(db, forceFullRefresh: true);
            return true;
        }

        public async Task<bool> SaveSignedOrderDocumentAsync(int orderId, string fileName, byte[] content)
        {
            return await SaveOrderDocumentAsync(orderId, fileName, content, isInvoice: false);
        }

        public async Task<bool> SaveInvoiceDocumentAsync(int orderId, string fileName, byte[] content)
        {
            return await SaveOrderDocumentAsync(orderId, fileName, content, isInvoice: true);
        }

        public StoredOrderDocument? GetSignedOrderDocument(int orderId)
        {
            return GetOrderDocument(orderId, isInvoice: false);
        }

        public StoredOrderDocument? GetInvoiceDocument(int orderId)
        {
            return GetOrderDocument(orderId, isInvoice: true);
        }

        public OrderDetailsViewModel? GetOrderDetails(int orderId)
        {
            using var db = new PurchaseOrderContext();
            var inventoryService = new InventoryService();

            var order = db.PurchaseOrders
                .AsNoTracking()
                .Include(item => item.Vendor)
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.PurchaseOrderId == orderId);

            if (order == null || !CanCurrentUserSeeOrder(order))
            {
                return null;
            }

            var linkedReceipts = inventoryService.GetPurchaseOrderReceiptLines(orderId)
                .Select(receipt => new OrderReceiptItemViewModel
                {
                    ReceiptNumber = receipt.ReceiptNumber,
                    ReceivedAt = receipt.ReceivedAt,
                    SupplierName = receipt.SupplierName,
                    ItemCode = receipt.ItemCode,
                    ItemName = receipt.ItemName,
                    Quantity = receipt.Quantity,
                    Notes = receipt.Notes ?? string.Empty
                })
                .ToList();

            var canOpenInvoice = CanCurrentUserOpenInvoice(order);

            return new OrderDetailsViewModel
            {
                PurchaseOrderId = order.PurchaseOrderId,
                OrderNumber = order.OrderNumber,
                Date = order.Date,
                CompanyName = order.Vendor?.Name ?? string.Empty,
                BillTo = order.BillTo,
                BillToAddress = order.BillToAddress,
                Reference = order.Reference,
                CreatedByDisplayName = order.CreatedByDisplayName ?? string.Empty,
                AssignedManagerDisplayName = order.AssignedManagerDisplayName ?? string.Empty,
                OrderStatus = OrderHistoryItem.GetOrderStatus(
                    order.ManagerApprovedAt,
                    order.DirectorApprovedAt,
                    order.RejectedAt,
                    order.SignedOrderFileName,
                    order.InvoiceFileName),
                ApprovalStatus = FormatOverallApprovalStatus(order.ManagerApprovedAt, order.DirectorApprovedAt),
                ManagerApprovalStatus = FormatWorkflowStatus(order.ManagerApprovedAt, "Pending"),
                DirectorApprovalStatus = FormatWorkflowStatus(order.DirectorApprovedAt, "Pending"),
                RejectionStatus = FormatWorkflowStatus(order.RejectedAt, "Active"),
                TotalAmount = order.Total,
                SignedOrderFileName = string.IsNullOrWhiteSpace(order.SignedOrderFileName) ? "Not Uploaded" : order.SignedOrderFileName,
                InvoiceFileName = !HasInvoice(order)
                    ? "Not Uploaded"
                    : canOpenInvoice
                        ? order.InvoiceFileName!
                        : "Restricted",
                IsApproved = HasFullApproval(order),
                IsManagerApproved = order.ManagerApprovedAt.HasValue || !string.IsNullOrWhiteSpace(order.SignedOrderFileName),
                IsDirectorApproved = order.DirectorApprovedAt.HasValue || !string.IsNullOrWhiteSpace(order.SignedOrderFileName),
                IsRejected = order.RejectedAt.HasValue,
                IsCompleted = !string.IsNullOrWhiteSpace(order.InvoiceFileName),
                CanManagerApprove = CanCurrentUserManagerApprove(order) &&
                    !order.ManagerApprovedAt.HasValue &&
                    !order.RejectedAt.HasValue &&
                    string.IsNullOrWhiteSpace(order.InvoiceFileName),
                CanDirectorApprove = CanApprovePurchaseOrders &&
                    order.ManagerApprovedAt.HasValue &&
                    !order.DirectorApprovedAt.HasValue &&
                    !order.RejectedAt.HasValue &&
                    string.IsNullOrWhiteSpace(order.InvoiceFileName),
                CanUploadInvoice = CanCurrentUserUploadInvoice(order),
                CanOpenInvoice = canOpenInvoice,
                CanAmend = CanAmendOrder(order),
                CanDelete = CanDeleteOrder(order),
                DeleteRestrictionMessage = GetDeleteRestrictionMessage(order),
                LinkedReceipts = new ObservableCollection<OrderReceiptItemViewModel>(linkedReceipts),
                Lines = new ObservableCollection<OrderDetailsLineItem>(
                    order.Lines.Select(line => new OrderDetailsLineItem
                    {
                        Quantity = line.Quantity,
                        PartNumber = line.PartNumber ?? string.Empty,
                        Description = line.Description ?? string.Empty,
                        UnitPrice = line.UnitPrice
                    }))
            };
        }

        public bool LoadExistingOrder(int orderId, bool skipAccessCheck = false)
        {
            using var db = new PurchaseOrderContext();

            var order = db.PurchaseOrders
                .AsNoTracking()
                .Include(item => item.Vendor)
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.PurchaseOrderId == orderId);

            if (order == null || (!skipAccessCheck && !CanCurrentUserSeeOrder(order)))
            {
                return false;
            }

            Vendors = new ObservableCollection<Vendor>(db.Vendors.AsNoTracking().ToList());
            var selectedVendor = Vendors.FirstOrDefault(item => item.VendorId == order.VendorId)
                ?? Vendors.FirstOrDefault(item => string.Equals(item.Name, order.Vendor?.Name, StringComparison.OrdinalIgnoreCase))
                ?? order.Vendor;

            var copiedLines = order.Lines
                .Select(line => new PurchaseOrderLine
                {
                    PurchaseOrderLineId = line.PurchaseOrderLineId,
                    PurchaseOrderId = line.PurchaseOrderId,
                    Quantity = line.Quantity,
                    PartNumber = line.PartNumber ?? string.Empty,
                    Description = line.Description ?? string.Empty,
                    UnitPrice = line.UnitPrice
                })
                .ToList();

            CurrentOrder = new PurchaseOrder
            {
                PurchaseOrderId = order.PurchaseOrderId,
                OrderNumber = order.OrderNumber,
                OrderNumberManuallyEdited = order.OrderNumberManuallyEdited,
                UpdatedAt = order.UpdatedAt,
                AssignedManagerAppUserId = order.AssignedManagerAppUserId,
                AssignedManagerDisplayName = order.AssignedManagerDisplayName,
                Date = order.Date,
                Reference = order.Reference,
                VendorId = selectedVendor?.VendorId ?? order.VendorId,
                Vendor = selectedVendor ?? order.Vendor,
                BillTo = order.BillTo,
                BillToAddress = order.BillToAddress,
                IncludeVat = order.IncludeVat,
                VATPercent = order.VATPercent,
                ManagerApprovedAt = order.ManagerApprovedAt,
                ManagerApprovedByAppUserId = order.ManagerApprovedByAppUserId,
                ManagerApprovedByDisplayName = order.ManagerApprovedByDisplayName,
                DirectorApprovedAt = order.DirectorApprovedAt,
                DirectorApprovedByAppUserId = order.DirectorApprovedByAppUserId,
                DirectorApprovedByDisplayName = order.DirectorApprovedByDisplayName,
                SupplierCopySentAt = order.SupplierCopySentAt,
                RejectedAt = order.RejectedAt,
                SignedOrderFileName = order.SignedOrderFileName,
                SignedOrderContent = order.SignedOrderContent,
                InvoiceFileName = order.InvoiceFileName,
                InvoiceUploadedByAppUserId = order.InvoiceUploadedByAppUserId,
                InvoiceUploadedByDisplayName = order.InvoiceUploadedByDisplayName,
                InvoiceUploadedAt = order.InvoiceUploadedAt,
                InvoiceContent = order.InvoiceContent,
                Lines = copiedLines
            };

            SelectedManagerApprover = ManagerApprovers.FirstOrDefault(manager => manager.AppUserId == order.AssignedManagerAppUserId);
            SelectedVendor = selectedVendor ?? Vendors.FirstOrDefault();
            SetLinesCollection(new ObservableCollection<PurchaseOrderLine>(copiedLines));
            RefreshTotals();
            return true;
        }

        [RelayCommand]
        public async Task SavePurchaseOrderAsync()
        {
            await TrySavePurchaseOrderAsync();
        }

        public async Task<bool> TrySavePurchaseOrderAsync()
        {
            RefreshTotals();
            LastSaveError = string.Empty;

            using var db = new PurchaseOrderContext();

            if (!ValidatePurchaseOrderForSave(db))
            {
                return false;
            }

            if (CurrentOrder.PurchaseOrderId > 0)
            {
                return await UpdateExistingPurchaseOrderAsync(db);
            }

            if (!CanCreatePurchaseOrders)
            {
                LastSaveError = "Managers and executives cannot create purchase orders.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(CurrentOrder.OrderNumber))
            {
                RefreshOrderNumber(db);
            }

            CurrentOrder.OrderNumber = (CurrentOrder.OrderNumber ?? string.Empty).Trim();
            if (IsDuplicateOrderNumber(db, CurrentOrder.OrderNumber, CurrentOrder.PurchaseOrderId))
            {
                LastSaveError = $"Order number {CurrentOrder.OrderNumber} already exists. Duplicate order numbers are not allowed.";
                return false;
            }

            CurrentOrder.Vendor = db.Vendors.Find(CurrentOrder.VendorId) ?? CurrentOrder.Vendor;

            var order = new PurchaseOrder
            {
                OrderNumber = CurrentOrder.OrderNumber,
                OrderNumberManuallyEdited = CurrentOrder.OrderNumberManuallyEdited,
                UpdatedAt = DateTime.Now,
                CreatedByAppUserId = signedInUserAppUserId,
                CreatedByDisplayName = NormalizeText(signedInUserDisplayName),
                AssignedManagerAppUserId = CurrentOrder.AssignedManagerAppUserId,
                AssignedManagerDisplayName = NormalizeText(CurrentOrder.AssignedManagerDisplayName),
                Date = CurrentOrder.Date,
                Reference = CurrentOrder.Reference,
                BillTo = CurrentOrder.BillTo,
                BillToAddress = CurrentOrder.BillToAddress,
                IncludeVat = CurrentOrder.IncludeVat,
                VATPercent = CurrentOrder.VATPercent,
                VendorId = CurrentOrder.VendorId,
                Lines = Lines.Select(l => new PurchaseOrderLine
                {
                    Quantity = l.Quantity,
                    PartNumber = l.PartNumber,
                    Description = l.Description,
                    UnitPrice = l.UnitPrice
                }).ToList()
            };

            db.PurchaseOrders.Add(order);
            await db.SaveChangesAsync();
            OrderArchiveService.TrySyncOrderFolder(order.PurchaseOrderId);
            await LoadOrderHistoryAsync(db, forceFullRefresh: true);
            CurrentOrder.OrderNumberManuallyEdited = false;
            RefreshOrderNumber(db);
            return true;
        }

        public bool ValidatePurchaseOrderForSave()
        {
            LastSaveError = string.Empty;

            using var db = new PurchaseOrderContext();
            return ValidatePurchaseOrderForSave(db);
        }

        private bool ValidatePurchaseOrderForSave(PurchaseOrderContext db)
        {
            if (CurrentOrder == null)
            {
                LastSaveError = "There is no purchase order loaded to save.";
                return false;
            }

            CurrentOrder.Reference = (CurrentOrder.Reference ?? string.Empty).Trim();
            CurrentOrder.BillTo = (CurrentOrder.BillTo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(CurrentOrder.Reference))
            {
                LastSaveError = "Reference is required before saving this purchase order.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(CurrentOrder.BillTo))
            {
                LastSaveError = "Bill To is required before saving this purchase order.";
                return false;
            }

            if (!CurrentOrder.AssignedManagerAppUserId.HasValue)
            {
                LastSaveError = "Assign a manager to approve this purchase order before saving.";
                return false;
            }

            if (CurrentOrder.PurchaseOrderId > 0)
            {
                var existingOrder = db.PurchaseOrders
                    .AsNoTracking()
                    .FirstOrDefault(item => item.PurchaseOrderId == CurrentOrder.PurchaseOrderId);

                if (existingOrder == null || !CanAmendOrder(existingOrder))
                {
                    LastSaveError = "Approved, rejected, signed, or completed orders cannot be amended.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(CurrentOrder.OrderNumber))
            {
                RefreshOrderNumber(db);
            }

            CurrentOrder.OrderNumber = (CurrentOrder.OrderNumber ?? string.Empty).Trim();
            if (IsDuplicateOrderNumber(db, CurrentOrder.OrderNumber, CurrentOrder.PurchaseOrderId))
            {
                LastSaveError = $"Order number {CurrentOrder.OrderNumber} already exists. Duplicate order numbers are not allowed.";
                return false;
            }

            return true;
        }

        private async Task<bool> UpdateExistingPurchaseOrderAsync(PurchaseOrderContext db)
        {
            var order = db.PurchaseOrders
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.PurchaseOrderId == CurrentOrder.PurchaseOrderId);

            if (order == null || !CanAmendOrder(order))
            {
                LastSaveError = "Approved, rejected, signed, or completed orders cannot be amended.";
                return false;
            }

            CurrentOrder.OrderNumber = (CurrentOrder.OrderNumber ?? string.Empty).Trim();
            if (IsDuplicateOrderNumber(db, CurrentOrder.OrderNumber, CurrentOrder.PurchaseOrderId))
            {
                LastSaveError = $"Order number {CurrentOrder.OrderNumber} already exists. Duplicate order numbers are not allowed.";
                return false;
            }

            order.OrderNumber = CurrentOrder.OrderNumber;
            order.OrderNumberManuallyEdited = CurrentOrder.OrderNumberManuallyEdited;
            order.Date = CurrentOrder.Date;
            order.Reference = CurrentOrder.Reference;
            order.BillTo = CurrentOrder.BillTo;
            order.BillToAddress = CurrentOrder.BillToAddress;
            order.AssignedManagerAppUserId = CurrentOrder.AssignedManagerAppUserId;
            order.AssignedManagerDisplayName = NormalizeText(CurrentOrder.AssignedManagerDisplayName);
            order.IncludeVat = CurrentOrder.IncludeVat;
            order.VATPercent = CurrentOrder.VATPercent;
            order.VendorId = CurrentOrder.VendorId;
            order.UpdatedAt = DateTime.Now;

            db.PurchaseOrderLines.RemoveRange(order.Lines);
            order.Lines = Lines.Select(l => new PurchaseOrderLine
            {
                PurchaseOrderId = order.PurchaseOrderId,
                Quantity = l.Quantity,
                PartNumber = l.PartNumber,
                Description = l.Description,
                UnitPrice = l.UnitPrice
            }).ToList();

            await db.SaveChangesAsync();
            OrderArchiveService.TrySyncOrderFolder(order.PurchaseOrderId);
            await LoadOrderHistoryAsync(db, forceFullRefresh: true);
            return true;
        }

        private static bool CanAmendOrder(PurchaseOrder order)
        {
            return !order.ManagerApprovedAt.HasValue &&
                !order.DirectorApprovedAt.HasValue &&
                !order.RejectedAt.HasValue &&
                string.IsNullOrWhiteSpace(order.SignedOrderFileName) &&
                string.IsNullOrWhiteSpace(order.InvoiceFileName);
        }

        private bool CanDeleteOrder(PurchaseOrder order)
        {
            return !IsCompletedOrder(order) && IsOrderCreatedBySignedInUser(order);
        }

        private bool CanCurrentUserManagerApprove(PurchaseOrder order)
        {
            if (!CanManagerApprovePurchaseOrders)
            {
                return false;
            }

            if (CanApprovePurchaseOrders)
            {
                return true;
            }

            return signedInUserAppUserId.HasValue &&
                order.AssignedManagerAppUserId == signedInUserAppUserId.Value;
        }

        private bool CanCurrentUserSeeOrder(PurchaseOrder order)
        {
            if (CanApprovePurchaseOrders)
            {
                return true;
            }

            if (CanManagerApprovePurchaseOrders)
            {
                return signedInUserAppUserId.HasValue &&
                    order.AssignedManagerAppUserId == signedInUserAppUserId.Value;
            }

            return IsOrderCreatedBySignedInUser(order);
        }

        private string GetDeleteRestrictionMessage(PurchaseOrder order)
        {
            if (IsCompletedOrder(order))
            {
                return "Completed purchase orders cannot be deleted.";
            }

            return IsOrderCreatedBySignedInUser(order)
                ? "Delete is available because you created this purchase order."
                : "Only the user who created this purchase order can delete it.";
        }

        private bool IsOrderCreatedBySignedInUser(PurchaseOrder order)
        {
            return IsOrderCreatedBySignedInUser(order.CreatedByAppUserId, order.CreatedByDisplayName);
        }

        private bool IsOrderCreatedBySignedInUser(int? createdByAppUserId, string? createdByDisplayName)
        {
            if (signedInUserAppUserId.HasValue && createdByAppUserId == signedInUserAppUserId.Value)
            {
                return true;
            }

            return !createdByAppUserId.HasValue &&
                !string.IsNullOrWhiteSpace(createdByDisplayName) &&
                !string.IsNullOrWhiteSpace(signedInUserDisplayName) &&
                string.Equals(createdByDisplayName.Trim(), signedInUserDisplayName.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCompletedOrder(PurchaseOrder order)
        {
            return order.InvoiceContent != null || !string.IsNullOrWhiteSpace(order.InvoiceFileName);
        }

        private bool CanCurrentUserUploadInvoice(PurchaseOrder order)
        {
            return CanApprovePurchaseOrders &&
                IsOrderCreatedBySignedInUser(order) &&
                HasFullApproval(order) &&
                !order.RejectedAt.HasValue &&
                !HasInvoice(order);
        }

        private bool CanCurrentUserOpenInvoice(PurchaseOrder order)
        {
            return HasInvoice(order) &&
                IsOrderCreatedBySignedInUser(order) &&
                IsInvoiceUploadedByOrderCreator(order);
        }

        private static bool HasInvoice(PurchaseOrder order)
        {
            return order.InvoiceContent is { Length: > 0 } &&
                !string.IsNullOrWhiteSpace(order.InvoiceFileName);
        }

        private static bool IsInvoiceUploadedByOrderCreator(PurchaseOrder order)
        {
            if (!HasInvoice(order))
            {
                return false;
            }

            if (!order.InvoiceUploadedByAppUserId.HasValue &&
                string.IsNullOrWhiteSpace(order.InvoiceUploadedByDisplayName))
            {
                return true;
            }

            if (order.CreatedByAppUserId.HasValue &&
                order.InvoiceUploadedByAppUserId == order.CreatedByAppUserId.Value)
            {
                return true;
            }

            return !order.CreatedByAppUserId.HasValue &&
                !string.IsNullOrWhiteSpace(order.CreatedByDisplayName) &&
                !string.IsNullOrWhiteSpace(order.InvoiceUploadedByDisplayName) &&
                string.Equals(
                    order.CreatedByDisplayName.Trim(),
                    order.InvoiceUploadedByDisplayName.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasFullApproval(PurchaseOrder order)
        {
            return (order.ManagerApprovedAt.HasValue && order.DirectorApprovedAt.HasValue) ||
                !string.IsNullOrWhiteSpace(order.SignedOrderFileName);
        }

        private static bool IsDuplicateOrderNumber(PurchaseOrderContext db, string? orderNumber, int currentOrderId)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                return false;
            }

            var normalizedOrderNumber = orderNumber.Trim();
            return db.PurchaseOrders
                .AsNoTracking()
                .Where(order => order.PurchaseOrderId != currentOrderId)
                .AsEnumerable()
                .Any(order => string.Equals(order.OrderNumber?.Trim(), normalizedOrderNumber, StringComparison.OrdinalIgnoreCase));
        }

        private static string? NormalizeText(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private void RefreshTotals()
        {
            SubTotal = Math.Round(Lines.Sum(l => l.LineTotal), 2);
            var appliedVatPercent = (CurrentOrder?.IncludeVat ?? true) ? (CurrentOrder?.VATPercent ?? 0m) : 0m;
            VatAmount = Math.Round(SubTotal * appliedVatPercent / 100m, 2);
            TotalAmount = Math.Round(SubTotal + VatAmount, 2);

            if (CurrentOrder != null)
            {
                CurrentOrder.Lines = Lines.ToList();
            }

            OnPropertyChanged(nameof(VatDisplayLabel));
        }

        public void RefreshOrderNumber()
        {
            using var db = new PurchaseOrderContext();
            RefreshOrderNumber(db);
        }

        public async Task LoadOrderHistoryAsync(bool forceFullRefresh = false)
        {
            using var db = new PurchaseOrderContext();
            await LoadOrderHistoryAsync(db, forceFullRefresh);
        }

        private async Task LoadManagerApproversAsync()
        {
            using var db = new PurchaseOrderContext();
            await LoadManagerApproversAsync(db);
        }

        private async Task LoadManagerApproversAsync(PurchaseOrderContext db)
        {
            var selectedManagerId = CurrentOrder?.AssignedManagerAppUserId ?? SelectedManagerApprover?.AppUserId;
            var managers = await db.AppUsers
                .AsNoTracking()
                .Include(user => user.Role)
                .Where(user => user.IsActive && user.Role.CanManagerApprovePurchaseOrders)
                .OrderBy(user => user.DisplayName)
                .ToListAsync();

            ManagerApprovers = new ObservableCollection<AppUser>(managers);
            SelectedManagerApprover = selectedManagerId.HasValue
                ? ManagerApprovers.FirstOrDefault(manager => manager.AppUserId == selectedManagerId.Value)
                : null;
        }

        private void RefreshOrderNumber(PurchaseOrderContext db)
        {
            if (CurrentOrder == null || CurrentOrder.PurchaseOrderId > 0)
            {
                return;
            }

            if (CurrentOrder.OrderNumberManuallyEdited && !string.IsNullOrWhiteSpace(CurrentOrder.OrderNumber))
            {
                return;
            }

            CurrentOrder.OrderNumber = GenerateNextOrderNumber(db);
            CurrentOrder.OrderNumberManuallyEdited = false;
            OnPropertyChanged(nameof(OrderNumber));
        }

        private async Task LoadOrderHistoryAsync(PurchaseOrderContext db, bool forceFullRefresh)
        {
            var selectedOrderId = SelectedOrderHistoryItem?.PurchaseOrderId;
            var refreshSince = forceFullRefresh ? null : lastHistoryRefreshAt;

            var query = db.PurchaseOrders
                .AsNoTracking()
                .Include(order => order.Vendor)
                .AsQueryable();

            if (!CanApprovePurchaseOrders)
            {
                if (CanManagerApprovePurchaseOrders && signedInUserAppUserId.HasValue)
                {
                    var managerId = signedInUserAppUserId.Value;
                    query = query.Where(order => order.AssignedManagerAppUserId == managerId);
                }
                else if (signedInUserAppUserId.HasValue)
                {
                    var creatorId = signedInUserAppUserId.Value;
                    query = query.Where(order => order.CreatedByAppUserId == creatorId);
                }
                else if (!string.IsNullOrWhiteSpace(signedInUserDisplayName))
                {
                    var creatorName = signedInUserDisplayName.Trim();
                    query = query.Where(order => order.CreatedByDisplayName == creatorName);
                }
                else
                {
                    query = query.Where(_ => false);
                }
            }
            if (refreshSince.HasValue)
            {
                query = query.Where(order => order.UpdatedAt > refreshSince.Value);
            }

            var changedOrders = await query
                .OrderByDescending(order => order.PurchaseOrderId)
                .Select(order => new PurchaseOrderHistoryProjection(
                    order.PurchaseOrderId,
                    order.OrderNumber,
                    order.Date,
                    order.UpdatedAt,
                    order.Vendor != null ? order.Vendor.Name : string.Empty,
                    order.BillTo,
                    order.Reference,
                    order.CreatedByDisplayName ?? string.Empty,
                    order.CreatedByAppUserId,
                    order.AssignedManagerDisplayName ?? string.Empty,
                    order.AssignedManagerAppUserId,
                    order.IncludeVat
                        ? Math.Round(
                            Math.Round(order.Lines.Sum(line => line.Quantity * line.UnitPrice), 2) +
                            Math.Round(Math.Round(order.Lines.Sum(line => line.Quantity * line.UnitPrice), 2) * order.VATPercent / 100m, 2),
                            2)
                        : Math.Round(order.Lines.Sum(line => line.Quantity * line.UnitPrice), 2),
                    order.ManagerApprovedAt,
                    order.DirectorApprovedAt,
                    order.SupplierCopySentAt,
                    order.RejectedAt,
                    order.SignedOrderFileName,
                    order.InvoiceFileName))
                .Take(200)
                .ToListAsync();

            if (!refreshSince.HasValue)
            {
                OrderHistory = new ObservableCollection<OrderHistoryItem>(
                    changedOrders.Select(MapHistoryItem));
            }
            else if (changedOrders.Count > 0)
            {
                var byId = OrderHistory.ToDictionary(item => item.PurchaseOrderId);
                foreach (var changed in changedOrders)
                {
                    var mapped = MapHistoryItem(changed);
                    byId[mapped.PurchaseOrderId] = mapped;
                }

                OrderHistory = new ObservableCollection<OrderHistoryItem>(
                    byId.Values
                        .OrderByDescending(item => item.PurchaseOrderId)
                        .Take(200));
            }

            lastHistoryRefreshAt = DateTime.Now;
            UpdateHistorySummary(OrderHistory);
            ApplyHistoryFilter(selectedOrderId);
        }

        partial void OnHistorySearchTextChanged(string value)
        {
            ApplyHistoryFilter(SelectedOrderHistoryItem?.PurchaseOrderId);
        }

        partial void OnSelectedHistoryScopeChanged(OrderHistoryScope value)
        {
            ApplyHistoryFilter(SelectedOrderHistoryItem?.PurchaseOrderId);
        }

        partial void OnFilteredOrderHistoryChanged(ObservableCollection<OrderHistoryItem> value)
        {
            OnPropertyChanged(nameof(HasVisibleOrders));
            OnPropertyChanged(nameof(HasNoVisibleOrders));
            OnPropertyChanged(nameof(EmptyHistoryMessage));
        }

        private async Task<bool> UpdateOrderWorkflowAsync(int orderId, Action<PurchaseOrder> updateAction)
        {
            using var db = new PurchaseOrderContext();

            var order = await db.PurchaseOrders.FirstOrDefaultAsync(item => item.PurchaseOrderId == orderId);
            if (order == null)
            {
                return false;
            }

            updateAction(order);
            if (db.Entry(order).State == EntityState.Unchanged)
            {
                return false;
            }

            order.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            await LoadOrderHistoryAsync(db, forceFullRefresh: false);
            return true;
        }

        private async Task<bool> SaveOrderDocumentAsync(int orderId, string fileName, byte[] content, bool isInvoice)
        {
            if (string.IsNullOrWhiteSpace(fileName) || content.Length == 0)
            {
                return false;
            }

            using var db = new PurchaseOrderContext();

            var order = db.PurchaseOrders.FirstOrDefault(item => item.PurchaseOrderId == orderId);
            if (order == null)
            {
                return false;
            }

            if (isInvoice)
            {
                if (!CanCurrentUserUploadInvoice(order))
                {
                    return false;
                }

                order.InvoiceFileName = fileName;
                order.InvoiceContent = content;
                order.InvoiceUploadedByAppUserId = signedInUserAppUserId;
                order.InvoiceUploadedByDisplayName = NormalizeText(signedInUserDisplayName);
                order.InvoiceUploadedAt = DateTime.Now;
            }
            else
            {
                order.SignedOrderFileName = fileName;
                order.SignedOrderContent = content;
                var approvalTime = order.ManagerApprovedAt ?? order.DirectorApprovedAt ?? DateTime.Now;
                order.ManagerApprovedAt = approvalTime;
                order.ManagerApprovedByAppUserId ??= signedInUserAppUserId;
                order.ManagerApprovedByDisplayName ??= NormalizeText(signedInUserDisplayName);
                order.DirectorApprovedAt = approvalTime;
                order.DirectorApprovedByAppUserId ??= signedInUserAppUserId;
                order.DirectorApprovedByDisplayName ??= NormalizeText(signedInUserDisplayName);
            }

            order.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            OrderArchiveService.TrySyncOrderFolder(orderId);
            await LoadOrderHistoryAsync(db, forceFullRefresh: false);

            if (HasFullApproval(order) &&
                !string.IsNullOrWhiteSpace(order.InvoiceFileName) &&
                order.InvoiceContent is { Length: > 0 })
            {
                OrderArchiveService.TryArchiveCompletedOrder(orderId);
            }

            return true;
        }

        private StoredOrderDocument? GetOrderDocument(int orderId, bool isInvoice)
        {
            using var db = new PurchaseOrderContext();

            var order = db.PurchaseOrders
                .AsNoTracking()
                .Where(item => item.PurchaseOrderId == orderId)
                .FirstOrDefault();

            if (order == null)
            {
                return null;
            }

            if (isInvoice)
            {
                if (!CanCurrentUserOpenInvoice(order))
                {
                    return null;
                }

                return order.InvoiceContent == null || string.IsNullOrWhiteSpace(order.InvoiceFileName)
                    ? null
                    : new StoredOrderDocument(order.InvoiceFileName, order.InvoiceContent);
            }

            return order.SignedOrderContent == null || string.IsNullOrWhiteSpace(order.SignedOrderFileName)
                ? null
                : new StoredOrderDocument(order.SignedOrderFileName, order.SignedOrderContent);
        }

        private static string FormatWorkflowStatus(DateTime? value, string fallback)
        {
            return value.HasValue ? value.Value.ToString("dd/MM/yyyy HH:mm") : fallback;
        }

        private static string FormatOverallApprovalStatus(DateTime? managerApprovedAt, DateTime? directorApprovedAt)
        {
            if (managerApprovedAt.HasValue && directorApprovedAt.HasValue)
            {
                return $"Signed by manager and director on {directorApprovedAt.Value:dd/MM/yyyy HH:mm}";
            }

            if (managerApprovedAt.HasValue)
            {
                return $"Manager approved {managerApprovedAt.Value:dd/MM/yyyy HH:mm}; awaiting director";
            }

            return "Awaiting manager approval";
        }

        private void ApplyHistoryFilter(int? preferredSelectedOrderId = null)
        {
            var searchTerms = (HistorySearchText ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var filteredItems = OrderHistory
                .Where(MatchesHistoryScope)
                .Where(item => MatchesHistorySearch(item, searchTerms))
                .ToList();

            FilteredOrderHistory = new ObservableCollection<OrderHistoryItem>(filteredItems);
            VisibleOrderCount = filteredItems.Count;
            SelectedOrderHistoryItem = preferredSelectedOrderId.HasValue
                ? FilteredOrderHistory.FirstOrDefault(item => item.PurchaseOrderId == preferredSelectedOrderId.Value)
                : FilteredOrderHistory.FirstOrDefault();
        }

        private void UpdateHistorySummary(IReadOnlyCollection<OrderHistoryItem> historyItems)
        {
            TotalOrderCount = historyItems.Count;
            CompletedOrderCount = historyItems.Count(item => item.IsCompleted);
            RejectedOrderCount = historyItems.Count(item => item.IsRejected);
            ActiveOrderCount = historyItems.Count - CompletedOrderCount - RejectedOrderCount;
            MyOrderCount = historyItems.Count(item => item.IsCreatedByCurrentUser);
            AwaitingManagerCount = historyItems.Count(item => string.Equals(item.OrderStatus, "Pending Manager Approval", StringComparison.OrdinalIgnoreCase));
            AwaitingExecutiveCount = historyItems.Count(item => string.Equals(item.OrderStatus, "Pending Director Approval", StringComparison.OrdinalIgnoreCase));
            AwaitingInvoiceCount = historyItems.Count(item => string.Equals(item.OrderStatus, "Pending Invoice", StringComparison.OrdinalIgnoreCase));
        }

        private bool MatchesHistoryScope(OrderHistoryItem item)
        {
            return SelectedHistoryScope switch
            {
                OrderHistoryScope.MyOrders => item.IsCreatedByCurrentUser,
                OrderHistoryScope.AwaitingManager => string.Equals(item.OrderStatus, "Pending Manager Approval", StringComparison.OrdinalIgnoreCase),
                OrderHistoryScope.AwaitingExecutive => string.Equals(item.OrderStatus, "Pending Director Approval", StringComparison.OrdinalIgnoreCase),
                OrderHistoryScope.AwaitingInvoice => string.Equals(item.OrderStatus, "Pending Invoice", StringComparison.OrdinalIgnoreCase),
                OrderHistoryScope.Completed => item.IsCompleted,
                _ => true
            };
        }

        private OrderHistoryItem MapHistoryItem(PurchaseOrderHistoryProjection order)
        {
            return new OrderHistoryItem
            {
                PurchaseOrderId = order.PurchaseOrderId,
                OrderNumber = order.OrderNumber,
                Date = order.Date,
                CompanyName = order.CompanyName,
                BillTo = order.BillTo,
                Reference = order.Reference,
                CreatedByDisplayName = order.CreatedByDisplayName,
                AssignedManagerDisplayName = order.AssignedManagerDisplayName,
                IsCreatedByCurrentUser = IsOrderCreatedBySignedInUser(order.CreatedByAppUserId, order.CreatedByDisplayName),
                TotalAmount = order.TotalAmount,
                ManagerApprovedAt = order.ManagerApprovedAt,
                DirectorApprovedAt = order.DirectorApprovedAt,
                SupplierCopySentAt = order.SupplierCopySentAt,
                RejectedAt = order.RejectedAt,
                SignedOrderFileName = order.SignedOrderFileName,
                InvoiceFileName = order.InvoiceFileName
            };
        }

        private static bool MatchesHistorySearch(OrderHistoryItem item, string[] searchTerms)
        {
            if (searchTerms.Length == 0)
            {
                return true;
            }

            var searchableText = string.Join(" ", new[]
            {
                item.OrderNumber,
                item.BillTo,
                item.OrderStatus,
                item.CompanyName,
                item.Reference
            }).ToUpperInvariant();

            return searchTerms.All(term => searchableText.Contains(term.ToUpperInvariant(), StringComparison.Ordinal));
        }

        private string GenerateNextOrderNumber(PurchaseOrderContext db)
        {
            var prefix = GetOrderPrefix();
            var nextSequence = db.PurchaseOrders
                .AsEnumerable()
                .Select(order => ExtractOrderSequence(order.OrderNumber))
                .Where(sequence => sequence >= 0)
                .DefaultIfEmpty(OrderSequenceStartingValue - 1)
                .Max() + 1;

            return $"{prefix}{nextSequence.ToString($"D{OrderSequenceDigits}")}";
        }

        private static int ExtractOrderSequence(string? orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                return -1;
            }

            var suffixStartIndex = orderNumber.Length;
            while (suffixStartIndex > 0 && char.IsDigit(orderNumber[suffixStartIndex - 1]))
            {
                suffixStartIndex--;
            }

            if (suffixStartIndex == orderNumber.Length)
            {
                return -1;
            }

            var suffix = orderNumber[suffixStartIndex..];
            return int.TryParse(suffix, out var sequence) ? sequence : -1;
        }

        private string GetOrderPrefix()
        {
            var condensedName = NormalizePrefix(signedInUserDisplayName);
            if (condensedName.Length >= 3)
            {
                return condensedName[..3];
            }

            var condensedUserName = NormalizePrefix(Environment.UserName);
            return condensedUserName.Length >= 3
                ? condensedUserName[..3]
                : "USR";
        }

        private static string NormalizePrefix(string? value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private void SetLinesCollection(ObservableCollection<PurchaseOrderLine> newLines)
        {
            DetachLineCollectionHandlers();
            Lines = newLines;
            observedLines = newLines;
            observedLines.CollectionChanged += OnLinesCollectionChanged;

            foreach (var line in observedLines)
            {
                line.PropertyChanged += OnLinePropertyChanged;
            }
        }

        private void DetachLineCollectionHandlers()
        {
            if (observedLines == null)
            {
                return;
            }

            observedLines.CollectionChanged -= OnLinesCollectionChanged;
            foreach (var line in observedLines)
            {
                line.PropertyChanged -= OnLinePropertyChanged;
            }
        }

        private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (PurchaseOrderLine line in e.OldItems)
                {
                    line.PropertyChanged -= OnLinePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (PurchaseOrderLine line in e.NewItems)
                {
                    line.PropertyChanged += OnLinePropertyChanged;
                }
            }

            RefreshTotals();
        }

        private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(PurchaseOrderLine.Quantity) or nameof(PurchaseOrderLine.UnitPrice) or nameof(PurchaseOrderLine.LineTotal))
            {
                RefreshTotals();
            }
        }
    }
}
