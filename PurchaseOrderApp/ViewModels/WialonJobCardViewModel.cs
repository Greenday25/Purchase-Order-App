using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;

namespace PurchaseOrderApp.ViewModels;

public partial class WialonJobCardViewModel : ObservableObject
{
    private const string DefaultApiHost = "hst-api.wialon.eu";
    private const long ReadOnlyUnitAccessMask = 0x04000001;
    private static readonly string[] StandardReadOnlyUserNames =
    {
        "Sindi Mntambo",
        "Recovery Controller"
    };
    private readonly JobCardSecretStore secretStore = new();
    private string? currentSessionId;
    private long? creatorId;
    private IReadOnlyDictionary<long, string> availableUsers = new Dictionary<long, string>();
    private bool isInitialized;

    public WialonJobCardViewModel()
    {
        StatusMessage = "Connect to Wialon, load the hardware types, then fill in the job card.";
    }

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;

        var credentials = secretStore.Load();
        var restoredAccessToken = false;
        var restoredFlickswitchKey = false;

        if (string.IsNullOrWhiteSpace(AccessToken) &&
            !string.IsNullOrWhiteSpace(credentials.WialonAccessToken))
        {
            AccessToken = credentials.WialonAccessToken;
            restoredAccessToken = true;
        }

        if (string.IsNullOrWhiteSpace(FlickswitchApiKey) &&
            !string.IsNullOrWhiteSpace(credentials.FlickswitchApiKey))
        {
            FlickswitchApiKey = credentials.FlickswitchApiKey;
            restoredFlickswitchKey = true;
        }

        if (!string.IsNullOrWhiteSpace(credentials.ApiHost))
        {
            var restoredHost = credentials.ApiHost.Trim();
            if (!string.Equals(restoredHost, "hst-api.wialon.com", StringComparison.OrdinalIgnoreCase))
            {
                ApiHost = restoredHost;
            }
        }

        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            if (restoredAccessToken || restoredFlickswitchKey)
            {
                StatusMessage = "Saved job card credentials loaded. Enter the Wialon access token to connect automatically next time.";
            }

