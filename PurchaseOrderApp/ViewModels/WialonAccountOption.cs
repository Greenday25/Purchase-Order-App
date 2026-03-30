namespace PurchaseOrderApp.ViewModels;

public sealed class WialonAccountOption
{
    public long AccountId { get; init; }

    public string AccountName { get; init; } = string.Empty;

    public long CreatorId { get; init; }

    public string DisplayText => AccountName;
}
