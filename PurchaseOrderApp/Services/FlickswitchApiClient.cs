using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace PurchaseOrderApp.Services;

internal sealed class FlickswitchApiClient
{
    private static readonly Uri SimsEndpoint = new("https://app.simcontrol.co.za/api/sims");
    private static readonly HttpClient SharedHttpClient = new();

    internal async Task<string> LookupMsisdnAsync(
        string apiKey,
        string iccid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new FlickswitchApiException("A Flickswitch API key is required.");
        }

        if (string.IsNullOrWhiteSpace(iccid))
        {
            throw new FlickswitchApiException("An ICCID is required.");
        }

        var sanitizedIccid = NormalizeIccid(iccid);
        if (string.IsNullOrWhiteSpace(sanitizedIccid))
        {
            throw new FlickswitchApiException("The ICCID is empty after removing formatting characters.");
        }

        var phoneNumber = await QueryMsisdnAsync(apiKey.Trim(), sanitizedIccid, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber;
        }

        throw new FlickswitchApiException($"No SIM was found for ICCID {sanitizedIccid}.");
    }

    private static async Task<string?> QueryMsisdnAsync(
        string apiKey,
        string iccid,
        CancellationToken cancellationToken)
    {
        var requestUri = new UriBuilder(SimsEndpoint)
        {
            Query = $"iccid={Uri.EscapeDataString(iccid)}&status=ALL&page_size=1"
        }.Uri;

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);

        using var response = await SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new FlickswitchApiException("Flickswitch rejected the API key. Check the key and try again.");
            }

            if (TryExtractErrorMessage(responseText, out var errorMessage))
            {
                throw new FlickswitchApiException(errorMessage);
            }

            throw new FlickswitchApiException(
                $"Flickswitch returned HTTP {(int)response.StatusCode} while looking up ICCID {iccid}.");
        }

        if (TryExtractMsisdn(responseText, out var msisdn))
        {
            return msisdn;
        }

        if (TryExtractErrorMessage(responseText, out var successErrorMessage))
        {
            throw new FlickswitchApiException(successErrorMessage);
        }

        return null;
    }

    private static bool TryExtractMsisdn(string responseText, out string? msisdn)
    {
        msisdn = null;

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var simElement in dataElement.EnumerateArray())
            {
                if (simElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var candidate = GetString(simElement, "msisdn");
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    msisdn = candidate.Trim();
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryExtractErrorMessage(string responseText, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var propertyName in new[] { "error", "message", "description" })
            {
                var candidate = GetString(document.RootElement, propertyName);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    errorMessage = candidate.Trim();
                    return true;
                }
            }

            if (document.RootElement.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Array &&
                errorsElement.GetArrayLength() > 0)
            {
                foreach (var errorElement in errorsElement.EnumerateArray())
                {
                    var candidate = GetString(errorElement, "detail") ?? GetString(errorElement, "title");
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        errorMessage = candidate.Trim();
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string NormalizeIccid(string iccid)
    {
        return new string(iccid.Where(char.IsLetterOrDigit).ToArray());
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}

internal sealed class FlickswitchApiException : Exception
{
    internal FlickswitchApiException(string message) : base(message)
    {
    }
}
