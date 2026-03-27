using CommunityToolkit.Mvvm.ComponentModel;

namespace PurchaseOrderApp.Models
{
    public partial class PurchaseOrderLine : ObservableObject
    {
        public int PurchaseOrderLineId { get; set; }
        public int PurchaseOrderId { get; set; }

        private decimal quantity = 1;
        public decimal Quantity
        {
            get => quantity;
            set
            {
                if (SetProperty(ref quantity, value))
                {
                    OnPropertyChanged(nameof(LineTotal));
                }
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
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal LineTotal => Quantity * UnitPrice;

        public PurchaseOrder PurchaseOrder { get; set; }
    }
}
