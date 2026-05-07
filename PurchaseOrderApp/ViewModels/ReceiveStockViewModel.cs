using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PurchaseOrderApp.ViewModels;

public partial class ReceiveStockViewModel : ObservableObject
{
    private readonly InventoryService inventoryService = new();

    private static readonly ReceiveStockTemplateOption[] StockTemplatesSource =
    [
        new()
        {
            DisplayText = "General Stock",
            ItemName = string.Empty,
            Category = "General",
            Description = "Use this when the stock does not fit one of the focused tracking or PC inventory groups."
        },
        new()
        {
            DisplayText = "Battery",
            ItemName = "Battery",
            Category = "Power & Batteries",
            Description = "Batteries and power-related stock used for tracking units, laptops, or workshop support."
        },
        new()
        {
            DisplayText = "Motherboard / PC Component",
            ItemName = "PC Component",
            Category = "PC Components",
            Description = "Motherboards and internal PC components such as RAM, SSDs, power supplies, or expansion cards."
        },
        new()
        {
            DisplayText = "Laptop",
            ItemName = "Laptop",
            Category = "Computers & Laptops",
            Description = "Laptops and portable computing devices."
        },
        new()
        {
            DisplayText = "Screwdriver / Tool",
            ItemName = "Tool",
            Category = "Tools & Workshop",
            Description = "Screwdrivers, workshop tools, and technician equipment."
        },
        new()
        {
            DisplayText = "Teltonika Tracking Unit",
            ItemName = "Teltonika Tracking Unit",
            Category = "Tracking Hardware",
            Description = "Teltonika tracking units installed into vehicles or mobile assets.",
            IsTrackingUnit = true
        },
        new()
        {
            DisplayText = "Queclink Tracking Unit",
            ItemName = "Queclink Tracking Unit",
            Category = "Tracking Hardware",
            Description = "Queclink tracking units installed into vehicles or mobile assets.",
            IsTrackingUnit = true
        },
        new()
        {
            DisplayText = "Custom Portable Tracking Unit",
            ItemName = "Custom Portable Tracking Unit",
            Category = "Tracking Hardware",
            Description = "Portable tracking units or custom mobile asset trackers.",
            IsTrackingUnit = true
        },
        new()
        {
            DisplayText = "Dashcam",
            ItemName = "Dashcam",
            Category = "Tracking Hardware",
            Description = "Vehicle dashcams and camera tracking accessories."
        },
        new()
        {
            DisplayText = "Panic Button",
            ItemName = "Panic Button",
            Category = "Tracking Accessories",
            Description = "Panic buttons and alert-trigger accessories used with tracking installations."
        },
        new()
        {
            DisplayText = "Relay",
            ItemName = "Relay",
            Category = "Tracking Accessories",
            Description = "Relays used in tracking, immobiliser, or vehicle control installations."
        },
        new()
        {
            DisplayText = "Receiver",
            ItemName = "Receiver",
            Category = "Tracking Accessories",
            Description = "Receivers and related signal or control accessories."
        },
        new()
        {
            DisplayText = "PC Case",
            ItemName = "PC Case",
            Category = "PC Components",
            Description = "Desktop PC cases and chassis hardware."
        },
        new()
        {
            DisplayText = "Monitor",
            ItemName = "Monitor",
            Category = "Monitors & Displays",
            Description = "Computer monitors and display units."
        },
        new()
        {
            DisplayText = "Monitor Accessory / Cable",
            ItemName = "Monitor Accessory",
            Category = "Accessories & Cables",
            Description = "Monitor stands, HDMI, DVI, VGA cables, adapters, and video converters."
        }
    ];

    private static readonly string[] CategoryOptionsSource =
    [
        "General",
        "Tracking Hardware",
        "Tracking Accessories",
        "Power & Batteries",
        "PC Components",
        "Computers & Laptops",
        "Tools & Workshop",
        "Monitors & Displays",
        "Accessories & Cables"
    ];

    public ReceiveStockViewModel()
    {
        StockTemplates = new ObservableCollection<ReceiveStockTemplateOption>(StockTemplatesSource);
        CategoryOptions = new ObservableCollection<string>(CategoryOptionsSource);
        SupplierSuggestions = new ObservableCollection<string>(inventoryService.GetSupplierSuggestions());
        PurchaseOrderOptions = new ObservableCollection<ReceiptPurchaseOrderOptionViewModel>(
            inventoryService.GetPurchaseOrderOptions()
                .Select(order => new ReceiptPurchaseOrderOptionViewModel
                {
                    PurchaseOrderId = order.PurchaseOrderId,
                    OrderNumber = order.OrderNumber,
                    SupplierName = order.SupplierName,
                    Date = order.Date,
                    Reference = order.Reference
                }));

        ReceiptLines.CollectionChanged += OnReceiptLinesCollectionChanged;
        ReceiptNumberPreview = inventoryService.GetNextReceiptNumber();
        AddLine();
        StatusMessage = "Create one receipt with as many supplier or item lines as you need.";
    }

