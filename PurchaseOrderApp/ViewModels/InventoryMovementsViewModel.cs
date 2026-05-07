using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;
using System.Collections.ObjectModel;

namespace PurchaseOrderApp.ViewModels;

public partial class InventoryMovementsViewModel : ObservableObject
{
    private readonly InventoryService inventoryService = new();

    public InventoryMovementsViewModel()
    {
        StatusMessage = "Loading recent stock movements...";
    }

    public void Initialize()
    {
        LoadTransactions();
    }

    [ObservableProperty]
    private ObservableCollection<InventoryTransactionHistoryItem> recentTransactions = [];

    [ObservableProperty]
    private ObservableCollection<InventoryTransactionHistoryItem> filteredTransactions = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [ObservableProperty]
    private int totalTransactionCount;

    [ObservableProperty]
    private int visibleTransactionCount;

    [ObservableProperty]
    private int linkedJobCardCount;

    [ObservableProperty]
    private int stockOutCount;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [RelayCommand]
    private void RefreshHistory()
    {
        LoadTransactions();
    }

    private void LoadTransactions()
    {
        try
        {
            inventoryService.EnsureSchema();

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

            RecentTransactions = new ObservableCollection<InventoryTransactionHistoryItem>(transactions);
            TotalTransactionCount = transactions.Count;
            LinkedJobCardCount = transactions.Count(item => !string.IsNullOrWhiteSpace(item.JobCardNumber));
            StockOutCount = transactions.Count(item =>
                string.Equals(item.TransactionType, InventoryTransactionTypes.StockOut, StringComparison.OrdinalIgnoreCase));

            ApplyFilter();
            StatusMessage = transactions.Count == 0
                ? "No stock movement has been recorded yet."
                : $"Loaded {transactions.Count} recent stock movement entr{(transactions.Count == 1 ? "y" : "ies")}.";
        }
        catch (Exception ex)
        {
            RecentTransactions = [];
            FilteredTransactions = [];
            TotalTransactionCount = 0;
            VisibleTransactionCount = 0;
            LinkedJobCardCount = 0;
            StockOutCount = 0;
            StatusMessage = $"I couldn't load the recent stock movements: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var searchTerms = (SearchText ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var filteredItems = RecentTransactions
            .Where(item => MatchesSearch(item, searchTerms))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.InventoryTransactionId)
            .ToList();

        FilteredTransactions = new ObservableCollection<InventoryTransactionHistoryItem>(filteredItems);
        VisibleTransactionCount = filteredItems.Count;
    }

    private static bool MatchesSearch(InventoryTransactionHistoryItem item, string[] searchTerms)
    {
        if (searchTerms.Length == 0)
        {
            return true;
        }

        var searchableText = string.Join(" ", new[]
        {
            item.CreatedAtDisplay,
            item.TransactionType,
            item.IssueOutNumber,
            item.ItemCode,
            item.ItemName,
            item.ItemDisplay,
            item.JobCardDisplay,
            item.Notes
        }).ToUpperInvariant();

        return searchTerms.All(term => searchableText.Contains(term.ToUpperInvariant(), StringComparison.Ordinal));
    }
}
