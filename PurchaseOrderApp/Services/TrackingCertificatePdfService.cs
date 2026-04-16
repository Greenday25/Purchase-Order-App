using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using PurchaseOrderApp.Models;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;

namespace PurchaseOrderApp.Services;

internal sealed class TrackingCertificatePdfService
{
    private const float PageWidth = 540.05f;
    private const float PageHeight = 340.20f;
    private const float BorderInset = 6f;
    private const float TableX = 143.15f;
    private const float TableWidth = 324.85f;
    private const float TableBottomY = 78f;
    private const float LabelColumnWidth = 127.10f;
    private const float ValueColumnWidth = 197.75f;

    private static readonly DeviceRgb SignatoryBlue = new(67, 82, 155);

    internal string ExportCertificate(TrackingCertificateData certificate, string? exportFolder = null)
    {
        var outputPath = GetPdfExportPath(certificate, exportFolder);
        Directory.CreateDirectory(IOPath.GetDirectoryName(outputPath)!);

        using var writer = new PdfWriter(outputPath);
        using var pdf = new PdfDocument(writer);
        var pageSize = new PageSize(PageWidth, PageHeight);
        var page = pdf.AddNewPage(pageSize);
        using var document = new Document(pdf, pageSize);
        document.SetMargins(0, 0, 0, 0);

        var bodyFont = CreateFont("arial.ttf", StandardFonts.HELVETICA);
        var boldFont = CreateFont("arialbd.ttf", StandardFonts.HELVETICA_BOLD);

        var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
        DrawBorder(canvas);

        AddTemplateImage(document, "certificate-banner.png", 8.25f, 236.10f, 529.40f, 96f);
        AddTemplateImage(document, "certificate-wasp.png", 6f, 16f, 144f, 146.68f);
        AddTemplateImage(document, "certificate-signature.png", 266.50f, 34f, 196.55f, 61.86f);
        AddCertificateTable(document, bodyFont, boldFont, certificate);

        return outputPath;
    }