    public int? ReceivedInventoryItemId { get; private set; }

    [ObservableProperty]
    private ObservableCollection<ReceiveStockTemplateOption> stockTemplates = [];

    [ObservableProperty]
    private ObservableCollection<string> categoryOptions = [];

    [ObservableProperty]
    private ObservableCollection<string> supplierSuggestions = [];

    [ObservableProperty]
    private ObservableCollection<ReceiptPurchaseOrderOptionViewModel> purchaseOrderOptions = [];

    [ObservableProperty]
    private ObservableCollection<ReceiveStockLineViewModel> receiptLines = [];

    [ObservableProperty]
    private ReceiveStockLineViewModel? selectedLine;

    partial void OnSelectedLineChanged(ReceiveStockLineViewModel? value)
    {
        RemoveSelectedLineCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedLineDisplay));
    }

    [ObservableProperty]
    private string receiptNumberPreview = string.Empty;

    [ObservableProperty]
    private string receiptNotes = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string InventoryFocusText =>
        "Receive multiple stock lines at once, split by supplier if needed, and optionally link each line back to a purchase order.";

    public string ReceiptSummaryText =>
        $"{ReceiptLines.Count} line(s) / {TotalQuantityReceived} unit(s) queued on this receipt.";

    public int TotalQuantityReceived => ReceiptLines.Sum(line => line.QuantityValue);

    public string SelectedLineDisplay => SelectedLine?.LineLabel ?? "No receipt line selected.";

    public bool TryReceiveStock()
    {
        if (ReceiptLines.Count == 0)
        {
            StatusMessage = "Add at least one receipt line first.";
            return false;
        }

        try
        {
            var result = inventoryService.ReceiveReceipt(
                new InventoryService.ReceiveInventoryReceiptRequest(
                    ReceiptNotes,
                    ReceiptLines.Select(line => line.BuildRequest()).ToList()));

            ReceivedInventoryItemId = result.PreferredInventoryItemId;
            StatusMessage = $"Created receipt {result.ReceiptNumber} with {result.LineCount} line(s) and {result.TotalQuantity} unit(s).";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private void AddLine()
    {
        var newLine = new ReceiveStockLineViewModel(
            ReceiptLines.Count + 1,
            StockTemplates,
            CategoryOptions,
            SupplierSuggestions,
            PurchaseOrderOptions);

        ReceiptLines.Add(newLine);
        SelectedLine = newLine;
        StatusMessage = $"Added {newLine.LineLabel}.";
        NotifyReceiptSummaryChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedLine))]
    private void RemoveSelectedLine()
    {
        if (SelectedLine is null)
        {
            return;
        }

        var removedLineNumber = SelectedLine.LineNumber;
        ReceiptLines.Remove(SelectedLine);

        if (ReceiptLines.Count == 0)
        {
            AddLine();
            StatusMessage = "The last line was removed, so a new blank receipt line has been started.";
            return;
        }

        RenumberLines();
        SelectedLine = ReceiptLines[Math.Min(removedLineNumber - 1, ReceiptLines.Count - 1)];
        StatusMessage = $"Removed line {removedLineNumber}.";
        NotifyReceiptSummaryChanged();
    }

    private bool CanRemoveSelectedLine()
    {
        return SelectedLine is not null;
    }

    private void OnReceiptLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ReceiveStockLineViewModel line in e.OldItems)
            {
                line.PropertyChanged -= OnReceiptLinePropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ReceiveStockLineViewModel line in e.NewItems)
            {
                line.PropertyChanged += OnReceiptLinePropertyChanged;
            }
        }

        RemoveSelectedLineCommand.NotifyCanExecuteChanged();
        NotifyReceiptSummaryChanged();
    }

    private void OnReceiptLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReceiveStockLineViewModel.QuantityText))
        {
            NotifyReceiptSummaryChanged();
        }
    }

    private void RenumberLines()
    {
        for (var index = 0; index < ReceiptLines.Count; index++)
        {
            ReceiptLines[index].Renumber(index + 1);
        }
    }

    private void NotifyReceiptSummaryChanged()
    {
        OnPropertyChanged(nameof(TotalQuantityReceived));
        OnPropertyChanged(nameof(ReceiptSummaryText));
    }
}
