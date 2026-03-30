using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Services;

namespace PurchaseOrderApp.ViewModels;

public partial class HomeWindowViewModel : ObservableObject
{
    private const string DefaultApiHost = "hst-api.wialon.eu";
    private readonly JobCardSecretStore secretStore = new();

    public async Task InitializeAsync()
    {
        var credentials = secretStore.Load();
        var hasSavedSettings =
            !string.IsNullOrWhiteSpace(credentials.ApiHost) ||
            !string.IsNullOrWhiteSpace(credentials.WialonAccessToken) ||
            !string.IsNullOrWhiteSpace(credentials.FlickswitchApiKey);

        if (string.IsNullOrWhiteSpace(credentials.ApiHost))
        {
            ApiHost = DefaultApiHost;
        }
        else
        {
            var restoredHost = credentials.ApiHost.Trim();
            if (!string.Equals(restoredHost, "hst-api.wialon.com", StringComparison.OrdinalIgnoreCase))
            {
                ApiHost = restoredHost;
            }
        }

        if (!string.IsNullOrWhiteSpace(credentials.WialonAccessToken))
        {
            AccessToken = credentials.WialonAccessToken;
        }

        if (!string.IsNullOrWhiteSpace(credentials.FlickswitchApiKey))
        {
            FlickswitchApiKey = credentials.FlickswitchApiKey;
        }

        StatusMessage = hasSavedSettings
            ? "Connectivity settings are saved and ready for Wialon Units and Job Cards."
            : "No saved connectivity settings yet. Open Connectivity Settings to add them.";
        await Task.CompletedTask;
    }

    [ObservableProperty]
    private string apiHost = DefaultApiHost;

    [ObservableProperty]
    private string accessToken = string.Empty;

    [ObservableProperty]
    private string flickswitchApiKey = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Open Connectivity Settings to store the Wialon host, access token, and Flickswitch key.";

    [RelayCommand]
    private void SaveConnectionSettings()
    {
        secretStore.Save(
            string.IsNullOrWhiteSpace(AccessToken) ? null : AccessToken,
            string.IsNullOrWhiteSpace(FlickswitchApiKey) ? null : FlickswitchApiKey,
            ApiHost);

        StatusMessage = "API settings saved. Wialon Units and Job Cards will use them automatically.";
    }
}
