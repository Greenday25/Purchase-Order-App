using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Data;
using PurchaseOrderApp.Models;
using System.Data;

namespace PurchaseOrderApp.Services;

internal sealed class InventoryService
{
    private const string ReceiptNumberPrefix = "RCV";
    private const int ReceiptNumberDigits = 5;
    private const int ReceiptNumberStartingValue = 1;
    private const string IssueOutNumberPrefix = "ISS";
    private const int IssueOutNumberDigits = 5;
    private const int IssueOutNumberStartingValue = 1;

    internal sealed record TrackingUnitIdentityInput(
        string SerialNumber,
        string ImeiNumber);

    internal sealed record SaveInventoryItemRequest(
        int? InventoryItemId,
        string ItemCode,
        string ItemName,
        string Category,
        string? Description,
        bool IsTrackingUnit);

    internal sealed record RecordInventoryTransactionRequest(
        int InventoryItemId,
        string TransactionType,
        int Quantity,
        int? JobCardRecordId,
        string? Notes,
        IReadOnlyList<TrackingUnitIdentityInput>? ReceivedTrackingUnits = null,
        IReadOnlyList<int>? IssuedTrackingUnitIds = null);

    internal sealed record ReceiveNewInventoryItemRequest(
        string ItemCode,
        string ItemName,
        string Category,
        string? Description,
        bool IsTrackingUnit,
        int Quantity,
        string? Notes,
        IReadOnlyList<TrackingUnitIdentityInput>? TrackingUnits = null);

    internal sealed record ReceiveNewInventoryItemResult(
        int InventoryItemId,
        string ItemCode,
        string ItemName,
        int QuantityReceived,
        int QuantityOnHand);

    internal sealed record ReceiveInventoryReceiptRequest(
        string? Notes,
        IReadOnlyList<ReceiveInventoryReceiptLineRequest> Lines);

    internal sealed record ReceiveInventoryReceiptLineRequest(
        string ItemCode,
        string ItemName,
        string Category,
        string? Description,
        bool IsTrackingUnit,
        int Quantity,
        string? SupplierName,
        int? PurchaseOrderId,
        string? Notes,
        IReadOnlyList<TrackingUnitIdentityInput>? TrackingUnits = null);

    internal sealed record ReceiveInventoryReceiptResult(
        int InventoryReceiptId,
        string ReceiptNumber,
        int LineCount,
        int TotalQuantity,
        int? PreferredInventoryItemId);

    internal sealed record InventoryReceiptPurchaseOrderOption(
        int PurchaseOrderId,
        string OrderNumber,
        string SupplierName,
        DateTime Date,
        string Reference);

    internal sealed record PurchaseOrderReceiptLineSummary(
        string ReceiptNumber,
        DateTime ReceivedAt,
        string SupplierName,
        string ItemCode,
        string ItemName,
        int Quantity,
        string? Notes);

    internal sealed record InventoryJobCardOption(
        int JobCardRecordId,
        string JobCardNumber,
        string VehicleDisplay,
        string Client,
        string WorkflowStatus);

    internal sealed record AvailableTrackingUnitRecord(
        int InventoryTrackingUnitId,
        string SerialNumber,
        string ImeiNumber,
        DateTime CreatedAt);

    internal sealed record JobCardInventorySummary(
        int JobCardRecordId,
        int IssueCount,
        int TotalQuantity,
        string DisplayText);

    internal sealed record JobCardInventoryIssue(
        int InventoryTransactionId,
        DateTime CreatedAt,
        string? IssueOutNumber,
        string ItemCode,
        string ItemName,
        int Quantity,
        string? Notes);

