using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;

namespace PurchaseOrderApp.ViewModels;

public partial class HomeWindowViewModel : ObservableObject
{
    private const string DefaultApiHost = "hst-api.wialon.eu";
    private readonly JobCardSecretStore secretStore = new();
    private readonly UserAccessService userAccessService = new();

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
        RefreshUsers();
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

    [ObservableProperty]
    private ObservableCollection<AppUser> users = [];

    [ObservableProperty]
    private AppUser? selectedUser;

    [ObservableProperty]
    private bool canAccessPurchaseOrders;

    [ObservableProperty]
    private bool canManagerApprovePurchaseOrders;

    [ObservableProperty]
    private bool canApprovePurchaseOrders;

    [ObservableProperty]
    private bool canAccessJobCards;

    [ObservableProperty]
    private bool canAccessWialonUnits;

    [ObservableProperty]
    private bool canAccessTrackingCertificates;

    [ObservableProperty]
    private bool canAccessInventory;

    [ObservableProperty]
    private bool canAccessConnectivitySettings;

    [ObservableProperty]
    private bool canManageUsers;

    private int? signedInUserId;

    public string CurrentRoleName => SelectedUser?.Role?.Name ?? "No role selected";

    public string CurrentAccessSummary =>
        SelectedUser is null
            ? "Select a user to enable workspace access."
            : $"{SelectedUser.DisplayName} is using the {CurrentRoleName} role.";

    public void RefreshUsers()
    {
        var selectedUserId = signedInUserId ?? SelectedUser?.AppUserId;
        var loadedUsers = userAccessService.GetActiveUsers();
        Users = new ObservableCollection<AppUser>(loadedUsers);
        SelectedUser = selectedUserId.HasValue
            ? Users.FirstOrDefault(user => user.AppUserId == selectedUserId.Value) ?? Users.FirstOrDefault()
            : Users.FirstOrDefault();
    }

    public void SetSignedInUser(AppUser user)
    {
        signedInUserId = user.AppUserId;
        SelectedUser = user;
    }

    public bool CanOpenWorkspace(string workspaceName)
    {
        return workspaceName switch
        {
            "Purchase Orders" => CanAccessPurchaseOrders,
            "Job Cards" => CanAccessJobCards,
            "Wialon Units" => CanAccessWialonUnits,
            "Tracking Certificates" => CanAccessTrackingCertificates,
            "Stock Inventory" => CanAccessInventory,
            "Connectivity Settings" => CanAccessConnectivitySettings,
            "User Management" => CanManageUsers,
            _ => false
        };
    }

    [RelayCommand]
    private void SaveConnectionSettings()
    {
        secretStore.Save(
            string.IsNullOrWhiteSpace(AccessToken) ? null : AccessToken,
            string.IsNullOrWhiteSpace(FlickswitchApiKey) ? null : FlickswitchApiKey,
            ApiHost);

        StatusMessage = "API settings saved. Wialon Units and Job Cards will use them automatically.";
    }

    partial void OnSelectedUserChanged(AppUser? value)
    {
        var permissions = userAccessService.GetPermissions(value);
        CanAccessPurchaseOrders = permissions.CanAccessPurchaseOrders;
        CanManagerApprovePurchaseOrders = permissions.CanManagerApprovePurchaseOrders;
        CanApprovePurchaseOrders = permissions.CanApprovePurchaseOrders;
        CanAccessJobCards = permissions.CanAccessJobCards;
        CanAccessWialonUnits = permissions.CanAccessWialonUnits;
        CanAccessTrackingCertificates = permissions.CanAccessTrackingCertificates;
        CanAccessInventory = permissions.CanAccessInventory;
        CanAccessConnectivitySettings = permissions.CanAccessConnectivitySettings;
        CanManageUsers = permissions.CanManageUsers;
        OnPropertyChanged(nameof(CurrentRoleName));
        OnPropertyChanged(nameof(CurrentAccessSummary));
    }
}
