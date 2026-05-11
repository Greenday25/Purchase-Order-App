using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Data;
using PurchaseOrderApp.Models;
using System.Text.Json;

var settings = MigrationSettings.Load();
if (!File.Exists(settings.Source.SqliteDatabasePath))
{
    Console.Error.WriteLine($"Source SQLite database not found: {settings.Source.SqliteDatabasePath}");
    return 1;
}

Console.WriteLine("Purchase Order App database migration");
Console.WriteLine($"Source: {settings.Source.SqliteDatabasePath}");
Console.WriteLine($"Target: {settings.Target.SqlServerConnectionString}");
Console.WriteLine();

var sourceOptions = new DbContextOptionsBuilder<PurchaseOrderContext>()
    .UseSqlite($"Data Source={settings.Source.SqliteDatabasePath}")
    .Options;

var targetOptions = new DbContextOptionsBuilder<PurchaseOrderContext>()
    .UseSqlServer(settings.Target.SqlServerConnectionString)
    .Options;

await using var source = new PurchaseOrderContext(sourceOptions);
await using var target = new PurchaseOrderContext(targetOptions);

Console.WriteLine("Creating target schema if needed...");
await target.Database.EnsureCreatedAsync();

if (await TargetHasData(target))
{
    Console.Error.WriteLine("Target database already contains data. Migration stopped to prevent duplicates.");
    Console.Error.WriteLine("Use a new empty SQL Server database, or back up and clear the target manually before retrying.");
    return 2;
}

Console.WriteLine("Copying data...");
await using var transaction = await target.Database.BeginTransactionAsync();

await CopyWithIdentityInsert(target, "AppRoles", await source.AppRoles.AsNoTracking().ToListAsync(), role => new AppRole
{
    AppRoleId = role.AppRoleId,
    Name = role.Name,
    CanAccessPurchaseOrders = role.CanAccessPurchaseOrders,
    CanManagerApprovePurchaseOrders = role.CanManagerApprovePurchaseOrders,
    CanApprovePurchaseOrders = role.CanApprovePurchaseOrders,
    CanAccessJobCards = role.CanAccessJobCards,
    CanAccessWialonUnits = role.CanAccessWialonUnits,
    CanAccessTrackingCertificates = role.CanAccessTrackingCertificates,
    CanAccessInventory = role.CanAccessInventory,
    CanAccessConnectivitySettings = role.CanAccessConnectivitySettings,
    CanManageUsers = role.CanManageUsers
});

await CopyWithIdentityInsert(target, "AppUsers", await source.AppUsers.AsNoTracking().ToListAsync(), user => new AppUser
{
    AppUserId = user.AppUserId,
    DisplayName = user.DisplayName,
    AppRoleId = user.AppRoleId,
    IsActive = user.IsActive,
    CreatedAt = user.CreatedAt,
    PasswordHash = user.PasswordHash,
    PasswordSalt = user.PasswordSalt,
    PasswordUpdatedAt = user.PasswordUpdatedAt
});

await CopyWithIdentityInsert(target, "Vendors", await source.Vendors.AsNoTracking().ToListAsync(), vendor => new Vendor
{
    VendorId = vendor.VendorId,
    Name = vendor.Name,
    Address = vendor.Address,
    Phone = vendor.Phone,
    Email = vendor.Email
});

await CopyWithIdentityInsert(target, "PurchaseOrders", await source.PurchaseOrders.AsNoTracking().ToListAsync(), order => new PurchaseOrder
{
    PurchaseOrderId = order.PurchaseOrderId,
    OrderNumber = order.OrderNumber,
    OrderNumberManuallyEdited = order.OrderNumberManuallyEdited,
    CreatedByAppUserId = order.CreatedByAppUserId,
    CreatedByDisplayName = order.CreatedByDisplayName,
    AssignedManagerAppUserId = order.AssignedManagerAppUserId,
    AssignedManagerDisplayName = order.AssignedManagerDisplayName,
    Date = order.Date,
    Reference = order.Reference,
    VendorId = order.VendorId,
    BillTo = order.BillTo,
    BillToAddress = order.BillToAddress,
    IncludeVat = order.IncludeVat,
    VATPercent = order.VATPercent,
    ManagerApprovedAt = order.ManagerApprovedAt,
    DirectorApprovedAt = order.DirectorApprovedAt,
    SupplierCopySentAt = order.SupplierCopySentAt,
    RejectedAt = order.RejectedAt,
    SignedOrderFileName = order.SignedOrderFileName,
    SignedOrderContent = order.SignedOrderContent,
    InvoiceFileName = order.InvoiceFileName,
    InvoiceContent = order.InvoiceContent
});

await CopyWithIdentityInsert(target, "PurchaseOrderLines", await source.PurchaseOrderLines.AsNoTracking().ToListAsync(), line => new PurchaseOrderLine
{
    PurchaseOrderLineId = line.PurchaseOrderLineId,
    PurchaseOrderId = line.PurchaseOrderId,
    Quantity = line.Quantity,
    PartNumber = line.PartNumber,
    Description = line.Description,
    UnitPrice = line.UnitPrice
});

