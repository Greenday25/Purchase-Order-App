using System.Globalization;

namespace PurchaseOrderApp.Models;

public static class JobCardBillingSystemTypes
{
    public const string QuickSting = "QUICK STING";
    public const string QuickStingPlus = "QUICK STING PLUS";
    public const string QuickStingFm = "QUICK STING FM";
    public const string Other = "OTHER";
}

internal static class JobCardBillingHelper
{
    internal static string ResolveSystemType(JobCardRecord record)
    {
        return ResolveSystemType(
            record.UseCustomBillingSystem,
            record.CustomBillingSystemName,
            record.HasPanicButton,
            record.HasEarlyWarningSystem,
            record.HasLvCanAdaptor);
    }

    internal static string ResolveSystemType(
        bool useCustomBillingSystem,
        string? customBillingSystemName,
        bool hasPanicButton,
        bool hasEarlyWarningSystem,
        bool hasLvCanAdaptor)
    {
        if (useCustomBillingSystem)
        {
            var customName = NormalizeText(customBillingSystemName);
            return string.IsNullOrWhiteSpace(customName)
                ? JobCardBillingSystemTypes.Other
                : customName.ToUpperInvariant();
        }

        if (hasLvCanAdaptor)
        {
            return JobCardBillingSystemTypes.QuickStingFm;
        }

        if (hasPanicButton || hasEarlyWarningSystem)
        {
            return JobCardBillingSystemTypes.QuickStingPlus;
        }

        return JobCardBillingSystemTypes.QuickSting;
    }

    internal static decimal? ParseAmount(string? rawValue)
    {
        var normalized = NormalizeText(rawValue);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var sanitized = normalized
            .Replace("R", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("ex vat", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (decimal.TryParse(
            sanitized,
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowThousands,
            CultureInfo.CurrentCulture,
            out var currentCultureAmount))
        {
            return currentCultureAmount;
        }

        if (decimal.TryParse(
            sanitized,
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var invariantAmount))
        {
            return invariantAmount;
        }

        return null;
    }

    internal static int ParseQuantity(string? rawValue)
    {
        var normalized = NormalizeText(rawValue);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity) && quantity > 0
            ? quantity
            : 0;
    }

    internal static decimal CalculateTotalExVat(JobCardRecord record)
    {
        return CalculateTotalExVat(
            record.SystemPriceExVat,
            record.HasPanicButton,
            record.PanicButtonPriceExVat,
            record.HasEarlyWarningSystem,
            record.EarlyWarningSystemPriceExVat,
            record.BleSensorQuantity,
            record.BleSensorUnitPriceExVat,
            record.HasLvCanAdaptor,
            record.LvCanAdaptorPriceExVat,
            record.OtherHardwarePriceExVat);
    }

    internal static decimal CalculateTotalExVat(
        string? systemPriceExVat,
        bool hasPanicButton,
        string? panicButtonPriceExVat,
        bool hasEarlyWarningSystem,
        string? earlyWarningSystemPriceExVat,
        string? bleSensorQuantity,
        string? bleSensorUnitPriceExVat,
        bool hasLvCanAdaptor,
        string? lvCanAdaptorPriceExVat,
        string? otherHardwarePriceExVat)
    {
        var total = ParseAmount(systemPriceExVat) ?? 0m;

        if (hasPanicButton)
        {
            total += ParseAmount(panicButtonPriceExVat) ?? 0m;
        }

        if (hasEarlyWarningSystem)
        {
            total += ParseAmount(earlyWarningSystemPriceExVat) ?? 0m;
        }

        var bleQuantity = ParseQuantity(bleSensorQuantity);
        if (bleQuantity > 0)
        {
            total += bleQuantity * (ParseAmount(bleSensorUnitPriceExVat) ?? 0m);
        }

        if (hasLvCanAdaptor)
        {
            total += ParseAmount(lvCanAdaptorPriceExVat) ?? 0m;
        }

        total += ParseAmount(otherHardwarePriceExVat) ?? 0m;
        return total;
    }

    internal static string FormatAmount(string? rawValue)
    {
        var amount = ParseAmount(rawValue);
        return amount.HasValue ? amount.Value.ToString("N2", CultureInfo.InvariantCulture) : "Pending";
    }

    internal static string FormatAmount(decimal amount)
    {
        return amount.ToString("N2", CultureInfo.InvariantCulture);
    }

    internal static string BuildOptionalLineDisplay(bool included, string? priceExVat)
    {
        return included
            ? $"Included | {FormatAmount(priceExVat)}"
            : "Not included";
    }

    internal static string BuildBleSensorDisplay(string? quantity, string? unitPriceExVat)
    {
        var parsedQuantity = ParseQuantity(quantity);
        if (parsedQuantity <= 0)
        {
            return "Not included";
        }

        var unitPrice = ParseAmount(unitPriceExVat) ?? 0m;
        var lineTotal = parsedQuantity * unitPrice;
        return $"{parsedQuantity} x {FormatAmount(unitPrice)} = {FormatAmount(lineTotal)}";
    }

    internal static string BuildOtherHardwareDisplay(string? description, string? priceExVat)
    {
        var normalizedDescription = NormalizeText(description);
        var amount = ParseAmount(priceExVat);

        if (string.IsNullOrWhiteSpace(normalizedDescription) && !amount.HasValue)
        {
            return "None";
        }

        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return FormatAmount(priceExVat);
        }

        return amount.HasValue
            ? $"{normalizedDescription} | {FormatAmount(amount.Value)}"
            : normalizedDescription;
    }

    internal static string NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : trimmed;
    }
}
