using PurchaseOrderApp.Models;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace PurchaseOrderApp.Services;

internal sealed class JobCardEvidenceService
{
    private const string JobCardsFolderName = "JobCards";

    internal string GetJobCardFolderPath(string jobCardNumber)
    {
        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var folderPath = Path.Combine(documentsFolder, JobCardsFolderName, jobCardNumber);
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    internal string GetPdfPath(string jobCardNumber)
    {
        return Path.Combine(GetJobCardFolderPath(jobCardNumber), $"{jobCardNumber}.pdf");
    }

    internal string GetEvidencePath(string jobCardNumber, JobCardEvidencePhotoType photoType)
    {
        return Path.Combine(GetJobCardFolderPath(jobCardNumber), photoType.GetStoredFileName());
    }

    internal string? GetExistingEvidencePath(string jobCardNumber, JobCardEvidencePhotoType photoType)
    {
        var path = GetEvidencePath(jobCardNumber, photoType);
        return File.Exists(path) ? path : null;
    }

    internal string SaveEvidencePhoto(string jobCardNumber, JobCardEvidencePhotoType photoType, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new InvalidOperationException("Select an image file first.");
        }

        var targetPath = GetEvidencePath(jobCardNumber, photoType);

        using var inputStream = File.OpenRead(sourceFilePath);
        var decoder = BitmapDecoder.Create(
            inputStream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames.FirstOrDefault()
            ?? throw new InvalidOperationException("I couldn't read that image file.");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(frame);

        using var outputStream = File.Create(targetPath);
        encoder.Save(outputStream);
        return targetPath;
    }

    internal void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }
}
