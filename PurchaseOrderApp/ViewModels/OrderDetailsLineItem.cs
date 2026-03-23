namespace PurchaseOrderApp.ViewModels
{
    public sealed class OrderDetailsLineItem
    {
        public decimal Quantity { get; init; }
        public string PartNumber { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