await CopyWithIdentityInsert(target, "JobCards", await source.JobCards.AsNoTracking().ToListAsync(), jobCard => new JobCardRecord
{
    JobCardRecordId = jobCard.JobCardRecordId,
    SequenceNumber = jobCard.SequenceNumber,
    JobCardNumber = jobCard.JobCardNumber,
    CreatedAt = jobCard.CreatedAt,
    WorkflowStatus = jobCard.WorkflowStatus,
    JobCardType = jobCard.JobCardType,
    StatusNotes = jobCard.StatusNotes,
    DetailsConfirmedAt = jobCard.DetailsConfirmedAt,
    LastAmendedAt = jobCard.LastAmendedAt,
    AmendmentNotes = jobCard.AmendmentNotes,
    EvidenceReceivedAt = jobCard.EvidenceReceivedAt,
    HasVehiclePhoto = jobCard.HasVehiclePhoto,
    HasRegistrationPhoto = jobCard.HasRegistrationPhoto,
    HasVinPhoto = jobCard.HasVinPhoto,
    HasTrackingUnitPhoto = jobCard.HasTrackingUnitPhoto,
    UseCustomBillingSystem = jobCard.UseCustomBillingSystem,
    CustomBillingSystemName = jobCard.CustomBillingSystemName,
    SystemPriceExVat = jobCard.SystemPriceExVat,
    HasPanicButton = jobCard.HasPanicButton,
    PanicButtonPriceExVat = jobCard.PanicButtonPriceExVat,
    HasEarlyWarningSystem = jobCard.HasEarlyWarningSystem,
    EarlyWarningSystemPriceExVat = jobCard.EarlyWarningSystemPriceExVat,
    BleSensorQuantity = jobCard.BleSensorQuantity,
    BleSensorUnitPriceExVat = jobCard.BleSensorUnitPriceExVat,
    HasLvCanAdaptor = jobCard.HasLvCanAdaptor,
    LvCanAdaptorPriceExVat = jobCard.LvCanAdaptorPriceExVat,
    OtherHardwareDescription = jobCard.OtherHardwareDescription,
    OtherHardwarePriceExVat = jobCard.OtherHardwarePriceExVat,
    BillingNotes = jobCard.BillingNotes,
    WialonUnitId = jobCard.WialonUnitId,
    WialonUnitName = jobCard.WialonUnitName,
    WialonAccountId = jobCard.WialonAccountId,
    WialonAccountName = jobCard.WialonAccountName,
    WialonCreatorId = jobCard.WialonCreatorId,
    WialonCreatorName = jobCard.WialonCreatorName,
    WialonHardwareTypeId = jobCard.WialonHardwareTypeId,
    WialonHardwareTypeName = jobCard.WialonHardwareTypeName,
    JobCardName = jobCard.JobCardName,
    UniqueId = jobCard.UniqueId,
    Iccid = jobCard.Iccid,
    PhoneNumber = jobCard.PhoneNumber,
    Brand = jobCard.Brand,
    Model = jobCard.Model,
    Year = jobCard.Year,
    Colour = jobCard.Colour,
    VehicleClass = jobCard.VehicleClass,
    VehicleType = jobCard.VehicleType,
    RegistrationPlate = jobCard.RegistrationPlate,
    Vin = jobCard.Vin,
    Client = jobCard.Client,
    Contact1 = jobCard.Contact1,
    Contact2 = jobCard.Contact2,
    MakeAndModel = jobCard.MakeAndModel,
    RegistrationFleet = jobCard.RegistrationFleet
});

await CopyWithIdentityInsert(target, "InventoryItems", await source.InventoryItems.AsNoTracking().ToListAsync(), item => new InventoryItem
{
    InventoryItemId = item.InventoryItemId,
    ItemCode = item.ItemCode,
    ItemName = item.ItemName,
    Category = item.Category,
    Description = item.Description,
    IsTrackingUnit = item.IsTrackingUnit,
    QuantityOnHand = item.QuantityOnHand,
    CreatedAt = item.CreatedAt,
    UpdatedAt = item.UpdatedAt
});

await CopyWithIdentityInsert(target, "InventoryTransactions", await source.InventoryTransactions.AsNoTracking().ToListAsync(), inventoryTransaction => new InventoryTransaction
{
    InventoryTransactionId = inventoryTransaction.InventoryTransactionId,
    InventoryItemId = inventoryTransaction.InventoryItemId,
    TransactionType = inventoryTransaction.TransactionType,
    Quantity = inventoryTransaction.Quantity,
    QuantityAfterTransaction = inventoryTransaction.QuantityAfterTransaction,
    CreatedAt = inventoryTransaction.CreatedAt,
    IssueOutNumber = inventoryTransaction.IssueOutNumber,
    ItemCodeSnapshot = inventoryTransaction.ItemCodeSnapshot,
    ItemNameSnapshot = inventoryTransaction.ItemNameSnapshot,
    CategorySnapshot = inventoryTransaction.CategorySnapshot,
    IsTrackingUnit = inventoryTransaction.IsTrackingUnit,
    JobCardRecordId = inventoryTransaction.JobCardRecordId,
    JobCardNumber = inventoryTransaction.JobCardNumber,
    Notes = inventoryTransaction.Notes
});

