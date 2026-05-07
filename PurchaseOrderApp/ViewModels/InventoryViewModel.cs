using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace PurchaseOrderApp.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private const string QuickFilterAllKey = "all";
    private const string QuickFilterTrackingKey = "tracking";
    private const string QuickFilterGeneralKey = "general";
    private const string QuickFilterInStockKey = "in-stock";
    private const string QuickFilterOutOfStockKey = "out-of-stock";
    private const string CategoryFilterAllKey = "__all__";
    private const int LowStockThreshold = 2;

    private readonly InventoryService inventoryService = new();
    private int? editingInventoryItemId;
    private bool isRestoringFilters;

    public InventoryViewModel()
    {
        StatusMessage = "Loading inventory...";
        ItemCategory = "General";
        SelectedMovementType = InventoryTransactionTypes.StockIn;
        MovementQuantityText = "1";
        IsSidebarOpen = false;
        IsMovementPanelOpen = false;
        TrackingUnitStockInEntries = [];
        AvailableTrackingUnits = [];
    }

    public void Initialize()
    {
        LoadInventoryData();
    }

    public void ReloadInventory(int? preferredSelectedItemId = null)
    {
        LoadInventoryData(preferredSelectedItemId ?? SelectedItem?.InventoryItemId);
    }

    [ObservableProperty]
    private ObservableCollection<InventoryItemRow> inventoryItems = [];

    [ObservableProperty]
    private ObservableCollection<InventoryItemRow> filteredInventoryItems = [];

    [ObservableProperty]
    private ObservableCollection<InventoryTransactionHistoryItem> recentTransactions = [];

    [ObservableProperty]
    private ObservableCollection<JobCardInventoryOption> jobCardOptions = [];

    [ObservableProperty]
    private ObservableCollection<TrackingUnitIdentityEntryViewModel> trackingUnitStockInEntries = [];

    [ObservableProperty]
    private ObservableCollection<AvailableTrackingUnitRowViewModel> availableTrackingUnits = [];

    [ObservableProperty]
    private ObservableCollection<InventorySidebarFilterOption> quickFilters = [];

    [ObservableProperty]
    private ObservableCollection<InventorySidebarFilterOption> categoryFilters = [];

    [ObservableProperty]
    private bool isSidebarOpen;

    partial void OnIsSidebarOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarToggleButtonText));
    }

    [ObservableProperty]
    private bool isMovementPanelOpen;

    partial void OnIsMovementPanelOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(MovementPanelToggleButtonText));
    }

    [ObservableProperty]
    private InventorySidebarFilterOption? selectedQuickFilter;

    partial void OnSelectedQuickFilterChanged(InventorySidebarFilterOption? value)
    {
        if (isRestoringFilters)
        {
            return;
        }

        ApplyItemFilter(SelectedItem?.InventoryItemId);
        NotifyInventoryViewPropertiesChanged();
    }

    [ObservableProperty]
    private InventorySidebarFilterOption? selectedCategoryFilter;

    partial void OnSelectedCategoryFilterChanged(InventorySidebarFilterOption? value)
    {
        if (isRestoringFilters)
        {
            return;
        }

        ApplyItemFilter(SelectedItem?.InventoryItemId);
        NotifyInventoryViewPropertiesChanged();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveItemCommand))]
    private string itemCode = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveItemCommand))]
    private string itemName = string.Empty;

    [ObservableProperty]
    private string itemCategory = string.Empty;

    [ObservableProperty]
    private string itemDescription = string.Empty;

    [ObservableProperty]
    private bool itemIsTrackingUnit;

    [ObservableProperty]
    private InventoryItemRow? selectedItem;

    partial void OnSelectedItemChanged(InventoryItemRow? value)
    {
        PopulateItemEditor(value);
        LoadAvailableTrackingUnits();
        SyncTrackingUnitStockInEntries();
        OnPropertyChanged(nameof(SelectedItemDisplay));
        OnPropertyChanged(nameof(SelectedItemHelpText));
        OnPropertyChanged(nameof(RequiresJobCardSelection));
        OnPropertyChanged(nameof(JobCardRequirementText));
        OnPropertyChanged(nameof(ItemSaveButtonText));
        OnPropertyChanged(nameof(ShowTrackingUnitStockInSection));
        OnPropertyChanged(nameof(ShowTrackingUnitStockOutSection));
        OnPropertyChanged(nameof(TrackingUnitStockInHelpText));
        OnPropertyChanged(nameof(TrackingUnitStockOutHelpText));
        OnPropertyChanged(nameof(SelectedTrackingUnitCountText));
        OpenIssueOutPanelCommand.NotifyCanExecuteChanged();
        RecordMovementCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private string itemSearchText = string.Empty;

    partial void OnItemSearchTextChanged(string value)
    {
        ApplyItemFilter();
    }

    [ObservableProperty]
    private string selectedMovementType = InventoryTransactionTypes.StockIn;

    partial void OnSelectedMovementTypeChanged(string value)
    {
        SyncTrackingUnitStockInEntries();
        OnPropertyChanged(nameof(RequiresJobCardSelection));
        OnPropertyChanged(nameof(JobCardRequirementText));
        OnPropertyChanged(nameof(ShowTrackingUnitStockInSection));
        OnPropertyChanged(nameof(ShowTrackingUnitStockOutSection));
        OnPropertyChanged(nameof(TrackingUnitStockInHelpText));
        OnPropertyChanged(nameof(TrackingUnitStockOutHelpText));
        RecordMovementCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private string movementQuantityText = "1";

    partial void OnMovementQuantityTextChanged(string value)
    {
        SyncTrackingUnitStockInEntries();
        OnPropertyChanged(nameof(SelectedTrackingUnitCountText));
        RecordMovementCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private JobCardInventoryOption? selectedJobCard;

    partial void OnSelectedJobCardChanged(JobCardInventoryOption? value)
    {
        RecordMovementCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private string movementNotes = string.Empty;

    [ObservableProperty]
    private int inventoryItemCount;

    [ObservableProperty]
    private int visibleItemCount;

    [ObservableProperty]
    private int trackingItemCount;

    [ObservableProperty]
    private int totalStockOnHand;

    [ObservableProperty]
    private int visibleStockOnHand;

    [ObservableProperty]
    private int generalStockItemCount;

    [ObservableProperty]
    private int outOfStockItemCount;

    [ObservableProperty]
    private int recentTransactionCount;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public IReadOnlyList<string> MovementTypes => InventoryTransactionTypes.All;

    public string SidebarToggleButtonText => IsSidebarOpen ? "Hide Views" : "Show Views";

    public string MovementPanelToggleButtonText => IsMovementPanelOpen ? "Hide Movement" : "Stock Movement";

    public string ActiveInventoryViewTitle
    {
        get
        {
            var quickFilterLabel = SelectedQuickFilter?.DisplayName ?? "All Items";
            var categoryLabel = SelectedCategoryFilter?.DisplayName ?? "All Categories";

            return string.Equals(SelectedCategoryFilter?.Key, CategoryFilterAllKey, StringComparison.Ordinal)
                ? quickFilterLabel
                : $"{quickFilterLabel} / {categoryLabel}";
        }
    }

    public string ActiveInventoryViewSubtitle =>
        $"{VisibleItemCount} visible item(s) with {VisibleStockOnHand} unit(s) on hand in this view.";

    public string TopCategoryMetric => GetTopCategorySummary() is { } summary
        ? summary.Name
        : "No items in view";

    public string TopCategoryDetail => GetTopCategorySummary() is { } summary
        ? $"{summary.Count} line(s) in this category with {summary.Units} unit(s) on hand."
        : "Adjust your filters or receive stock to populate this view.";

    public string AttentionMetric
    {
        get
        {
            var outOfStockItems = GetOutOfStockVisibleItems();
            if (outOfStockItems.Count > 0)
            {
                return outOfStockItems.Count == 1 ? "1 out of stock" : $"{outOfStockItems.Count} out of stock";
            }

            var lowStockItems = GetLowStockVisibleItems();
            if (lowStockItems.Count > 0)
            {
                return lowStockItems.Count == 1 ? "1 low stock line" : $"{lowStockItems.Count} low stock lines";
            }

            return "No shortages";
        }
    }

    public string AttentionDetail
    {
        get
        {
            var outOfStockItems = GetOutOfStockVisibleItems();
            if (outOfStockItems.Count > 0)
            {
                return $"Out: {SummarizeItemLines(outOfStockItems)}";
            }

            var lowStockItems = GetLowStockVisibleItems();
            if (lowStockItems.Count > 0)
            {
                return $"Low: {SummarizeLowStockLines(lowStockItems)}";
            }

            return "All visible lines currently have stock on hand.";
        }
    }

    public string TopStockMetric => GetTopStockItem() is { } item
        ? item.ProductDisplay
        : "Nothing on hand";

    public string TopStockDetail => GetTopStockItem() is { } item
        ? $"Highest balance: {item.QuantityOnHand} unit(s) in {item.Category}."
        : "No visible line currently has available stock.";

    public string LatestMovementMetric => GetLatestMovement() is { } movement
        ? $"{movement.TransactionType} {movement.QuantityDisplay}"
        : "No movements yet";

    public string LatestMovementDetail
    {
        get
        {
            var movement = GetLatestMovement();
            if (movement is null)
            {
                return "Receive stock or issue stock to start movement history.";
            }

            return string.IsNullOrWhiteSpace(movement.JobCardNumber)
                ? $"{movement.ItemDisplay} • {movement.CreatedAtDisplay}"
                : $"{movement.ItemDisplay} • {movement.JobCardNumber} • {movement.CreatedAtDisplay}";
        }
    }

    public string SelectedItemDisplay => SelectedItem is null
        ? "No stock item selected yet."
        : $"{SelectedItem.ItemCode} - {SelectedItem.ItemName}";

    public string SelectedItemHelpText => SelectedItem is null
        ? "Pick an inventory row to receive more stock or issue stock. Use Receive Stock to add a brand-new item."
        : SelectedItem.IsTrackingUnit
            ? "This item is marked as a tracking unit. Any stock-out movement must be linked to a job card."
            : "This item can be topped up or issued without a mandatory job card link.";

    public bool RequiresJobCardSelection =>
        SelectedItem?.IsTrackingUnit == true &&
        string.Equals(SelectedMovementType, InventoryTransactionTypes.StockOut, StringComparison.OrdinalIgnoreCase);

    public string JobCardRequirementText => RequiresJobCardSelection
        ? "Tracking unit issue detected: choose the job card that received this stock."
        : "Job card link is optional unless you are issuing out a tracking unit.";

    public string ItemSaveButtonText => editingInventoryItemId.HasValue ? "Save Item Changes" : "Create Item";

    public bool ShowTrackingUnitStockInSection =>
        SelectedItem?.IsTrackingUnit == true &&
        string.Equals(SelectedMovementType, InventoryTransactionTypes.StockIn, StringComparison.OrdinalIgnoreCase);

    public bool ShowTrackingUnitStockOutSection =>
        SelectedItem?.IsTrackingUnit == true &&
        string.Equals(SelectedMovementType, InventoryTransactionTypes.StockOut, StringComparison.OrdinalIgnoreCase);

    public string TrackingUnitStockInHelpText =>
        ShowTrackingUnitStockInSection
            ? "Enter the serial number and IMEI for each tracking unit being received into this stock line."
            : string.Empty;

    public string TrackingUnitStockOutHelpText =>
        ShowTrackingUnitStockOutSection
            ? "Select the exact tracking units being issued to the chosen job card."
            : string.Empty;

    public string SelectedTrackingUnitCountText
    {
        get
        {
            var selectedCount = AvailableTrackingUnits.Count(unit => unit.IsSelected);
            return selectedCount == 1
                ? "1 tracking unit selected"
                : $"{selectedCount} tracking units selected";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveItem))]
    private void SaveItem()
    {
        try
        {
            var isEditing = editingInventoryItemId.HasValue;
            var savedItem = inventoryService.SaveItem(
                new InventoryService.SaveInventoryItemRequest(
                    editingInventoryItemId,
                    ItemCode,
                    ItemName,
                    ItemCategory,
                    ItemDescription,
                    ItemIsTrackingUnit));

            LoadInventoryData(savedItem.InventoryItemId);
            StatusMessage = isEditing
                ? $"Saved changes to {savedItem.ItemCode}."
                : $"Created inventory item {savedItem.ItemCode}. Use Stock In to load the opening quantity.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private bool CanSaveItem()
    {
        return !string.IsNullOrWhiteSpace(ItemCode) &&
               !string.IsNullOrWhiteSpace(ItemName);
    }

    [RelayCommand]
    private void ClearItemEditor()
    {
        editingInventoryItemId = null;
        ItemCode = string.Empty;
        ItemName = string.Empty;
        ItemCategory = "General";
        ItemDescription = string.Empty;
        ItemIsTrackingUnit = false;
        SelectedItem = null;
        OnPropertyChanged(nameof(ItemSaveButtonText));
        SaveItemCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRecordMovement))]
    private void RecordMovement()
    {
        if (SelectedItem is null)
        {
            StatusMessage = "Select an inventory item first.";
            return;
        }

        if (!int.TryParse(MovementQuantityText, out var quantity) || quantity <= 0)
        {
            StatusMessage = "Enter a whole-number quantity greater than zero.";
            return;
        }

        if (RequiresJobCardSelection && SelectedJobCard is null)
        {
            StatusMessage = "Choose the job card that received this tracking unit.";
            return;
        }

        try
        {
            var movement = inventoryService.RecordTransaction(
                new InventoryService.RecordInventoryTransactionRequest(
                    SelectedItem.InventoryItemId,
                    SelectedMovementType,
                    quantity,
                    SelectedJobCard?.JobCardRecordId,
                    MovementNotes,
                    BuildTrackingUnitStockInInputs(),
                    BuildSelectedTrackingUnitIds()));

            var itemName = string.IsNullOrWhiteSpace(movement.ItemNameSnapshot)
                ? movement.ItemCodeSnapshot
                : $"{movement.ItemCodeSnapshot} - {movement.ItemNameSnapshot}";

            LoadInventoryData(SelectedItem.InventoryItemId);
            MovementQuantityText = "1";
            MovementNotes = string.Empty;
            TrackingUnitStockInEntries = [];

            if (!RequiresJobCardSelection)
            {
                SelectedJobCard = null;
            }

            StatusMessage = string.IsNullOrWhiteSpace(movement.JobCardNumber)
                ? $"{BuildMovementStatusPrefix(movement)} recorded for {itemName}. New on-hand balance: {movement.QuantityAfterTransaction}."
                : $"{BuildMovementStatusPrefix(movement)} recorded for {itemName} and linked to {movement.JobCardNumber}. New on-hand balance: {movement.QuantityAfterTransaction}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private bool CanRecordMovement()
    {
        if (SelectedItem is null)
        {
            return false;
        }

        if (!int.TryParse(MovementQuantityText, out var quantity) || quantity <= 0)
        {
            return false;
        }

        if (ShowTrackingUnitStockInSection &&
            !HasCompleteTrackingUnitStockInEntries(quantity))
        {
            return false;
        }

        if (ShowTrackingUnitStockOutSection &&
            AvailableTrackingUnits.Count(unit => unit.IsSelected) != quantity)
        {
            return false;
        }

        return !RequiresJobCardSelection || SelectedJobCard is not null;
    }

    [RelayCommand]
    private void RefreshInventory()
    {
        LoadInventoryData(SelectedItem?.InventoryItemId);
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    private void CloseSidebar()
    {
        IsSidebarOpen = false;
    }

    [RelayCommand]
    private void ToggleMovementPanel()
    {
        IsMovementPanelOpen = !IsMovementPanelOpen;
    }

    [RelayCommand(CanExecute = nameof(CanOpenIssueOutPanel))]
    private void OpenIssueOutPanel()
    {
        if (SelectedItem is null)
        {
            StatusMessage = "Select an inventory row first, then use Issue Out.";
            return;
        }

        SelectedMovementType = InventoryTransactionTypes.StockOut;
        IsMovementPanelOpen = true;

        StatusMessage = SelectedItem.IsTrackingUnit
            ? $"Issue Out is ready for {SelectedItem.ItemCode}. Choose the job card and select the exact tracking units to issue."
            : $"Issue Out is ready for {SelectedItem.ItemCode}. Enter the quantity you want to issue from stock.";
    }

    private bool CanOpenIssueOutPanel()
    {
        return SelectedItem is not null;
    }

    [RelayCommand]
    private void CloseMovementPanel()
    {
        IsMovementPanelOpen = false;
    }

    private void LoadInventoryData(int? preferredSelectedItemId = null)
    {
        try
        {
            inventoryService.EnsureSchema();

            var items = inventoryService.GetItems()
                .Select(item => new InventoryItemRow
                {
                    InventoryItemId = item.InventoryItemId,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Category = NormalizeCategory(item.Category),
                    Description = item.Description ?? string.Empty,
                    IsTrackingUnit = item.IsTrackingUnit,
                    QuantityOnHand = item.QuantityOnHand,
                    UpdatedAt = item.UpdatedAt
                })
                .ToList();

            var transactions = inventoryService.GetRecentTransactions()
                .Select(transaction => new InventoryTransactionHistoryItem
                {
                    InventoryTransactionId = transaction.InventoryTransactionId,
                    TransactionType = transaction.TransactionType,
                    IssueOutNumber = transaction.IssueOutNumber ?? string.Empty,
                    ItemCode = transaction.ItemCodeSnapshot,
                    ItemName = transaction.ItemNameSnapshot,
                    Quantity = transaction.Quantity,
                    QuantityAfterTransaction = transaction.QuantityAfterTransaction,
                    CreatedAt = transaction.CreatedAt,
                    JobCardNumber = transaction.JobCardNumber ?? string.Empty,
                    Notes = transaction.Notes ?? string.Empty
                })
                .ToList();

            var jobCards = inventoryService.GetJobCardOptions()
                .Select(jobCard => new JobCardInventoryOption
                {
                    JobCardRecordId = jobCard.JobCardRecordId,
                    JobCardNumber = jobCard.JobCardNumber,
                    VehicleDisplay = jobCard.VehicleDisplay,
                    Client = jobCard.Client,
                    WorkflowStatus = jobCard.WorkflowStatus
                })
                .ToList();

            InventoryItems = new ObservableCollection<InventoryItemRow>(items);
            RecentTransactions = new ObservableCollection<InventoryTransactionHistoryItem>(transactions);
            JobCardOptions = new ObservableCollection<JobCardInventoryOption>(jobCards);

            InventoryItemCount = items.Count;
            TrackingItemCount = items.Count(item => item.IsTrackingUnit);
            GeneralStockItemCount = items.Count(item => !item.IsTrackingUnit);
            OutOfStockItemCount = items.Count(item => item.QuantityOnHand <= 0);
            TotalStockOnHand = items.Sum(item => item.QuantityOnHand);
            RecentTransactionCount = transactions.Count;

            RestoreSidebarFilters(items);
            ApplyItemFilter(preferredSelectedItemId ?? SelectedItem?.InventoryItemId);
            NotifyInventoryViewPropertiesChanged();

            StatusMessage = items.Count == 0
                ? "Use Receive Stock to create the first inventory item and book it into stock."
                : $"Loaded {items.Count} inventory item(s) and {transactions.Count} recent movement(s).";
        }
        catch (Exception ex)
        {
            InventoryItems = [];
            FilteredInventoryItems = [];
            RecentTransactions = [];
            JobCardOptions = [];
            TrackingUnitStockInEntries = [];
            AvailableTrackingUnits = [];
            QuickFilters = [];
            CategoryFilters = [];
            SelectedQuickFilter = null;
            SelectedCategoryFilter = null;
            StatusMessage = $"I couldn't load the inventory data: {ex.Message}";
        }
    }

    private void ApplyItemFilter(int? preferredSelectedItemId = null)
    {
        var searchTerms = (ItemSearchText ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var filteredItems = InventoryItems
            .Where(MatchesQuickFilter)
            .Where(MatchesCategoryFilter)
            .Where(item => MatchesItemSearch(item, searchTerms))
            .OrderBy(item => item.ItemName)
            .ThenBy(item => item.ItemCode)
            .ToList();

        FilteredInventoryItems = new ObservableCollection<InventoryItemRow>(filteredItems);
        VisibleItemCount = filteredItems.Count;
        VisibleStockOnHand = filteredItems.Sum(item => item.QuantityOnHand);

        SelectedItem = preferredSelectedItemId.HasValue
            ? FilteredInventoryItems.FirstOrDefault(item => item.InventoryItemId == preferredSelectedItemId.Value)
            : FilteredInventoryItems.FirstOrDefault();

        NotifyInventoryViewPropertiesChanged();
    }

    private void PopulateItemEditor(InventoryItemRow? item)
    {
        if (item is null)
        {
            editingInventoryItemId = null;
            ItemCode = string.Empty;
            ItemName = string.Empty;
            ItemCategory = "General";
            ItemDescription = string.Empty;
            ItemIsTrackingUnit = false;
        }
        else
        {
            editingInventoryItemId = item.InventoryItemId;
            ItemCode = item.ItemCode;
            ItemName = item.ItemName;
            ItemCategory = string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category;
            ItemDescription = item.Description;
            ItemIsTrackingUnit = item.IsTrackingUnit;
        }

        OnPropertyChanged(nameof(ItemSaveButtonText));
        SaveItemCommand.NotifyCanExecuteChanged();
    }

    private static bool MatchesItemSearch(InventoryItemRow item, string[] searchTerms)
    {
        if (searchTerms.Length == 0)
        {
            return true;
        }

        var searchableText = string.Join(" ", new[]
        {
            item.ItemCode,
            item.ItemName,
            item.Category,
            item.Description,
            item.TrackingTypeDisplay
        }).ToUpperInvariant();

        return searchTerms.All(term => searchableText.Contains(term.ToUpperInvariant(), StringComparison.Ordinal));
    }

    private void RestoreSidebarFilters(IReadOnlyCollection<InventoryItemRow> items)
    {
        var previousQuickFilterKey = SelectedQuickFilter?.Key ?? QuickFilterAllKey;
        var previousCategoryFilterKey = SelectedCategoryFilter?.Key ?? CategoryFilterAllKey;

        QuickFilters = new ObservableCollection<InventorySidebarFilterOption>(BuildQuickFilters(items));
        CategoryFilters = new ObservableCollection<InventorySidebarFilterOption>(BuildCategoryFilters(items));

        isRestoringFilters = true;
        SelectedQuickFilter = QuickFilters.FirstOrDefault(filter => string.Equals(filter.Key, previousQuickFilterKey, StringComparison.Ordinal))
            ?? QuickFilters.FirstOrDefault();
        SelectedCategoryFilter = CategoryFilters.FirstOrDefault(filter => string.Equals(filter.Key, previousCategoryFilterKey, StringComparison.Ordinal))
            ?? CategoryFilters.FirstOrDefault();
        isRestoringFilters = false;
    }

    private IEnumerable<InventorySidebarFilterOption> BuildQuickFilters(IEnumerable<InventoryItemRow> items)
    {
        var materializedItems = items.ToList();

        return
        [
            new InventorySidebarFilterOption
            {
                Key = QuickFilterAllKey,
                DisplayName = "All Items",
                Count = materializedItems.Count
            },
            new InventorySidebarFilterOption
            {
                Key = QuickFilterTrackingKey,
                DisplayName = "Tracking Units",
                Count = materializedItems.Count(item => item.IsTrackingUnit)
            },
            new InventorySidebarFilterOption
            {
                Key = QuickFilterGeneralKey,
                DisplayName = "General Stock",
                Count = materializedItems.Count(item => !item.IsTrackingUnit)
            },
            new InventorySidebarFilterOption
            {
                Key = QuickFilterInStockKey,
                DisplayName = "In Stock",
                Count = materializedItems.Count(item => item.QuantityOnHand > 0)
            },
            new InventorySidebarFilterOption
            {
                Key = QuickFilterOutOfStockKey,
                DisplayName = "Out of Stock",
                Count = materializedItems.Count(item => item.QuantityOnHand <= 0)
            }
        ];
    }

    private IEnumerable<InventorySidebarFilterOption> BuildCategoryFilters(IEnumerable<InventoryItemRow> items)
    {
        var categoryGroups = items
            .GroupBy(item => NormalizeCategory(item.Category))
            .OrderBy(group => group.Key)
            .Select(group => new InventorySidebarFilterOption
            {
                Key = group.Key,
                DisplayName = group.Key,
                Count = group.Count()
            });

        return
        [
            new InventorySidebarFilterOption
            {
                Key = CategoryFilterAllKey,
                DisplayName = "All Categories",
                Count = items.Count()
            },
            .. categoryGroups
        ];
    }

    private bool MatchesQuickFilter(InventoryItemRow item)
    {
        return SelectedQuickFilter?.Key switch
        {
            QuickFilterTrackingKey => item.IsTrackingUnit,
            QuickFilterGeneralKey => !item.IsTrackingUnit,
            QuickFilterInStockKey => item.QuantityOnHand > 0,
            QuickFilterOutOfStockKey => item.QuantityOnHand <= 0,
            _ => true
        };
    }

    private bool MatchesCategoryFilter(InventoryItemRow item)
    {
        return SelectedCategoryFilter?.Key switch
        {
            null => true,
            CategoryFilterAllKey => true,
            var selectedCategory => string.Equals(
                NormalizeCategory(item.Category),
                selectedCategory,
                StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string NormalizeCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category)
            ? "General"
            : category.Trim();
    }

    private void NotifyInventoryViewPropertiesChanged()
    {
        OnPropertyChanged(nameof(ActiveInventoryViewTitle));
        OnPropertyChanged(nameof(ActiveInventoryViewSubtitle));
        OnPropertyChanged(nameof(TopCategoryMetric));
        OnPropertyChanged(nameof(TopCategoryDetail));
        OnPropertyChanged(nameof(AttentionMetric));
        OnPropertyChanged(nameof(AttentionDetail));
        OnPropertyChanged(nameof(TopStockMetric));
        OnPropertyChanged(nameof(TopStockDetail));
        OnPropertyChanged(nameof(LatestMovementMetric));
        OnPropertyChanged(nameof(LatestMovementDetail));
        OnPropertyChanged(nameof(ShowTrackingUnitStockInSection));
        OnPropertyChanged(nameof(ShowTrackingUnitStockOutSection));
        OnPropertyChanged(nameof(TrackingUnitStockInHelpText));
        OnPropertyChanged(nameof(TrackingUnitStockOutHelpText));
        OnPropertyChanged(nameof(SelectedTrackingUnitCountText));
    }

    private void LoadAvailableTrackingUnits()
    {
        UnwireTrackingUnitSelectionHandlers();

        if (SelectedItem?.IsTrackingUnit != true)
        {
            AvailableTrackingUnits = [];
            return;
        }

        var availableUnits = inventoryService.GetAvailableTrackingUnits(SelectedItem.InventoryItemId)
            .Select(unit => new AvailableTrackingUnitRowViewModel
            {
                InventoryTrackingUnitId = unit.InventoryTrackingUnitId,
                SerialNumber = unit.SerialNumber,
                ImeiNumber = unit.ImeiNumber,
                CreatedAt = unit.CreatedAt
            })
            .ToList();

        AvailableTrackingUnits = new ObservableCollection<AvailableTrackingUnitRowViewModel>(availableUnits);
        WireTrackingUnitSelectionHandlers();
    }

    private void SyncTrackingUnitStockInEntries()
    {
        if (!ShowTrackingUnitStockInSection)
        {
            TrackingUnitStockInEntries = [];
            return;
        }

        if (!int.TryParse(MovementQuantityText, out var quantity) || quantity <= 0)
        {
            TrackingUnitStockInEntries = [];
            return;
        }

        var existingEntries = TrackingUnitStockInEntries.ToList();
        var synchronizedEntries = new List<TrackingUnitIdentityEntryViewModel>();

        for (var index = 0; index < quantity; index++)
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

        TrackingUnitStockInEntries = new ObservableCollection<TrackingUnitIdentityEntryViewModel>(synchronizedEntries);
    }

    private IReadOnlyList<InventoryService.TrackingUnitIdentityInput>? BuildTrackingUnitStockInInputs()
    {
        if (!ShowTrackingUnitStockInSection)
        {
            return null;
        }

        return TrackingUnitStockInEntries
            .Select(entry => new InventoryService.TrackingUnitIdentityInput(entry.SerialNumber, entry.ImeiNumber))
            .ToList();
    }

    private IReadOnlyList<int>? BuildSelectedTrackingUnitIds()
    {
        if (!ShowTrackingUnitStockOutSection)
        {
            return null;
        }

        return AvailableTrackingUnits
            .Where(unit => unit.IsSelected)
            .Select(unit => unit.InventoryTrackingUnitId)
            .ToList();
    }

    private bool HasCompleteTrackingUnitStockInEntries(int expectedQuantity)
    {
        return TrackingUnitStockInEntries.Count == expectedQuantity &&
               TrackingUnitStockInEntries.All(entry =>
                   !string.IsNullOrWhiteSpace(entry.SerialNumber) &&
                   !string.IsNullOrWhiteSpace(entry.ImeiNumber));
    }

    private void WireTrackingUnitSelectionHandlers()
    {
        foreach (var unit in AvailableTrackingUnits)
        {
            unit.PropertyChanged += OnAvailableTrackingUnitPropertyChanged;
        }
    }

    private void UnwireTrackingUnitSelectionHandlers()
    {
        foreach (var unit in AvailableTrackingUnits)
        {
            unit.PropertyChanged -= OnAvailableTrackingUnitPropertyChanged;
        }
    }

    private void OnAvailableTrackingUnitPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AvailableTrackingUnitRowViewModel.IsSelected), StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedTrackingUnitCountText));
        RecordMovementCommand.NotifyCanExecuteChanged();
    }

    private (string Name, int Count, int Units)? GetTopCategorySummary()
    {
        var summary = FilteredInventoryItems
            .GroupBy(item => NormalizeCategory(item.Category))
            .Select(group => (Name: group.Key, Count: group.Count(), Units: group.Sum(item => item.QuantityOnHand)))
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.Units)
            .ThenBy(group => group.Name)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(summary.Name) ? null : summary;
    }

    private List<InventoryItemRow> GetOutOfStockVisibleItems()
    {
        return FilteredInventoryItems
            .Where(item => item.QuantityOnHand <= 0)
            .OrderBy(item => item.ItemName)
            .ThenBy(item => item.ItemCode)
            .ToList();
    }

    private List<InventoryItemRow> GetLowStockVisibleItems()
    {
        return FilteredInventoryItems
            .Where(item => item.QuantityOnHand > 0 && item.QuantityOnHand <= LowStockThreshold)
            .OrderBy(item => item.QuantityOnHand)
            .ThenBy(item => item.ItemName)
            .ThenBy(item => item.ItemCode)
            .ToList();
    }

    private InventoryItemRow? GetTopStockItem()
    {
        return FilteredInventoryItems
            .Where(item => item.QuantityOnHand > 0)
            .OrderByDescending(item => item.QuantityOnHand)
            .ThenBy(item => item.ItemName)
            .ThenBy(item => item.ItemCode)
            .FirstOrDefault();
    }

    private InventoryTransactionHistoryItem? GetLatestMovement()
    {
        return RecentTransactions
            .OrderByDescending(transaction => transaction.CreatedAt)
            .FirstOrDefault();
    }

    private static string SummarizeItemLines(IEnumerable<InventoryItemRow> items)
    {
        var visibleItems = items.Take(2)
            .Select(item => item.ProductDisplay)
            .ToList();

        return string.Join(", ", visibleItems);
    }

    private static string SummarizeLowStockLines(IEnumerable<InventoryItemRow> items)
    {
        var visibleItems = items.Take(2)
            .Select(item => $"{item.ItemCode} ({item.QuantityOnHand})")
            .ToList();

        return string.Join(", ", visibleItems);
    }

    private static string BuildMovementStatusPrefix(Models.InventoryTransaction movement)
    {
        return string.IsNullOrWhiteSpace(movement.IssueOutNumber)
            ? movement.TransactionType
            : $"{movement.TransactionType} {movement.IssueOutNumber}";
    }
}
