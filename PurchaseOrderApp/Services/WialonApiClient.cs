using PurchaseOrderApp.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace PurchaseOrderApp.Services;

internal sealed class WialonApiClient
{
    private const int UnitListFlags = 1439;
    private const long UnitDetailFlags = 4611686018427387903L;
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseUrl;

    internal WialonApiClient(string host)
    {
        _baseUrl = NormalizeHost(host);
    }

    internal async Task<WialonSession> LoginAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new WialonApiException("A Wialon access token is required.");
        }

        using var response = await SendAsync(
            BuildUri("token/login", new { token = token.Trim() }),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon login failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("eid", out var sidElement) || sidElement.ValueKind != JsonValueKind.String)
        {
            throw new WialonApiException("Wialon login succeeded, but no session ID was returned.");
        }

        var sid = sidElement.GetString();
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("Wialon login succeeded, but the session ID was empty.");
        }

        var creatorId = GetNullableLong(response.RootElement, "user", "id") ?? GetNullableLong(response.RootElement, "crt");
        var creatorName = GetString(response.RootElement, "au") ?? GetString(response.RootElement, "nm");
        return new WialonSession(sid, creatorId, creatorName);
    }

    internal async Task<IReadOnlyList<WialonUnitSummary>> GetUnitsAsync(string sid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        var request = new
        {
            spec = new
            {
                itemsType = "avl_unit",
                propName = "sys_name",
                propValueMask = "*",
                sortType = "sys_name"
            },
            force = 1,
            flags = UnitListFlags,
            from = 0,
            to = 0
        };

        using var response = await SendAsync(
            BuildUri("core/search_items", request, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon unit search failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var units = new List<WialonUnitSummary>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            units.Add(ParseUnit(item));
        }

        return units
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal async Task<WialonUnitDetails> GetUnitDetailsAsync(string sid, long unitId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        var response = await SendAsync(
            BuildUri("core/search_item", new { id = unitId, flags = UnitDetailFlags }, sid),
            cancellationToken).ConfigureAwait(false);

        using (response)
        {
            if (TryGetError(response, out var errorCode))
            {
                throw new WialonApiException($"Wialon unit profile lookup failed with error code {errorCode}.", errorCode);
            }

            if (!response.RootElement.TryGetProperty("item", out var itemElement) || itemElement.ValueKind != JsonValueKind.Object)
            {
                throw new WialonApiException("Wialon unit profile lookup did not return an item.");
            }

            return ParseUnitDetails(itemElement);
        }
    }

    internal async Task<WialonCreatedUnit> CreateUnitAsync(
        string sid,
        long creatorId,
        string name,
        long hardwareTypeId,
        long dataFlags = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        if (creatorId <= 0)
        {
            throw new WialonApiException("A valid Wialon creator ID is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new WialonApiException("A unit name is required.");
        }

        if (hardwareTypeId <= 0)
        {
            throw new WialonApiException("A valid hardware type ID is required.");
        }

        using var response = await SendAsync(
            BuildUri("core/create_unit", new
            {
                creatorId,
                name = name.Trim(),
                hwTypeId = hardwareTypeId,
                dataFlags
            }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            if (errorCode == 1)
            {
                throw new WialonApiException(
                    "Wialon says the session is invalid while creating the unit. Double-check that the API host matches your Wialon region, then load Wialon setup again.",
                    errorCode);
            }

            throw new WialonApiException($"Wialon unit creation failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("item", out var itemElement) || itemElement.ValueKind != JsonValueKind.Object)
        {
            throw new WialonApiException("Wialon unit creation did not return the created unit.");
        }

        var unitId = GetNullableLong(itemElement, "id");
        if (!unitId.HasValue)
        {
            throw new WialonApiException("Wialon unit creation succeeded, but no unit ID was returned.");
        }

        return new WialonCreatedUnit(
            unitId.Value,
            GetString(itemElement, "nm") ?? name.Trim(),
            GetNullableLong(itemElement, "hw") ?? hardwareTypeId);
    }

    internal async Task ChangeAccountAsync(
        string sid,
        long unitId,
        long resourceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        if (unitId <= 0)
        {
            throw new WialonApiException("A valid Wialon unit ID is required.");
        }

        if (resourceId <= 0)
        {
            throw new WialonApiException("A valid Wialon account ID is required.");
        }

        using var response = await SendAsync(
            BuildUri("account/change_account", new
            {
                itemId = unitId,
                resourceId
            }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon account transfer failed with error code {errorCode}.", errorCode);
        }
    }

    internal async Task<string> UpdatePhoneAsync(
        string sid,
        long unitId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        if (unitId <= 0)
        {
            throw new WialonApiException("A valid Wialon unit ID is required.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new WialonApiException("A phone number is required.");
        }

        using var response = await SendAsync(
            BuildUri("unit/update_phone", new
            {
                itemId = unitId,
                phoneNumber = phoneNumber.Trim()
            }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon phone update failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("ph", out var phoneElement))
        {
            return phoneNumber.Trim();
        }

        var updatedPhone = GetText(phoneElement) ?? phoneNumber.Trim();
        return string.IsNullOrWhiteSpace(updatedPhone) ? phoneNumber.Trim() : updatedPhone;
    }

    internal async Task<WialonDeviceTypeUpdateResult> UpdateDeviceTypeAsync(
        string sid,
        long unitId,
        long deviceTypeId,
        string uniqueId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        if (unitId <= 0)
        {
            throw new WialonApiException("A valid Wialon unit ID is required.");
        }

        if (deviceTypeId <= 0)
        {
            throw new WialonApiException("A valid device type ID is required.");
        }

        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            throw new WialonApiException("A unique ID is required.");
        }

        using var response = await SendAsync(
            BuildUri("unit/update_device_type", new
            {
                itemId = unitId,
                deviceTypeId,
                uniqueId = uniqueId.Trim()
            }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon device type update failed with error code {errorCode}.", errorCode);
        }

        var uniqueIdValue = GetString(response.RootElement, "uid") ?? uniqueId.Trim();
        var hardwareTypeValue = GetNullableLong(response.RootElement, "hw") ?? deviceTypeId;
        return new WialonDeviceTypeUpdateResult(uniqueIdValue, hardwareTypeValue);
    }

    internal async Task UpdateProfileFieldAsync(
        string sid,
        long itemId,
        string fieldName,
        string fieldValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        if (itemId <= 0)
        {
            throw new WialonApiException("A valid Wialon item ID is required.");
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new WialonApiException("A profile field name is required.");
        }

        if (string.IsNullOrWhiteSpace(fieldValue))
        {
            return;
        }

        using var response = await SendAsync(
            BuildUri("item/update_profile_field", new
            {
                itemId,
                n = fieldName.Trim(),
                v = fieldValue.Trim()
            }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon profile field update failed with error code {errorCode}.", errorCode);
        }
    }

    internal async Task UpdateCustomFieldAsync(
        string sid,
        long itemId,
        string fieldName,
        string fieldValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        if (itemId <= 0)
        {
            throw new WialonApiException("A valid Wialon item ID is required.");
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new WialonApiException("A custom field name is required.");
        }

        if (string.IsNullOrWhiteSpace(fieldValue))
        {
            return;
        }

        using var response = await SendAsync(
            BuildUri("item/update_custom_field", new
            {
                itemId,
                id = 0,
                callMode = "create",
                n = fieldName.Trim(),
                v = fieldValue.Trim()
            }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon custom field update failed with error code {errorCode}.", errorCode);
        }
    }

    internal async Task<IReadOnlyDictionary<long, string>> GetHardwareTypeNamesAsync(
        string sid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        using var response = await SendAsync(
            BuildUri("core/get_hw_types", new { includeType = true }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon hardware type lookup failed with error code {errorCode}.", errorCode);
        }

        if (response.RootElement.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<long, string>();
        }

        var hardwareTypes = new Dictionary<long, string>();
        foreach (var item in response.RootElement.EnumerateArray())
        {
            var name = GetString(item, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var label = name.Trim();

            var id = GetNullableLong(item, "id");
            if (id.HasValue && !hardwareTypes.ContainsKey(id.Value))
            {
                hardwareTypes[id.Value] = label;
            }

            var secondaryId = GetNullableLong(item, "uid2");
            if (secondaryId.HasValue && !hardwareTypes.ContainsKey(secondaryId.Value))
            {
                hardwareTypes[secondaryId.Value] = label;
            }
        }

        return hardwareTypes;
    }

    internal async Task<IReadOnlyList<WialonAccountOption>> GetAccountOptionsAsync(
        string sid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        var request = new
        {
            spec = new
            {
                itemsType = "avl_resource",
                propName = "rel_is_account",
                propValueMask = "1",
                sortType = "sys_name",
                propType = "property"
            },
            force = 1,
            flags = 5,
            from = 0,
            to = 0
        };

        using var response = await SendAsync(
            BuildUri("core/search_items", request, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon account lookup failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var accounts = new List<WialonAccountOption>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            var id = GetNullableLong(item, "id");
            var name = GetString(item, "nm");
            var creatorId = GetNullableLong(item, "crt");

            if (id.HasValue && !string.IsNullOrWhiteSpace(name))
            {
                accounts.Add(new WialonAccountOption
                {
                    AccountId = id.Value,
                    AccountName = name.Trim(),
                    CreatorId = creatorId ?? 0
                });
            }
        }

        return accounts
            .OrderBy(item => item.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.AccountId)
            .ToList();
    }

    internal async Task<IReadOnlyDictionary<long, string>> GetAccountNamesAsync(
        string sid,
        CancellationToken cancellationToken = default)
    {
        var accountOptions = await GetAccountOptionsAsync(sid, cancellationToken).ConfigureAwait(false);
        return accountOptions.ToDictionary(item => item.AccountId, item => item.AccountName);
    }

    internal async Task<IReadOnlyDictionary<long, string>> GetUserNamesAsync(
        string sid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        var request = new
        {
            spec = new
            {
                itemsType = "user",
                propName = "sys_name",
                propValueMask = "*",
                sortType = "sys_name",
                propType = "property"
            },
            force = 1,
            flags = 1,
            from = 0,
            to = 0
        };

        using var response = await SendAsync(
            BuildUri("core/search_items", request, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon user lookup failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<long, string>();
        }

        var users = new Dictionary<long, string>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            var id = GetNullableLong(item, "id");
            var name = GetString(item, "nm");
            if (id.HasValue && !string.IsNullOrWhiteSpace(name))
            {
                users[id.Value] = name.Trim();
            }
        }

        return users;
    }

    internal async Task UpdateItemAccessAsync(
        string sid,
        long userId,
        long itemId,
        long accessMask,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        if (userId <= 0)
        {
            throw new WialonApiException("A valid Wialon user ID is required.");
        }

        if (itemId <= 0)
        {
            throw new WialonApiException("A valid Wialon item ID is required.");
        }

        using var response = await SendAsync(
            BuildUri("user/update_item_access", new
            {
                userId,
                itemId,
                accessMask
            }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon access update failed with error code {errorCode}.", errorCode);
        }
    }

    private async Task<JsonDocument> SendAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Uri BuildUri(string service, object parameters, string? sid = null)
    {
        var builder = new UriBuilder(_baseUrl)
        {
            Path = "wialon/ajax.html"
        };

        var queryParts = new List<string>
        {
            $"svc={Uri.EscapeDataString(service)}",
            $"params={Uri.EscapeDataString(JsonSerializer.Serialize(parameters, JsonOptions))}"
        };

        if (!string.IsNullOrWhiteSpace(sid))
        {
            queryParts.Add($"sid={Uri.EscapeDataString(sid)}");
        }

        builder.Query = string.Join("&", queryParts);
        return builder.Uri;
    }

    private static bool TryGetError(JsonDocument response, out int errorCode)
    {
        if (response.RootElement.ValueKind == JsonValueKind.Object &&
            response.RootElement.TryGetProperty("error", out var errorElement) &&
            errorElement.ValueKind == JsonValueKind.Number &&
            errorElement.TryGetInt32(out errorCode) &&
            errorCode != 0)
        {
            return true;
        }

        errorCode = 0;
        return false;
    }

    private static WialonUnitSummary ParseUnit(JsonElement item)
    {
        var unitId = GetInt64(item, "id");
        var name = GetString(item, "nm") ?? string.Empty;
        var uniqueId = GetString(item, "sys_unique_id") ?? GetString(item, "uid") ?? GetString(item, "uid2");
        var phoneNumber = GetString(item, "sys_phone_number") ?? GetString(item, "ph") ?? GetString(item, "ph2");
        var accountId = GetNullableLong(item, "bact") ?? GetNullableLong(item, "sys_billing_account_guid");
        var accountLabel = GetString(item, "rel_billing_account_name");
        var hardwareTypeId = GetNullableLong(item, "rel_hw_type_id") ?? GetNullableInt64(item, "hw");
        var hardwareTypeName = GetString(item, "rel_hw_type_name");
        var latitude = GetNullableDouble(item, "pos", "y");
        var longitude = GetNullableDouble(item, "pos", "x");
        var lastMessageUnix = GetNullableLong(item, "lmsg", "t") ?? GetNullableLong(item, "pos", "t");

        return new WialonUnitSummary
        {
            UnitId = unitId,
            Name = name,
            UniqueId = uniqueId,
            PhoneNumber = phoneNumber,
            AccountId = accountId,
            AccountLabel = accountLabel,
            HardwareTypeId = hardwareTypeId,
            HardwareTypeName = hardwareTypeName,
            AccessRights = GetNullableLong(item, "uacl") ?? 0,
            LastMessageAt = lastMessageUnix.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(lastMessageUnix.Value)
                : null,
            Latitude = latitude,
            Longitude = longitude
        };
    }

    private static WialonUnitDetails ParseUnitDetails(JsonElement item)
    {
        var summary = ParseUnit(item);
        var profileFields = ParseFieldGroup(item, "pflds", "Profile");
        var customFields = ParseFieldGroup(item, "flds", "Custom");
        var adminFields = ParseFieldGroup(item, "aflds", "Admin");

        return new WialonUnitDetails
        {
            UnitId = summary.UnitId,
            Name = summary.Name,
            UniqueId = summary.UniqueId,
            UniqueId2 = GetString(item, "uid2"),
            PhoneNumber = summary.PhoneNumber,
            PhoneNumber2 = GetString(item, "ph2"),
            AccountId = summary.AccountId,
            AccountLabel = summary.AccountLabel,
            HardwareTypeId = summary.HardwareTypeId,
            HardwareTypeName = summary.HardwareTypeName,
            Guid = GetString(item, "gd"),
            AccessRights = GetNullableLong(item, "uacl") ?? 0,
            LastMessageAt = summary.LastMessageAt,
            Latitude = summary.Latitude,
            Longitude = summary.Longitude,
            Fields = profileFields
                .Concat(customFields)
                .Concat(adminFields)
                .OrderBy(field => field.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static List<WialonUnitDetailField> ParseFieldGroup(JsonElement item, string propertyName, string category)
    {
        if (!item.TryGetProperty(propertyName, out var groupElement) || groupElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var fields = new List<WialonUnitDetailField>();
        foreach (var fieldEntry in groupElement.EnumerateObject())
        {
            if (fieldEntry.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fieldId = GetNullableInt64(fieldEntry.Value, "id")?.ToString() ?? fieldEntry.Name;
            var fieldName = GetString(fieldEntry.Value, "n") ?? fieldEntry.Name;
            var fieldValue = GetText(fieldEntry.Value, "v") ?? string.Empty;

            fields.Add(new WialonUnitDetailField
            {
                Category = category,
                FieldId = fieldId,
                Name = fieldName,
                Value = fieldValue
            });
        }

        return fields;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return FindStringRecursive(element, propertyName);
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static long GetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result) ? result : 0;
    }

    private static long? GetNullableInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return FindNullableInt64Recursive(element, propertyName);
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static long? GetNullableLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return FindNullableLongRecursive(element, propertyName);
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static string? GetText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }

    private static long? GetNullableLong(JsonElement element, string parentPropertyName, string childPropertyName)
    {
        if (!element.TryGetProperty(parentPropertyName, out var parentElement) || parentElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parentElement.TryGetProperty(childPropertyName, out var childElement))
        {
            return null;
        }

        return childElement.ValueKind == JsonValueKind.Number && childElement.TryGetInt64(out var result)
            ? result
            : null;
    }

    internal async Task<string?> GetItemNameAsync(string sid, long itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        using var response = await SendAsync(
            BuildUri("core/search_item", new { id = itemId, flags = 1 }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon item lookup failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("item", out var itemElement) || itemElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(itemElement, "nm");
    }

    internal async Task<string?> GetAccountNameAsync(string sid, long accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        var accountIdText = accountId.ToString(CultureInfo.InvariantCulture);
        var accountName = await TrySearchAccountNameAsync(
            sid,
            "sys_id",
            accountIdText,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(accountName))
        {
            return accountName.Trim();
        }

        accountName = await TrySearchAccountNameAsync(
            sid,
            "sys_billing_account_guid",
            accountIdText,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(accountName))
        {
            return accountName.Trim();
        }

        accountName = await GetItemNameAsync(sid, accountId, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(accountName) ? null : accountName.Trim();
    }

    internal async Task<string?> GetAccountNameFromChangeListAsync(
        string sid,
        long unitId,
        long accountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new WialonApiException("A Wialon session ID is required.");
        }

        using var response = await SendAsync(
            BuildUri("account/list_change_accounts", new { units = new[] { unitId } }, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon account lookup failed with error code {errorCode}.", errorCode);
        }

        if (response.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var matches = new List<(long Id, string Name)>();
        foreach (var item in response.RootElement.EnumerateArray())
        {
            var id = GetNullableLong(item, "id");
            var name = GetString(item, "name");
            if (id.HasValue && !string.IsNullOrWhiteSpace(name))
            {
                matches.Add((id.Value, name.Trim()));
            }
        }

        var exactMatch = matches.FirstOrDefault(match => match.Id == accountId);
        if (!string.IsNullOrWhiteSpace(exactMatch.Name))
        {
            return exactMatch.Name;
        }

        return null;
    }

    private static double? GetNullableDouble(JsonElement element, string parentPropertyName, string childPropertyName)
    {
        if (!element.TryGetProperty(parentPropertyName, out var parentElement) || parentElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parentElement.TryGetProperty(childPropertyName, out var childElement))
        {
            return null;
        }

        return childElement.ValueKind == JsonValueKind.Number && childElement.TryGetDouble(out var result)
            ? result
            : null;
    }

    private static string NormalizeHost(string host)
    {
        var trimmed = (host ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            trimmed = "hst-api.wialon.eu";
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"https://{trimmed}";
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static string? FindStringRecursive(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(propertyName))
                    {
                        var directValue = GetText(property.Value);
                        if (!string.IsNullOrWhiteSpace(directValue))
                        {
                            return directValue;
                        }
                    }

                    var nested = FindStringRecursive(property.Value, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    var nested = FindStringRecursive(child, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
                break;
        }

        return null;
    }

    private static long? FindNullableInt64Recursive(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(propertyName))
                    {
                        var directValue = GetInt64Value(property.Value);
                        if (directValue.HasValue)
                        {
                            return directValue;
                        }
                    }

                    var nested = FindNullableInt64Recursive(property.Value, propertyName);
                    if (nested.HasValue)
                    {
                        return nested;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    var nested = FindNullableInt64Recursive(child, propertyName);
                    if (nested.HasValue)
                    {
                        return nested;
                    }
                }
                break;
        }

        return null;
    }

    private static long? FindNullableLongRecursive(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(propertyName))
                    {
                        var directValue = GetInt64Value(property.Value);
                        if (directValue.HasValue)
                        {
                            return directValue;
                        }
                    }

                    var nested = FindNullableLongRecursive(property.Value, propertyName);
                    if (nested.HasValue)
                    {
                        return nested;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    var nested = FindNullableLongRecursive(child, propertyName);
                    if (nested.HasValue)
                    {
                        return nested;
                    }
                }
                break;
        }

        return null;
    }

    private static string? GetText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }

    private static long? GetInt64Value(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private async Task<string?> TrySearchAccountNameAsync(
        string sid,
        string propertyName,
        string propertyValue,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            spec = new
            {
                itemsType = "avl_resource",
                propName = $"rel_is_account,{propertyName}",
                propValueMask = $"1,{propertyValue}",
                sortType = "sys_name",
                propType = "property,property"
            },
            force = 1,
            flags = 1,
            from = 0,
            to = 0
        };

        using var response = await SendAsync(
            BuildUri("core/search_items", request, sid),
            cancellationToken).ConfigureAwait(false);

        if (TryGetError(response, out var errorCode))
        {
            throw new WialonApiException($"Wialon account lookup failed with error code {errorCode}.", errorCode);
        }

        if (!response.RootElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in itemsElement.EnumerateArray())
        {
            var label = GetString(item, "nm") ?? GetString(item, "rel_billing_account_name");
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }
        }

        return null;
    }
}

internal sealed record WialonSession(string SessionId, long? CreatorId, string? CreatorName);

internal sealed record WialonCreatedUnit(long UnitId, string Name, long HardwareTypeId);

internal sealed record WialonDeviceTypeUpdateResult(string UniqueId, long HardwareTypeId);

internal sealed class WialonApiException : Exception
{
    internal WialonApiException(string message, int? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }

    internal int? ErrorCode { get; }
}

internal sealed class WialonUnitDetails
{
    public long UnitId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? UniqueId { get; init; }

    public string? UniqueId2 { get; init; }

    public string? PhoneNumber { get; init; }

    public string? PhoneNumber2 { get; init; }

    public long? AccountId { get; init; }

    public string? AccountLabel { get; init; }

    public long? HardwareTypeId { get; init; }

    public string? HardwareTypeName { get; init; }

    public string? Guid { get; init; }

    public DateTimeOffset? LastMessageAt { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public long AccessRights { get; init; }

    public IReadOnlyList<WialonUnitDetailField> Fields { get; init; } = [];
}

internal sealed class WialonUnitDetailField
{
    public string Category { get; init; } = string.Empty;

    public string FieldId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
