namespace PurchaseOrderApp.ViewModels;

public sealed class PurchaseOrderSpendChartPoint
{
    public string MonthLabel { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}
