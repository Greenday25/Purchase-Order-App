using PurchaseOrderApp;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PurchaseOrderApp.Services;

internal static class OrderArchiveService
{
    private const string ArchiveRootFolderName = "orders";
    private const string OrderPdfFileName = "Purchase Order.pdf";
    private const string SignedOrderBaseFileName = "Approved Purchase Order";
    private const string InvoiceBaseFileName = "Invoice";

    private static readonly HashSet<char> InvalidFileNameCharacters = new(Path.GetInvalidFileNameChars());

    internal static bool TryArchiveCompletedOrder(int orderId)
    {
        return TrySyncOrderFolder(orderId, showWarnings: true);
    }

    internal static bool TrySyncOrderFolder(int orderId)
    {
        return TrySyncOrderFolder(orderId, showWarnings: false);
    }

    private static bool TrySyncOrderFolder(int orderId, bool showWarnings)
    {
        try
        {
            var archiveViewModel = new MainViewModel();
            if (!archiveViewModel.LoadExistingOrder(orderId, skipAccessCheck: true) || archiveViewModel.CurrentOrder == null)
            {
                ShowWarningIfRequested(showWarnings, $"I couldn't reload order {orderId} for archiving.", "Archive failed");
                return false;
            }

            var order = archiveViewModel.CurrentOrder;
            var archiveFolderPath = GetWritableArchiveFolderPath(order);

            var orderPdfPath = Path.Combine(archiveFolderPath, OrderPdfFileName);
            var exportWindow = new OrderEditorWindow(archiveViewModel);
            var exportSucceeded = exportWindow.ExportCurrentOrderToPdf(orderPdfPath, openAfterSave: false);
            if (!exportSucceeded || !File.Exists(orderPdfPath))
            {
                return false;
            }

            if (HasDocument(order.SignedOrderFileName, order.SignedOrderContent) &&
                !SaveArchivedDocument(archiveFolderPath, SignedOrderBaseFileName, order.SignedOrderFileName, order.SignedOrderContent))
            {
                ShowWarningIfRequested(
                    showWarnings,
                    $"I couldn't copy the signed order for {order.OrderNumber} into the archive folder.",
                    "Archive failed");
                return false;
            }

            if (HasDocument(order.InvoiceFileName, order.InvoiceContent) &&
                !SaveArchivedDocument(archiveFolderPath, InvoiceBaseFileName, order.InvoiceFileName, order.InvoiceContent))
            {
                ShowWarningIfRequested(
                    showWarnings,
                    $"I couldn't copy the invoice for {order.OrderNumber} into the archive folder.",
                    "Archive failed");
                return false;
            }

            return true;
        }
        catch (System.Exception ex)
        {
            ShowWarningIfRequested(showWarnings, $"I couldn't save the order folder.\n\n{ex.Message}", "Archive failed");
            return false;
        }
    }

    private static bool HasDocument(string? fileName, byte[]? content)
    {
        return !string.IsNullOrWhiteSpace(fileName) && content is { Length: > 0 };
    }

    private static string GetWritableArchiveFolderPath(PurchaseOrder order)
    {
        var preferredArchiveFolderPath = GetArchiveFolderPath(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            order);

        if (TryPrepareWritableFolder(preferredArchiveFolderPath))
        {
            return preferredArchiveFolderPath;
        }

        var fallbackArchiveFolderPath = GetArchiveFolderPath(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PurchaseOrderApp"),
            order);
        Directory.CreateDirectory(fallbackArchiveFolderPath);
        return fallbackArchiveFolderPath;
    }

    private static string GetArchiveFolderPath(string rootFolder, PurchaseOrder order)
    {
        var companyFolder = GetCompanyFolderName(order.Vendor?.Name);
        var orderFolder = $"{CleanFileNameSegment(order.OrderNumber, "Order")} - {order.Date:yyyy-MM-dd}";

        return Path.Combine(rootFolder, ArchiveRootFolderName, companyFolder, orderFolder);
    }

    private static bool TryPrepareWritableFolder(string folderPath)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            var probePath = Path.Combine(folderPath, $".write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probePath, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetCompanyFolderName(string? companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return "Unknown Company";
        }

        return companyName switch
        {
            var name when string.Equals(name, "CAPITAL AIR (Pty) Ltd", StringComparison.OrdinalIgnoreCase) => "Capital Air",
            var name when string.Equals(name, "Capital Air Reaction Services CC", StringComparison.OrdinalIgnoreCase) => "Capital Air Reaction Services",
            var name when string.Equals(name, "Capital Air Reaction Services (Pty) Ltd", StringComparison.OrdinalIgnoreCase) => "Capital Air Reaction Services",
            var name when string.Equals(name, "Capital Air Security Operations (Pty) Ltd", StringComparison.OrdinalIgnoreCase) => "Capital Air Security Operations",
            _ => CleanFileNameSegment(companyName, "Unknown Company")
        };
    }

    private static bool SaveArchivedDocument(string archiveFolderPath, string baseFileName, string? sourceFileName, byte[]? content)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName) || content is null || content.Length == 0)
        {
            return false;
        }

        var extension = Path.GetExtension(sourceFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pdf";
        }

        var archivePath = Path.Combine(archiveFolderPath, $"{baseFileName}{extension}");
        File.WriteAllBytes(archivePath, content);
        return true;
    }

    private static string CleanFileNameSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var cleaned = string.Concat(value.Where(ch => !InvalidFileNameCharacters.Contains(ch)))
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    private static void ShowWarning(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void ShowWarningIfRequested(bool showWarning, string message, string title)
    {
        if (showWarning)
        {
            ShowWarning(message, title);
        }
    }
}
