using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace PurchaseOrderApp.Models
{
    public partial class PurchaseOrderLine : ObservableObject
    {
        public int PurchaseOrderLineId { get; set; }
        public int PurchaseOrderId { get; set; }

        private decimal quantity;
        public decimal Quantity
        {
            get => quantity;
            set
            {
                if (SetProperty(ref quantity, value))
                {
                    OnPropertyChanged(nameof(QuantityText));
                    OnPropertyChanged(nameof(LineTotal));
                    OnPropertyChanged(nameof(LineTotalDisplay));
                }
            }
        }

        [NotMapped]
        public string QuantityText
        {
            get => FormatEditorNumber(Quantity);
            set
            {
                Quantity = ParseEditorNumber(value);
            }
        }

        private string partNumber = string.Empty;
        public string PartNumber
        {
            get => partNumber;
            set => SetProperty(ref partNumber, value);
        }

        private string description = string.Empty;
        public string Description
        {
            get => description;
            set => SetProperty(ref description, value);
        }

        private decimal unitPrice;
        public decimal UnitPrice
        {
            get => unitPrice;
            set
            {
                if (SetProperty(ref unitPrice, value))
                {
                    OnPropertyChanged(nameof(UnitPriceText));
                    OnPropertyChanged(nameof(LineTotal));
                    OnPropertyChanged(nameof(LineTotalDisplay));
                }
            }
        }

        [NotMapped]
        public string UnitPriceText
        {
            get => FormatEditorNumber(UnitPrice);
            set
            {
                UnitPrice = ParseEditorNumber(value);
            }
        }

        public decimal LineTotal => Quantity * UnitPrice;

        [NotMapped]
        public string LineTotalDisplay => LineTotal == 0m ? string.Empty : LineTotal.ToString("N2");

        public PurchaseOrder PurchaseOrder { get; set; }

        private static decimal ParseEditorNumber(string? value)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) ||
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0m;
        }

        private static string FormatEditorNumber(decimal value)
        {
            if (value == 0m)
            {
                return string.Empty;
            }

            return decimal.Truncate(value) == value
                ? value.ToString("0")
                : value.ToString("0.##");
        }
    }
}
