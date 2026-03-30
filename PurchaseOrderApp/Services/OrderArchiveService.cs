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
    private const string SignedOrderBaseFileName = "Signed Order";
    private const string InvoiceBaseFileName = "Invoice";

    private static readonly HashSet<char> InvalidFileNameCharacters = new(Path.GetInvalidFileNameChars());

    internal static bool TryArchiveCompletedOrder(int orderId)
    {
        try
        {
            var archiveViewModel = new MainViewModel();
            if (!archiveViewModel.LoadExistingOrder(orderId) || archiveViewModel.CurrentOrder == null)
            {
                ShowWarning($"I couldn't reload order {orderId} for archiving.", "Archive failed");
                return false;
            }

            var order = archiveViewModel.CurrentOrder;
            if (!HasCompletedDocuments(order))
            {
                ShowWarning(
                    $"Order {order.OrderNumber} does not have both the signed order and invoice yet, so the archive folder was not created.",
                    "Archive not ready");
                return false;
            }

            var archiveFolderPath = GetArchiveFolderPath(order);
            Directory.CreateDirectory(archiveFolderPath);

            var orderPdfPath = Path.Combine(archiveFolderPath, OrderPdfFileName);
            var exportWindow = new OrderEditorWindow(archiveViewModel);
            var exportSucceeded = exportWindow.ExportCurrentOrderToPdf(orderPdfPath, openAfterSave: false);
            if (!exportSucceeded || !File.Exists(orderPdfPath))
            {
                return false;
            }

            if (!SaveArchivedDocument(archiveFolderPath, SignedOrderBaseFileName, order.SignedOrderFileName, order.SignedOrderContent))
            {
                ShowWarning(
                    $"I couldn't copy the signed order for {order.OrderNumber} into the archive folder.",
                    "Archive failed");
                return false;
            }

            if (!SaveArchivedDocument(archiveFolderPath, InvoiceBaseFileName, order.InvoiceFileName, order.InvoiceContent))
            {
                ShowWarning(
                    $"I couldn't copy the invoice for {order.OrderNumber} into the archive folder.",
                    "Archive failed");
                return false;
            }

            return true;
        }
        catch (System.Exception ex)
        {
            ShowWarning($"I couldn't save the completed order archive.\n\n{ex.Message}", "Archive failed");
            return false;
        }
    }

    private static bool HasCompletedDocuments(PurchaseOrder order)
    {
        return !string.IsNullOrWhiteSpace(order.OrderNumber)
            && !string.IsNullOrWhiteSpace(order.SignedOrderFileName)
            && order.SignedOrderContent is { Length: > 0 }
            && !string.IsNullOrWhiteSpace(order.InvoiceFileName)
            && order.InvoiceContent is { Length: > 0 };
    }

    private static string GetArchiveFolderPath(PurchaseOrder order)
    {
        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var companyFolder = GetCompanyFolderName(order.Vendor?.Name);
        var orderFolder = $"{CleanFileNameSegment(order.OrderNumber, "Order")} - {order.Date:yyyy-MM-dd}";

        return Path.Combine(documentsFolder, ArchiveRootFolderName, companyFolder, orderFolder);
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
}
