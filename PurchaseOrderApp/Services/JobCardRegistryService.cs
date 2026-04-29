using Microsoft.EntityFrameworkCore;
using PurchaseOrderApp.Data;
using PurchaseOrderApp.Models;
using System.Data;

namespace PurchaseOrderApp.Services;

internal sealed class JobCardRegistryService
{
    private const string JobCardPrefix = "JC";
    private const int SequenceDigits = 5;
    private const int SequenceStartingValue = 100;

    internal sealed record JobCardNumberInfo(int SequenceNumber, string JobCardNumber);

    internal sealed record DuplicateJobCardMatch(
        string FieldName,
        string FieldValue,
        string JobCardNumber,
        DateTime CreatedAt);

    internal sealed record SaveJobCardRequest(
        string JobCardType,
        string WorkflowStatus,
        string? StatusNotes,
        long? WialonUnitId,
        string? WialonUnitName,
        long? WialonAccountId,
        string? WialonAccountName,
        long? WialonCreatorId,
        string? WialonCreatorName,
        long? WialonHardwareTypeId,
        string? WialonHardwareTypeName,
        string JobCardName,
        string UniqueId,
        string Iccid,
        string? PhoneNumber,
        string Brand,
        string Model,
        string Year,
        string Colour,
        string VehicleClass,
        string VehicleType,
        string RegistrationPlate,
        string Vin,
        string Client,
        string Contact1,
        string Contact2,
        string MakeAndModel,
        string RegistrationFleet,
        bool UseCustomBillingSystem,
        string? CustomBillingSystemName,
        string? SystemPriceExVat,
        bool HasPanicButton,
        string? PanicButtonPriceExVat,
        bool HasEarlyWarningSystem,
        string? EarlyWarningSystemPriceExVat,
        string? BleSensorQuantity,
        string? BleSensorUnitPriceExVat,
        bool HasLvCanAdaptor,
        string? LvCanAdaptorPriceExVat,
        string? OtherHardwareDescription,
        string? OtherHardwarePriceExVat,
        string? BillingNotes);

    internal sealed record UpdateBillingRequest(
        bool UseCustomBillingSystem,
        string? CustomBillingSystemName,
        string? SystemPriceExVat,
        bool HasPanicButton,
        string? PanicButtonPriceExVat,
        bool HasEarlyWarningSystem,
        string? EarlyWarningSystemPriceExVat,
        string? BleSensorQuantity,
        string? BleSensorUnitPriceExVat,
        bool HasLvCanAdaptor,
        string? LvCanAdaptorPriceExVat,
        string? OtherHardwareDescription,
        string? OtherHardwarePriceExVat,
        string? BillingNotes);

