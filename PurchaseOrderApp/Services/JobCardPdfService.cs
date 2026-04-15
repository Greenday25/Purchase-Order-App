using PurchaseOrderApp.Models;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using System.IO;
using System.Text.RegularExpressions;

namespace PurchaseOrderApp.Services;

internal sealed class JobCardPdfService
{
    private static readonly DeviceRgb HeaderBackground = new(226, 243, 247);
    private static readonly DeviceRgb BorderColor = new(216, 229, 236);
    private static readonly DeviceRgb SecondaryTextColor = new(94, 117, 133);
    private static readonly Regex TrailingNumericIdentifierRegex = new(@"\s*\(\d+\)\s*$", RegexOptions.Compiled);
    private const float PhotoFrameHeight = 220f;
    private const float PhotoCellMinHeight = 268f;

    internal string ExportJobCardPdf(JobCardRecord record, JobCardEvidenceService evidenceService)
    {
        var outputPath = evidenceService.GetPdfPath(record.JobCardNumber);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var writer = new PdfWriter(outputPath);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);
        document.SetMargins(28, 28, 28, 28);

        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        AddHeader(document, font, boldFont, record);
        AddSection(document, "Wialon Details", font, boldFont, new[]
        {
            ("Unit", BuildUnitDisplay(record)),
            ("Account", BuildEntityDisplay(record.WialonAccountName, record.WialonAccountId)),
            ("Creator", BuildEntityDisplay(record.WialonCreatorName, record.WialonCreatorId)),
            ("Hardware Type", BuildEntityDisplay(record.WialonHardwareTypeName, record.WialonHardwareTypeId)),
            ("Unit Name", record.JobCardName),
            ("Unique ID", record.UniqueId),
            ("ICCID", record.Iccid),
            ("Phone", Normalize(record.PhoneNumber))
        });

        AddSection(document, "Vehicle", font, boldFont, new[]
        {
            ("Client", record.Client),
            ("Brand", record.Brand),
            ("Model", record.Model),
            ("Year", record.Year),
            ("Colour", record.Colour),
            ("Vehicle Class", record.VehicleClass),
            ("Vehicle Type", record.VehicleType),
            ("Registration Plate", record.RegistrationPlate),
            ("VIN", record.Vin),
            ("Make & Model", record.MakeAndModel),
            ("Registration & Fleet", record.RegistrationFleet)
        });

        AddSection(document, "Contacts", font, boldFont, new[]
        {
            ("Contact 1", record.Contact1),
            ("Contact 2", record.Contact2)
        });

        AddSection(document, "Billing (Ex VAT)", font, boldFont, new[]
        {
            ("System Installed", JobCardBillingHelper.ResolveSystemType(record)),
            ("System Price", JobCardBillingHelper.FormatAmount(record.SystemPriceExVat)),
            ("Panic Button", JobCardBillingHelper.BuildOptionalLineDisplay(record.HasPanicButton, record.PanicButtonPriceExVat)),
            ("Early Warning System", JobCardBillingHelper.BuildOptionalLineDisplay(record.HasEarlyWarningSystem, record.EarlyWarningSystemPriceExVat)),
            ("BLE Sensors", JobCardBillingHelper.BuildBleSensorDisplay(record.BleSensorQuantity, record.BleSensorUnitPriceExVat)),
            ("LV CAN Adaptor", JobCardBillingHelper.BuildOptionalLineDisplay(record.HasLvCanAdaptor, record.LvCanAdaptorPriceExVat)),
            ("Other Hardware", JobCardBillingHelper.BuildOtherHardwareDisplay(record.OtherHardwareDescription, record.OtherHardwarePriceExVat)),
            ("Billing Notes", Normalize(record.BillingNotes)),
            ("Total Ex VAT", JobCardBillingHelper.FormatAmount(JobCardBillingHelper.CalculateTotalExVat(record)))
        });

        AddPhotoSection(document, font, boldFont, record, evidenceService);

