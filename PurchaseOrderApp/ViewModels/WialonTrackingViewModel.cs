using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurchaseOrderApp.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;

namespace PurchaseOrderApp.ViewModels;

public partial class WialonTrackingViewModel : ObservableObject
{
    private const string DefaultApiHost = "hst-api.wialon.eu";
    private const int ConnectivityHydrationThreshold = 75;
    private readonly JobCardSecretStore secretStore = new();
    private readonly TrackingCertificateDataBuilder trackingCertificateDataBuilder = new();
    private readonly TrackingCertificatePdfService trackingCertificatePdfService = new();
    private readonly List<WialonUnitSummary> allUnits = [];
    private CancellationTokenSource? connectivityHydrationCts;
    private IReadOnlyDictionary<long, string> hardwareTypeNames = new Dictionary<long, string>();
    private bool isInitialized;

    [ObservableProperty]
    private string apiHost = DefaultApiHost;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadUnitsCommand))]
    private string accessToken = string.Empty;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastLoadedDisplay))]
    private DateTimeOffset? lastLoadedAt;

    [ObservableProperty]
    private string statusMessage = "Enter your Wialon access token and load the available units.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadUnitsCommand))]
    [NotifyCanExecuteChangedFor(nameof(BulkExportCertificatesForClientCommand))]
    private bool isBusy;

    [ObservableProperty]
    private ObservableCollection<WialonUnitSummary> units = [];

    [ObservableProperty]
    private ObservableCollection<WialonUnitSummary> filteredUnits = [];

    [ObservableProperty]
    private WialonUnitSummary? selectedUnit;

    [ObservableProperty]
    private ObservableCollection<WialonAccountFilterOption> accountFilters = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BulkExportCertificatesForClientCommand))]
    private WialonAccountFilterOption? selectedAccountFilter;

    [ObservableProperty]
    private int totalUnitCount;

    [ObservableProperty]
    private int visibleUnitCount;

    public string? CurrentSessionId { get; private set; }

    public string LastLoadedDisplay =>
        LastLoadedAt.HasValue
            ? LastLoadedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Not loaded yet";

    public WialonTrackingViewModel()
    {
        AccountFilters = new ObservableCollection<WialonAccountFilterOption>
        {
            new()
            {
                FilterValue = string.Empty,
                DisplayText = "All Accounts"
            }
        };
        SelectedAccountFilter = AccountFilters[0];
        ApplyFilter();
    }

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;

        var credentials = secretStore.Load();

        if (!string.IsNullOrWhiteSpace(credentials.ApiHost))
        {
            var restoredHost = credentials.ApiHost.Trim();
            if (!string.Equals(restoredHost, "hst-api.wialon.com", StringComparison.OrdinalIgnoreCase))
            {
                ApiHost = restoredHost;
            }
        }

        if (!string.IsNullOrWhiteSpace(credentials.WialonAccessToken))
        {
            AccessToken = credentials.WialonAccessToken.Trim();
            StatusMessage = "Saved Wialon connectivity settings loaded. Loading units automatically...";
            await LoadUnitsAsync();
        }
        else
        {
            StatusMessage = "No saved Wialon access token found. Open Connectivity Settings from the Home screen to add it.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadUnits))]
    private async Task LoadUnitsAsync()
    {
        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            StatusMessage = "Paste a Wialon access token first.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Connecting to {ApiHost}...";
            await Task.Yield();

            var previousSelectionId = SelectedUnit?.UnitId;
            var previousAccountFilter = SelectedAccountFilter?.FilterValue;
            var searchTerms = (FilterText ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var client = new WialonApiClient(ApiHost);
            var session = await client.LoginAsync(AccessToken.Trim()).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => StatusMessage = "Loading units, hardware types, and account names...");

            var hardwareTypesTask = LoadHardwareTypeNamesAsync(client, session.SessionId);
            var accountNamesTask = LoadAccountNamesAsync(client, session.SessionId);
            var unitsTask = client.GetUnitsAsync(session.SessionId);

            var loadedUnits = await unitsTask.ConfigureAwait(false);
            var loadedHardwareTypeNames = await hardwareTypesTask.ConfigureAwait(false);
            var accountLabels = await accountNamesTask.ConfigureAwait(false);
            var enrichedUnits = loadedUnits
                .Select(unit =>
                {
                    if (unit.AccountId.HasValue && accountLabels.TryGetValue(unit.AccountId.Value, out var accountLabel))
                    {
                        unit.AccountLabel = accountLabel;
                    }

                    ApplyHardwareTypeName(unit);

                    return unit;
                })
                .ToList();

            var hasConnectivityDetails = enrichedUnits.Any(unit =>
                !string.IsNullOrWhiteSpace(unit.UniqueId) ||
                !string.IsNullOrWhiteSpace(unit.PhoneNumber));
            var accountFilterOptions = BuildAccountFilters(enrichedUnits);
            var resolvedAccountFilter = accountFilterOptions.FirstOrDefault(option =>
                string.Equals(option.FilterValue, previousAccountFilter, StringComparison.OrdinalIgnoreCase))
                ?? accountFilterOptions.First();
            var filteredUnits = GetFilteredUnits(enrichedUnits, resolvedAccountFilter.FilterValue, searchTerms);

            var statusMessage = enrichedUnits.Count == 0
                ? "Login succeeded, but no units were returned."
                : hasConnectivityDetails
                    ? $"Loaded {enrichedUnits.Count} unit(s) from Wialon."
                    : $"Loaded {enrichedUnits.Count} unit(s), but Wialon did not return any unique ID or phone values. Check the token's View connectivity settings rights.";

            await RunOnUiThreadAsync(() =>
            {
                CurrentSessionId = session.SessionId;
                hardwareTypeNames = loadedHardwareTypeNames;
                allUnits.Clear();
                allUnits.AddRange(enrichedUnits);

                AccountFilters = accountFilterOptions;
                SelectedAccountFilter = resolvedAccountFilter;
                Units = new ObservableCollection<WialonUnitSummary>(allUnits);
                FilteredUnits = new ObservableCollection<WialonUnitSummary>(filteredUnits);
                TotalUnitCount = allUnits.Count;
                VisibleUnitCount = filteredUnits.Count;
                SelectedUnit = previousSelectionId.HasValue
                    ? filteredUnits.FirstOrDefault(unit => unit.UnitId == previousSelectionId.Value)
                    : filteredUnits.FirstOrDefault();
                LastLoadedAt = DateTimeOffset.Now;
                StatusMessage = statusMessage;
                QueueConnectivityHydration();
            });
        }
        catch (WialonApiException ex)
        {
            await RunOnUiThreadAsync(() =>
            {
                StatusMessage = ex.Message;
                allUnits.Clear();
                Units = [];
                ApplyFilter();
            });
        }
        catch (HttpRequestException ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = $"Unable to reach Wialon: {ex.Message}");
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = $"Unexpected error while loading units: {ex.Message}");
        }
        finally
        {
            await RunOnUiThreadAsync(() => IsBusy = false);
        }
    }

    private bool CanLoadUnits()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(AccessToken);
    }

    [RelayCommand(CanExecute = nameof(CanBulkExportCertificatesForClient))]
    private async Task BulkExportCertificatesForClientAsync()
    {
        if (SelectedAccountFilter is null || string.IsNullOrWhiteSpace(SelectedAccountFilter.FilterValue))
        {
            StatusMessage = "Choose a client in the Account filter before bulk exporting certificates.";
            return;
        }

        var clientUnits = allUnits
            .Where(unit => string.Equals(unit.AccountId?.ToString(), SelectedAccountFilter.FilterValue, StringComparison.OrdinalIgnoreCase))
            .OrderBy(unit => unit.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (clientUnits.Count == 0)
        {
            StatusMessage = "No units were found for the selected client.";
            return;
        }

        try
        {
            IsBusy = true;

            var clientDisplayName = ResolveClientDisplayName(clientUnits);
            StatusMessage = $"Preparing {clientUnits.Count} certificate(s) for {clientDisplayName}...";

            var client = new WialonApiClient(ApiHost);
            var sessionId = await EnsureSessionIdAsync(client).ConfigureAwait(true);
            var exportFolder = TrackingCertificatePdfService.CreateClientBatchExportFolder(clientDisplayName);
            var exportedCount = 0;
            var failedUnits = new List<string>();

            for (var index = 0; index < clientUnits.Count; index++)
            {
                var unit = clientUnits[index];
                StatusMessage = $"Exporting {index + 1} of {clientUnits.Count} for {clientDisplayName}: {unit.Name}";

                try
                {
                    var details = await GetUnitDetailsWithRetryAsync(client, sessionId, unit.UnitId).ConfigureAwait(true);
                    sessionId = CurrentSessionId ?? sessionId;

                    var fallbackHardwareTypeName = ResolveHardwareTypeName(unit, details);
                    var certificate = trackingCertificateDataBuilder.Build(details, fallbackHardwareTypeName, unit.AccountLabel);
                    trackingCertificatePdfService.ExportCertificate(certificate, exportFolder);
                    exportedCount++;
                }
                catch (WialonApiException)
                {
                    failedUnits.Add(unit.Name);
                }
                catch (HttpRequestException)
                {
                    failedUnits.Add(unit.Name);
                }
                catch (IOException)
                {
                    failedUnits.Add(unit.Name);
                }
                catch (UnauthorizedAccessException)
                {
                    failedUnits.Add(unit.Name);
                }
            }

            var failureMessage = failedUnits.Count == 0
                ? string.Empty
                : $" {failedUnits.Count} unit(s) could not be exported.";

            StatusMessage = exportedCount == 0
                ? $"No certificates were exported for {clientDisplayName}.{failureMessage}"
                : $"Exported {exportedCount} certificate(s) for {clientDisplayName} to {exportFolder}.{failureMessage}";

            if (exportedCount > 0)
            {
                Process.Start(new ProcessStartInfo(exportFolder)
                {
                    UseShellExecute = true
                });
            }
        }
        catch (WialonApiException ex)
        {
            StatusMessage = $"Unable to bulk export certificates: {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Unable to reach Wialon during bulk export: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error during bulk export: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBulkExportCertificatesForClient()
    {
        return !IsBusy &&
               allUnits.Count > 0 &&
               SelectedAccountFilter is { FilterValue.Length: > 0 };
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedAccountFilterChanged(WialonAccountFilterOption? value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var previousSelectionId = SelectedUnit?.UnitId;
        var searchTerms = (FilterText ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var selectedAccount = SelectedAccountFilter?.FilterValue ?? string.Empty;
        var filtered = GetFilteredUnits(allUnits, selectedAccount, searchTerms);

        FilteredUnits = new ObservableCollection<WialonUnitSummary>(filtered);
        TotalUnitCount = allUnits.Count;
        VisibleUnitCount = filtered.Count;
        SelectedUnit = previousSelectionId.HasValue
            ? filtered.FirstOrDefault(unit => unit.UnitId == previousSelectionId.Value)
            : filtered.FirstOrDefault();

        QueueConnectivityHydration();
    }

    private static bool MatchesSearch(WialonUnitSummary unit, string[] searchTerms)
    {
        if (searchTerms.Length == 0)
        {
            return true;
        }

        var searchableText = string.Join(" ", new[]
        {
            unit.Name,
            unit.UniqueId ?? string.Empty,
            unit.PhoneNumber ?? string.Empty,
            unit.AccountDisplay,
            unit.HardwareTypeDisplay,
            unit.AccountId?.ToString() ?? string.Empty,
            unit.UnitId.ToString()
        });

        return searchTerms.All(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static List<WialonUnitSummary> GetFilteredUnits(
        IEnumerable<WialonUnitSummary> units,
        string selectedAccount,
        string[] searchTerms)
    {
        return units
            .Where(unit => string.IsNullOrWhiteSpace(selectedAccount) ||
                           string.Equals(unit.AccountId?.ToString(), selectedAccount, StringComparison.OrdinalIgnoreCase))
            .Where(unit => MatchesSearch(unit, searchTerms))
            .ToList();
    }

    private static ObservableCollection<WialonAccountFilterOption> BuildAccountFilters(IEnumerable<WialonUnitSummary> units)
    {
        var filters = new List<WialonAccountFilterOption>
        {
            new()
            {
                FilterValue = string.Empty,
                DisplayText = "All Accounts"
            }
        };

        var accountGroups = units
            .Where(unit => unit.AccountId.HasValue)
            .GroupBy(unit => unit.AccountId!.Value)
            .Select(group =>
            {
                var displayName = group
                    .Select(unit => unit.AccountLabel)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?.Trim();

                return new
                {
                    AccountId = group.Key,
                    DisplayName = displayName,
                    UnitCount = group.Count()
                };
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.DisplayName))
            .OrderBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase);

        filters.AddRange(accountGroups.Select(group => new WialonAccountFilterOption
        {
            FilterValue = group.AccountId.ToString(),
            DisplayText = $"{group.DisplayName} ({group.UnitCount} unit{(group.UnitCount == 1 ? string.Empty : "s")})"
        }));

        return new ObservableCollection<WialonAccountFilterOption>(filters);
    }

    private void QueueConnectivityHydration()
    {
        if (string.IsNullOrWhiteSpace(CurrentSessionId) || FilteredUnits.Count == 0)
        {
            return;
        }

        var visibleUnits = FilteredUnits
            .Where(unit => string.IsNullOrWhiteSpace(unit.UniqueId) || string.IsNullOrWhiteSpace(unit.PhoneNumber))
            .Take(ConnectivityHydrationThreshold + 1)
            .ToList();

        if (visibleUnits.Count == 0 || visibleUnits.Count > ConnectivityHydrationThreshold)
        {
            return;
        }

        connectivityHydrationCts?.Cancel();
        connectivityHydrationCts?.Dispose();
        connectivityHydrationCts = new CancellationTokenSource();

        var sessionId = CurrentSessionId;
        var unitIds = visibleUnits.Select(unit => unit.UnitId).Distinct().ToList();
        _ = HydrateConnectivityDetailsAsync(sessionId!, unitIds, connectivityHydrationCts.Token);
    }

    private static async Task<Dictionary<long, string>> LoadAccountNamesAsync(
        WialonApiClient client,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await client.GetAccountNamesAsync(sessionId, cancellationToken).ConfigureAwait(false);
            return new Dictionary<long, string>(accounts);
        }
        catch (WialonApiException)
        {
            return new Dictionary<long, string>();
        }
        catch (HttpRequestException)
        {
            return new Dictionary<long, string>();
        }
    }

    private async Task HydrateConnectivityDetailsAsync(
        string sessionId,
        IReadOnlyList<long> unitIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || unitIds.Count == 0)
        {
            return;
        }

        try
        {
            var client = new WialonApiClient(ApiHost);
            var unitLookup = allUnits.ToDictionary(unit => unit.UnitId);
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);

            foreach (var unitId in unitIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!unitLookup.TryGetValue(unitId, out var unit))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(unit.UniqueId) && !string.IsNullOrWhiteSpace(unit.PhoneNumber))
                {
                    continue;
                }

                try
                {
                    var details = await client.GetUnitDetailsAsync(sessionId, unitId, cancellationToken).ConfigureAwait(false);
                    await RunOnUiThreadAsync(() =>
                    {
                        if (string.IsNullOrWhiteSpace(unit.UniqueId) && !string.IsNullOrWhiteSpace(details.UniqueId))
                        {
                            unit.UniqueId = details.UniqueId;
                        }

                        if (string.IsNullOrWhiteSpace(unit.PhoneNumber) && !string.IsNullOrWhiteSpace(details.PhoneNumber))
                        {
                            unit.PhoneNumber = details.PhoneNumber;
                        }

                        if (string.IsNullOrWhiteSpace(unit.AccountLabel) && !string.IsNullOrWhiteSpace(details.AccountLabel))
                        {
                            unit.AccountLabel = details.AccountLabel;
                        }

                        if (!unit.HardwareTypeId.HasValue && details.HardwareTypeId.HasValue)
                        {
                            unit.HardwareTypeId = details.HardwareTypeId;
                        }

                        if (string.IsNullOrWhiteSpace(unit.HardwareTypeName) && !string.IsNullOrWhiteSpace(details.HardwareTypeName))
                        {
                            unit.HardwareTypeName = details.HardwareTypeName;
                        }

                        ApplyHardwareTypeName(unit);
                    });
                }
                catch (WialonApiException)
                {
                    // Ignore per-unit lookup failures. The list already has the base data.
                }
                catch (HttpRequestException)
                {
                    // Ignore per-unit lookup failures. The list already has the base data.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A newer filter or load request superseded this refresh.
        }
    }

    private async Task<IReadOnlyDictionary<long, string>> LoadHardwareTypeNamesAsync(
        WialonApiClient client,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetHardwareTypeNamesAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }
        catch (WialonApiException)
        {
            return new Dictionary<long, string>();
        }
        catch (HttpRequestException)
        {
            return new Dictionary<long, string>();
        }
    }

    private void ApplyHardwareTypeName(WialonUnitSummary unit)
    {
        if (unit.HardwareTypeId.HasValue &&
            string.IsNullOrWhiteSpace(unit.HardwareTypeName) &&
            hardwareTypeNames.TryGetValue(unit.HardwareTypeId.Value, out var hardwareTypeName))
        {
            unit.HardwareTypeName = hardwareTypeName;
        }
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        if (Application.Current?.Dispatcher is not { } dispatcher || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private async Task<string> EnsureSessionIdAsync(WialonApiClient client, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(CurrentSessionId))
        {
            return CurrentSessionId!;
        }

        var session = await client.LoginAsync(AccessToken.Trim(), cancellationToken).ConfigureAwait(true);
        CurrentSessionId = session.SessionId;
        return session.SessionId;
    }

    private async Task<WialonUnitDetails> GetUnitDetailsWithRetryAsync(
        WialonApiClient client,
        string sessionId,
        long unitId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetUnitDetailsAsync(sessionId, unitId, cancellationToken).ConfigureAwait(true);
        }
        catch (WialonApiException ex) when (ex.ErrorCode == 6)
        {
            var refreshedSessionId = await EnsureRefreshedSessionIdAsync(client, cancellationToken).ConfigureAwait(true);
            return await client.GetUnitDetailsAsync(refreshedSessionId, unitId, cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task<string> EnsureRefreshedSessionIdAsync(WialonApiClient client, CancellationToken cancellationToken = default)
    {
        CurrentSessionId = null;
        return await EnsureSessionIdAsync(client, cancellationToken).ConfigureAwait(true);
    }

    private string ResolveClientDisplayName(IReadOnlyList<WialonUnitSummary> clientUnits)
    {
        var accountLabel = clientUnits
            .Select(unit => unit.AccountLabel)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(accountLabel))
        {
            return accountLabel.Trim();
        }

        var displayText = SelectedAccountFilter?.DisplayText ?? "Client";
        var suffixIndex = displayText.LastIndexOf(" (", StringComparison.Ordinal);
        return suffixIndex > 0 ? displayText[..suffixIndex].Trim() : displayText.Trim();
    }

    private string? ResolveHardwareTypeName(WialonUnitSummary unit, WialonUnitDetails details)
    {
        if (!string.IsNullOrWhiteSpace(details.HardwareTypeName))
        {
            return details.HardwareTypeName;
        }

        if (!string.IsNullOrWhiteSpace(unit.HardwareTypeName))
        {
            return unit.HardwareTypeName;
        }

        var hardwareTypeId = details.HardwareTypeId ?? unit.HardwareTypeId;
        if (hardwareTypeId.HasValue &&
            hardwareTypeNames.TryGetValue(hardwareTypeId.Value, out var hardwareTypeName))
        {
            return hardwareTypeName;
        }

        return null;
    }
}
