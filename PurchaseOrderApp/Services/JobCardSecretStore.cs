using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PurchaseOrderApp.Services;

internal sealed class JobCardSecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PurchaseOrderApp.JobCardCredentials.v1");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PurchaseOrderApp",
        "jobcard-credentials.dat");

    internal JobCardCredentials Load()
    {
        try
        {
            if (!File.Exists(StorePath))
            {
                return new JobCardCredentials(null, null, null);
            }

            var protectedBytes = File.ReadAllBytes(StorePath);
            if (protectedBytes.Length == 0)
            {
                return new JobCardCredentials(null, null, null);
            }

            var jsonBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(jsonBytes);

            return JsonSerializer.Deserialize<JobCardCredentials>(json, JsonOptions)
                ?? new JobCardCredentials(null, null, null);
        }
        catch
        {
            return new JobCardCredentials(null, null, null);
        }
    }

    internal void Save(string? wialonAccessToken, string? flickswitchApiKey, string? apiHost)
    {
        var directory = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = JsonSerializer.Serialize(
            new JobCardCredentials(wialonAccessToken?.Trim(), flickswitchApiKey?.Trim(), apiHost?.Trim()),
            JsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(payload);
        var protectedBytes = ProtectedData.Protect(jsonBytes, Entropy, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(StorePath, protectedBytes);
    }
}

internal sealed record JobCardCredentials(string? WialonAccessToken, string? FlickswitchApiKey, string? ApiHost);
