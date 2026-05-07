using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Services;
using System.Collections.ObjectModel;

namespace PurchaseOrderApp.ViewModels;

public partial class ReceiveStockLineViewModel : ObservableObject
{
    public ReceiveStockLineViewModel(
        int lineNumber,
        IEnumerable<ReceiveStockTemplateOption> stockTemplates,
        IEnumerable<string> categoryOptions,
        IEnumerable<string> supplierSuggestions,
        IEnumerable<ReceiptPurchaseOrderOptionViewModel> purchaseOrderOptions)
    {
        LineNumber = lineNumber;
        StockTemplates = new ObservableCollection<ReceiveStockTemplateOption>(stockTemplates);
        CategoryOptions = new ObservableCollection<string>(categoryOptions);
        SupplierSuggestions = new ObservableCollection<string>(supplierSuggestions);
        PurchaseOrderOptions = new ObservableCollection<ReceiptPurchaseOrderOptionViewModel>(purchaseOrderOptions);
        QuantityText = "1";
        Category = "General";
        TrackingUnitEntries = [];
        SelectedTemplate = StockTemplates.FirstOrDefault();
    }

    [ObservableProperty]
    private int lineNumber;

    partial void OnLineNumberChanged(int value)
    {
        OnPropertyChanged(nameof(LineLabel));
    }

    [ObservableProperty]
    private ObservableCollection<ReceiveStockTemplateOption> stockTemplates = [];

    [ObservableProperty]
    private ObservableCollection<string> categoryOptions = [];

    [ObservableProperty]
    private ObservableCollection<string> supplierSuggestions = [];

    [ObservableProperty]
    private ObservableCollection<ReceiptPurchaseOrderOptionViewModel> purchaseOrderOptions = [];

    [ObservableProperty]
    private ObservableCollection<TrackingUnitIdentityEntryViewModel> trackingUnitEntries = [];

    [ObservableProperty]
    private ReceiveStockTemplateOption? selectedTemplate;