await CopyWithIdentityInsert(target, "InventoryTrackingUnits", await source.InventoryTrackingUnits.AsNoTracking().ToListAsync(), unit => new InventoryTrackingUnit
{
    InventoryTrackingUnitId = unit.InventoryTrackingUnitId,
    InventoryItemId = unit.InventoryItemId,
    SerialNumber = unit.SerialNumber,
    ImeiNumber = unit.ImeiNumber,
    CreatedAt = unit.CreatedAt,
    ReceivedInventoryTransactionId = unit.ReceivedInventoryTransactionId,
    IsIssued = unit.IsIssued,
    IssuedAt = unit.IssuedAt,
    IssuedInventoryTransactionId = unit.IssuedInventoryTransactionId,
    IssuedJobCardRecordId = unit.IssuedJobCardRecordId,
    IssuedJobCardNumber = unit.IssuedJobCardNumber
});

await CopyWithIdentityInsert(target, "InventoryReceipts", await source.InventoryReceipts.AsNoTracking().ToListAsync(), receipt => new InventoryReceipt
{
    InventoryReceiptId = receipt.InventoryReceiptId,
    ReceiptNumber = receipt.ReceiptNumber,
    CreatedAt = receipt.CreatedAt,
    Notes = receipt.Notes
});

await CopyWithIdentityInsert(target, "InventoryReceiptLines", await source.InventoryReceiptLines.AsNoTracking().ToListAsync(), line => new InventoryReceiptLine
{
    InventoryReceiptLineId = line.InventoryReceiptLineId,
    InventoryReceiptId = line.InventoryReceiptId,
    LineNumber = line.LineNumber,
    ReceiptNumber = line.ReceiptNumber,
    InventoryItemId = line.InventoryItemId,
    InventoryTransactionId = line.InventoryTransactionId,
    SupplierName = line.SupplierName,
    PurchaseOrderId = line.PurchaseOrderId,
    PurchaseOrderNumber = line.PurchaseOrderNumber,
    QuantityReceived = line.QuantityReceived,
    ItemCodeSnapshot = line.ItemCodeSnapshot,
    ItemNameSnapshot = line.ItemNameSnapshot,
    CategorySnapshot = line.CategorySnapshot,
    IsTrackingUnit = line.IsTrackingUnit,
    Notes = line.Notes,
    CreatedAt = line.CreatedAt
});

await transaction.CommitAsync();

Console.WriteLine();
Console.WriteLine("Migration completed successfully.");
Console.WriteLine($"Roles: {await target.AppRoles.CountAsync()}");
Console.WriteLine($"Users: {await target.AppUsers.CountAsync()}");
Console.WriteLine($"Vendors: {await target.Vendors.CountAsync()}");
Console.WriteLine($"Purchase orders: {await target.PurchaseOrders.CountAsync()}");
Console.WriteLine($"Job cards: {await target.JobCards.CountAsync()}");
Console.WriteLine($"Inventory items: {await target.InventoryItems.CountAsync()}");
return 0;

static async Task<bool> TargetHasData(PurchaseOrderContext target)
{
    return await target.AppRoles.AnyAsync() ||
        await target.AppUsers.AnyAsync() ||
        await target.Vendors.AnyAsync() ||
        await target.PurchaseOrders.AnyAsync() ||
        await target.JobCards.AnyAsync() ||
        await target.InventoryItems.AnyAsync();
}

static async Task CopyWithIdentityInsert<TSource, TTarget>(
    PurchaseOrderContext target,
    string tableName,
    IReadOnlyCollection<TSource> sourceRows,
    Func<TSource, TTarget> clone)
    where TTarget : class
{
    if (sourceRows.Count == 0)
    {
        Console.WriteLine($"{tableName}: 0");
        return;
    }

#pragma warning disable EF1002
    await target.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] ON");
#pragma warning restore EF1002
    try
    {
        target.Set<TTarget>().AddRange(sourceRows.Select(clone));
        await target.SaveChangesAsync();
        target.ChangeTracker.Clear();
    }
    finally
    {
#pragma warning disable EF1002
        await target.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] OFF");
#pragma warning restore EF1002
    }

    Console.WriteLine($"{tableName}: {sourceRows.Count}");
}

internal sealed record MigrationSettings(SourceSettings Source, TargetSettings Target)
{
    public static MigrationSettings Load()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "migration-settings.json");
        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException("Migration settings file was not found.", settingsPath);
        }

        var settings = JsonSerializer.Deserialize<MigrationSettings>(
            File.ReadAllText(settingsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (settings is null ||
            string.IsNullOrWhiteSpace(settings.Source.SqliteDatabasePath) ||
            string.IsNullOrWhiteSpace(settings.Target.SqlServerConnectionString))
        {
            throw new InvalidOperationException("migration-settings.json must include Source.SqliteDatabasePath and Target.SqlServerConnectionString.");
        }

        return settings;
    }
}

internal sealed record SourceSettings(string SqliteDatabasePath);

internal sealed record TargetSettings(string SqlServerConnectionString);
