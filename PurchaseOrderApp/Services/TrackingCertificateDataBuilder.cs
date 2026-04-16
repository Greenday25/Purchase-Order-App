using PurchaseOrderApp.Models;
using System.Linq;
using System.Text.RegularExpressions;

namespace PurchaseOrderApp.Services;

internal sealed class TrackingCertificateDataBuilder
{
    private static readonly Regex DeviceModelRegex = new(@"\b([A-Z]{2,6}\s?-?\d{2,5}[A-Z]?)\b", RegexOptions.Compiled);

    internal TrackingCertificateData Build(
        WialonUnitDetails details,
        string? fallbackHardwareTypeName = null,
        string? fallbackAccountLabel = null)
    {
        var hardwareTypeName = FirstNonEmpty(details.HardwareTypeName, fallbackHardwareTypeName);
        var trackerDeviceType = GetTrackerDeviceType(details);
        var typeOfSystem = ResolveCertificateSystemName(
            hardwareTypeName,
            trackerDeviceType,
            GetDetailFieldValue(details, "Type of System", "System Type", "type_of_system"));

        return new TrackingCertificateData
        {
            UnitId = details.UnitId,
            UnitName = details.Name,
            CustomerClient = FirstNonEmpty(
                GetDetailFieldValue(details, "Customer/Client", "Customer", "Client"),
                details.AccountLabel,
                fallbackAccountLabel) ?? string.Empty,
            RegistrationNumber = FirstNonEmpty(
                GetDetailFieldValue(details, "Registration Number", "Registration Plate", "registration_plate", "Registration & Fleet"),
                details.Name) ?? string.Empty,
            Vin = FirstNonEmpty(
                GetDetailFieldValue(details, "VIN", "vin"),
                details.UniqueId) ?? string.Empty,
            VehicleType = BuildVehicleType(details) ?? details.Name,
            Colour = GetDetailFieldValue(details, "Colour", "color", "colour") ?? string.Empty,
            SystemName = typeOfSystem,
            SerialNumber = FirstNonEmpty(details.UniqueId, details.UniqueId2) ?? string.Empty,
            TypeOfSystem = typeOfSystem ?? string.Empty,
            VesaSaiaNumber = "301719",
            InstallationDate = ResolveInstallationDate(details),
            UnitCreatedAt = details.CreatedAt
        };
    }

    private static string ResolveInstallationDate(WialonUnitDetails details)
    {
        if (details.CreatedAt.HasValue)
        {
            return details.CreatedAt.Value.ToLocalTime().ToString("dd.MM.yyyy");
        }

        var rawValue = GetDetailFieldValue(details, "Installation Date", "Install Date", "Date Installed", "installation_date");
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        return DateTime.TryParse(rawValue, out var parsedDate)
            ? parsedDate.ToString("dd.MM.yyyy")
            : rawValue.Trim();
    }

    private static string ResolveCertificateSystemName(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var extractedCandidate = ExtractCertificateSystemName(candidate);
            if (string.IsNullOrWhiteSpace(extractedCandidate))
            {
                continue;
            }

            if (!IsNumericOnly(extractedCandidate))
            {
                return extractedCandidate;
            }
        }

        return "STING";
    }

    private static string? BuildVehicleType(WialonUnitDetails details)
    {
        var combinedMakeAndModel = string.Join(" ",
            new[]
            {
                GetDetailFieldValue(details, "brand", "Brand"),
                GetDetailFieldValue(details, "model", "Model")
            }
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(combinedMakeAndModel))
        {
            return combinedMakeAndModel;
        }

        var explicitMakeAndModel = RemoveTrailingColour(
            GetDetailFieldValue(details, "Make & Model"),
            GetDetailFieldValue(details, "color", "colour", "Colour"));

        if (!string.IsNullOrWhiteSpace(explicitMakeAndModel))
        {
            return explicitMakeAndModel;
        }

        return GetDetailFieldValue(details, "Vehicle Type", "vehicle_type");
    }

    private static string? RemoveTrailingColour(string? vehicleType, string? colour)
    {
        var trimmedVehicleType = vehicleType?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedVehicleType))
        {
            return null;
        }

        var trimmedColour = colour?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedColour))
        {
            return trimmedVehicleType;
        }

        if (trimmedVehicleType.EndsWith(trimmedColour, StringComparison.OrdinalIgnoreCase))
        {
            var withoutColour = trimmedVehicleType[..^trimmedColour.Length].TrimEnd();
            return string.IsNullOrWhiteSpace(withoutColour) ? trimmedVehicleType : withoutColour;
        }

        return trimmedVehicleType;
    }

    private static string? GetTrackerDeviceType(WialonUnitDetails details)
    {
        return GetDetailFieldValue(
            details,
            "Device Model",
            "Device Type",
            "device_type",
            "Tracker Model",
            "Tracker Type",
            "Hardware Type");
    }

    private static string? ExtractCertificateSystemName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var bracketIndex = trimmed.IndexOf('(');
        if (bracketIndex > 0)
        {
            trimmed = trimmed[..bracketIndex].Trim();
        }

        var modelMatch = DeviceModelRegex.Match(trimmed.ToUpperInvariant());
        if (modelMatch.Success)
        {
            return Regex.Replace(modelMatch.Groups[1].Value.Trim(), @"\s+", " ");
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsNumericOnly(string value)
    {
        var compact = new string(value.Where(ch => !char.IsWhiteSpace(ch) && ch != '-' && ch != '/').ToArray());
        return compact.Length > 0 && compact.All(char.IsDigit);
    }

    private static string? GetDetailFieldValue(WialonUnitDetails details, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var match = details.Fields.FirstOrDefault(field =>
                string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match?.Value))
            {
                return match.Value.Trim();
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