    internal void EnsureSchema()
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);
    }

    internal IReadOnlyList<InventoryItem> GetItems()
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.InventoryItems
            .AsNoTracking()
            .OrderBy(item => item.ItemName)
            .ThenBy(item => item.ItemCode)
            .ToList();
    }

    internal IReadOnlyList<InventoryTransaction> GetRecentTransactions(int maxResults = 120)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.InventoryTransactions
            .AsNoTracking()
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.InventoryTransactionId)
            .Take(Math.Max(1, maxResults))
            .ToList();
    }

    internal IReadOnlyList<InventoryJobCardOption> GetJobCardOptions(int maxResults = 250)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.JobCards
            .AsNoTracking()
            .OrderByDescending(record => record.SequenceNumber)
            .Take(Math.Max(1, maxResults))
            .Select(record => new InventoryJobCardOption(
                record.JobCardRecordId,
                record.JobCardNumber,
                BuildVehicleDisplay(record.RegistrationPlate, record.Vin, record.JobCardName),
                record.Client,
                record.WorkflowStatus))
            .ToList();
    }

    internal IReadOnlyList<AvailableTrackingUnitRecord> GetAvailableTrackingUnits(int inventoryItemId)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.InventoryTrackingUnits
            .AsNoTracking()
            .Where(unit => unit.InventoryItemId == inventoryItemId && !unit.IsIssued)
            .OrderBy(unit => unit.CreatedAt)
            .ThenBy(unit => unit.InventoryTrackingUnitId)
            .Select(unit => new AvailableTrackingUnitRecord(
                unit.InventoryTrackingUnitId,
                unit.SerialNumber,
                unit.ImeiNumber,
                unit.CreatedAt))
            .ToList();
    }

    internal string GetNextReceiptNumber()
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);
        return GenerateNextReceiptNumber(db);
    }

    internal IReadOnlyList<string> GetSupplierSuggestions()
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.PurchaseOrders
            .AsNoTracking()
            .Select(order => order.BillTo)
            .Concat(db.InventoryReceiptLines.AsNoTracking().Select(line => line.SupplierName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();
    }

    internal IReadOnlyList<InventoryReceiptPurchaseOrderOption> GetPurchaseOrderOptions(int maxResults = 250)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(order => order.PurchaseOrderId)
            .Take(Math.Max(1, maxResults))
            .Select(order => new InventoryReceiptPurchaseOrderOption(
                order.PurchaseOrderId,
                order.OrderNumber,
                order.BillTo,
                order.Date,
                order.Reference))
            .ToList();
    }

    internal IReadOnlyList<PurchaseOrderReceiptLineSummary> GetPurchaseOrderReceiptLines(int purchaseOrderId)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.InventoryReceiptLines
            .AsNoTracking()
            .Where(line => line.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(line => line.CreatedAt)
            .ThenByDescending(line => line.InventoryReceiptLineId)
            .Select(line => new PurchaseOrderReceiptLineSummary(
                line.ReceiptNumber,
                line.CreatedAt,
                line.SupplierName,
                line.ItemCodeSnapshot,
                line.ItemNameSnapshot,
                line.QuantityReceived,
                line.Notes))
            .ToList();
    }

    internal InventoryItem SaveItem(SaveInventoryItemRequest request)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var itemCode = NormalizeRequiredText(request.ItemCode, "item code");
        var itemName = NormalizeRequiredText(request.ItemName, "item name");
        var category = NormalizeText(request.Category) ?? "General";
        var description = NormalizeText(request.Description);

        var duplicate = FindDuplicateItemByCode(
            db,
            itemCode,
            request.InventoryItemId);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"An inventory item with code {duplicate.ItemCode} already exists. Use a unique stock code.");
        }

        var utcNow = DateTime.UtcNow;
        InventoryItem item;

        if (request.InventoryItemId.HasValue)
        {
            item = db.InventoryItems.FirstOrDefault(existing => existing.InventoryItemId == request.InventoryItemId.Value)
                ?? throw new InvalidOperationException("I couldn't find that inventory item.");
        }
        else
        {
            item = new InventoryItem
            {
                CreatedAt = utcNow,
                QuantityOnHand = 0
            };

            db.InventoryItems.Add(item);
        }

        item.ItemCode = itemCode;
        item.ItemName = itemName;
        item.Category = category;
        item.Description = description;
        item.IsTrackingUnit = request.IsTrackingUnit;
        item.UpdatedAt = utcNow;

        db.SaveChanges();
        return item;
    }

    internal InventoryTransaction RecordTransaction(RecordInventoryTransactionRequest request)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var quantity = request.Quantity;
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Enter a quantity greater than zero.");
        }

        var transactionType = NormalizeTransactionType(request.TransactionType);
        var isStockOut = string.Equals(transactionType, InventoryTransactionTypes.StockOut, StringComparison.OrdinalIgnoreCase);
        using var dbTransaction = db.Database.BeginTransaction();

        var item = db.InventoryItems.FirstOrDefault(existing => existing.InventoryItemId == request.InventoryItemId)
            ?? throw new InvalidOperationException("Select a valid inventory item first.");

        JobCardRecord? linkedJobCard = null;
        if (request.JobCardRecordId.HasValue)
        {
            linkedJobCard = db.JobCards
                .AsNoTracking()
                .FirstOrDefault(record => record.JobCardRecordId == request.JobCardRecordId.Value)
                ?? throw new InvalidOperationException("The selected job card could not be found.");
        }

        if (item.IsTrackingUnit &&
            isStockOut &&
            linkedJobCard is null)
        {
            throw new InvalidOperationException("Tracking unit issues must be linked to a job card.");
        }

        var receivedTrackingUnits = item.IsTrackingUnit &&
            string.Equals(transactionType, InventoryTransactionTypes.StockIn, StringComparison.OrdinalIgnoreCase)
            ? NormalizeTrackingUnitInputs(db, request.ReceivedTrackingUnits, quantity)
            : [];

        var issuedTrackingUnits = item.IsTrackingUnit &&
            isStockOut
            ? LoadTrackingUnitsToIssue(db, item.InventoryItemId, request.IssuedTrackingUnitIds, quantity)
            : [];

        if (isStockOut && item.QuantityOnHand < quantity)
        {
            throw new InvalidOperationException(
                $"Only {item.QuantityOnHand} unit(s) of {item.ItemName} are currently on hand.");
        }

        item.QuantityOnHand += string.Equals(transactionType, InventoryTransactionTypes.StockIn, StringComparison.OrdinalIgnoreCase)
            ? quantity
            : -quantity;
        item.UpdatedAt = DateTime.UtcNow;

        var transaction = new InventoryTransaction
        {
            InventoryItemId = item.InventoryItemId,
            TransactionType = transactionType,
            Quantity = quantity,
            QuantityAfterTransaction = item.QuantityOnHand,
            CreatedAt = DateTime.UtcNow,
            IssueOutNumber = isStockOut ? GenerateNextIssueOutNumber(db) : null,
            ItemCodeSnapshot = item.ItemCode,
            ItemNameSnapshot = item.ItemName,
            CategorySnapshot = item.Category,
            IsTrackingUnit = item.IsTrackingUnit,
            JobCardRecordId = linkedJobCard?.JobCardRecordId,
            JobCardNumber = linkedJobCard?.JobCardNumber,
            Notes = NormalizeText(request.Notes)
        };

        db.InventoryTransactions.Add(transaction);
        db.SaveChanges();

        if (receivedTrackingUnits.Count > 0)
        {
            CreateTrackingUnitRecords(
                db,
                item.InventoryItemId,
                transaction.InventoryTransactionId,
                transaction.CreatedAt,
                receivedTrackingUnits);
        }

        if (issuedTrackingUnits.Count > 0)
        {
            MarkTrackingUnitsAsIssued(
                db,
                issuedTrackingUnits,
                transaction.InventoryTransactionId,
                linkedJobCard);
        }

        if (receivedTrackingUnits.Count > 0 || issuedTrackingUnits.Count > 0)
        {
            db.SaveChanges();
        }

        dbTransaction.Commit();
        return transaction;
    }

    internal ReceiveNewInventoryItemResult ReceiveNewItem(ReceiveNewInventoryItemRequest request)
    {
        var receiptResult = ReceiveReceipt(
            new ReceiveInventoryReceiptRequest(
                request.Notes,
                [
                    new ReceiveInventoryReceiptLineRequest(
                        request.ItemCode,
                        request.ItemName,
                        request.Category,
                        request.Description,
                        request.IsTrackingUnit,
                        request.Quantity,
                        "Inventory Receipt",
                        null,
                        request.Notes,
                        request.TrackingUnits)
                ]));

        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var item = db.InventoryItems
            .AsNoTracking()
            .FirstOrDefault(existing => existing.InventoryItemId == receiptResult.PreferredInventoryItemId)
            ?? throw new InvalidOperationException("The received inventory item could not be reloaded.");

        return new ReceiveNewInventoryItemResult(
            item.InventoryItemId,
            item.ItemCode,
            item.ItemName,
            request.Quantity,
            item.QuantityOnHand);
    }

    internal ReceiveInventoryReceiptResult ReceiveReceipt(ReceiveInventoryReceiptRequest request)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var lines = request.Lines?.ToList() ?? [];
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("Add at least one receipt line first.");
        }

        using var dbTransaction = db.Database.BeginTransaction();
        var utcNow = DateTime.UtcNow;
        var receipt = new InventoryReceipt
        {
            ReceiptNumber = GenerateNextReceiptNumber(db),
            CreatedAt = utcNow,
            Notes = NormalizeText(request.Notes)
        };

        db.InventoryReceipts.Add(receipt);
        db.SaveChanges();

        int? preferredInventoryItemId = null;
        var totalQuantity = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            var lineRequest = lines[index];
            var lineNumber = index + 1;
            if (lineRequest.Quantity <= 0)
            {
                throw new InvalidOperationException($"Enter a whole-number quantity greater than zero for receipt line {lineNumber}.");
            }

            PurchaseOrder? linkedPurchaseOrder = null;
            if (lineRequest.PurchaseOrderId.HasValue)
            {
                linkedPurchaseOrder = db.PurchaseOrders
                    .AsNoTracking()
                    .FirstOrDefault(order => order.PurchaseOrderId == lineRequest.PurchaseOrderId.Value)
                    ?? throw new InvalidOperationException($"The selected purchase order for receipt line {lineNumber} could not be found.");
            }

            var supplierName = ResolveReceiptSupplier(lineRequest.SupplierName, linkedPurchaseOrder?.BillTo);
            var item = ResolveOrCreateReceiptItem(db, lineRequest, lineNumber, utcNow);
            var receivedTrackingUnits = item.IsTrackingUnit
                ? NormalizeTrackingUnitInputs(db, lineRequest.TrackingUnits, lineRequest.Quantity)
                : [];

            item.QuantityOnHand += lineRequest.Quantity;
            item.UpdatedAt = utcNow;

            var transaction = new InventoryTransaction
            {
                InventoryItemId = item.InventoryItemId,
                TransactionType = InventoryTransactionTypes.StockIn,
                Quantity = lineRequest.Quantity,
                QuantityAfterTransaction = item.QuantityOnHand,
                CreatedAt = utcNow,
                ItemCodeSnapshot = item.ItemCode,
                ItemNameSnapshot = item.ItemName,
                CategorySnapshot = item.Category,
                IsTrackingUnit = item.IsTrackingUnit,
                Notes = NormalizeText(lineRequest.Notes)
            };

            db.InventoryTransactions.Add(transaction);
            db.SaveChanges();

            if (receivedTrackingUnits.Count > 0)
            {
                CreateTrackingUnitRecords(
                    db,
                    item.InventoryItemId,
                    transaction.InventoryTransactionId,
                    utcNow,
                    receivedTrackingUnits);
            }

            db.InventoryReceiptLines.Add(new InventoryReceiptLine
            {
                InventoryReceiptId = receipt.InventoryReceiptId,
                LineNumber = lineNumber,
                ReceiptNumber = receipt.ReceiptNumber,
                InventoryItemId = item.InventoryItemId,
                InventoryTransactionId = transaction.InventoryTransactionId,
                SupplierName = supplierName,
                PurchaseOrderId = linkedPurchaseOrder?.PurchaseOrderId,
                PurchaseOrderNumber = linkedPurchaseOrder?.OrderNumber,
                QuantityReceived = lineRequest.Quantity,
                ItemCodeSnapshot = item.ItemCode,
                ItemNameSnapshot = item.ItemName,
                CategorySnapshot = item.Category,
                IsTrackingUnit = item.IsTrackingUnit,
                Notes = NormalizeText(lineRequest.Notes),
                CreatedAt = utcNow
            });

            db.SaveChanges();

            preferredInventoryItemId ??= item.InventoryItemId;
            totalQuantity += lineRequest.Quantity;
        }

        dbTransaction.Commit();

        return new ReceiveInventoryReceiptResult(
            receipt.InventoryReceiptId,
            receipt.ReceiptNumber,
            lines.Count,
            totalQuantity,
            preferredInventoryItemId);
    }

    internal IReadOnlyDictionary<int, JobCardInventorySummary> GetJobCardIssueSummaries(IEnumerable<int> jobCardRecordIds)
    {
        var requestedIds = jobCardRecordIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (requestedIds.Count == 0)
        {
            return new Dictionary<int, JobCardInventorySummary>();
        }

        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var transactions = db.InventoryTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.JobCardRecordId.HasValue &&
                requestedIds.Contains(transaction.JobCardRecordId.Value) &&
                transaction.TransactionType == InventoryTransactionTypes.StockOut)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ToList();

        return transactions
            .GroupBy(transaction => transaction.JobCardRecordId!.Value)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var totalQuantity = group.Sum(transaction => transaction.Quantity);
                    var issueCount = group.Count();
                    var groupedItems = group
                        .GroupBy(transaction => $"{transaction.ItemCodeSnapshot}|{transaction.ItemNameSnapshot}")
                        .ToList();

                    string displayText;
                    if (groupedItems.Count == 1)
                    {
                        var firstTransaction = group.First();
                        displayText = $"{BuildItemDisplay(firstTransaction.ItemCodeSnapshot, firstTransaction.ItemNameSnapshot)} x{totalQuantity}";
                    }
                    else
                    {
                        displayText = $"{issueCount} issue(s) / {totalQuantity} unit(s)";
                    }

                    return new JobCardInventorySummary(group.Key, issueCount, totalQuantity, displayText);
                });
    }

    internal IReadOnlyList<JobCardInventoryIssue> GetJobCardIssues(int jobCardRecordId)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.InventoryTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.JobCardRecordId == jobCardRecordId &&
                transaction.TransactionType == InventoryTransactionTypes.StockOut)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.InventoryTransactionId)
            .Select(transaction => new JobCardInventoryIssue(
                transaction.InventoryTransactionId,
                transaction.CreatedAt,
                transaction.IssueOutNumber,
                transaction.ItemCodeSnapshot,
                transaction.ItemNameSnapshot,
                transaction.Quantity,
                transaction.Notes))
            .ToList();
    }

    private static void EnsureSchema(PurchaseOrderContext db)
    {
        db.Database.EnsureCreated();
        if (!db.Database.IsSqlite())
        {
            return;
        }

        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS InventoryItems (
                InventoryItemId INTEGER NOT NULL CONSTRAINT PK_InventoryItems PRIMARY KEY AUTOINCREMENT,
                ItemCode TEXT NOT NULL,
                ItemName TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT '',
                Description TEXT NULL,
                IsTrackingUnit INTEGER NOT NULL DEFAULT 0,
                QuantityOnHand INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )
            """);

        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS InventoryTransactions (
                InventoryTransactionId INTEGER NOT NULL CONSTRAINT PK_InventoryTransactions PRIMARY KEY AUTOINCREMENT,
                InventoryItemId INTEGER NOT NULL,
                TransactionType TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                QuantityAfterTransaction INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                IssueOutNumber TEXT NULL,
                ItemCodeSnapshot TEXT NOT NULL DEFAULT '',
                ItemNameSnapshot TEXT NOT NULL DEFAULT '',
                CategorySnapshot TEXT NULL,
                IsTrackingUnit INTEGER NOT NULL DEFAULT 0,
                JobCardRecordId INTEGER NULL,
                JobCardNumber TEXT NULL,
                Notes TEXT NULL,
                CONSTRAINT FK_InventoryTransactions_InventoryItems FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (InventoryItemId) ON DELETE RESTRICT
            )
            """);

        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS InventoryTrackingUnits (
                InventoryTrackingUnitId INTEGER NOT NULL CONSTRAINT PK_InventoryTrackingUnits PRIMARY KEY AUTOINCREMENT,
                InventoryItemId INTEGER NOT NULL,
                SerialNumber TEXT NOT NULL,
                ImeiNumber TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                ReceivedInventoryTransactionId INTEGER NULL,
                IsIssued INTEGER NOT NULL DEFAULT 0,
                IssuedAt TEXT NULL,
                IssuedInventoryTransactionId INTEGER NULL,
                IssuedJobCardRecordId INTEGER NULL,
                IssuedJobCardNumber TEXT NULL,
                CONSTRAINT FK_InventoryTrackingUnits_InventoryItems FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (InventoryItemId) ON DELETE RESTRICT
            )
            """);

        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS InventoryReceipts (
                InventoryReceiptId INTEGER NOT NULL CONSTRAINT PK_InventoryReceipts PRIMARY KEY AUTOINCREMENT,
                ReceiptNumber TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                Notes TEXT NULL
            )
            """);

        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS InventoryReceiptLines (
                InventoryReceiptLineId INTEGER NOT NULL CONSTRAINT PK_InventoryReceiptLines PRIMARY KEY AUTOINCREMENT,
                InventoryReceiptId INTEGER NOT NULL,
                LineNumber INTEGER NOT NULL DEFAULT 1,
                ReceiptNumber TEXT NOT NULL DEFAULT '',
                InventoryItemId INTEGER NOT NULL,
                InventoryTransactionId INTEGER NOT NULL,
                SupplierName TEXT NOT NULL DEFAULT '',
                PurchaseOrderId INTEGER NULL,
                PurchaseOrderNumber TEXT NULL,
                QuantityReceived INTEGER NOT NULL DEFAULT 0,
                ItemCodeSnapshot TEXT NOT NULL DEFAULT '',
                ItemNameSnapshot TEXT NOT NULL DEFAULT '',
                CategorySnapshot TEXT NULL,
                IsTrackingUnit INTEGER NOT NULL DEFAULT 0,
                Notes TEXT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_InventoryReceiptLines_InventoryReceipts FOREIGN KEY (InventoryReceiptId) REFERENCES InventoryReceipts (InventoryReceiptId) ON DELETE RESTRICT,
                CONSTRAINT FK_InventoryReceiptLines_InventoryItems FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems (InventoryItemId) ON DELETE RESTRICT,
                CONSTRAINT FK_InventoryReceiptLines_InventoryTransactions FOREIGN KEY (InventoryTransactionId) REFERENCES InventoryTransactions (InventoryTransactionId) ON DELETE RESTRICT,
                CONSTRAINT FK_InventoryReceiptLines_PurchaseOrders FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrders (PurchaseOrderId) ON DELETE RESTRICT
            )
            """);

        EnsureColumnExists(db, "InventoryItems", "Category", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryItems", "Description", "TEXT NULL");
        EnsureColumnExists(db, "InventoryItems", "IsTrackingUnit", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryItems", "QuantityOnHand", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryItems", "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");
        EnsureColumnExists(db, "InventoryItems", "UpdatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");

        EnsureColumnExists(db, "InventoryTransactions", "QuantityAfterTransaction", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryTransactions", "IssueOutNumber", "TEXT NULL");
        EnsureColumnExists(db, "InventoryTransactions", "ItemCodeSnapshot", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryTransactions", "ItemNameSnapshot", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryTransactions", "CategorySnapshot", "TEXT NULL");
        EnsureColumnExists(db, "InventoryTransactions", "IsTrackingUnit", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryTransactions", "JobCardRecordId", "INTEGER NULL");
        EnsureColumnExists(db, "InventoryTransactions", "JobCardNumber", "TEXT NULL");
        EnsureColumnExists(db, "InventoryTransactions", "Notes", "TEXT NULL");

        EnsureColumnExists(db, "InventoryTrackingUnits", "SerialNumber", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryTrackingUnits", "ImeiNumber", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryTrackingUnits", "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");
        EnsureColumnExists(db, "InventoryTrackingUnits", "ReceivedInventoryTransactionId", "INTEGER NULL");
        EnsureColumnExists(db, "InventoryTrackingUnits", "IsIssued", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryTrackingUnits", "IssuedAt", "TEXT NULL");
        EnsureColumnExists(db, "InventoryTrackingUnits", "IssuedInventoryTransactionId", "INTEGER NULL");
        EnsureColumnExists(db, "InventoryTrackingUnits", "IssuedJobCardRecordId", "INTEGER NULL");
        EnsureColumnExists(db, "InventoryTrackingUnits", "IssuedJobCardNumber", "TEXT NULL");

        EnsureColumnExists(db, "InventoryReceipts", "ReceiptNumber", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryReceipts", "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");
        EnsureColumnExists(db, "InventoryReceipts", "Notes", "TEXT NULL");

        EnsureColumnExists(db, "InventoryReceiptLines", "LineNumber", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(db, "InventoryReceiptLines", "ReceiptNumber", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryReceiptLines", "InventoryItemId", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryReceiptLines", "InventoryTransactionId", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryReceiptLines", "SupplierName", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryReceiptLines", "PurchaseOrderId", "INTEGER NULL");
        EnsureColumnExists(db, "InventoryReceiptLines", "PurchaseOrderNumber", "TEXT NULL");
        EnsureColumnExists(db, "InventoryReceiptLines", "QuantityReceived", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryReceiptLines", "ItemCodeSnapshot", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryReceiptLines", "ItemNameSnapshot", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(db, "InventoryReceiptLines", "CategorySnapshot", "TEXT NULL");
        EnsureColumnExists(db, "InventoryReceiptLines", "IsTrackingUnit", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "InventoryReceiptLines", "Notes", "TEXT NULL");
        EnsureColumnExists(db, "InventoryReceiptLines", "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");

        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_InventoryItems_ItemCode ON InventoryItems (ItemCode)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryItems_ItemName ON InventoryItems (ItemName)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryTransactions_InventoryItemId ON InventoryTransactions (InventoryItemId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryTransactions_CreatedAt ON InventoryTransactions (CreatedAt)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryTransactions_JobCardRecordId ON InventoryTransactions (JobCardRecordId)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_InventoryTransactions_IssueOutNumber ON InventoryTransactions (IssueOutNumber)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_InventoryTrackingUnits_SerialNumber ON InventoryTrackingUnits (SerialNumber)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_InventoryTrackingUnits_ImeiNumber ON InventoryTrackingUnits (ImeiNumber)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryTrackingUnits_InventoryItemId ON InventoryTrackingUnits (InventoryItemId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryTrackingUnits_IsIssued ON InventoryTrackingUnits (IsIssued)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_InventoryReceipts_ReceiptNumber ON InventoryReceipts (ReceiptNumber)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryReceipts_CreatedAt ON InventoryReceipts (CreatedAt)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryReceiptLines_InventoryReceiptId ON InventoryReceiptLines (InventoryReceiptId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryReceiptLines_InventoryItemId ON InventoryReceiptLines (InventoryItemId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryReceiptLines_InventoryTransactionId ON InventoryReceiptLines (InventoryTransactionId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryReceiptLines_PurchaseOrderId ON InventoryReceiptLines (PurchaseOrderId)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_InventoryReceiptLines_ReceiptNumber ON InventoryReceiptLines (ReceiptNumber)");
    }

    private static void EnsureColumnExists(PurchaseOrderContext db, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(db, tableName, columnName))
        {
            return;
        }

        var sql = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDefinition;
        db.Database.ExecuteSqlRaw(sql);
    }

    private static bool ColumnExists(PurchaseOrderContext db, string tableName, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName})";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var existingColumnName = reader["name"]?.ToString();
                if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                connection.Close();
            }
        }
    }

    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"Enter the {fieldName} first.");
        }

        return normalized;
    }

    private static string NormalizeTransactionType(string? value)
    {
        if (string.Equals(value, InventoryTransactionTypes.StockOut, StringComparison.OrdinalIgnoreCase))
        {
            return InventoryTransactionTypes.StockOut;
        }

        return InventoryTransactionTypes.StockIn;
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static IReadOnlyList<TrackingUnitIdentityInput> NormalizeTrackingUnitInputs(
        PurchaseOrderContext db,
        IReadOnlyList<TrackingUnitIdentityInput>? inputs,
        int expectedQuantity)
    {
        var normalizedInputs = (inputs ?? [])
            .Select((input, index) => new
            {
                Index = index + 1,
                SerialNumber = NormalizeRequiredText(input.SerialNumber, $"serial number for tracking unit {index + 1}"),
                ImeiNumber = NormalizeRequiredText(input.ImeiNumber, $"IMEI number for tracking unit {index + 1}")
            })
            .Select(entry => new TrackingUnitIdentityInput(entry.SerialNumber, entry.ImeiNumber))
            .ToList();

        if (expectedQuantity <= 0)
        {
            throw new InvalidOperationException("Enter a quantity greater than zero.");
        }

        if (normalizedInputs.Count != expectedQuantity)
        {
            throw new InvalidOperationException(
                $"Provide a serial number and IMEI for each tracking unit. Expected {expectedQuantity} entr{(expectedQuantity == 1 ? "y" : "ies")}.");
        }

        var duplicateSerial = normalizedInputs
            .GroupBy(input => NormalizeIdentifier(input.SerialNumber))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateSerial is not null)
        {
            throw new InvalidOperationException("Each tracking unit serial number must be unique.");
        }

        var duplicateImei = normalizedInputs
            .GroupBy(input => NormalizeIdentifier(input.ImeiNumber))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateImei is not null)
        {
            throw new InvalidOperationException("Each tracking unit IMEI number must be unique.");
        }

        foreach (var input in normalizedInputs)
        {
            var existingUnit = FindTrackingUnitByIdentifiers(db, input.SerialNumber, input.ImeiNumber);
            if (existingUnit is not null)
            {
                throw new InvalidOperationException(
                    $"Tracking unit {existingUnit.SerialNumber} / {existingUnit.ImeiNumber} already exists in inventory.");
            }
        }

        return normalizedInputs;
    }

    private static List<InventoryTrackingUnit> LoadTrackingUnitsToIssue(
        PurchaseOrderContext db,
        int inventoryItemId,
        IReadOnlyList<int>? trackingUnitIds,
        int expectedQuantity)
    {
        var selectedIds = (trackingUnitIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (selectedIds.Count != expectedQuantity)
        {
            throw new InvalidOperationException(
                $"Select the exact tracking units being issued out. Expected {expectedQuantity} unit{(expectedQuantity == 1 ? string.Empty : "s")}.");
        }

        var availableUnits = db.InventoryTrackingUnits
            .Where(unit =>
                unit.InventoryItemId == inventoryItemId &&
                !unit.IsIssued &&
                selectedIds.Contains(unit.InventoryTrackingUnitId))
            .OrderBy(unit => unit.CreatedAt)
            .ThenBy(unit => unit.InventoryTrackingUnitId)
            .ToList();

        if (availableUnits.Count != expectedQuantity)
        {
            throw new InvalidOperationException("One or more selected tracking units are no longer available.");
        }

        return availableUnits;
    }

    private static void CreateTrackingUnitRecords(
        PurchaseOrderContext db,
        int inventoryItemId,
        int inventoryTransactionId,
        DateTime createdAt,
        IReadOnlyList<TrackingUnitIdentityInput> trackingUnits)
    {
        foreach (var unit in trackingUnits)
        {
            db.InventoryTrackingUnits.Add(new InventoryTrackingUnit
            {
                InventoryItemId = inventoryItemId,
                SerialNumber = unit.SerialNumber,
                ImeiNumber = unit.ImeiNumber,
                CreatedAt = createdAt,
                ReceivedInventoryTransactionId = inventoryTransactionId,
                IsIssued = false
            });
        }
    }

    private static void MarkTrackingUnitsAsIssued(
        PurchaseOrderContext db,
        IReadOnlyList<InventoryTrackingUnit> trackingUnits,
        int inventoryTransactionId,
        JobCardRecord? linkedJobCard)
    {
        var issuedAt = DateTime.UtcNow;

        foreach (var unit in trackingUnits)
        {
            unit.IsIssued = true;
            unit.IssuedAt = issuedAt;
            unit.IssuedInventoryTransactionId = inventoryTransactionId;
            unit.IssuedJobCardRecordId = linkedJobCard?.JobCardRecordId;
            unit.IssuedJobCardNumber = linkedJobCard?.JobCardNumber;
        }
    }

    private static InventoryTrackingUnit? FindTrackingUnitByIdentifiers(
        PurchaseOrderContext db,
        string serialNumber,
        string imeiNumber)
    {
        var normalizedSerial = NormalizeIdentifier(serialNumber);
        var normalizedImei = NormalizeIdentifier(imeiNumber);

        return db.InventoryTrackingUnits
            .AsNoTracking()
            .AsEnumerable()
            .FirstOrDefault(unit =>
                string.Equals(NormalizeIdentifier(unit.SerialNumber), normalizedSerial, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeIdentifier(unit.ImeiNumber), normalizedImei, StringComparison.OrdinalIgnoreCase));
    }

    private static InventoryItem? FindDuplicateItemByCode(
        PurchaseOrderContext db,
        string itemCode,
        int? excludeInventoryItemId = null)
    {
        var normalizedItemCode = NormalizeIdentifier(itemCode);
        if (string.IsNullOrWhiteSpace(normalizedItemCode))
        {
            return null;
        }

        return db.InventoryItems
            .AsEnumerable()
            .FirstOrDefault(item =>
                item.InventoryItemId != excludeInventoryItemId.GetValueOrDefault() &&
                string.Equals(
                    NormalizeIdentifier(item.ItemCode),
                    normalizedItemCode,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateNextReceiptNumber(PurchaseOrderContext db)
    {
        var nextSequence = db.InventoryReceipts
            .AsNoTracking()
            .AsEnumerable()
            .Select(receipt => ExtractReceiptSequence(receipt.ReceiptNumber))
            .Where(sequence => sequence >= 0)
            .DefaultIfEmpty(ReceiptNumberStartingValue - 1)
            .Max() + 1;

        return $"{ReceiptNumberPrefix}{nextSequence.ToString($"D{ReceiptNumberDigits}")}";
    }

    private static string GenerateNextIssueOutNumber(PurchaseOrderContext db)
    {
        var nextSequence = db.InventoryTransactions
            .AsNoTracking()
            .AsEnumerable()
            .Select(transaction => ExtractIssueOutSequence(transaction.IssueOutNumber))
            .Where(sequence => sequence >= 0)
            .DefaultIfEmpty(IssueOutNumberStartingValue - 1)
            .Max() + 1;

        return $"{IssueOutNumberPrefix}{nextSequence.ToString($"D{IssueOutNumberDigits}")}";
    }

    private static int ExtractReceiptSequence(string? receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber) ||
            !receiptNumber.StartsWith(ReceiptNumberPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        var suffix = receiptNumber[ReceiptNumberPrefix.Length..];
        return int.TryParse(suffix, out var sequence) ? sequence : -1;
    }

    private static int ExtractIssueOutSequence(string? issueOutNumber)
    {
        if (string.IsNullOrWhiteSpace(issueOutNumber) ||
            !issueOutNumber.StartsWith(IssueOutNumberPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        var suffix = issueOutNumber[IssueOutNumberPrefix.Length..];
        return int.TryParse(suffix, out var sequence) ? sequence : -1;
    }

    private static string ResolveReceiptSupplier(string? supplierName, string? purchaseOrderSupplierName)
    {
        return NormalizeText(supplierName)
            ?? NormalizeText(purchaseOrderSupplierName)
            ?? "Unspecified Supplier";
    }

    private static InventoryItem ResolveOrCreateReceiptItem(
        PurchaseOrderContext db,
        ReceiveInventoryReceiptLineRequest request,
        int lineNumber,
        DateTime utcNow)
    {
        var itemCode = NormalizeRequiredText(request.ItemCode, $"item code for receipt line {lineNumber}");
        var existingItem = FindDuplicateItemByCode(db, itemCode);

        if (existingItem is not null)
        {
            ValidateExistingReceiptItemMatch(existingItem, request, lineNumber);

            var normalizedDescription = NormalizeText(request.Description);
            if (string.IsNullOrWhiteSpace(existingItem.Description) && !string.IsNullOrWhiteSpace(normalizedDescription))
            {
                existingItem.Description = normalizedDescription;
            }

            existingItem.UpdatedAt = utcNow;
            return existingItem;
        }

        var itemName = NormalizeRequiredText(request.ItemName, $"item name for receipt line {lineNumber}");
        var category = NormalizeText(request.Category) ?? "General";

        var item = new InventoryItem
        {
            ItemCode = itemCode,
            ItemName = itemName,
            Category = category,
            Description = NormalizeText(request.Description),
            IsTrackingUnit = request.IsTrackingUnit,
            QuantityOnHand = 0,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        db.InventoryItems.Add(item);
        db.SaveChanges();
        return item;
    }

    private static void ValidateExistingReceiptItemMatch(
        InventoryItem existingItem,
        ReceiveInventoryReceiptLineRequest request,
        int lineNumber)
    {
        var normalizedItemName = NormalizeText(request.ItemName);
        if (!string.IsNullOrWhiteSpace(normalizedItemName) &&
            !string.Equals(existingItem.ItemName, normalizedItemName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Receipt line {lineNumber} uses stock code {existingItem.ItemCode}, which already belongs to {existingItem.ItemName}.");
        }

        var normalizedCategory = NormalizeText(request.Category) ?? "General";
        var existingCategory = NormalizeText(existingItem.Category) ?? "General";
        if (!string.Equals(existingCategory, normalizedCategory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Receipt line {lineNumber} uses stock code {existingItem.ItemCode}, which is already classified under {existingItem.Category}.");
        }

        if (existingItem.IsTrackingUnit != request.IsTrackingUnit)
        {
            throw new InvalidOperationException(
                $"Receipt line {lineNumber} uses stock code {existingItem.ItemCode}, but the tracking-unit flag does not match the existing item.");
        }
    }

    private static string NormalizeIdentifier(string? value)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return new string(normalized
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static string BuildVehicleDisplay(string? registrationPlate, string? vin, string? jobCardName)
    {
        var registration = NormalizeText(registrationPlate);
        if (!string.IsNullOrWhiteSpace(registration))
        {
            return registration;
        }

        var vinValue = NormalizeText(vin);
        if (!string.IsNullOrWhiteSpace(vinValue))
        {
            return vinValue;
        }

        return NormalizeText(jobCardName) ?? "Pending";
    }

    private static string BuildItemDisplay(string? itemCode, string? itemName)
    {
        var code = NormalizeText(itemCode);
        var name = NormalizeText(itemName);

        if (string.IsNullOrWhiteSpace(code))
        {
            return name ?? "Inventory Item";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return code;
        }

        return $"{code} - {name}";
    }
}
