using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Data;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PurchaseOrderApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private const string CarsOrderPrefix = "CARS";
        private const int OrderSequenceDigits = 5;
        private const int OrderSequenceStartingValue = 90;
        private const int NameDisplayFormat = 3;

        [DllImport("Secur32.dll", EntryPoint = "GetUserNameExW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetUserNameEx(int nameFormat, StringBuilder userName, ref uint userNameSize);

        public MainViewModel()
        {
            InitializeModel();
            AddSampleLine();
        }

        private void InitializeModel()
        {
            using var db = new PurchaseOrderContext();
            db.Database.EnsureCreated();

            if (!db.Vendors.Any())
            {
                var vendor = new Vendor
                {
                    Name = "CAPITAL AIR (Pty) Ltd",
                    Address = "P.O. BOX 10009, RAND AIRPORT 1419, GERMISTON, SOUTH AFRICA",
                    Phone = "+27 11 827 0335 / 82 2634/2840",
                    Email = "info@capitalair.com"
                };
                db.Vendors.Add(vendor);
                db.SaveChanges();
            }

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

        private void RefreshOrderNumber(PurchaseOrderContext db)
        {
            if (CurrentOrder == null)
            {
                return;
            }

            CurrentOrder.OrderNumber = GenerateNextOrderNumber(db, CurrentOrder.Reference);
            OnPropertyChanged(nameof(CurrentOrder));
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
