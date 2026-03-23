using PurchaseOrderApp.ViewModels;
using System.Diagnostics;
using System.IO;

namespace PurchaseOrderApp;

internal static class OrderDocumentLauncher
{
    internal static void OpenStoredDocument(MainViewModel.StoredOrderDocument storedDocument)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "PurchaseOrderApp", "Documents");
        Directory.CreateDirectory(tempDirectory);

        var safeFileName = Path.GetFileNameWithoutExtension(storedDocument.FileName);
        var extension = Path.GetExtension(storedDocument.FileName);
        var tempFilePath = Path.Combine(tempDirectory, $"{safeFileName}_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(tempFilePath, storedDocument.Content);

        Process.Start(new ProcessStartInfo(tempFilePath)
        {
            UseShellExecute = true
        });
    }
}
