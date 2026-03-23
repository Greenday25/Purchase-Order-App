using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Data;
using System.Collections.ObjectModel;
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

        private const string CarsOrderPrefix = "CARS";
        private const int OrderSequenceDigits = 5;
        private const int OrderSequenceStartingValue = 90;
        private const int NameDisplayFormat = 3;
        private const string ReactionServicesCompanyName = "Capital Air Reaction Services CC";
        private const string LegacyReactionServicesCompanyName = "Capital Air Reaction Services (Pty) Ltd";
        private static readonly Vendor[] DefaultCompanies =
        [
            new()
            {
                Name = "CAPITAL AIR (Pty) Ltd",
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
                Name = "Capital Air Security Operations (Pty) Ltd",
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
            AddSampleLine();
            LoadOrderHistory();
        }

        private void InitializeModel()
        {
            using var db = new PurchaseOrderContext();
            db.Database.EnsureCreated();
            EnsureDatabaseSchema(db);

            EnsureDefaultCompanies(db);

            Vendors = new ObservableCollection<Vendor>(db.Vendors.ToList());
            SelectedVendor = Vendors.FirstOrDefault();

            CurrentOrder = new PurchaseOrder
            {
                OrderNumber = string.Empty,
                Date = DateTime.Today,
                Reference = "CARS OPS OFFICE",
                BillTo = "ALBERTON HARDWARE",
                BillToAddress = "",
                VATPercent = 15m,
                VendorId = Vendors[0]?.VendorId ?? 0,
                Vendor = Vendors[0],
                Lines = new ObservableCollection<PurchaseOrderLine>().ToList()
            };

            Lines = new ObservableCollection<PurchaseOrderLine>(CurrentOrder.Lines);
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

        private void AddSampleLine()
        {
            Lines.Add(new PurchaseOrderLine { Quantity = 2, PartNumber = "", Description = "PLASCON BRILLIANT WHITE", UnitPrice = 2098.00m });
            Lines.Add(new PurchaseOrderLine { Quantity = 1, PartNumber = "", Description = "ROLLER TRAY SET PREMIUM", UnitPrice = 152.00m });
            Lines.Add(new PurchaseOrderLine { Quantity = 1, PartNumber = "", Description = "POLY POLYFILLA EXTERIOR", UnitPrice = 51.85m });
            Lines.Add(new PurchaseOrderLine { Quantity = 1, PartNumber = "", Description = "ROLLER CLASSIC HAMILTON TRAYSET", UnitPrice = 89.25m });
            Lines.Add(new PurchaseOrderLine { Quantity = 5, PartNumber = "", Description = "MASKING TAPE 60 DEGREE", UnitPrice = 68.00m });
            Lines.Add(new PurchaseOrderLine { Quantity = 2, PartNumber = "", Description = "ROLLER CLASSIC HAMILTON REFILL 225ML", UnitPrice = 100.30m });

            RefreshTotals();
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
            }
        }

        [ObservableProperty]
        private PurchaseOrder currentOrder;

        [ObservableProperty]
        private ObservableCollection<PurchaseOrderLine> lines;

        [ObservableProperty]
        private decimal subTotal;

        [ObservableProperty]
        private decimal vatAmount;

        [ObservableProperty]
        private decimal totalAmount;

        [ObservableProperty]
        private ObservableCollection<OrderHistoryItem> orderHistory = [];

        [ObservableProperty]
        private ObservableCollection<OrderHistoryItem> filteredOrderHistory = [];

        [ObservableProperty]
        private OrderHistoryItem? selectedOrderHistoryItem;

        [ObservableProperty]
        private string historySearchText = string.Empty;

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

        [RelayCommand]
        public void SavePurchaseOrder()
        {
            RefreshTotals();

            using var db = new PurchaseOrderContext();

            if (CurrentOrder == null) return;

            RefreshOrderNumber(db);
            CurrentOrder.Vendor = db.Vendors.Find(CurrentOrder.VendorId) ?? CurrentOrder.Vendor;

            var order = new PurchaseOrder
            {
                OrderNumber = CurrentOrder.OrderNumber,
                Date = CurrentOrder.Date,
                Reference = CurrentOrder.Reference,
                BillTo = CurrentOrder.BillTo,
                BillToAddress = CurrentOrder.BillToAddress,
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
            RefreshOrderNumber(db);
        }

        private void RefreshTotals()
        {
            SubTotal = Math.Round(Lines.Sum(l => l.Quantity * l.UnitPrice), 2);
            VatAmount = Math.Round(SubTotal * (CurrentOrder?.VATPercent ?? 0) / 100m, 2);
            TotalAmount = Math.Round(SubTotal + VatAmount, 2);

            if (CurrentOrder != null)
            {
                CurrentOrder.Lines = Lines.ToList();
            }
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
            if (CurrentOrder == null)
            {
                return;
            }

            CurrentOrder.OrderNumber = GenerateNextOrderNumber(db, CurrentOrder.Reference);
            OnPropertyChanged(nameof(CurrentOrder));
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
            ApplyHistoryFilter(selectedOrderId);
        }

        partial void OnHistorySearchTextChanged(string value)
        {
            ApplyHistoryFilter(SelectedOrderHistoryItem?.PurchaseOrderId);
        }

        private static void EnsureDatabaseSchema(PurchaseOrderContext db)
        {
            var columnNames = GetPurchaseOrderColumnNames(db);
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
            SelectedOrderHistoryItem = preferredSelectedOrderId.HasValue
                ? FilteredOrderHistory.FirstOrDefault(item => item.PurchaseOrderId == preferredSelectedOrderId.Value)
                : FilteredOrderHistory.FirstOrDefault();
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

        private static string GenerateNextOrderNumber(PurchaseOrderContext db, string? reference)
        {
            var prefix = GetOrderPrefix(reference);
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

        private static string GetOrderPrefix(string? reference)
        {
            if (!string.IsNullOrWhiteSpace(reference) &&
                reference.TrimStart().StartsWith(CarsOrderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return CarsOrderPrefix;
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
    }
}