    internal static string CreateClientBatchExportFolder(string clientName)
    {
        var batchFolder = IOPath.Combine(
            GetDefaultExportRoot(),
            "Bulk Exports",
            SanitizePathSegment(clientName, "Client"),
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        Directory.CreateDirectory(batchFolder);
        return batchFolder;
    }

    private static string GetPdfExportPath(TrackingCertificateData certificate, string? exportFolder)
    {
        var targetFolder = string.IsNullOrWhiteSpace(exportFolder)
            ? GetDefaultExportRoot()
            : exportFolder;
        var fileNameBase = string.IsNullOrWhiteSpace(certificate.RegistrationNumber)
            ? $"{certificate.UnitName} certificate"
            : $"{certificate.RegistrationNumber} certificate";

        var safeFileName = SanitizePathSegment(fileNameBase, $"tracking-certificate-{certificate.UnitId}");
        var outputPath = IOPath.Combine(targetFolder, $"{safeFileName}.pdf");
        if (!File.Exists(outputPath))
        {
            return outputPath;
        }

        var uniqueBaseName = $"{safeFileName}-{certificate.UnitId}";
        outputPath = IOPath.Combine(targetFolder, $"{uniqueBaseName}.pdf");
        if (!File.Exists(outputPath))
        {
            return outputPath;
        }

        var sequence = 2;
        while (true)
        {
            outputPath = IOPath.Combine(targetFolder, $"{uniqueBaseName}-{sequence}.pdf");
            if (!File.Exists(outputPath))
            {
                return outputPath;
            }

            sequence++;
        }
    }

    private static string GetDefaultExportRoot()
    {
        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return IOPath.Combine(documentsFolder, "Tracking Certificates");
    }

    private static string SanitizePathSegment(string value, string fallbackValue)
    {
        var invalidChars = IOPath.GetInvalidFileNameChars();
        var safeValue = new string(value.Where(ch => !invalidChars.Contains(ch)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeValue) ? fallbackValue : safeValue;
    }

    private static void DrawBorder(iText.Kernel.Pdf.Canvas.PdfCanvas canvas)
    {
        canvas.SaveState()
            .SetLineWidth(1f)
            .Rectangle(BorderInset, BorderInset, PageWidth - (BorderInset * 2), PageHeight - (BorderInset * 2))
            .Stroke()
            .RestoreState();
    }

    private static void AddTemplateImage(
        Document document,
        string fileName,
        float x,
        float y,
        float width,
        float height)
    {
        var assetPath = IOPath.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (!File.Exists(assetPath))
        {
            return;
        }

        var image = new Image(ImageDataFactory.Create(assetPath))
            .ScaleAbsolute(width, height)
            .SetFixedPosition(x, y);
        document.Add(image);
    }

    private static void AddCertificateTable(
        Document document,
        PdfFont bodyFont,
        PdfFont boldFont,
        TrackingCertificateData certificate)
    {
        var table = new Table(UnitValue.CreatePointArray(new[] { LabelColumnWidth, ValueColumnWidth }))
            .SetFixedPosition(TableX, TableBottomY, TableWidth)
            .SetBorder(Border.NO_BORDER)
            .SetMargin(0);

        table.AddCell(CreateLabelCell(bodyFont, "Customer/Client:"));
        table.AddCell(CreateValueCell(bodyFont, certificate.CustomerClient, includeTopBorder: false));

        table.AddCell(CreateLabelCell(bodyFont, "Registration Number:"));
        table.AddCell(CreateValueCell(bodyFont, certificate.RegistrationNumber));

        table.AddCell(CreateLabelCell(bodyFont, "VIN:"));
        table.AddCell(CreateValueCell(bodyFont, certificate.Vin));

        table.AddCell(CreateLabelCell(bodyFont, "Vehicle Type:"));
        table.AddCell(CreateValueCell(bodyFont, certificate.VehicleType));

        table.AddCell(CreateLabelCell(bodyFont, "Colour:"));
        table.AddCell(CreateValueCell(bodyFont, certificate.Colour));

        table.AddCell(CreateLabelCell(bodyFont, "Type of System:"));
        table.AddCell(CreateSystemTypeCell(bodyFont, certificate));

        table.AddCell(CreateLabelCell(bodyFont, "VESA/SAIA Number:"));
        table.AddCell(CreateValueCell(bodyFont, "301719"));

        table.AddCell(CreateLabelCell(bodyFont, "Installation Date:"));
        table.AddCell(CreateValueCell(bodyFont, certificate.InstallationDate));

        table.AddCell(CreateLabelCell(bodyFont, string.Empty, 18f));
        table.AddCell(CreateBlankValueCell(18f));

        document.Add(table);
    }

    private static Cell CreateLabelCell(PdfFont font, string text, float minHeight = 14f)
    {
        return new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetPaddingTop(1f)
            .SetPaddingBottom(0.8f)
            .SetPaddingLeft(0)
            .SetPaddingRight(8f)
            .SetMinHeight(minHeight)
            .Add(new Paragraph(text)
                .SetFont(font)
                .SetFontSize(10)
                .SetFontColor(ColorConstants.BLACK)
                .SetMargin(0));
    }

    private static Cell CreateValueCell(PdfFont font, string value, bool includeTopBorder = true, float minHeight = 14f)
    {
        var cell = new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetPaddingTop(1f)
            .SetPaddingBottom(0.8f)
            .SetPaddingLeft(0)
            .SetPaddingRight(0)
            .SetMinHeight(minHeight)
            .SetBorderBottom(new DashedBorder(ColorConstants.BLACK, 0.8f))
            .Add(new Paragraph(string.IsNullOrWhiteSpace(value) ? "Pending" : value.Trim())
                .SetFont(font)
                .SetFontSize(10)
                .SetFontColor(ColorConstants.BLACK)
                .SetMargin(0));

        if (includeTopBorder)
        {
            cell.SetBorderTop(new DashedBorder(ColorConstants.BLACK, 0.8f));
        }

        return cell;
    }

    private static Cell CreateBlankValueCell(float minHeight)
    {
        return new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetPaddingTop(1f)
            .SetPaddingBottom(0.8f)
            .SetPaddingLeft(0)
            .SetPaddingRight(0)
            .SetMinHeight(minHeight)
            .SetBorderTop(new DashedBorder(ColorConstants.BLACK, 0.8f))
            .SetBorderBottom(new DashedBorder(ColorConstants.BLACK, 0.8f))
            .Add(new Paragraph(string.Empty).SetMargin(0));
    }

    private static Cell CreateSystemTypeCell(PdfFont font, TrackingCertificateData certificate)
    {
        var cell = new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetPaddingTop(1f)
            .SetPaddingBottom(0.8f)
            .SetPaddingLeft(0)
            .SetPaddingRight(0)
            .SetMinHeight(14f)
            .SetBorderTop(new DashedBorder(ColorConstants.BLACK, 0.8f))
            .SetBorderBottom(new DashedBorder(ColorConstants.BLACK, 0.8f));

        var paragraph = new Paragraph().SetMargin(0);
        var systemName = string.IsNullOrWhiteSpace(certificate.SystemName) ? "STING" : certificate.SystemName.Trim();
        paragraph.Add(new Text(systemName).SetFont(font).SetFontSize(10));

        if (!string.IsNullOrWhiteSpace(certificate.SerialNumber))
        {
            paragraph.Add(new Text(" (").SetFont(font).SetFontSize(10));
            paragraph.Add(new Text($"S/N: {certificate.SerialNumber.Trim()}").SetFont(font).SetFontSize(9));
            paragraph.Add(new Text(")").SetFont(font).SetFontSize(9));
        }

        cell.Add(paragraph);
        return cell;
    }

    private static PdfFont CreateFont(string fileName, string fallbackFont)
    {
        try
        {
            var systemFontPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fileName);
            if (File.Exists(systemFontPath))
            {
                return PdfFontFactory.CreateFont(systemFontPath);
            }
        }
        catch
        {
            // Fall back to standard fonts below.
        }

        return PdfFontFactory.CreateFont(fallbackFont);
    }
}