            return;
        }

        await LoadWialonSetupAsync().ConfigureAwait(true);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadWialonSetupCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string apiHost = DefaultApiHost;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadWialonSetupCommand))]
    private string accessToken = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string jobCardName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string uniqueId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string iccid = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string flickswitchApiKey = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private WialonAccountOption? selectedAccount;

    partial void OnSelectedAccountChanged(WialonAccountOption? value)
    {
        var accountName = value?.AccountName?.Trim() ?? string.Empty;
        if (!string.Equals(Client, accountName, StringComparison.Ordinal))
        {
            Client = accountName;
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string brand = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string model = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string year = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string colour = string.Empty;

    partial void OnBrandChanged(string value) => UpdateMakeAndModel();

    partial void OnModelChanged(string value) => UpdateMakeAndModel();

    partial void OnColourChanged(string value) => UpdateMakeAndModel();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string vehicleClass = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string vehicleType = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string registrationPlate = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string vin = string.Empty;

    partial void OnRegistrationPlateChanged(string value)
    {
        UpdateJobCardName();
        UpdateRegistrationFleet();
    }

    partial void OnVinChanged(string value) => UpdateJobCardName();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string client = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string contact1 = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string contact2 = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string makeAndModel = string.Empty;

    [ObservableProperty]
    private string registrationFleet = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private WialonHardwareTypeOption? selectedHardwareType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredHardwareTypes))]
    private ObservableCollection<WialonHardwareTypeOption> hardwareTypes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredHardwareTypes))]
    private string hardwareTypeSearchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCreatorDisplay))]
    private string? creatorName;

    [ObservableProperty]
    private long? createdUnitId;

    [ObservableProperty]
    private string? createdUnitName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedPhoneDisplay))]
    private string? resolvedPhoneNumber;

    [ObservableProperty]
    private ObservableCollection<WialonAccountOption> accountOptions = [];

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public string CurrentCreatorDisplay =>
        creatorId.HasValue
            ? !string.IsNullOrWhiteSpace(CreatorName)
                ? $"{CreatorName} ({creatorId.Value})"
                : creatorId.Value.ToString(CultureInfo.InvariantCulture)
            : "Not connected";

    public IEnumerable<WialonHardwareTypeOption> FilteredHardwareTypes =>
        string.IsNullOrWhiteSpace(HardwareTypeSearchText)
            ? HardwareTypes
            : HardwareTypes.Where(option =>
                option.DisplayText.Contains(HardwareTypeSearchText.Trim(), StringComparison.OrdinalIgnoreCase));

    public string ResolvedPhoneDisplay =>
        string.IsNullOrWhiteSpace(ResolvedPhoneNumber)
            ? "Pending"
            : ResolvedPhoneNumber;

    public bool IsWialonConnected => !string.IsNullOrWhiteSpace(currentSessionId) && creatorId.HasValue;

    [RelayCommand(CanExecute = nameof(CanLoadWialonSetup))]
    private async Task LoadWialonSetupAsync()
    {
        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            StatusMessage = "Enter a Wialon access token first.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Connecting to Wialon...";

            HardwareTypes = [];
            HardwareTypeSearchText = string.Empty;
            AccountOptions = [];
            availableUsers = new Dictionary<long, string>();
            SelectedHardwareType = null;
            SelectedAccount = null;
            CreatedUnitId = null;
            CreatedUnitName = null;
            ResolvedPhoneNumber = null;
            CreatorName = null;
            currentSessionId = null;
            creatorId = null;
            OnPropertyChanged(nameof(CurrentCreatorDisplay));

            var client = new WialonApiClient(ApiHost);
            var session = await client.LoginAsync(AccessToken.Trim()).ConfigureAwait(true);
            currentSessionId = session.SessionId;
            creatorId = session.CreatorId;
            CreatorName = session.CreatorName;
            OnPropertyChanged(nameof(CurrentCreatorDisplay));

            StatusMessage = "Loading hardware types and accounts...";
            var hardwareTypesTask = client.GetHardwareTypeNamesAsync(currentSessionId);
            var accountsTask = client.GetAccountOptionsAsync(currentSessionId);
            var usersTask = client.GetUserNamesAsync(currentSessionId);

            var hardwareTypes = await hardwareTypesTask.ConfigureAwait(true);
            var accounts = await accountsTask.ConfigureAwait(true);
            var users = await usersTask.ConfigureAwait(true);
            var options = hardwareTypes
                .OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Key)
                .Select(item => new WialonHardwareTypeOption
                {
                    HardwareTypeId = item.Key,
                    DisplayText = $"{item.Value} ({item.Key})"
                })
                .ToList();

            HardwareTypes = new ObservableCollection<WialonHardwareTypeOption>(options);
            if (HardwareTypes.Count == 1)
            {
                SelectedHardwareType = HardwareTypes[0];
            }

            AccountOptions = new ObservableCollection<WialonAccountOption>(accounts);
            SelectedAccount = AccountOptions.FirstOrDefault(option =>
                creatorId.HasValue &&
                option.CreatorId == creatorId.Value)
                ?? (AccountOptions.Count == 1 ? AccountOptions[0] : null);
            availableUsers = users;

            SaveCredentials();

            StatusMessage = $"Connected to {CurrentCreatorDisplay} and loaded {HardwareTypes.Count} hardware type(s), {AccountOptions.Count} account(s), and {availableUsers.Count} user(s).";
            if (SelectedAccount is null && AccountOptions.Count > 0)
            {
                StatusMessage += " Select the account to create under.";
            }
        }
        catch (WialonApiException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Unable to reach Wialon: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error while connecting to Wialon: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CreateJobCardCommand.NotifyCanExecuteChanged();
            LoadWialonSetupCommand.NotifyCanExecuteChanged();
        }
    }

    private void UpdateMakeAndModel()
    {
        var combined = string.Join(" ",
            new[] { Brand, Model, Colour }
                .Select(value => value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.Equals(MakeAndModel, combined, StringComparison.Ordinal))
        {
            MakeAndModel = combined;
        }
    }

    private void UpdateJobCardName()
    {
        var resolvedName = GetResolvedUnitName();
        if (!string.Equals(JobCardName, resolvedName, StringComparison.Ordinal))
        {
            JobCardName = resolvedName;
        }
    }

    private void UpdateRegistrationFleet()
    {
        var fleetValue = RegistrationPlate?.Trim() ?? string.Empty;
        if (!string.Equals(RegistrationFleet, fleetValue, StringComparison.Ordinal))
        {
            RegistrationFleet = fleetValue;
        }
    }

    private string GetResolvedUnitName()
    {
        var registration = RegistrationPlate?.Trim();
        if (!string.IsNullOrWhiteSpace(registration))
        {
            return registration;
        }

        var vinValue = Vin?.Trim();
        if (!string.IsNullOrWhiteSpace(vinValue))
        {
            return vinValue;
        }

        return string.Empty;
    }

    public void SaveCredentials()
    {
        try
        {
            var existing = secretStore.Load();
            var wialonAccessToken = !string.IsNullOrWhiteSpace(AccessToken)
                ? AccessToken.Trim()
                : existing.WialonAccessToken;
            var flickswitchApiKey = !string.IsNullOrWhiteSpace(FlickswitchApiKey)
                ? FlickswitchApiKey.Trim()
                : existing.FlickswitchApiKey;

            if (string.IsNullOrWhiteSpace(wialonAccessToken) && string.IsNullOrWhiteSpace(flickswitchApiKey))
            {
                return;
            }

            secretStore.Save(wialonAccessToken, flickswitchApiKey, ApiHost);
        }
        catch
        {
            // Saving credentials should never block the job card workflow.
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateJobCard))]
    private async Task CreateJobCardAsync()
    {
        if (!IsWialonConnected)
        {
            StatusMessage = "Load the Wialon setup before creating the job card.";
            return;
        }

        if (SelectedHardwareType is null)
        {
            StatusMessage = "Select a hardware type first.";
            return;
        }

        if (SelectedAccount is null)
        {
            StatusMessage = "Select the Wialon account to create the unit under.";
            return;
        }

        if (SelectedAccount.CreatorId <= 0)
        {
            StatusMessage = $"The selected account {SelectedAccount.AccountName} does not expose a valid creator ID.";
            return;
        }

        SaveCredentials();

        try
        {
            IsBusy = true;
            var readOnlyAccessTargets = ResolveReadOnlyAccessTargets();
            StatusMessage = "Looking up the phone number from Flickswitch...";

            var flickswitchClient = new FlickswitchApiClient();
            var phoneNumber = await flickswitchClient.LookupMsisdnAsync(
                FlickswitchApiKey.Trim(),
                Iccid.Trim()).ConfigureAwait(true);

            ResolvedPhoneNumber = phoneNumber;

            var wialonClient = new WialonApiClient(ApiHost);
            StatusMessage = $"Creating the unit under {SelectedAccount.AccountName}...";
            var createdUnit = await wialonClient.CreateUnitAsync(
                currentSessionId!,
                SelectedAccount.CreatorId,
                GetResolvedUnitName(),
                SelectedHardwareType.HardwareTypeId).ConfigureAwait(true);

            CreatedUnitId = createdUnit.UnitId;
            CreatedUnitName = createdUnit.Name;

            StatusMessage = "Applying device type, phone number, and job card fields...";
            await wialonClient.UpdateDeviceTypeAsync(
                currentSessionId!,
                createdUnit.UnitId,
                SelectedHardwareType.HardwareTypeId,
                UniqueId.Trim()).ConfigureAwait(true);

            await wialonClient.UpdatePhoneAsync(
                currentSessionId!,
                createdUnit.UnitId,
                phoneNumber).ConfigureAwait(true);

            await ApplyProfileFieldsAsync(wialonClient, currentSessionId!, createdUnit.UnitId).ConfigureAwait(true);
            await ApplyCustomFieldsAsync(wialonClient, currentSessionId!, createdUnit.UnitId).ConfigureAwait(true);
            await ApplyReadOnlyAccessAsync(
                wialonClient,
                currentSessionId!,
                createdUnit.UnitId,
                readOnlyAccessTargets).ConfigureAwait(true);

            SaveCredentials();
            StatusMessage = $"Created Wialon unit {createdUnit.Name} ({createdUnit.UnitId}) under {SelectedAccount.AccountName}.";
        }
        catch (FlickswitchApiException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (WialonApiException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Unable to reach the API: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error while creating the job card: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CreateJobCardCommand.NotifyCanExecuteChanged();
            LoadWialonSetupCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanLoadWialonSetup()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(AccessToken);
    }

    private bool CanCreateJobCard()
    {
        return !IsBusy &&
               IsWialonConnected &&
               SelectedHardwareType is not null &&
               SelectedAccount is not null &&
               SelectedAccount.CreatorId > 0 &&
               !string.IsNullOrWhiteSpace(GetResolvedUnitName()) &&
               !string.IsNullOrWhiteSpace(UniqueId) &&
               !string.IsNullOrWhiteSpace(Iccid) &&
               !string.IsNullOrWhiteSpace(FlickswitchApiKey);
    }

    private async Task ApplyProfileFieldsAsync(
        WialonApiClient client,
        string sessionId,
        long itemId)
    {
        var profileFields = new (string Name, string? Value)[]
        {
            ("brand", Brand),
            ("model", Model),
            ("year", Year),
            ("color", Colour),
            ("vehicle_class", VehicleClass),
            ("vehicle_type", VehicleType),
            ("registration_plate", RegistrationPlate),
            ("vin", Vin)
        };

        foreach (var (name, value) in profileFields)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            await client.UpdateProfileFieldAsync(sessionId, itemId, name, value).ConfigureAwait(true);
        }
    }

    private async Task ApplyCustomFieldsAsync(
        WialonApiClient client,
        string sessionId,
        long itemId)
    {
        var customFields = new (string Name, string? Value)[]
        {
            ("Client", Client),
            ("Colour", Colour),
            ("Contact 1", Contact1),
            ("Contact 2", Contact2),
            ("Make & Model", MakeAndModel),
            ("Registration & Fleet", RegistrationFleet),
            ("VIN", Vin)
        };

        foreach (var (name, value) in customFields)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            await client.UpdateCustomFieldAsync(sessionId, itemId, name, value).ConfigureAwait(true);
        }
    }

    private IReadOnlyList<long> ResolveReadOnlyAccessTargets()
    {
        var requestedUsers = StandardReadOnlyUserNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedUsers.Count == 0)
        {
            return [];
        }

        var resolvedUserIds = new List<long>();
        var missingUsers = new List<string>();

        foreach (var requestedUser in requestedUsers)
        {
            var match = availableUsers.FirstOrDefault(pair =>
                string.Equals(pair.Value, requestedUser, StringComparison.OrdinalIgnoreCase));

            if (match.Key <= 0)
            {
                missingUsers.Add(requestedUser);
                continue;
            }

            resolvedUserIds.Add(match.Key);
        }

        if (missingUsers.Count > 0)
        {
            throw new WialonApiException(
                $"The following Wialon user(s) could not be found: {string.Join(", ", missingUsers)}.");
        }

        return resolvedUserIds;
    }

    private async Task ApplyReadOnlyAccessAsync(
        WialonApiClient client,
        string sessionId,
        long itemId,
        IReadOnlyList<long> userIds)
    {
        foreach (var userId in userIds)
        {
            await client.UpdateItemAccessAsync(sessionId, userId, itemId, ReadOnlyUnitAccessMask).ConfigureAwait(true);
        }
    }
}
