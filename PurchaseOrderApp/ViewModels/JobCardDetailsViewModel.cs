using CommunityToolkit.Mvvm.ComponentModel;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace PurchaseOrderApp.ViewModels;

internal sealed partial class JobCardDetailsViewModel : ObservableObject
{
    private readonly int jobCardRecordId;
    private readonly JobCardRegistryService jobCardRegistryService = new();
    private readonly JobCardEvidenceService jobCardEvidenceService = new();
    private readonly JobCardPdfService jobCardPdfService = new();
    private JobCardRecord? currentRecord;

    public JobCardDetailsViewModel(int jobCardRecordId)
    {
        this.jobCardRecordId = jobCardRecordId;
        StatusMessage = "Loading job card details...";
        Refresh();
    }

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string jobCardNumber = string.Empty;

    [ObservableProperty]
    private string workflowStatus = string.Empty;

    [ObservableProperty]
    private string createdAtDisplay = string.Empty;

    [ObservableProperty]
    private string client = string.Empty;

    [ObservableProperty]
    private string unitDisplay = "Pending";

    [ObservableProperty]
    private string accountDisplay = "Pending";

    [ObservableProperty]
    private string creatorDisplay = "Pending";

    [ObservableProperty]
    private string hardwareTypeDisplay = "Pending";

    [ObservableProperty]
    private string phoneNumberDisplay = "Pending";

    [ObservableProperty]
    private string uniqueId = string.Empty;

    [ObservableProperty]
    private string iccid = string.Empty;

    [ObservableProperty]
    private string jobCardName = string.Empty;

    [ObservableProperty]
    private string brand = string.Empty;

    [ObservableProperty]
    private string model = string.Empty;

    [ObservableProperty]
    private string year = string.Empty;

    [ObservableProperty]
    private string colour = string.Empty;

    [ObservableProperty]
    private string vehicleClass = string.Empty;

    [ObservableProperty]
    private string vehicleType = string.Empty;

    [ObservableProperty]
    private string registrationPlate = string.Empty;

    [ObservableProperty]
    private string vin = string.Empty;

    [ObservableProperty]
    private string contact1 = string.Empty;

    [ObservableProperty]
    private string contact2 = string.Empty;

    [ObservableProperty]
    private string makeAndModel = string.Empty;

    [ObservableProperty]
    private string registrationFleet = string.Empty;

    [ObservableProperty]
    private string statusNotes = "None";

    [ObservableProperty]
    private string amendmentNotes = "None";

    [ObservableProperty]
    private string evidenceStatusDisplay = string.Empty;

    [ObservableProperty]
    private string generatedPdfPath = string.Empty;

    [ObservableProperty]
    private string billingSystemTypeDisplay = JobCardBillingSystemTypes.QuickSting;

    [ObservableProperty]
    private string systemPriceExVatDisplay = "Pending";

    [ObservableProperty]
    private string panicButtonSummaryDisplay = "Not included";

    [ObservableProperty]
    private string earlyWarningSystemSummaryDisplay = "Not included";

    [ObservableProperty]
    private string bleSensorSummaryDisplay = "Not included";

    [ObservableProperty]
    private string lvCanAdaptorSummaryDisplay = "Not included";

    [ObservableProperty]
    private string otherHardwareSummaryDisplay = "None";

    [ObservableProperty]
    private string billingNotesDisplay = "None";

    [ObservableProperty]
    private string billingTotalExVatDisplay = "0.00 ex VAT";

    [ObservableProperty]
    private BitmapImage? vehiclePhotoPreview;

    [ObservableProperty]
    private BitmapImage? registrationPhotoPreview;

    [ObservableProperty]
    private BitmapImage? vinPhotoPreview;

    [ObservableProperty]
    private BitmapImage? trackingUnitPhotoPreview;

    public bool HasVehiclePhoto => VehiclePhotoPreview is not null;

    public bool HasRegistrationPhoto => RegistrationPhotoPreview is not null;

    public bool HasVinPhoto => VinPhotoPreview is not null;

    public bool HasTrackingUnitPhoto => TrackingUnitPhotoPreview is not null;

    public string VehiclePhotoStatus => HasVehiclePhoto ? "Uploaded" : "Not uploaded";

    public string RegistrationPhotoStatus => HasRegistrationPhoto ? "Uploaded" : "Not uploaded";

    public string VinPhotoStatus => HasVinPhoto ? "Uploaded" : "Not uploaded";

    public string TrackingUnitPhotoStatus => HasTrackingUnitPhoto ? "Uploaded" : "Not uploaded";