    partial void OnSelectedTemplateChanged(ReceiveStockTemplateOption? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ItemName) || string.Equals(ItemName, SelectedTemplate?.ItemName, StringComparison.Ordinal))
        {
            ItemName = value.ItemName;
        }

        Category = value.Category;
        Description = value.Description;
        IsTrackingUnit = value.IsTrackingUnit;
        NotifyDisplayPropertiesChanged();
    }

    [ObservableProperty]
    private ReceiptPurchaseOrderOptionViewModel? selectedPurchaseOrder;

    partial void OnSelectedPurchaseOrderChanged(ReceiptPurchaseOrderOptionViewModel? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(SupplierName))
        {
            SupplierName = value.SupplierName;
        }

        NotifyDisplayPropertiesChanged();
    }

    [ObservableProperty]
    private string itemCode = string.Empty;

    partial void OnItemCodeChanged(string value)
    {
        NotifyDisplayPropertiesChanged();
    }

    [ObservableProperty]
    private string itemName = string.Empty;

    partial void OnItemNameChanged(string value)
    {
        NotifyDisplayPropertiesChanged();
    }

    [ObservableProperty]
    private string category = "General";

    partial void OnCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(AutomaticCategoryText));
    }

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isTrackingUnit;

    partial void OnIsTrackingUnitChanged(bool value)
    {
        OnPropertyChanged(nameof(TrackingUnitHelpText));
        OnPropertyChanged(nameof(ShowTrackingUnitIdentitySection));
        NotifyDisplayPropertiesChanged();
        SyncTrackingUnitEntries();
    }

    [ObservableProperty]
    private string quantityText = "1";

    partial void OnQuantityTextChanged(string value)
    {
        OnPropertyChanged(nameof(QuantityValue));
        NotifyDisplayPropertiesChanged();
        SyncTrackingUnitEntries();
    }

    [ObservableProperty]
    private string supplierName = string.Empty;

    partial void OnSupplierNameChanged(string value)
    {
        NotifyDisplayPropertiesChanged();
    }

    [ObservableProperty]
    private string notes = string.Empty;

    public string LineLabel => $"Line {LineNumber}";

    public int QuantityValue => int.TryParse(QuantityText, out var quantity) && quantity > 0 ? quantity : 0;

    public bool UsesAutomaticCategory => SelectedTemplate?.IsTrackingUnit == true;

    public bool CanEditTrackingUnitFlag => !UsesAutomaticCategory;

    public bool ShowTrackingUnitIdentitySection => IsTrackingUnit;

    public string AutomaticCategoryText =>
        string.IsNullOrWhiteSpace(Category)
            ? "Tracking unit stock types are classified automatically."
            : $"Tracking unit stock types are classified automatically as {Category}.";

    public string TrackingUnitHelpText => UsesAutomaticCategory
        ? "This stock type is automatically treated as a tracking unit and will require a job card when issued out."
        : "Use this when stock issues must be tied to a job card.";

    public string SummaryItemDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ItemCode) && !string.IsNullOrWhiteSpace(ItemName))
            {
                return $"{ItemCode} - {ItemName}";
            }

            if (!string.IsNullOrWhiteSpace(ItemCode))
            {
                return ItemCode;
            }

            if (!string.IsNullOrWhiteSpace(ItemName))
            {
                return ItemName;
            }

            return "New stock line";
        }
    }

    public string SummarySupplierDisplay =>
        string.IsNullOrWhiteSpace(SupplierName)
            ? "Supplier pending"
            : SupplierName;

    public string SummaryPurchaseOrderDisplay =>
        SelectedPurchaseOrder?.OrderNumber ?? "No PO link";

    public string SummaryQuantityDisplay =>
        QuantityValue <= 0 ? "Qty pending" : $"{QuantityValue} unit(s)";

    public string SummaryTypeDisplay => IsTrackingUnit ? "Tracking Unit" : "General Stock";

    [RelayCommand]
    private void ClearPurchaseOrderLink()
    {
        SelectedPurchaseOrder = null;
    }

    internal InventoryService.ReceiveInventoryReceiptLineRequest BuildRequest()
    {
        return new InventoryService.ReceiveInventoryReceiptLineRequest(
            ItemCode,
            ItemName,
            Category,
            Description,
            IsTrackingUnit,
            QuantityValue,
            EffectiveSupplierName,
            SelectedPurchaseOrder?.PurchaseOrderId,
            Notes,
            BuildTrackingUnitInputs());
    }

    public void Renumber(int newLineNumber)
    {
        LineNumber = newLineNumber;
    }

    private string EffectiveSupplierName =>
        !string.IsNullOrWhiteSpace(SupplierName)
            ? SupplierName
            : SelectedPurchaseOrder?.SupplierName ?? string.Empty;

    private IReadOnlyList<InventoryService.TrackingUnitIdentityInput>? BuildTrackingUnitInputs()
    {
        if (!IsTrackingUnit)
        {
            return null;
        }

        return TrackingUnitEntries
            .Select(entry => new InventoryService.TrackingUnitIdentityInput(entry.SerialNumber, entry.ImeiNumber))
            .ToList();
    }

    private void SyncTrackingUnitEntries()
    {
        if (!IsTrackingUnit || QuantityValue <= 0)
        {
            TrackingUnitEntries = [];
            return;
        }

        var existingEntries = TrackingUnitEntries.ToList();
        var synchronizedEntries = new List<TrackingUnitIdentityEntryViewModel>();

        for (var index = 0; index < QuantityValue; index++)
        {
            if (index < existingEntries.Count)
            {
                synchronizedEntries.Add(new TrackingUnitIdentityEntryViewModel(index + 1)
                {
                    SerialNumber = existingEntries[index].SerialNumber,
                    ImeiNumber = existingEntries[index].ImeiNumber
                });
            }
            else
            {
                synchronizedEntries.Add(new TrackingUnitIdentityEntryViewModel(index + 1));
            }
        }

        TrackingUnitEntries = new ObservableCollection<TrackingUnitIdentityEntryViewModel>(synchronizedEntries);
    }

    private void NotifyDisplayPropertiesChanged()
    {
        OnPropertyChanged(nameof(UsesAutomaticCategory));
        OnPropertyChanged(nameof(CanEditTrackingUnitFlag));
        OnPropertyChanged(nameof(AutomaticCategoryText));
        OnPropertyChanged(nameof(TrackingUnitHelpText));
        OnPropertyChanged(nameof(ShowTrackingUnitIdentitySection));
        OnPropertyChanged(nameof(SummaryItemDisplay));
        OnPropertyChanged(nameof(SummarySupplierDisplay));
        OnPropertyChanged(nameof(SummaryPurchaseOrderDisplay));
        OnPropertyChanged(nameof(SummaryQuantityDisplay));
        OnPropertyChanged(nameof(SummaryTypeDisplay));
    }
}