        return outputPath;
    }

    private static void AddHeader(Document document, PdfFont font, PdfFont boldFont, JobCardRecord record)
    {
        var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 2.8f }))
            .UseAllAvailableWidth()
            .SetBorder(Border.NO_BORDER)
            .SetMarginBottom(16);

        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        var logoCell = new Cell()
            .SetBorder(new SolidBorder(BorderColor, 1))
            .SetBackgroundColor(ColorConstants.WHITE)
            .SetPadding(10);

        if (File.Exists(logoPath))
        {
            var logo = new Image(ImageDataFactory.Create(logoPath));
            logo.ScaleToFit(115, 70);
            logoCell.Add(logo);
        }
        else
        {
            logoCell.Add(new Paragraph("Capital Air")
                .SetFont(boldFont)
                .SetFontSize(18));
        }

        var infoCell = new Cell()
            .SetBorder(new SolidBorder(BorderColor, 1))
            .SetBackgroundColor(HeaderBackground)
            .SetPadding(12);

        infoCell.Add(new Paragraph("JOB CARD")
            .SetFont(boldFont)
            .SetFontSize(22)
            .SetMarginBottom(4));
        infoCell.Add(new Paragraph(record.JobCardNumber)
            .SetFont(boldFont)
            .SetFontSize(15)
            .SetMarginTop(0)
            .SetMarginBottom(6));
        infoCell.Add(new Paragraph($"Printable installation record for {Normalize(record.Client)}")
            .SetFont(font)
            .SetFontSize(10)
            .SetFontColor(SecondaryTextColor)
            .SetMargin(0));

        headerTable.AddCell(logoCell);
        headerTable.AddCell(infoCell);
        document.Add(headerTable);
    }

    private static void AddSection(Document document, string title, PdfFont font, PdfFont boldFont, IReadOnlyList<(string Label, string Value)> items)
    {
        document.Add(new Paragraph(title)
            .SetFont(boldFont)
            .SetFontSize(13)
            .SetMarginBottom(8));

        var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.1f, 1.9f, 1.1f, 1.9f }))
            .UseAllAvailableWidth()
            .SetMarginBottom(16);

        var pairs = items.ToList();
        if (pairs.Count % 2 != 0)
        {
            pairs.Add((string.Empty, string.Empty));
        }

        for (var index = 0; index < pairs.Count; index += 2)
        {
            AddFieldRow(table, font, boldFont, pairs[index].Label, pairs[index].Value);
            AddFieldRow(table, font, boldFont, pairs[index + 1].Label, pairs[index + 1].Value);
        }

        document.Add(table);
    }

    private static void AddFieldRow(Table table, PdfFont font, PdfFont boldFont, string label, string value)
    {
        table.AddCell(new Cell()
            .SetBorder(new SolidBorder(BorderColor, 1))
            .SetBackgroundColor(HeaderBackground)
            .SetPadding(6)
            .Add(new Paragraph(label)
                .SetFont(boldFont)
                .SetFontSize(9)
                .SetMargin(0)));

        table.AddCell(new Cell()
            .SetBorder(new SolidBorder(BorderColor, 1))
            .SetPadding(6)
            .Add(new Paragraph(Normalize(value))
                .SetFont(font)
                .SetFontSize(9)
                .SetMargin(0)));
    }

    private static void AddPhotoSection(Document document, PdfFont font, PdfFont boldFont, JobCardRecord record, JobCardEvidenceService evidenceService)
    {
        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));

        document.Add(new Paragraph("Installation Photos")
            .SetFont(boldFont)
            .SetFontSize(13)
            .SetMarginBottom(8));

        document.Add(new Paragraph("Scaled for A4 printing and phone photos captured during the installation.")
            .SetFont(font)
            .SetFontSize(9)
            .SetFontColor(SecondaryTextColor)
            .SetMarginTop(0)
            .SetMarginBottom(12));

        var photoTable = new Table(UnitValue.CreatePercentArray(new float[] { 1f, 1f }))
            .UseAllAvailableWidth()
            .SetKeepTogether(true)
            .SetMarginBottom(8);

        AddPhotoCell(photoTable, font, boldFont, record, evidenceService, JobCardEvidencePhotoType.Vehicle);
        AddPhotoCell(photoTable, font, boldFont, record, evidenceService, JobCardEvidencePhotoType.Registration);
        AddPhotoCell(photoTable, font, boldFont, record, evidenceService, JobCardEvidencePhotoType.Vin);
        AddPhotoCell(photoTable, font, boldFont, record, evidenceService, JobCardEvidencePhotoType.TrackingUnit);

        document.Add(photoTable);
    }

    private static void AddPhotoCell(
        Table photoTable,
        PdfFont font,
        PdfFont boldFont,
        JobCardRecord record,
        JobCardEvidenceService evidenceService,
        JobCardEvidencePhotoType photoType)
    {
        var cell = new Cell()
            .SetBorder(new SolidBorder(BorderColor, 1))
            .SetPadding(8)
            .SetMinHeight(PhotoCellMinHeight);

        cell.Add(new Paragraph(photoType.GetDisplayName())
            .SetFont(boldFont)
            .SetFontSize(10)
            .SetMarginTop(0)
            .SetMarginBottom(8));

        var frameTable = new Table(1)
            .UseAllAvailableWidth()
            .SetBorder(Border.NO_BORDER);

        var frameCell = new Cell()
            .SetBorder(new SolidBorder(BorderColor, 1))
            .SetBackgroundColor(ColorConstants.WHITE)
            .SetPadding(8)
            .SetMinHeight(PhotoFrameHeight)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE);

        var photoPath = evidenceService.GetExistingEvidencePath(record.JobCardNumber, photoType);
        if (!string.IsNullOrWhiteSpace(photoPath) && File.Exists(photoPath))
        {
            var image = new Image(ImageDataFactory.Create(photoPath));
            image.SetAutoScale(false);
            image.ScaleToFit(240, PhotoFrameHeight - 16);
            image.SetHorizontalAlignment(HorizontalAlignment.CENTER);
            frameCell.Add(image);
        }
        else
        {
            frameCell.Add(new Paragraph("Photo not uploaded yet.")
                .SetFont(font)
                .SetFontSize(9)
                .SetFontColor(SecondaryTextColor)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(8));
        }

        frameTable.AddCell(frameCell);
        cell.Add(frameTable);
        photoTable.AddCell(cell);
    }

    private static string BuildUnitDisplay(JobCardRecord record)
    {
        return NormalizePdfDisplay(record.WialonUnitName);
    }

    private static string BuildEntityDisplay(string? name, long? id)
    {
        if (string.IsNullOrWhiteSpace(name) && !id.HasValue)
        {
            return "Pending";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return id!.Value.ToString();
        }

        return NormalizePdfDisplay(name);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Pending" : value.Trim();
    }

    private static string NormalizePdfDisplay(string? value)
    {
        var normalized = Normalize(value);
        if (string.Equals(normalized, "Pending", StringComparison.Ordinal))
        {
            return normalized;
        }

        var cleaned = normalized;
        while (TrailingNumericIdentifierRegex.IsMatch(cleaned))
        {
            cleaned = TrailingNumericIdentifierRegex.Replace(cleaned, string.Empty).Trim();
        }

        return string.IsNullOrWhiteSpace(cleaned) ? normalized : cleaned;
    }
}