    public void Refresh()
    {
        currentRecord = jobCardRegistryService.GetJobCard(jobCardRecordId)
            ?? throw new InvalidOperationException("I couldn't find that job card record.");

        JobCardNumber = currentRecord.JobCardNumber;
        WorkflowStatus = currentRecord.WorkflowStatus;
        CreatedAtDisplay = currentRecord.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        Client = currentRecord.Client;
        UnitDisplay = currentRecord.WialonUnitId.HasValue
            ? string.IsNullOrWhiteSpace(currentRecord.WialonUnitName)
                ? currentRecord.WialonUnitId.Value.ToString(CultureInfo.InvariantCulture)
                : $"{currentRecord.WialonUnitName} ({currentRecord.WialonUnitId.Value})"
            : string.IsNullOrWhiteSpace(currentRecord.WialonUnitName)
                ? "Pending"
                : currentRecord.WialonUnitName;
        AccountDisplay = BuildEntityDisplay(currentRecord.WialonAccountName, currentRecord.WialonAccountId);
        CreatorDisplay = BuildEntityDisplay(currentRecord.WialonCreatorName, currentRecord.WialonCreatorId);
        HardwareTypeDisplay = BuildEntityDisplay(currentRecord.WialonHardwareTypeName, currentRecord.WialonHardwareTypeId);
        PhoneNumberDisplay = string.IsNullOrWhiteSpace(currentRecord.PhoneNumber) ? "Pending" : currentRecord.PhoneNumber;
        UniqueId = currentRecord.UniqueId;
        Iccid = currentRecord.Iccid;
        JobCardName = currentRecord.JobCardName;
        Brand = currentRecord.Brand;
        Model = currentRecord.Model;
        Year = currentRecord.Year;
        Colour = currentRecord.Colour;
        VehicleClass = currentRecord.VehicleClass;
        VehicleType = currentRecord.VehicleType;
        RegistrationPlate = currentRecord.RegistrationPlate;
        Vin = currentRecord.Vin;
        Contact1 = currentRecord.Contact1;
        Contact2 = currentRecord.Contact2;
        MakeAndModel = currentRecord.MakeAndModel;
        RegistrationFleet = currentRecord.RegistrationFleet;
        BillingSystemTypeDisplay = JobCardBillingHelper.ResolveSystemType(currentRecord);
        SystemPriceExVatDisplay = JobCardBillingHelper.FormatAmount(currentRecord.SystemPriceExVat);
        PanicButtonSummaryDisplay = JobCardBillingHelper.BuildOptionalLineDisplay(currentRecord.HasPanicButton, currentRecord.PanicButtonPriceExVat);
        EarlyWarningSystemSummaryDisplay = JobCardBillingHelper.BuildOptionalLineDisplay(currentRecord.HasEarlyWarningSystem, currentRecord.EarlyWarningSystemPriceExVat);
        BleSensorSummaryDisplay = JobCardBillingHelper.BuildBleSensorDisplay(currentRecord.BleSensorQuantity, currentRecord.BleSensorUnitPriceExVat);
        LvCanAdaptorSummaryDisplay = JobCardBillingHelper.BuildOptionalLineDisplay(currentRecord.HasLvCanAdaptor, currentRecord.LvCanAdaptorPriceExVat);
        OtherHardwareSummaryDisplay = JobCardBillingHelper.BuildOtherHardwareDisplay(currentRecord.OtherHardwareDescription, currentRecord.OtherHardwarePriceExVat);
        BillingNotesDisplay = string.IsNullOrWhiteSpace(currentRecord.BillingNotes) ? "None" : currentRecord.BillingNotes;
        BillingTotalExVatDisplay = $"{JobCardBillingHelper.FormatAmount(JobCardBillingHelper.CalculateTotalExVat(currentRecord))} ex VAT";
        StatusNotes = string.IsNullOrWhiteSpace(currentRecord.StatusNotes) ? "None" : currentRecord.StatusNotes;
        AmendmentNotes = string.IsNullOrWhiteSpace(currentRecord.AmendmentNotes) ? "None" : currentRecord.AmendmentNotes;
        GeneratedPdfPath = jobCardEvidenceService.GetPdfPath(currentRecord.JobCardNumber);

        VehiclePhotoPreview = LoadPhotoPreview(JobCardEvidencePhotoType.Vehicle);
        RegistrationPhotoPreview = LoadPhotoPreview(JobCardEvidencePhotoType.Registration);
        VinPhotoPreview = LoadPhotoPreview(JobCardEvidencePhotoType.Vin);
        TrackingUnitPhotoPreview = LoadPhotoPreview(JobCardEvidencePhotoType.TrackingUnit);

        OnPropertyChanged(nameof(HasVehiclePhoto));
        OnPropertyChanged(nameof(HasRegistrationPhoto));
        OnPropertyChanged(nameof(HasVinPhoto));
        OnPropertyChanged(nameof(HasTrackingUnitPhoto));
        OnPropertyChanged(nameof(VehiclePhotoStatus));
        OnPropertyChanged(nameof(RegistrationPhotoStatus));
        OnPropertyChanged(nameof(VinPhotoStatus));
        OnPropertyChanged(nameof(TrackingUnitPhotoStatus));

        EvidenceStatusDisplay = BuildEvidenceStatusDisplay();
        StatusMessage = $"Loaded job card {JobCardNumber}.";
    }

