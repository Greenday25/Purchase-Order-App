using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Models;
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
    private readonly JobCardRegistryService jobCardRegistryService = new();
    private readonly JobCardSecretStore secretStore = new();
    private string? currentSessionId;
    private long? creatorId;
    private IReadOnlyDictionary<long, string> availableUsers = new Dictionary<long, string>();
    private bool isInitialized;

    public WialonJobCardViewModel()
    {
        StatusMessage = "Loading saved Wialon setup...";
    }

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;
        LoadJobCardRegister();

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
        if (IsWialonConnected)
        {
            await RefreshExistingWialonUnitsCacheAsync(false).ConfigureAwait(true);
            StatusMessage = $"Connected to {CurrentCreatorDisplay}. Cached {HardwareTypes.Count} hardware type(s), {AccountOptions.Count} account(s), and {ExistingWialonUnits.Count} Wialon unit(s).";
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadWialonSetupCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadExistingUnitsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportExistingUnitDetailsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegisterExistingUnitJobCardCommand))]
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

        ApplySelectedAccountExistingUnitFilter();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private string selectedJobCardType = Models.JobCardTypes.Installation;

    partial void OnSelectedJobCardTypeChanged(string value)
    {
        OnPropertyChanged(nameof(RequiresExistingUnitSelection));
        OnPropertyChanged(nameof(CreateJobCardButtonText));
        OnPropertyChanged(nameof(WialonUnitDetailsHelpText));
        ApplySelectedAccountExistingUnitFilter();
        CreateJobCardCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<string> JobCardTypes => Models.JobCardTypes.All;

    public bool RequiresExistingUnitSelection =>
        string.Equals(SelectedJobCardType, Models.JobCardTypes.Transfer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(SelectedJobCardType, Models.JobCardTypes.Removal, StringComparison.OrdinalIgnoreCase);

    public string CreateJobCardButtonText => RequiresExistingUnitSelection ? "Record Job Card" : "Create Job Card";

    public string WialonUnitDetailsHelpText => RequiresExistingUnitSelection
        ? "Select an existing unit on the chosen account to auto-fill these fields from Wialon before you record the job card."
        : "These fields will be written into Wialon when the unit is created.";

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
    [NotifyPropertyChangedFor(nameof(BillingSystemTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private bool useCustomBillingSystem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BillingSystemTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private string customBillingSystemName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingPackagePriceDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingPlusPackagePriceDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingFmPackagePriceDisplay))]
    private string systemPriceExVat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BillingSystemTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(PanicButtonSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingPlusPackagePriceDisplay))]
    private bool hasPanicButton;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanicButtonSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingPlusPackagePriceDisplay))]
    private string panicButtonPriceExVat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BillingSystemTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(EarlyWarningSystemSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingPlusPackagePriceDisplay))]
    private bool hasEarlyWarningSystem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EarlyWarningSystemSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingPlusPackagePriceDisplay))]
    private string earlyWarningSystemPriceExVat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BleSensorSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private string bleSensorQuantity = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BleSensorSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private string bleSensorUnitPriceExVat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BillingSystemTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(LvCanAdaptorSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingFmPackagePriceDisplay))]
    private bool hasLvCanAdaptor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LvCanAdaptorSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(QuickStingFmPackagePriceDisplay))]
    private string lvCanAdaptorPriceExVat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OtherHardwareSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private string otherHardwareDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OtherHardwareSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private string otherHardwarePriceExVat = string.Empty;

    [ObservableProperty]
    private string billingNotes = string.Empty;

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
    private ObservableCollection<JobCardHistoryItem> jobCardHistory = [];

    [ObservableProperty]
    private ObservableCollection<WialonUnitSummary> existingWialonUnits = [];

    [ObservableProperty]
    private ObservableCollection<WialonUnitSummary> filteredExistingWialonUnits = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportExistingUnitDetailsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegisterExistingUnitJobCardCommand))]
    private WialonUnitSummary? selectedExistingWialonUnit;

    [ObservableProperty]
    private string existingUnitSearchText = string.Empty;

    [ObservableProperty]
    private string existingUnitsStatusMessage = "Wialon units are cached automatically from the saved setup.";

    [ObservableProperty]
    private ObservableCollection<WialonUnitSummary> selectedAccountExistingWialonUnits = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateJobCardCommand))]
    private WialonUnitSummary? selectedAccountExistingWialonUnit;

    partial void OnSelectedAccountExistingWialonUnitChanged(WialonUnitSummary? value)
    {
        if (value is null || !RequiresExistingUnitSelection || !IsWialonConnected)
        {
            return;
        }

        _ = LoadSelectedAccountExistingUnitDetailsAsync(value);
    }

    [ObservableProperty]
    private string selectedAccountExistingUnitSearchText = string.Empty;

    partial void OnSelectedAccountExistingUnitSearchTextChanged(string value)
    {
        ApplySelectedAccountExistingUnitFilter();
    }

    [ObservableProperty]
    private string selectedAccountExistingUnitsStatusMessage = "Select Transfer or Removal to work from an existing Wialon unit.";

    [ObservableProperty]
    private string nextJobCardNumber = "JC-00100";

    [ObservableProperty]
    private string? createdJobCardNumber;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadExistingUnitsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportExistingUnitDetailsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegisterExistingUnitJobCardCommand))]
    private bool isBusy;

    [ObservableProperty]
    private bool isLoadingFromWialon;

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

    public string BillingSystemTypeDisplay =>
        JobCardBillingHelper.ResolveSystemType(
            UseCustomBillingSystem,
            CustomBillingSystemName,
            HasPanicButton,
            HasEarlyWarningSystem,
            HasLvCanAdaptor);

    public string PanicButtonSummaryDisplay => JobCardBillingHelper.BuildOptionalLineDisplay(HasPanicButton, PanicButtonPriceExVat);

    public string EarlyWarningSystemSummaryDisplay => JobCardBillingHelper.BuildOptionalLineDisplay(HasEarlyWarningSystem, EarlyWarningSystemPriceExVat);

    public string BleSensorSummaryDisplay => JobCardBillingHelper.BuildBleSensorDisplay(BleSensorQuantity, BleSensorUnitPriceExVat);

    public string LvCanAdaptorSummaryDisplay => JobCardBillingHelper.BuildOptionalLineDisplay(HasLvCanAdaptor, LvCanAdaptorPriceExVat);

    public string OtherHardwareSummaryDisplay => JobCardBillingHelper.BuildOtherHardwareDisplay(OtherHardwareDescription, OtherHardwarePriceExVat);

    public string BillingTotalExVatDisplay =>
        $"{JobCardBillingHelper.FormatAmount(JobCardBillingHelper.CalculateTotalExVat(
            SystemPriceExVat,
            HasPanicButton,
            PanicButtonPriceExVat,
            HasEarlyWarningSystem,
            EarlyWarningSystemPriceExVat,
            BleSensorQuantity,
            BleSensorUnitPriceExVat,
            HasLvCanAdaptor,
            LvCanAdaptorPriceExVat,
            OtherHardwarePriceExVat))} ex VAT";

    public string BillingSummaryDisplay => $"{BillingSystemTypeDisplay} | Total: {BillingTotalExVatDisplay}";

    public string QuickStingPackagePriceDisplay =>
        $"{JobCardBillingHelper.FormatAmount(SystemPriceExVat)} ex VAT";

    public string QuickStingPlusPackagePriceDisplay =>
        $"{JobCardBillingHelper.FormatAmount(
            (JobCardBillingHelper.ParseAmount(SystemPriceExVat) ?? 0m) +
            (HasPanicButton ? JobCardBillingHelper.ParseAmount(PanicButtonPriceExVat) ?? 0m : 0m) +
            (HasEarlyWarningSystem ? JobCardBillingHelper.ParseAmount(EarlyWarningSystemPriceExVat) ?? 0m : 0m))} ex VAT";

    public string QuickStingFmPackagePriceDisplay =>
        $"{JobCardBillingHelper.FormatAmount(
            (JobCardBillingHelper.ParseAmount(SystemPriceExVat) ?? 0m) +
            (HasLvCanAdaptor ? JobCardBillingHelper.ParseAmount(LvCanAdaptorPriceExVat) ?? 0m : 0m))} ex VAT";

    public bool IsWialonConnected => !string.IsNullOrWhiteSpace(currentSessionId) && creatorId.HasValue;

    partial void OnExistingUnitSearchTextChanged(string value)
    {
        ApplyExistingUnitFilter();
    }

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
            IsLoadingFromWialon = true;
            StatusMessage = "Connecting to Wialon...";

            HardwareTypes = [];
            HardwareTypeSearchText = string.Empty;
            AccountOptions = [];
            availableUsers = new Dictionary<long, string>();
            ExistingWialonUnits = [];
            FilteredExistingWialonUnits = [];
            SelectedExistingWialonUnit = null;
            ExistingUnitSearchText = string.Empty;
            ExistingUnitsStatusMessage = "Wialon units are cached automatically from the saved setup.";
            SelectedAccountExistingWialonUnits = [];
            SelectedAccountExistingWialonUnit = null;
            SelectedAccountExistingUnitSearchText = string.Empty;
            SelectedAccountExistingUnitsStatusMessage = "Select Transfer or Removal to work from an existing Wialon unit.";
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
            IsLoadingFromWialon = false;
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

    private void LoadJobCardRegister()
    {
        try
        {
            jobCardRegistryService.EnsureSchema();
            NextJobCardNumber = jobCardRegistryService.GetNextJobCardNumber().JobCardNumber;

            var historyItems = jobCardRegistryService
                .GetRecentJobCards()
                .Select(record => new JobCardHistoryItem
                {
                    JobCardRecordId = record.JobCardRecordId,
                    JobCardNumber = record.JobCardNumber,
                    CreatedAt = record.CreatedAt,
                    JobCardType = record.JobCardType,
                    WorkflowStatus = record.WorkflowStatus,
                    VehicleDisplay = GetVehicleDisplay(record.RegistrationPlate, record.Vin, record.JobCardName),
                    Client = record.Client,
                    WialonUnitName = record.WialonUnitName,
                    WialonUnitId = record.WialonUnitId,
                    StatusNotes = record.StatusNotes ?? string.Empty
                })
                .ToList();

            JobCardHistory = new ObservableCollection<JobCardHistoryItem>(historyItems);
        }
        catch
        {
            NextJobCardNumber = "Unavailable";
            JobCardHistory = [];
        }
    }

    public void RefreshJobCardRegister()
    {
        LoadJobCardRegister();
    }

    public void DeleteJobCardEntry(int jobCardRecordId)
    {
        var deletedRecord = jobCardRegistryService.DeleteJobCardEntry(jobCardRecordId);
        LoadJobCardRegister();
        StatusMessage = $"Deleted local job card entry {deletedRecord.JobCardNumber}. Wialon was not updated.";
    }

    private JobCardRecord? TryRecordCreatedJobCard(
        WialonCreatedUnit? createdUnit,
        string? resolvedPhoneNumber,
        string workflowStatus,
        string? statusNotes)
    {
        if (createdUnit is null)
        {
            return null;
        }

        try
        {
            var createdRecord = jobCardRegistryService.SaveCreatedJobCard(
                BuildSaveJobCardRequest(
                    workflowStatus,
                    statusNotes,
                    createdUnit.UnitId,
                    createdUnit.Name,
                    SelectedAccount?.AccountId,
                    SelectedAccount?.AccountName,
                    creatorId,
                    CreatorName,
                    SelectedHardwareType?.HardwareTypeId,
                    SelectedHardwareType?.DisplayText,
                    resolvedPhoneNumber));

            CreatedJobCardNumber = createdRecord.JobCardNumber;
            LoadJobCardRegister();
            return createdRecord;
        }
        catch
        {
            return null;
        }
    }

    private JobCardRegistryService.SaveJobCardRequest BuildSaveJobCardRequest(
        string workflowStatus,
        string? statusNotes,
        long? wialonUnitId,
        string? wialonUnitName,
        long? wialonAccountId,
        string? wialonAccountName,
        long? wialonCreatorId,
        string? wialonCreatorName,
        long? wialonHardwareTypeId,
        string? wialonHardwareTypeName,
        string? phoneNumber)
    {
        return new JobCardRegistryService.SaveJobCardRequest(
            SelectedJobCardType,
            workflowStatus,
            statusNotes,
            wialonUnitId,
            wialonUnitName,
            wialonAccountId,
            wialonAccountName,
            wialonCreatorId,
            wialonCreatorName,
            wialonHardwareTypeId,
            wialonHardwareTypeName,
            GetResolvedUnitName(),
            UniqueId,
            Iccid,
            phoneNumber,
            Brand,
            Model,
            Year,
            Colour,
            VehicleClass,
            VehicleType,
            RegistrationPlate,
            Vin,
            Client,
            Contact1,
            Contact2,
            MakeAndModel,
            RegistrationFleet,
            UseCustomBillingSystem,
            CustomBillingSystemName,
            SystemPriceExVat,
            HasPanicButton,
            PanicButtonPriceExVat,
            HasEarlyWarningSystem,
            EarlyWarningSystemPriceExVat,
            BleSensorQuantity,
            BleSensorUnitPriceExVat,
            HasLvCanAdaptor,
            LvCanAdaptorPriceExVat,
            OtherHardwareDescription,
            OtherHardwarePriceExVat,
            BillingNotes);
    }

    private static string GetVehicleDisplay(string? registrationPlate, string? vin, string? jobCardName)
    {
        var registration = registrationPlate?.Trim();
        if (!string.IsNullOrWhiteSpace(registration))
        {
            return registration;
        }

        var vinValue = vin?.Trim();
        if (!string.IsNullOrWhiteSpace(vinValue))
        {
            return vinValue;
        }

        return string.IsNullOrWhiteSpace(jobCardName) ? "Pending" : jobCardName.Trim();
    }

    [RelayCommand(CanExecute = nameof(CanLoadExistingUnits))]
    private async Task LoadExistingUnitsAsync()
    {
        if (!IsWialonConnected)
        {
            StatusMessage = "The saved Wialon setup has not loaded yet.";
            return;
        }

        try
        {
            IsBusy = true;
            await RefreshExistingWialonUnitsCacheAsync(true).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            LoadExistingUnitsCommand.NotifyCanExecuteChanged();
            ImportExistingUnitDetailsCommand.NotifyCanExecuteChanged();
            RegisterExistingUnitJobCardCommand.NotifyCanExecuteChanged();
            CreateJobCardCommand.NotifyCanExecuteChanged();
            LoadWialonSetupCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RefreshExistingWialonUnitsCacheAsync(bool updateStatusMessage)
    {
        if (!IsWialonConnected)
        {
            return;
        }

        try
        {
            IsLoadingFromWialon = true;
            if (updateStatusMessage)
            {
                StatusMessage = "Refreshing Wialon unit cache...";
            }

            ExistingUnitsStatusMessage = "Refreshing Wialon unit cache...";

            var client = new WialonApiClient(ApiHost);
            var units = await client.GetUnitsAsync(currentSessionId!).ConfigureAwait(true);

            ExistingWialonUnits = new ObservableCollection<WialonUnitSummary>(units);
            ApplyExistingUnitFilter();
            ApplySelectedAccountExistingUnitFilter();

            ExistingUnitsStatusMessage = units.Count == 0
                ? "No existing Wialon units were returned."
                : $"Cached {units.Count} existing Wialon unit(s).";

            if (updateStatusMessage)
            {
                StatusMessage = ExistingUnitsStatusMessage;
            }
        }
        catch (WialonApiException ex)
        {
            ExistingUnitsStatusMessage = ex.Message;
            if (updateStatusMessage)
            {
                StatusMessage = ex.Message;
            }
        }
        catch (HttpRequestException ex)
        {
            ExistingUnitsStatusMessage = $"Unable to reach Wialon: {ex.Message}";
            if (updateStatusMessage)
            {
                StatusMessage = ExistingUnitsStatusMessage;
            }
        }
        catch (Exception ex)
        {
            ExistingUnitsStatusMessage = $"Unexpected error while refreshing Wialon units: {ex.Message}";
            if (updateStatusMessage)
            {
                StatusMessage = ExistingUnitsStatusMessage;
            }
        }
        finally
        {
            IsLoadingFromWialon = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanImportExistingUnitDetails))]
    private async Task ImportExistingUnitDetailsAsync()
    {
        if (!IsWialonConnected)
        {
            ExistingUnitsStatusMessage = "Load the Wialon setup before importing an existing unit.";
            StatusMessage = ExistingUnitsStatusMessage;
            return;
        }

        if (SelectedExistingWialonUnit is null)
        {
            ExistingUnitsStatusMessage = "Select an existing Wialon unit first.";
            StatusMessage = ExistingUnitsStatusMessage;
            return;
        }

        try
        {
            IsBusy = true;
            IsLoadingFromWialon = true;
            StatusMessage = $"Loading the Wialon details for {SelectedExistingWialonUnit.Name}...";

            var details = await GetUnitDetailsWithRetryAsync(SelectedExistingWialonUnit.UnitId).ConfigureAwait(true);

            ApplyExistingUnitDetails(details);
            ExistingUnitsStatusMessage = $"Loaded details from existing unit {details.Name} ({details.UnitId}).";
            StatusMessage = ExistingUnitsStatusMessage;
        }
        catch (WialonApiException ex)
        {
            ExistingUnitsStatusMessage = ex.Message;
            StatusMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ExistingUnitsStatusMessage = $"Unable to reach Wialon: {ex.Message}";
            StatusMessage = ExistingUnitsStatusMessage;
        }
        catch (Exception ex)
        {
            ExistingUnitsStatusMessage = $"Unexpected error while loading unit details: {ex.Message}";
            StatusMessage = ExistingUnitsStatusMessage;
        }
        finally
        {
            IsLoadingFromWialon = false;
            IsBusy = false;
            LoadExistingUnitsCommand.NotifyCanExecuteChanged();
            ImportExistingUnitDetailsCommand.NotifyCanExecuteChanged();
            RegisterExistingUnitJobCardCommand.NotifyCanExecuteChanged();
            CreateJobCardCommand.NotifyCanExecuteChanged();
            LoadWialonSetupCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRegisterExistingUnitJobCard))]
    private Task RegisterExistingUnitJobCardAsync()
    {
        if (!IsWialonConnected)
        {
            ExistingUnitsStatusMessage = "Load the Wialon setup before recording a historical job card.";
            StatusMessage = ExistingUnitsStatusMessage;
            return Task.CompletedTask;
        }

        if (SelectedExistingWialonUnit is null)
        {
            ExistingUnitsStatusMessage = "Select an existing Wialon unit first.";
            StatusMessage = ExistingUnitsStatusMessage;
            return Task.CompletedTask;
        }

        try
        {
            var createdRecord = jobCardRegistryService.SaveCreatedJobCard(
                BuildSaveJobCardRequest(
                    JobCardWorkflowStatuses.AwaitingInstallationPhotos,
                    "Registered from an existing Wialon unit.",
                    SelectedExistingWialonUnit.UnitId,
                    string.IsNullOrWhiteSpace(CreatedUnitName) ? SelectedExistingWialonUnit.Name : CreatedUnitName,
                    SelectedExistingWialonUnit.AccountId,
                    NormalizeLegacyText(SelectedExistingWialonUnit.AccountLabel) ?? SelectedAccount?.AccountName,
                    creatorId,
                    CreatorName,
                    SelectedExistingWialonUnit.HardwareTypeId ?? SelectedHardwareType?.HardwareTypeId,
                    NormalizeLegacyText(SelectedExistingWialonUnit.HardwareTypeName) ?? SelectedHardwareType?.DisplayText,
                    string.IsNullOrWhiteSpace(ResolvedPhoneNumber) ? SelectedExistingWialonUnit.PhoneNumber : ResolvedPhoneNumber));

            CreatedJobCardNumber = createdRecord.JobCardNumber;
            CreatedUnitId = SelectedExistingWialonUnit.UnitId;
            CreatedUnitName = SelectedExistingWialonUnit.Name;
            LoadJobCardRegister();
            StatusMessage = $"Recorded historical job card {createdRecord.JobCardNumber} for existing Wialon unit {SelectedExistingWialonUnit.Name} ({SelectedExistingWialonUnit.UnitId}).";
            ExistingUnitsStatusMessage = "Historical job card recorded locally.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"I couldn't record that historical job card: {ex.Message}";
            ExistingUnitsStatusMessage = StatusMessage;
        }

        return Task.CompletedTask;
    }

    private bool CanLoadExistingUnits()
    {
        return !IsBusy && IsWialonConnected;
    }

    private bool CanImportExistingUnitDetails()
    {
        return !IsBusy;
    }

    private bool CanRegisterExistingUnitJobCard()
    {
        return !IsBusy;
    }

    private void ApplyExistingUnitFilter()
    {
        var searchText = ExistingUnitSearchText?.Trim() ?? string.Empty;
        var filteredUnits = string.IsNullOrWhiteSpace(searchText)
            ? ExistingWialonUnits.ToList()
            : ExistingWialonUnits
                .Where(unit =>
                    unit.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    unit.UnitId.ToString(CultureInfo.InvariantCulture).Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(unit.AccountDisplay) && unit.AccountDisplay.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(unit.UniqueId) && unit.UniqueId.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(unit.PhoneNumber) && unit.PhoneNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        FilteredExistingWialonUnits = new ObservableCollection<WialonUnitSummary>(filteredUnits);
        if (SelectedExistingWialonUnit is not null &&
            filteredUnits.All(unit => unit.UnitId != SelectedExistingWialonUnit.UnitId))
        {
            SelectedExistingWialonUnit = filteredUnits.FirstOrDefault();
        }
    }

    private void ApplySelectedAccountExistingUnitFilter()
    {
        if (!RequiresExistingUnitSelection)
        {
            SelectedAccountExistingWialonUnits = [];
            SelectedAccountExistingWialonUnit = null;
            SelectedAccountExistingUnitsStatusMessage = "Select Transfer or Removal to work from an existing Wialon unit.";
            return;
        }

        if (!IsWialonConnected)
        {
            SelectedAccountExistingWialonUnits = [];
            SelectedAccountExistingWialonUnit = null;
            SelectedAccountExistingUnitsStatusMessage = "Load the Wialon setup first.";
            return;
        }

        if (SelectedAccount is null)
        {
            SelectedAccountExistingWialonUnits = [];
            SelectedAccountExistingWialonUnit = null;
            SelectedAccountExistingUnitsStatusMessage = "Select the Wialon account first.";
            return;
        }

        if (ExistingWialonUnits.Count == 0)
        {
            SelectedAccountExistingWialonUnits = [];
            SelectedAccountExistingWialonUnit = null;
            SelectedAccountExistingUnitsStatusMessage = $"Load existing Wialon units to choose one from {SelectedAccount.AccountName}.";
            return;
        }

        var searchText = SelectedAccountExistingUnitSearchText?.Trim() ?? string.Empty;
        var filteredUnits = ExistingWialonUnits
            .Where(unit => MatchesSelectedAccount(unit, SelectedAccount))
            .Where(unit =>
                string.IsNullOrWhiteSpace(searchText) ||
                unit.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                unit.UnitId.ToString(CultureInfo.InvariantCulture).Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(unit.UniqueId) && unit.UniqueId.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(unit.PhoneNumber) && unit.PhoneNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(unit => unit.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SelectedAccountExistingWialonUnits = new ObservableCollection<WialonUnitSummary>(filteredUnits);
        if (SelectedAccountExistingWialonUnit is not null &&
            filteredUnits.All(unit => unit.UnitId != SelectedAccountExistingWialonUnit.UnitId))
        {
            SelectedAccountExistingWialonUnit = null;
        }

        if (filteredUnits.Count == 0)
        {
            SelectedAccountExistingUnitsStatusMessage = string.IsNullOrWhiteSpace(searchText)
                ? $"No existing units were found under {SelectedAccount.AccountName}."
                : $"No existing units matched \"{searchText}\" under {SelectedAccount.AccountName}.";
            return;
        }

        SelectedAccountExistingUnitsStatusMessage = SelectedAccountExistingWialonUnit is null
            ? $"{filteredUnits.Count} existing unit(s) found under {SelectedAccount.AccountName}. Select one to auto-fill the form."
            : $"Selected {SelectedAccountExistingWialonUnit.Name}. The form is using details captured on Wialon.";
    }

    private async Task LoadSelectedAccountExistingUnitDetailsAsync(WialonUnitSummary selectedUnit)
    {
        try
        {
            IsBusy = true;
            IsLoadingFromWialon = true;
            SelectedAccountExistingUnitsStatusMessage = $"Loading the Wialon details for {selectedUnit.Name}...";
            StatusMessage = SelectedAccountExistingUnitsStatusMessage;

            var details = await GetUnitDetailsWithRetryAsync(selectedUnit.UnitId).ConfigureAwait(true);
            if (SelectedAccountExistingWialonUnit?.UnitId != selectedUnit.UnitId)
            {
                return;
            }

            ApplyExistingUnitDetails(details);
            SelectedExistingWialonUnit = selectedUnit;
            ExistingUnitsStatusMessage = $"Loaded details from existing unit {details.Name} ({details.UnitId}).";
            SelectedAccountExistingUnitsStatusMessage = $"Loaded details from {details.Name} ({details.UnitId}).";
            StatusMessage = SelectedAccountExistingUnitsStatusMessage;
        }
        catch (WialonApiException ex)
        {
            SelectedAccountExistingUnitsStatusMessage = ex.Message;
            StatusMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            SelectedAccountExistingUnitsStatusMessage = $"Unable to reach Wialon: {ex.Message}";
            StatusMessage = SelectedAccountExistingUnitsStatusMessage;
        }
        catch (Exception ex)
        {
            SelectedAccountExistingUnitsStatusMessage = $"Unexpected error while loading unit details: {ex.Message}";
            StatusMessage = SelectedAccountExistingUnitsStatusMessage;
        }
        finally
        {
            IsLoadingFromWialon = false;
            IsBusy = false;
            LoadExistingUnitsCommand.NotifyCanExecuteChanged();
            ImportExistingUnitDetailsCommand.NotifyCanExecuteChanged();
            RegisterExistingUnitJobCardCommand.NotifyCanExecuteChanged();
            CreateJobCardCommand.NotifyCanExecuteChanged();
            LoadWialonSetupCommand.NotifyCanExecuteChanged();
            ApplySelectedAccountExistingUnitFilter();
        }
    }

    private async Task<WialonUnitDetails> GetUnitDetailsWithRetryAsync(long unitId)
    {
        var client = new WialonApiClient(ApiHost);
        if (!string.IsNullOrWhiteSpace(currentSessionId))
        {
            try
            {
                return await client.GetUnitDetailsAsync(currentSessionId, unitId).ConfigureAwait(true);
            }
            catch (WialonApiException ex) when (ShouldRefreshSession(ex))
            {
                currentSessionId = null;
            }
        }

        var session = await ReconnectToWialonAsync(client).ConfigureAwait(true);
        return await client.GetUnitDetailsAsync(session.SessionId, unitId).ConfigureAwait(true);
    }

    private async Task<WialonSession> ReconnectToWialonAsync(WialonApiClient client)
    {
        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            throw new WialonApiException("The saved Wialon session expired and there is no access token available to reconnect.");
        }

        var session = await client.LoginAsync(AccessToken.Trim()).ConfigureAwait(true);
        currentSessionId = session.SessionId;
        creatorId = session.CreatorId;
        CreatorName = session.CreatorName;
        OnPropertyChanged(nameof(CurrentCreatorDisplay));
        return session;
    }

    private static bool ShouldRefreshSession(WialonApiException ex)
    {
        return ex.ErrorCode is 1 or 6 or 1011;
    }

    private static bool MatchesSelectedAccount(WialonUnitSummary unit, WialonAccountOption selectedAccount)
    {
        if (unit.AccountId.HasValue && unit.AccountId.Value == selectedAccount.AccountId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(unit.AccountLabel) &&
            string.Equals(unit.AccountLabel.Trim(), selectedAccount.AccountName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyExistingUnitDetails(WialonUnitDetails details)
    {
        CreatedUnitId = details.UnitId;
        CreatedUnitName = details.Name;
        JobCardName = details.Name;

        if (!string.IsNullOrWhiteSpace(details.UniqueId))
        {
            UniqueId = details.UniqueId;
        }

        if (!string.IsNullOrWhiteSpace(details.PhoneNumber))
        {
            ResolvedPhoneNumber = details.PhoneNumber;
        }

        if (details.AccountId.HasValue)
        {
            var matchedAccount = AccountOptions.FirstOrDefault(option => option.AccountId == details.AccountId.Value);
            if (matchedAccount is not null)
            {
                SelectedAccount = matchedAccount;
            }
        }

        if (details.HardwareTypeId.HasValue)
        {
            var matchedHardwareType = HardwareTypes.FirstOrDefault(option => option.HardwareTypeId == details.HardwareTypeId.Value);
            if (matchedHardwareType is not null)
            {
                SelectedHardwareType = matchedHardwareType;
            }
        }

        Brand = GetDetailFieldValue(details, "brand") ?? Brand;
        Model = GetDetailFieldValue(details, "model") ?? Model;
        Year = GetDetailFieldValue(details, "year") ?? Year;
        Colour = GetDetailFieldValue(details, "color", "colour") ?? Colour;
        VehicleClass = GetDetailFieldValue(details, "vehicle_class") ?? VehicleClass;
        VehicleType = GetDetailFieldValue(details, "vehicle_type") ?? VehicleType;
        RegistrationPlate = GetDetailFieldValue(details, "registration_plate") ?? RegistrationPlate;
        Vin = GetDetailFieldValue(details, "vin") ?? Vin;
        Client = GetDetailFieldValue(details, "Client") ?? Client;
        Contact1 = GetDetailFieldValue(details, "Contact 1") ?? Contact1;
        Contact2 = GetDetailFieldValue(details, "Contact 2") ?? Contact2;
        MakeAndModel = GetDetailFieldValue(details, "Make & Model") ?? MakeAndModel;
        RegistrationFleet = GetDetailFieldValue(details, "Registration & Fleet") ?? RegistrationFleet;
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

    private static string? NormalizeLegacyText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    [RelayCommand(CanExecute = nameof(CanCreateJobCard))]
    private async Task CreateJobCardAsync()
    {
        if (!IsWialonConnected)
        {
            StatusMessage = "Load the Wialon setup before creating the job card.";
            return;
        }

        if (SelectedAccount is null)
        {
            StatusMessage = "Select the Wialon account to create the unit under.";
            return;
        }

        if (RequiresExistingUnitSelection)
        {
            await CreateExistingUnitJobCardAsync().ConfigureAwait(true);
            return;
        }

        if (SelectedHardwareType is null)
        {
            StatusMessage = "Select a hardware type first.";
            return;
        }

        if (SelectedAccount.CreatorId <= 0)
        {
            StatusMessage = $"The selected account {SelectedAccount.AccountName} does not expose a valid creator ID.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FlickswitchApiKey))
        {
            StatusMessage = "Add the Flickswitch API key in Connectivity Settings before creating a new job card.";
            return;
        }

        if (HasDuplicateJobCardIdentifiers())
        {
            return;
        }

        SaveCredentials();
        WialonCreatedUnit? createdUnit = null;
        JobCardRecord? createdRecord = null;
        string? phoneNumber = null;

        try
        {
            IsBusy = true;
            IsLoadingFromWialon = true;
            CreatedJobCardNumber = null;
            var readOnlyAccessTargets = ResolveReadOnlyAccessTargets();
            StatusMessage = "Looking up the phone number from Flickswitch...";

            var flickswitchClient = new FlickswitchApiClient();
            phoneNumber = await flickswitchClient.LookupMsisdnAsync(
                FlickswitchApiKey.Trim(),
                Iccid.Trim()).ConfigureAwait(true);

            ResolvedPhoneNumber = phoneNumber;

            var wialonClient = new WialonApiClient(ApiHost);
            StatusMessage = $"Creating the unit under {SelectedAccount.AccountName}...";
            createdUnit = await wialonClient.CreateUnitAsync(
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

            createdRecord = TryRecordCreatedJobCard(
                createdUnit,
                phoneNumber,
                JobCardWorkflowStatuses.Created,
                null);

            SaveCredentials();
            StatusMessage = createdRecord is null
                ? $"Created Wialon unit {createdUnit.Name} ({createdUnit.UnitId}) under {SelectedAccount.AccountName}."
                : $"Created Wialon unit {createdUnit.Name} ({createdUnit.UnitId}) under {SelectedAccount.AccountName}. Job card {createdRecord.JobCardNumber} recorded.";
        }
        catch (FlickswitchApiException ex)
        {
            createdRecord ??= TryRecordCreatedJobCard(
                createdUnit,
                phoneNumber,
                JobCardWorkflowStatuses.CreatedWithWarnings,
                ex.Message);
            StatusMessage = createdRecord is null
                ? ex.Message
                : $"{ex.Message} Job card {createdRecord.JobCardNumber} was still recorded for follow-up.";
        }
        catch (WialonApiException ex)
        {
            createdRecord ??= TryRecordCreatedJobCard(
                createdUnit,
                phoneNumber,
                JobCardWorkflowStatuses.CreatedWithWarnings,
                ex.Message);
            StatusMessage = createdRecord is null
                ? ex.Message
                : $"{ex.Message} Job card {createdRecord.JobCardNumber} was still recorded for follow-up.";
        }
        catch (HttpRequestException ex)
        {
            createdRecord ??= TryRecordCreatedJobCard(
                createdUnit,
                phoneNumber,
                JobCardWorkflowStatuses.CreatedWithWarnings,
                ex.Message);
            var message = $"Unable to reach the API: {ex.Message}";
            StatusMessage = createdRecord is null
                ? message
                : $"{message} Job card {createdRecord.JobCardNumber} was still recorded for follow-up.";
        }
        catch (Exception ex)
        {
            createdRecord ??= TryRecordCreatedJobCard(
                createdUnit,
                phoneNumber,
                JobCardWorkflowStatuses.CreatedWithWarnings,
                ex.Message);
            var message = $"Unexpected error while creating the job card: {ex.Message}";
            StatusMessage = createdRecord is null
                ? message
                : $"{message} Job card {createdRecord.JobCardNumber} was still recorded for follow-up.";
        }
        finally
        {
            IsLoadingFromWialon = false;
            IsBusy = false;
            if (createdUnit is not null)
            {
                await RefreshExistingWialonUnitsCacheAsync(false).ConfigureAwait(true);
            }

            CreateJobCardCommand.NotifyCanExecuteChanged();
            LoadWialonSetupCommand.NotifyCanExecuteChanged();
            LoadExistingUnitsCommand.NotifyCanExecuteChanged();
            ImportExistingUnitDetailsCommand.NotifyCanExecuteChanged();
            RegisterExistingUnitJobCardCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task CreateExistingUnitJobCardAsync()
    {
        if (SelectedAccountExistingWialonUnit is null)
        {
            StatusMessage = $"Select an existing Wialon unit under {SelectedAccount?.AccountName ?? "the selected account"} first.";
            SelectedAccountExistingUnitsStatusMessage = StatusMessage;
            return;
        }

        try
        {
            IsBusy = true;
            CreatedJobCardNumber = null;

            if (HasDuplicateJobCardIdentifiers())
            {
                return;
            }

            var selectedUnit = SelectedAccountExistingWialonUnit;
            var createdRecord = jobCardRegistryService.SaveCreatedJobCard(
                BuildSaveJobCardRequest(
                    JobCardWorkflowStatuses.Created,
                    $"Created from existing Wialon unit for {SelectedJobCardType.ToLowerInvariant()} workflow.",
                    selectedUnit.UnitId,
                    string.IsNullOrWhiteSpace(CreatedUnitName) ? selectedUnit.Name : CreatedUnitName,
                    selectedUnit.AccountId ?? SelectedAccount?.AccountId,
                    NormalizeLegacyText(selectedUnit.AccountLabel) ?? SelectedAccount?.AccountName,
                    creatorId,
                    CreatorName,
                    selectedUnit.HardwareTypeId ?? SelectedHardwareType?.HardwareTypeId,
                    NormalizeLegacyText(selectedUnit.HardwareTypeName) ?? SelectedHardwareType?.DisplayText,
                    string.IsNullOrWhiteSpace(ResolvedPhoneNumber) ? selectedUnit.PhoneNumber : ResolvedPhoneNumber));

            CreatedJobCardNumber = createdRecord.JobCardNumber;
            CreatedUnitId = selectedUnit.UnitId;
            CreatedUnitName = string.IsNullOrWhiteSpace(CreatedUnitName) ? selectedUnit.Name : CreatedUnitName;
            LoadJobCardRegister();
            await RefreshExistingWialonUnitsCacheAsync(false).ConfigureAwait(true);

            StatusMessage = $"Recorded {SelectedJobCardType.ToLowerInvariant()} job card {createdRecord.JobCardNumber} for existing Wialon unit {selectedUnit.Name} ({selectedUnit.UnitId}).";
            SelectedAccountExistingUnitsStatusMessage = $"Recorded {createdRecord.JobCardNumber} from {selectedUnit.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"I couldn't record that {SelectedJobCardType.ToLowerInvariant()} job card: {ex.Message}";
            SelectedAccountExistingUnitsStatusMessage = StatusMessage;
        }
        finally
        {
            IsBusy = false;
            CreateJobCardCommand.NotifyCanExecuteChanged();
            LoadWialonSetupCommand.NotifyCanExecuteChanged();
            LoadExistingUnitsCommand.NotifyCanExecuteChanged();
            ImportExistingUnitDetailsCommand.NotifyCanExecuteChanged();
            RegisterExistingUnitJobCardCommand.NotifyCanExecuteChanged();
        }
    }

    private bool HasDuplicateJobCardIdentifiers()
    {
        var duplicate = jobCardRegistryService.FindDuplicateJobCard(RegistrationPlate, Iccid, UniqueId);
        if (duplicate is null)
        {
            return false;
        }

        var createdAt = duplicate.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        StatusMessage = $"Duplicate blocked: this {duplicate.FieldName} ({duplicate.FieldValue}) already exists on job card {duplicate.JobCardNumber}, created {createdAt}. Wialon was not updated.";
        SelectedAccountExistingUnitsStatusMessage = StatusMessage;
        return true;
    }

    private bool CanLoadWialonSetup()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(AccessToken);
    }

    private bool CanCreateJobCard()
    {
        return !IsBusy && (!RequiresExistingUnitSelection || SelectedAccountExistingWialonUnit is not null);
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
