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
using System.Runtime.InteropServices;
using System.Text;

namespace PurchaseOrderApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public sealed record StoredOrderDocument(string FileName, byte[] Content);
        private ObservableCollection<PurchaseOrderLine>? observedLines;

        private const string CapitalAirCompanyName = "CAPITAL AIR (Pty) Ltd";
        private const string ReactionServicesOrderPrefix = "CARS";
        private const string SecurityOperationsCompanyName = "Capital Air Security Operations (Pty) Ltd";
        private const string SecurityOperationsOrderPrefix = "CASO";
        private const int OrderSequenceDigits = 5;
        private const int OrderSequenceStartingValue = 100;
        private const int NameDisplayFormat = 3;
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

        [DllImport("Secur32.dll", EntryPoint = "GetUserNameExW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetUserNameEx(int nameFormat, StringBuilder userName, ref uint userNameSize);

        public MainViewModel()
        {
            InitializeModel();
            LoadOrderHistory();
        }

        private void InitializeModel()
        {
            using var db = new PurchaseOrderContext();
            db.Database.EnsureCreated();
            EnsureDatabaseSchema(db);

            EnsureDefaultCompanies(db);
            NormalizeExistingOrderNumbers(db);

            Vendors = new ObservableCollection<Vendor>(db.Vendors.ToList());
            SelectedVendor = Vendors.FirstOrDefault();

            CurrentOrder = new PurchaseOrder
            {
                OrderNumber = string.Empty,
                Date = DateTime.Today,
                Reference = "CARS OPS OFFICE",
                BillTo = "ALBERTON HARDWARE",
                BillToAddress = "",
                IncludeVat = true,
                VATPercent = 15m,
                VendorId = Vendors[0]?.VendorId ?? 0,
                Vendor = Vendors[0],
                Lines = new ObservableCollection<PurchaseOrderLine>().ToList()
            };

            SetLinesCollection(new ObservableCollection<PurchaseOrderLine>(CurrentOrder.Lines));
            RefreshOrderNumber(db);
        }

        private static void EnsureDefaultCompanies(PurchaseOrderContext db)
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
                db.SaveChanges();
            }
        }

        [ObservableProperty]
        private ObservableCollection<Vendor> vendors;

        [ObservableProperty]
        private Vendor selectedVendor;

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

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new PurchaseOrderLine { Quantity = 1, PartNumber = "", Description = "", UnitPrice = 0m });
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
        public void RefreshHistory()
        {
            LoadOrderHistory();
        }

        public bool MarkApprovalsCompleted(int orderId)
        {
            return UpdateOrderWorkflow(orderId, order =>
            {
                var approvalTime = order.ManagerApprovedAt ?? order.DirectorApprovedAt ?? DateTime.Now;
                order.ManagerApprovedAt = approvalTime;
                order.DirectorApprovedAt = approvalTime;
            });
        }

        public bool MarkRejected(int orderId)
        {
            return UpdateOrderWorkflow(orderId, order => order.RejectedAt ??= DateTime.Now);
        }

        public bool DeleteOrder(int orderId)
        {
            using var db = new PurchaseOrderContext();
            EnsureDatabaseSchema(db);

            var order = db.PurchaseOrders
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.PurchaseOrderId == orderId);

            if (order == null || order.InvoiceContent != null || !string.IsNullOrWhiteSpace(order.InvoiceFileName))
            {
                return false;
            }

            db.PurchaseOrders.Remove(order);
            db.SaveChanges();
            LoadOrderHistory(db);
            return true;
        }

        public bool SaveSignedOrderDocument(int orderId, string fileName, byte[] content)
        {
            return SaveOrderDocument(orderId, fileName, content, isInvoice: false);
        }

        public bool SaveInvoiceDocument(int orderId, string fileName, byte[] content)
        {
            return SaveOrderDocument(orderId, fileName, content, isInvoice: true);
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
            EnsureDatabaseSchema(db);

            var order = db.PurchaseOrders
                .AsNoTracking()
                .Include(item => item.Vendor)
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.PurchaseOrderId == orderId);

            if (order == null)
            {
                return null;
            }

            return new OrderDetailsViewModel
            {
                PurchaseOrderId = order.PurchaseOrderId,
                OrderNumber = order.OrderNumber,
                Date = order.Date,
                CompanyName = order.Vendor?.Name ?? string.Empty,
                BillTo = order.BillTo,
                BillToAddress = order.BillToAddress,
                Reference = order.Reference,
                OrderStatus = OrderHistoryItem.GetOrderStatus(
                    order.ManagerApprovedAt,
                    order.DirectorApprovedAt,
                    order.RejectedAt,
                    order.SignedOrderFileName,
                    order.InvoiceFileName),
                ApprovalStatus = FormatWorkflowStatus(order.DirectorApprovedAt ?? order.ManagerApprovedAt, "Pending"),
                RejectionStatus = FormatWorkflowStatus(order.RejectedAt, "Active"),
                TotalAmount = order.Total,
                SignedOrderFileName = string.IsNullOrWhiteSpace(order.SignedOrderFileName) ? "Not Uploaded" : order.SignedOrderFileName,
                InvoiceFileName = string.IsNullOrWhiteSpace(order.InvoiceFileName) ? "Not Uploaded" : order.InvoiceFileName,
                IsApproved = order.ManagerApprovedAt.HasValue || order.DirectorApprovedAt.HasValue,
                IsRejected = order.RejectedAt.HasValue,
                IsCompleted = !string.IsNullOrWhiteSpace(order.InvoiceFileName),
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

        public bool LoadExistingOrder(int orderId)
        {
            using var db = new PurchaseOrderContext();
            EnsureDatabaseSchema(db);

            var order = db.PurchaseOrders
                .AsNoTracking()
                .Include(item => item.Vendor)
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.PurchaseOrderId == orderId);

            if (order == null)
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
                Date = order.Date,
                Reference = order.Reference,
                VendorId = selectedVendor?.VendorId ?? order.VendorId,
                Vendor = selectedVendor ?? order.Vendor,
                BillTo = order.BillTo,
                BillToAddress = order.BillToAddress,
                IncludeVat = order.IncludeVat,
                VATPercent = order.VATPercent,
                ManagerApprovedAt = order.ManagerApprovedAt,
                DirectorApprovedAt = order.DirectorApprovedAt,
                SupplierCopySentAt = order.SupplierCopySentAt,
                RejectedAt = order.RejectedAt,
                SignedOrderFileName = order.SignedOrderFileName,
                SignedOrderContent = order.SignedOrderContent,
                InvoiceFileName = order.InvoiceFileName,
                InvoiceContent = order.InvoiceContent,
                Lines = copiedLines
            };

            SelectedVendor = selectedVendor ?? Vendors.FirstOrDefault();
            SetLinesCollection(new ObservableCollection<PurchaseOrderLine>(copiedLines));
            RefreshTotals();
            return true;
        }

        [RelayCommand]
        public void SavePurchaseOrder()
        {
            RefreshTotals();

            using var db = new PurchaseOrderContext();

            if (CurrentOrder == null) return;

            if (string.IsNullOrWhiteSpace(CurrentOrder.OrderNumber))
            {
                RefreshOrderNumber(db);
            }

            CurrentOrder.Vendor = db.Vendors.Find(CurrentOrder.VendorId) ?? CurrentOrder.Vendor;

            var order = new PurchaseOrder
            {
                OrderNumber = CurrentOrder.OrderNumber,
                OrderNumberManuallyEdited = CurrentOrder.OrderNumberManuallyEdited,
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
            db.SaveChanges();
            LoadOrderHistory(db);
            CurrentOrder.OrderNumberManuallyEdited = false;
            RefreshOrderNumber(db);
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

        public void LoadOrderHistory()
        {
            using var db = new PurchaseOrderContext();
            EnsureDatabaseSchema(db);
            LoadOrderHistory(db);
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

            var companyName = CurrentOrder.Vendor?.Name
                ?? SelectedVendor?.Name
                ?? db.Vendors
                    .Where(vendor => vendor.VendorId == CurrentOrder.VendorId)
                    .Select(vendor => vendor.Name)
                    .FirstOrDefault();

            CurrentOrder.OrderNumber = GenerateNextOrderNumber(db, companyName);
            CurrentOrder.OrderNumberManuallyEdited = false;
            OnPropertyChanged(nameof(OrderNumber));
        }

        private void LoadOrderHistory(PurchaseOrderContext db)
        {
            var selectedOrderId = SelectedOrderHistoryItem?.PurchaseOrderId;
            var orders = db.PurchaseOrders
                .AsNoTracking()
                .Include(order => order.Vendor)
                .Include(order => order.Lines)
                .OrderByDescending(order => order.PurchaseOrderId)
                .ToList();

            var historyItems = orders
                .Select(order => new OrderHistoryItem
                {
                    PurchaseOrderId = order.PurchaseOrderId,
                    OrderNumber = order.OrderNumber,
                    Date = order.Date,
                    CompanyName = order.Vendor != null ? order.Vendor.Name : string.Empty,
                    BillTo = order.BillTo,
                    Reference = order.Reference,
                    TotalAmount = order.Total,
                    ManagerApprovedAt = order.ManagerApprovedAt,
                    DirectorApprovedAt = order.DirectorApprovedAt,
                    SupplierCopySentAt = order.SupplierCopySentAt,
                    RejectedAt = order.RejectedAt,
                    SignedOrderFileName = order.SignedOrderFileName,
                    InvoiceFileName = order.InvoiceFileName
                })
                .ToList();

            OrderHistory = new ObservableCollection<OrderHistoryItem>(historyItems);
            UpdateHistorySummary(historyItems);
            ApplyHistoryFilter(selectedOrderId);
        }

        partial void OnHistorySearchTextChanged(string value)
        {
            ApplyHistoryFilter(SelectedOrderHistoryItem?.PurchaseOrderId);
        }

        private static void EnsureDatabaseSchema(PurchaseOrderContext db)
        {
            var columnNames = GetPurchaseOrderColumnNames(db);
            AddColumnIfMissing(db, columnNames, "OrderNumberManuallyEdited", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(db, columnNames, "IncludeVat", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing(db, columnNames, "ManagerApprovedAt", "TEXT NULL");
            AddColumnIfMissing(db, columnNames, "DirectorApprovedAt", "TEXT NULL");
            AddColumnIfMissing(db, columnNames, "SupplierCopySentAt", "TEXT NULL");
            AddColumnIfMissing(db, columnNames, "RejectedAt", "TEXT NULL");
            AddColumnIfMissing(db, columnNames, "SignedOrderFileName", "TEXT NULL");
            AddColumnIfMissing(db, columnNames, "SignedOrderContent", "BLOB NULL");
            AddColumnIfMissing(db, columnNames, "InvoiceFileName", "TEXT NULL");
            AddColumnIfMissing(db, columnNames, "InvoiceContent", "BLOB NULL");
        }

        private static HashSet<string> GetPurchaseOrderColumnNames(PurchaseOrderContext db)
        {
            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info('PurchaseOrders')";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    columnNames.Add(reader.GetString(1));
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }

            return columnNames;
        }

        private static void AddColumnIfMissing(PurchaseOrderContext db, HashSet<string> existingColumns, string columnName, string definition)
        {
            if (existingColumns.Contains(columnName))
            {
                return;
            }

            var sql = columnName switch
            {
                "OrderNumberManuallyEdited" => "ALTER TABLE PurchaseOrders ADD COLUMN OrderNumberManuallyEdited INTEGER NOT NULL DEFAULT 0",
                "IncludeVat" => "ALTER TABLE PurchaseOrders ADD COLUMN IncludeVat INTEGER NOT NULL DEFAULT 1",
                "ManagerApprovedAt" => "ALTER TABLE PurchaseOrders ADD COLUMN ManagerApprovedAt TEXT NULL",
                "DirectorApprovedAt" => "ALTER TABLE PurchaseOrders ADD COLUMN DirectorApprovedAt TEXT NULL",
                "SupplierCopySentAt" => "ALTER TABLE PurchaseOrders ADD COLUMN SupplierCopySentAt TEXT NULL",
                "RejectedAt" => "ALTER TABLE PurchaseOrders ADD COLUMN RejectedAt TEXT NULL",
                "SignedOrderFileName" => "ALTER TABLE PurchaseOrders ADD COLUMN SignedOrderFileName TEXT NULL",
                "SignedOrderContent" => "ALTER TABLE PurchaseOrders ADD COLUMN SignedOrderContent BLOB NULL",
                "InvoiceFileName" => "ALTER TABLE PurchaseOrders ADD COLUMN InvoiceFileName TEXT NULL",
                "InvoiceContent" => "ALTER TABLE PurchaseOrders ADD COLUMN InvoiceContent BLOB NULL",
                _ => throw new InvalidOperationException($"Unsupported column migration: {columnName} {definition}")
            };

            db.Database.ExecuteSqlRaw(sql);
            existingColumns.Add(columnName);
        }

        private bool UpdateOrderWorkflow(int orderId, Action<PurchaseOrder> updateAction)
        {
            using var db = new PurchaseOrderContext();
            EnsureDatabaseSchema(db);

            var order = db.PurchaseOrders.FirstOrDefault(item => item.PurchaseOrderId == orderId);
            if (order == null)
            {
                return false;
            }

            updateAction(order);
            db.SaveChanges();
            LoadOrderHistory(db);
            return true;
        }

        private bool SaveOrderDocument(int orderId, string fileName, byte[] content, bool isInvoice)
        {
            if (string.IsNullOrWhiteSpace(fileName) || content.Length == 0)
            {
                return false;
            }

            using var db = new PurchaseOrderContext();
            EnsureDatabaseSchema(db);

            var order = db.PurchaseOrders.FirstOrDefault(item => item.PurchaseOrderId == orderId);
            if (order == null)
            {
                return false;
            }

            if (isInvoice)
            {
                order.InvoiceFileName = fileName;
                order.InvoiceContent = content;
            }
            else
            {
                order.SignedOrderFileName = fileName;
                order.SignedOrderContent = content;
            }

            db.SaveChanges();
            LoadOrderHistory(db);

            if (!string.IsNullOrWhiteSpace(order.SignedOrderFileName) &&
                order.SignedOrderContent is { Length: > 0 } &&
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
            EnsureDatabaseSchema(db);

            var order = db.PurchaseOrders
                .AsNoTracking()
                .Where(item => item.PurchaseOrderId == orderId)
                .Select(item => new
                {
                    item.SignedOrderFileName,
                    item.SignedOrderContent,
                    item.InvoiceFileName,
                    item.InvoiceContent
                })
                .FirstOrDefault();

            if (order == null)
            {
                return null;
            }

            if (isInvoice)
            {
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

        private void ApplyHistoryFilter(int? preferredSelectedOrderId = null)
        {
            var searchTerms = (HistorySearchText ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var filteredItems = OrderHistory
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

        private static void NormalizeExistingOrderNumbers(PurchaseOrderContext db)
        {
            var orders = db.PurchaseOrders
                .Include(order => order.Vendor)
                .OrderBy(order => order.PurchaseOrderId)
                .ToList();

            if (orders.Count == 0)
            {
                return;
            }

            var nextSequenceByPrefix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var hasChanges = false;

            foreach (var order in orders)
            {
                var prefix = GetOrderPrefix(order.Vendor?.Name);
                var nextSequence = nextSequenceByPrefix.TryGetValue(prefix, out var existingSequence)
                    ? existingSequence
                    : OrderSequenceStartingValue;
                var currentSequence = ExtractOrderSequence(order.OrderNumber, prefix);

                if (order.OrderNumberManuallyEdited && !string.IsNullOrWhiteSpace(order.OrderNumber))
                {
                    if (currentSequence >= nextSequence)
                    {
                        nextSequence = currentSequence + 1;
                    }

                    nextSequenceByPrefix[prefix] = nextSequence;
                    continue;
                }

                var expectedOrderNumber = $"{prefix}{nextSequence.ToString($"D{OrderSequenceDigits}")}";
                if (!string.Equals(order.OrderNumber, expectedOrderNumber, StringComparison.OrdinalIgnoreCase))
                {
                    order.OrderNumber = expectedOrderNumber;
                    hasChanges = true;
                }

                nextSequenceByPrefix[prefix] = nextSequence + 1;
            }

            if (hasChanges)
            {
                db.SaveChanges();
            }
        }

        private static string GenerateNextOrderNumber(PurchaseOrderContext db, string? companyName)
        {
            var prefix = GetOrderPrefix(companyName);
            var nextSequence = db.PurchaseOrders
                .AsEnumerable()
                .Select(order => ExtractOrderSequence(order.OrderNumber, prefix))
                .Where(sequence => sequence >= 0)
                .DefaultIfEmpty(OrderSequenceStartingValue - 1)
                .Max() + 1;

            return $"{prefix}{nextSequence.ToString($"D{OrderSequenceDigits}")}";
        }

        private static int ExtractOrderSequence(string? orderNumber, string prefix)
        {
            if (string.IsNullOrWhiteSpace(orderNumber) ||
                !orderNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            var suffix = orderNumber[prefix.Length..];
            return int.TryParse(suffix, out var sequence) ? sequence : -1;
        }

        private static string GetOrderPrefix(string? companyName)
        {
            if (string.Equals(companyName, ReactionServicesCompanyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(companyName, LegacyReactionServicesCompanyName, StringComparison.OrdinalIgnoreCase))
            {
                return ReactionServicesOrderPrefix;
            }

            if (string.Equals(companyName, SecurityOperationsCompanyName, StringComparison.OrdinalIgnoreCase))
            {
                return SecurityOperationsOrderPrefix;
            }

            var condensedName = NormalizePrefix(GetCurrentUserDisplayName());
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

        private static string GetCurrentUserDisplayName()
        {
            uint capacity = 256;
            var buffer = new StringBuilder((int)capacity);
            return GetUserNameEx(NameDisplayFormat, buffer, ref capacity) && !string.IsNullOrWhiteSpace(buffer.ToString())
                ? buffer.ToString().Trim()
                : Environment.UserName;
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