    public void UploadPhoto(JobCardEvidencePhotoType photoType, string sourceFilePath)
    {
        if (currentRecord is null)
        {
            throw new InvalidOperationException("The job card details are not loaded yet.");
        }

        jobCardEvidenceService.SaveEvidencePhoto(currentRecord.JobCardNumber, photoType, sourceFilePath);
        jobCardRegistryService.UpdateEvidenceStatus(jobCardRecordId, photoType);
        Refresh();
        StatusMessage = $"{photoType.GetDisplayName()} uploaded for {JobCardNumber}.";
    }

    public string ExportPdf(bool openAfterSave)
    {
        if (currentRecord is null)
        {
            throw new InvalidOperationException("The job card details are not loaded yet.");
        }

        var pdfPath = jobCardPdfService.ExportJobCardPdf(currentRecord, jobCardEvidenceService);
        GeneratedPdfPath = pdfPath;
        StatusMessage = $"Saved the printable PDF for {JobCardNumber}.";

        if (openAfterSave)
        {
            jobCardEvidenceService.OpenPath(pdfPath);
        }

        return pdfPath;
    }

    public void OpenEvidence(JobCardEvidencePhotoType photoType)
    {
        if (currentRecord is null)
        {
            throw new InvalidOperationException("The job card details are not loaded yet.");
        }

        var path = jobCardEvidenceService.GetExistingEvidencePath(currentRecord.JobCardNumber, photoType);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"There is no {photoType.GetDisplayName().ToLowerInvariant()} uploaded for this job card yet.");
        }

        jobCardEvidenceService.OpenPath(path);
    }

    public void OpenPdf()
    {
        if (string.IsNullOrWhiteSpace(GeneratedPdfPath) || !File.Exists(GeneratedPdfPath))
        {
            throw new InvalidOperationException("Generate the printable PDF first.");
        }

        jobCardEvidenceService.OpenPath(GeneratedPdfPath);
    }

    public void OpenJobCardFolder()
    {
        if (currentRecord is null)
        {
            throw new InvalidOperationException("The job card details are not loaded yet.");
        }

        jobCardEvidenceService.OpenPath(jobCardEvidenceService.GetJobCardFolderPath(currentRecord.JobCardNumber));
    }

    private BitmapImage? LoadPhotoPreview(JobCardEvidencePhotoType photoType)
    {
        if (currentRecord is null)
        {
            return null;
        }

        var path = jobCardEvidenceService.GetExistingEvidencePath(currentRecord.JobCardNumber, photoType);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        using var stream = File.OpenRead(path);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.DecodePixelWidth = 420;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private string BuildEvidenceStatusDisplay()
    {
        if (currentRecord is null)
        {
            return "No evidence loaded.";
        }

        var uploadedCount = new[]
        {
            HasVehiclePhoto,
            HasRegistrationPhoto,
            HasVinPhoto,
            HasTrackingUnitPhoto
        }.Count(flag => flag);

        var uploadedAt = currentRecord.EvidenceReceivedAt.HasValue
            ? currentRecord.EvidenceReceivedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Pending";

        return $"{uploadedCount} / 4 evidence photos uploaded. Evidence received: {uploadedAt}.";
    }

    private static string BuildEntityDisplay(string? name, long? id)
    {
        if (string.IsNullOrWhiteSpace(name) && !id.HasValue)
        {
            return "Pending";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return id!.Value.ToString(CultureInfo.InvariantCulture);
        }

        return id.HasValue
            ? $"{name.Trim()} ({id.Value})"
            : name.Trim();
    }
}