    internal void EnsureSchema()
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);
    }

    internal JobCardNumberInfo GetNextJobCardNumber()
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var nextSequence = GetNextSequenceNumber(db);
        return new JobCardNumberInfo(nextSequence, FormatJobCardNumber(nextSequence));
    }

    internal IReadOnlyList<JobCardRecord> GetRecentJobCards(int maxResults = 20)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.JobCards
            .AsNoTracking()
            .OrderByDescending(item => item.SequenceNumber)
            .Take(Math.Max(1, maxResults))
            .ToList();
    }

    internal JobCardRecord? GetJobCard(int jobCardRecordId)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        return db.JobCards
            .AsNoTracking()
            .FirstOrDefault(item => item.JobCardRecordId == jobCardRecordId);
    }

    internal DuplicateJobCardMatch? FindDuplicateJobCard(string? registrationPlate, string? iccid, string? uniqueId)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var normalizedRegistration = NormalizeIdentifier(registrationPlate);
        var normalizedIccid = NormalizeIdentifier(iccid);
        var normalizedUniqueId = NormalizeIdentifier(uniqueId);

        if (string.IsNullOrWhiteSpace(normalizedRegistration) &&
            string.IsNullOrWhiteSpace(normalizedIccid) &&
            string.IsNullOrWhiteSpace(normalizedUniqueId))
        {
            return null;
        }

        var records = db.JobCards
            .AsNoTracking()
            .OrderByDescending(item => item.SequenceNumber)
            .ToList();

        foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(normalizedRegistration) &&
                string.Equals(NormalizeIdentifier(record.RegistrationPlate), normalizedRegistration, StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicateJobCardMatch("registration", record.RegistrationPlate, record.JobCardNumber, record.CreatedAt);
            }

            if (!string.IsNullOrWhiteSpace(normalizedIccid) &&
                string.Equals(NormalizeIdentifier(record.Iccid), normalizedIccid, StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicateJobCardMatch("ICCID", record.Iccid, record.JobCardNumber, record.CreatedAt);
            }

            if (!string.IsNullOrWhiteSpace(normalizedUniqueId) &&
                string.Equals(NormalizeIdentifier(record.UniqueId), normalizedUniqueId, StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicateJobCardMatch("IMEI", record.UniqueId, record.JobCardNumber, record.CreatedAt);
            }
        }

        return null;
    }

    internal JobCardRecord SaveCreatedJobCard(SaveJobCardRequest request)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        ThrowIfDuplicateJobCardExists(db, request.RegistrationPlate, request.Iccid, request.UniqueId);

        var nextSequence = GetNextSequenceNumber(db);
        var record = new JobCardRecord
        {
            SequenceNumber = nextSequence,
            JobCardNumber = FormatJobCardNumber(nextSequence),
            CreatedAt = DateTime.UtcNow,
            JobCardType = string.IsNullOrWhiteSpace(request.JobCardType)
                ? JobCardTypes.Installation
                : request.JobCardType.Trim(),
            WorkflowStatus = string.IsNullOrWhiteSpace(request.WorkflowStatus)
                ? JobCardWorkflowStatuses.Created
                : request.WorkflowStatus.Trim(),
            StatusNotes = NormalizeText(request.StatusNotes),
            WialonUnitId = request.WialonUnitId,
            WialonUnitName = NormalizeText(request.WialonUnitName),
            WialonAccountId = request.WialonAccountId,
            WialonAccountName = NormalizeText(request.WialonAccountName),
            WialonCreatorId = request.WialonCreatorId,
            WialonCreatorName = NormalizeText(request.WialonCreatorName),
            WialonHardwareTypeId = request.WialonHardwareTypeId,
            WialonHardwareTypeName = NormalizeText(request.WialonHardwareTypeName),
            JobCardName = request.JobCardName.Trim(),
            UniqueId = request.UniqueId.Trim(),
            Iccid = request.Iccid.Trim(),
            PhoneNumber = NormalizeText(request.PhoneNumber),
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim(),
            Year = request.Year.Trim(),
            Colour = request.Colour.Trim(),
            VehicleClass = request.VehicleClass.Trim(),
            VehicleType = request.VehicleType.Trim(),
            RegistrationPlate = request.RegistrationPlate.Trim(),
            Vin = request.Vin.Trim(),
            Client = request.Client.Trim(),
            Contact1 = request.Contact1.Trim(),
            Contact2 = request.Contact2.Trim(),
            MakeAndModel = request.MakeAndModel.Trim(),
            RegistrationFleet = request.RegistrationFleet.Trim(),
            UseCustomBillingSystem = request.UseCustomBillingSystem,
            CustomBillingSystemName = NormalizeText(request.CustomBillingSystemName),
            SystemPriceExVat = NormalizeText(request.SystemPriceExVat),
            HasPanicButton = request.HasPanicButton,
            PanicButtonPriceExVat = NormalizeText(request.PanicButtonPriceExVat),
            HasEarlyWarningSystem = request.HasEarlyWarningSystem,
            EarlyWarningSystemPriceExVat = NormalizeText(request.EarlyWarningSystemPriceExVat),
            BleSensorQuantity = NormalizeText(request.BleSensorQuantity),
            BleSensorUnitPriceExVat = NormalizeText(request.BleSensorUnitPriceExVat),
            HasLvCanAdaptor = request.HasLvCanAdaptor,
            LvCanAdaptorPriceExVat = NormalizeText(request.LvCanAdaptorPriceExVat),
            OtherHardwareDescription = NormalizeText(request.OtherHardwareDescription),
            OtherHardwarePriceExVat = NormalizeText(request.OtherHardwarePriceExVat),
            BillingNotes = NormalizeText(request.BillingNotes)
        };

        db.JobCards.Add(record);
        db.SaveChanges();
        return record;
    }

    internal JobCardRecord UpdateEvidenceStatus(int jobCardRecordId, JobCardEvidencePhotoType photoType)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var record = db.JobCards.FirstOrDefault(item => item.JobCardRecordId == jobCardRecordId)
            ?? throw new InvalidOperationException("I couldn't find that job card record.");

        switch (photoType)
        {
            case JobCardEvidencePhotoType.Vehicle:
                record.HasVehiclePhoto = true;
                break;
            case JobCardEvidencePhotoType.Registration:
                record.HasRegistrationPhoto = true;
                break;
            case JobCardEvidencePhotoType.Vin:
                record.HasVinPhoto = true;
                break;
            case JobCardEvidencePhotoType.TrackingUnit:
                record.HasTrackingUnitPhoto = true;
                break;
        }

        var allEvidenceReceived =
            record.HasVehiclePhoto &&
            record.HasRegistrationPhoto &&
            record.HasVinPhoto &&
            record.HasTrackingUnitPhoto;

        record.EvidenceReceivedAt = allEvidenceReceived
            ? DateTime.UtcNow
            : null;
        record.WorkflowStatus = allEvidenceReceived
            ? JobCardWorkflowStatuses.InstallationCompleted
            : JobCardWorkflowStatuses.AwaitingInstallationPhotos;

        db.SaveChanges();
        return record;
    }

    internal JobCardRecord DeleteJobCardEntry(int jobCardRecordId)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var record = db.JobCards.FirstOrDefault(item => item.JobCardRecordId == jobCardRecordId)
            ?? throw new InvalidOperationException("I couldn't find that job card record.");

        db.JobCards.Remove(record);
        db.SaveChanges();
        return record;
    }

    internal JobCardRecord UpdateBillingDetails(int jobCardRecordId, UpdateBillingRequest request)
    {
        using var db = new PurchaseOrderContext();
        EnsureSchema(db);

        var record = db.JobCards.FirstOrDefault(item => item.JobCardRecordId == jobCardRecordId)
            ?? throw new InvalidOperationException("I couldn't find that job card record.");

        record.UseCustomBillingSystem = request.UseCustomBillingSystem;
        record.CustomBillingSystemName = NormalizeText(request.CustomBillingSystemName);
        record.SystemPriceExVat = NormalizeText(request.SystemPriceExVat);
        record.HasPanicButton = request.HasPanicButton;
        record.PanicButtonPriceExVat = NormalizeText(request.PanicButtonPriceExVat);
        record.HasEarlyWarningSystem = request.HasEarlyWarningSystem;
        record.EarlyWarningSystemPriceExVat = NormalizeText(request.EarlyWarningSystemPriceExVat);
        record.BleSensorQuantity = NormalizeText(request.BleSensorQuantity);
        record.BleSensorUnitPriceExVat = NormalizeText(request.BleSensorUnitPriceExVat);
        record.HasLvCanAdaptor = request.HasLvCanAdaptor;
        record.LvCanAdaptorPriceExVat = NormalizeText(request.LvCanAdaptorPriceExVat);
        record.OtherHardwareDescription = NormalizeText(request.OtherHardwareDescription);
        record.OtherHardwarePriceExVat = NormalizeText(request.OtherHardwarePriceExVat);
        record.BillingNotes = NormalizeText(request.BillingNotes);

        db.SaveChanges();
        return record;
    }

    private static void EnsureSchema(PurchaseOrderContext db)
    {
        db.Database.EnsureCreated();

        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS JobCards (
                JobCardRecordId INTEGER NOT NULL CONSTRAINT PK_JobCards PRIMARY KEY AUTOINCREMENT,
                SequenceNumber INTEGER NOT NULL,
                JobCardNumber TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                JobCardType TEXT NOT NULL DEFAULT 'Installation',
                WorkflowStatus TEXT NOT NULL,
                StatusNotes TEXT NULL,
                DetailsConfirmedAt TEXT NULL,
                LastAmendedAt TEXT NULL,
                AmendmentNotes TEXT NULL,
                EvidenceReceivedAt TEXT NULL,
                HasVehiclePhoto INTEGER NOT NULL DEFAULT 0,
                HasRegistrationPhoto INTEGER NOT NULL DEFAULT 0,
                HasVinPhoto INTEGER NOT NULL DEFAULT 0,
                HasTrackingUnitPhoto INTEGER NOT NULL DEFAULT 0,
                UseCustomBillingSystem INTEGER NOT NULL DEFAULT 0,
                CustomBillingSystemName TEXT NULL,
                SystemPriceExVat TEXT NULL,
                HasPanicButton INTEGER NOT NULL DEFAULT 0,
                PanicButtonPriceExVat TEXT NULL,
                HasEarlyWarningSystem INTEGER NOT NULL DEFAULT 0,
                EarlyWarningSystemPriceExVat TEXT NULL,
                BleSensorQuantity TEXT NULL,
                BleSensorUnitPriceExVat TEXT NULL,
                HasLvCanAdaptor INTEGER NOT NULL DEFAULT 0,
                LvCanAdaptorPriceExVat TEXT NULL,
                OtherHardwareDescription TEXT NULL,
                OtherHardwarePriceExVat TEXT NULL,
                BillingNotes TEXT NULL,
                WialonUnitId INTEGER NULL,
                WialonUnitName TEXT NULL,
                WialonAccountId INTEGER NULL,
                WialonAccountName TEXT NULL,
                WialonCreatorId INTEGER NULL,
                WialonCreatorName TEXT NULL,
                WialonHardwareTypeId INTEGER NULL,
                WialonHardwareTypeName TEXT NULL,
                JobCardName TEXT NOT NULL DEFAULT '',
                UniqueId TEXT NOT NULL DEFAULT '',
                Iccid TEXT NOT NULL DEFAULT '',
                PhoneNumber TEXT NULL,
                Brand TEXT NOT NULL DEFAULT '',
                Model TEXT NOT NULL DEFAULT '',
                Year TEXT NOT NULL DEFAULT '',
                Colour TEXT NOT NULL DEFAULT '',
                VehicleClass TEXT NOT NULL DEFAULT '',
                VehicleType TEXT NOT NULL DEFAULT '',
                RegistrationPlate TEXT NOT NULL DEFAULT '',
                Vin TEXT NOT NULL DEFAULT '',
                Client TEXT NOT NULL DEFAULT '',
                Contact1 TEXT NOT NULL DEFAULT '',
                Contact2 TEXT NOT NULL DEFAULT '',
                MakeAndModel TEXT NOT NULL DEFAULT '',
                RegistrationFleet TEXT NOT NULL DEFAULT ''
            )
            """);

        EnsureColumnExists(db, "UseCustomBillingSystem", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "JobCardType", "TEXT NOT NULL DEFAULT 'Installation'");
        EnsureColumnExists(db, "CustomBillingSystemName", "TEXT NULL");
        EnsureColumnExists(db, "SystemPriceExVat", "TEXT NULL");
        EnsureColumnExists(db, "HasPanicButton", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "PanicButtonPriceExVat", "TEXT NULL");
        EnsureColumnExists(db, "HasEarlyWarningSystem", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "EarlyWarningSystemPriceExVat", "TEXT NULL");
        EnsureColumnExists(db, "BleSensorQuantity", "TEXT NULL");
        EnsureColumnExists(db, "BleSensorUnitPriceExVat", "TEXT NULL");
        EnsureColumnExists(db, "HasLvCanAdaptor", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(db, "LvCanAdaptorPriceExVat", "TEXT NULL");
        EnsureColumnExists(db, "OtherHardwareDescription", "TEXT NULL");
        EnsureColumnExists(db, "OtherHardwarePriceExVat", "TEXT NULL");
        EnsureColumnExists(db, "BillingNotes", "TEXT NULL");

        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_JobCards_SequenceNumber ON JobCards (SequenceNumber)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_JobCards_JobCardNumber ON JobCards (JobCardNumber)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_JobCards_CreatedAt ON JobCards (CreatedAt)");
    }

    private static void EnsureColumnExists(PurchaseOrderContext db, string columnName, string columnDefinition)
    {
        if (ColumnExists(db, "JobCards", columnName))
        {
            return;
        }

        var sql = $"ALTER TABLE JobCards ADD COLUMN {columnName} {columnDefinition}";
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

    private static int GetNextSequenceNumber(PurchaseOrderContext db)
    {
        var maxSequence = db.JobCards
            .AsNoTracking()
            .Select(item => (int?)item.SequenceNumber)
            .Max();

        return Math.Max(SequenceStartingValue, (maxSequence ?? (SequenceStartingValue - 1)) + 1);
    }

    private static string FormatJobCardNumber(int sequenceNumber)
    {
        return $"{JobCardPrefix}-{sequenceNumber.ToString($"D{SequenceDigits}")}";
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static void ThrowIfDuplicateJobCardExists(
        PurchaseOrderContext db,
        string? registrationPlate,
        string? iccid,
        string? uniqueId)
    {
        var normalizedRegistration = NormalizeIdentifier(registrationPlate);
        var normalizedIccid = NormalizeIdentifier(iccid);
        var normalizedUniqueId = NormalizeIdentifier(uniqueId);

        if (string.IsNullOrWhiteSpace(normalizedRegistration) &&
            string.IsNullOrWhiteSpace(normalizedIccid) &&
            string.IsNullOrWhiteSpace(normalizedUniqueId))
        {
            return;
        }

        var records = db.JobCards
            .AsNoTracking()
            .OrderByDescending(item => item.SequenceNumber)
            .ToList();

        foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(normalizedRegistration) &&
                string.Equals(NormalizeIdentifier(record.RegistrationPlate), normalizedRegistration, StringComparison.OrdinalIgnoreCase))
            {
                throw BuildDuplicateException("registration", record.RegistrationPlate, record.JobCardNumber);
            }

            if (!string.IsNullOrWhiteSpace(normalizedIccid) &&
                string.Equals(NormalizeIdentifier(record.Iccid), normalizedIccid, StringComparison.OrdinalIgnoreCase))
            {
                throw BuildDuplicateException("ICCID", record.Iccid, record.JobCardNumber);
            }

            if (!string.IsNullOrWhiteSpace(normalizedUniqueId) &&
                string.Equals(NormalizeIdentifier(record.UniqueId), normalizedUniqueId, StringComparison.OrdinalIgnoreCase))
            {
                throw BuildDuplicateException("IMEI", record.UniqueId, record.JobCardNumber);
            }
        }
    }

    private static InvalidOperationException BuildDuplicateException(string fieldName, string fieldValue, string jobCardNumber)
    {
        return new InvalidOperationException(
            $"A job card already exists with this {fieldName} ({fieldValue}) on {jobCardNumber}. No duplicate registrations, ICCID numbers, or IMEI numbers are allowed.");
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
}
