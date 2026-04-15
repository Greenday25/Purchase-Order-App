namespace PurchaseOrderApp.Models;

internal enum JobCardEvidencePhotoType
{
    Vehicle,
    Registration,
    Vin,
    TrackingUnit
}

internal static class JobCardEvidencePhotoTypeExtensions
{
    internal static string GetDisplayName(this JobCardEvidencePhotoType photoType)
    {
        return photoType switch
        {
            JobCardEvidencePhotoType.Vehicle => "Vehicle Photo",
            JobCardEvidencePhotoType.Registration => "Registration Photo",
            JobCardEvidencePhotoType.Vin => "VIN Photo",
            JobCardEvidencePhotoType.TrackingUnit => "Tracking Unit Photo",
            _ => "Photo"
        };
    }

    internal static string GetStoredFileName(this JobCardEvidencePhotoType photoType)
    {
        return photoType switch
        {
            JobCardEvidencePhotoType.Vehicle => "vehicle.png",
            JobCardEvidencePhotoType.Registration => "registration.png",
            JobCardEvidencePhotoType.Vin => "vin.png",
            JobCardEvidencePhotoType.TrackingUnit => "tracking-unit.png",
            _ => "photo.png"
        };
    }
}
