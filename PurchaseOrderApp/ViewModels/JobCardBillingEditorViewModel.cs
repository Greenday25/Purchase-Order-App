using CommunityToolkit.Mvvm.ComponentModel;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;

namespace PurchaseOrderApp.ViewModels;

internal sealed partial class JobCardBillingEditorViewModel : ObservableObject
{
    private readonly int jobCardRecordId;
    private readonly JobCardRegistryService jobCardRegistryService = new();

    public JobCardBillingEditorViewModel(int jobCardRecordId)
    {
        this.jobCardRecordId = jobCardRecordId;
        Load();
    }

    [ObservableProperty]
    private string jobCardNumber = string.Empty;

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
    private string systemPriceExVat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BillingSystemTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(PanicButtonSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private bool hasPanicButton;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanicButtonSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private string panicButtonPriceExVat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BillingSystemTypeDisplay))]
    [NotifyPropertyChangedFor(nameof(EarlyWarningSystemSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
    private bool hasEarlyWarningSystem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EarlyWarningSystemSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
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
    private bool hasLvCanAdaptor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LvCanAdaptorSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingTotalExVatDisplay))]
    [NotifyPropertyChangedFor(nameof(BillingSummaryDisplay))]
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

    public void Save()
    {
        jobCardRegistryService.UpdateBillingDetails(
            jobCardRecordId,
            new JobCardRegistryService.UpdateBillingRequest(
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
                BillingNotes));
    }

    private void Load()
    {
        var record = jobCardRegistryService.GetJobCard(jobCardRecordId)
            ?? throw new InvalidOperationException("I couldn't find that job card record.");

        JobCardNumber = record.JobCardNumber;
        UseCustomBillingSystem = record.UseCustomBillingSystem;
        CustomBillingSystemName = record.CustomBillingSystemName ?? string.Empty;
        SystemPriceExVat = record.SystemPriceExVat ?? string.Empty;
        HasPanicButton = record.HasPanicButton;
        PanicButtonPriceExVat = record.PanicButtonPriceExVat ?? string.Empty;
        HasEarlyWarningSystem = record.HasEarlyWarningSystem;
        EarlyWarningSystemPriceExVat = record.EarlyWarningSystemPriceExVat ?? string.Empty;
        BleSensorQuantity = record.BleSensorQuantity ?? string.Empty;
        BleSensorUnitPriceExVat = record.BleSensorUnitPriceExVat ?? string.Empty;
        HasLvCanAdaptor = record.HasLvCanAdaptor;
        LvCanAdaptorPriceExVat = record.LvCanAdaptorPriceExVat ?? string.Empty;
        OtherHardwareDescription = record.OtherHardwareDescription ?? string.Empty;
        OtherHardwarePriceExVat = record.OtherHardwarePriceExVat ?? string.Empty;
        BillingNotes = record.BillingNotes ?? string.Empty;
    }
}
