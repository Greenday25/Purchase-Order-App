using PurchaseOrderApp.ViewModels;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iTextParagraph = iText.Layout.Element.Paragraph;
using iTextTable = iText.Layout.Element.Table;
using iTextCell = iText.Layout.Element.Cell;
using iTextTextAlignment = iText.Layout.Properties.TextAlignment;
using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly NumberFormatInfo OrderAmountNumberFormat = CreateOrderAmountNumberFormat();
    private const string CompanyName = "CAPITAL AIR (Pty) Ltd";
    private const string CompanyRegistrationLine = "Company Reg. No. 1979/006598/07 - VAT Reg. No. 4120110046";
    private const string AuthorisedSignatureLabel = "AUTHORISED SIGNATURE";
    private const string QuoteOrderFooterText = "PLEASE QUOTE OUR ORDER NUMBER ON ALL CORRESPONDENCE";
    private const float OfficialOrderTitleFontSize = 9f;
    private const double PostInvoiceBoxWidth = 212;
    private const double PostInvoiceHeaderColumnWidth = 212;
    private const double OrderInfoBoxWidth = 184;
    private const double OrderInfoBoxMinWidth = OrderInfoBoxWidth;
    private const double OrderInfoBoxMaxWidth = OrderInfoBoxWidth;
    private const double DocumentBoxGapWidth = 8;
    private const double RecipientOrderGapWidth = 26;
    private const double PrintBorderThickness = 0.7;
    private const double PrintLogoTopOffset = -4;
    private const double PrintBoxPaddingHorizontal = 4;
    private const double PrintBoxPaddingVertical = 3;
    private const double PrintPostInvoicePaddingVertical = 2;
    private const double PrintPostInvoiceLineFontSize = 6;
    private const double PrintPostInvoiceEmphasisFontSize = 6.5;
    private const double PrintPostInvoiceLineHeight = 6;
    private const double PrintPostInvoiceLabelWidth = 42;
    private const double PrintBoxRowMinHeight = 22;
    private const double PrintItemCellPadding = 2;
    private const double FooterSectionTopSpacing = 12;
    private const double HeaderToBoxesSpacing = 16;
    private const float PdfBorderThickness = 0.7f;
    private const float PdfLogoTopOffset = -4f;
    private const float PdfBoxPadding = 4f;
    private const float PdfPostInvoicePaddingVertical = 3f;
    private const float PdfPostInvoiceLineFontSize = 6f;
    private const float PdfPostInvoiceEmphasisFontSize = 6.5f;
    private const float PdfPostInvoiceLineLeading = 6f;
    private const float PdfBoxRowMinHeight = 21f;
    private const float PdfItemHeaderPadding = 3f;
    private const float PdfItemRowPadding = 1.5f;
    private const float PdfItemHeaderMinHeight = 22f;
    private const float PdfItemRowMinHeight = 17f;
    private const double PrintDocumentContentWidth = 604;
    private const double PrintRecipientBoxWidth = PrintDocumentContentWidth - OrderInfoBoxWidth - RecipientOrderGapWidth;
    private sealed record PostInvoiceLineDefinition(string? Label, string Value, bool IsEmphasized = false);

    private static readonly PostInvoiceLineDefinition[] PostInvoiceLines =
    [
        new(null, "CAPITAL AIR (Pty) Ltd", true),
        new(null, "P.O. BOX 18009"),
        new(null, "RAND AIRPORT 1419"),
        new(null, "GERMISTON, SOUTH AFRICA"),
        new("TEL:", "+27 11 827 0335 / 82 2634/2840"),
        new("24 HOURS:", "+27 83 407 0222", true),
        new("FAX:", "+27 11 827 3888 / 827 2295"),
        new("VOIP:", "+27 10 590 4591 / 593 4432")
    ];

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnReferenceChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RefreshOrderNumber();
        }
    }

    private static NumberFormatInfo CreateOrderAmountNumberFormat()
    {
        var numberFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        numberFormat.NumberDecimalSeparator = ".";
        numberFormat.NumberGroupSeparator = " ";
        numberFormat.NumberGroupSizes = [3];
        return numberFormat;
    }

    private static double GetPrintPostInvoiceFontSize(PostInvoiceLineDefinition line)
    {
        return line.IsEmphasized ? PrintPostInvoiceEmphasisFontSize : PrintPostInvoiceLineFontSize;
    }

    private static float GetPdfPostInvoiceFontSize(PostInvoiceLineDefinition line)
    {
        return line.IsEmphasized ? PdfPostInvoiceEmphasisFontSize : PdfPostInvoiceLineFontSize;
    }

    private static FrameworkElement CreatePrintPostInvoiceElement(PostInvoiceLineDefinition line)
    {
        var fontSize = GetPrintPostInvoiceFontSize(line);
        var fontWeight = line.IsEmphasized ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal;

        if (string.IsNullOrWhiteSpace(line.Label))
        {
            return new TextBlock
            {
                Text = line.Value,
                FontSize = fontSize,
                FontWeight = fontWeight,
                LineHeight = fontSize,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0)
            };
        }

        var rowGrid = new Grid();
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PrintPostInvoiceLabelWidth) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = line.Label,
            FontSize = fontSize,
            FontWeight = fontWeight,
            LineHeight = fontSize,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Margin = new Thickness(0)
        };

        var valueBlock = new TextBlock
        {
            Text = line.Value,
            FontSize = fontSize,
            FontWeight = fontWeight,
            LineHeight = fontSize,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0)
        };

        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(valueBlock, 1);
        rowGrid.Children.Add(labelBlock);
        rowGrid.Children.Add(valueBlock);
        return rowGrid;
    }

    private static void AddPdfPostInvoiceLine(iTextCell container, PostInvoiceLineDefinition line, PdfFont font, PdfFont boldFont, float labelWidth, float valueWidth)
    {
        var lineFont = line.IsEmphasized ? boldFont : font;
        var fontSize = GetPdfPostInvoiceFontSize(line);

        if (string.IsNullOrWhiteSpace(line.Label))
        {
            container.Add(new iTextParagraph(line.Value)
                .SetFont(lineFont)
                .SetFontSize(fontSize)
                .SetFixedLeading(fontSize)
                .SetMarginTop(0)
                .SetMarginBottom(0));
            return;
        }

        var rowTable = new iTextTable(iText.Layout.Properties.UnitValue.CreatePointArray(new float[] { labelWidth, valueWidth }))
            .SetWidth(labelWidth + valueWidth);
        rowTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

        rowTable.AddCell(new iTextCell()
            .Add(new iTextParagraph(line.Label)
                .SetFont(lineFont)
                .SetFontSize(fontSize)
                .SetFixedLeading(fontSize)
                .SetMarginTop(0)
                .SetMarginBottom(0))
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetPadding(0));

        rowTable.AddCell(new iTextCell()
            .Add(new iTextParagraph(line.Value)
                .SetFont(lineFont)
                .SetFontSize(fontSize)
                .SetFixedLeading(fontSize)
                .SetMarginTop(0)
                .SetMarginBottom(0))
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetPadding(0));

        container.Add(rowTable);
    }

    private static PdfFont CreatePdfDocumentFont(string fileName, string fallbackFont)
    {
        try
        {
            var fontPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fileName);
            if (File.Exists(fontPath))
            {
                return PdfFontFactory.CreateFont(fontPath, PdfEncodings.WINANSI);
            }
        }
        catch
        {
        }

        return PdfFontFactory.CreateFont(fallbackFont);
    }

    private static string GetLogoPath()
    {
        return System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "logo.png");
    }

    private static byte[]? TryReadLogoBytes()
    {
        var logoPath = GetLogoPath();
        if (!File.Exists(logoPath))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(logoPath);
        }
        catch
        {
            return null;
        }
    }

    private static Image? CreatePrintLogoImage(double width, double height)
    {
        var logoBytes = TryReadLogoBytes();
        if (logoBytes == null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(logoBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Top
            };
        }
        catch
        {
            return null;
        }
    }

    private static iText.Layout.Element.Image? CreatePdfLogoImage(float maxWidth, float maxHeight)
    {
        var logoBytes = TryReadLogoBytes();
        if (logoBytes == null)
        {
            return null;
        }

        try
        {
            return new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(logoBytes))
                .ScaleToFit(maxWidth, maxHeight);
        }
        catch
        {
            return null;
        }
    }

    private static Block CreatePrintHeaderBlock()
    {
        var headerSection = new Section
        {
            Margin = new Thickness(0, 0, 0, HeaderToBoxesSpacing)
        };

        headerSection.Blocks.Add(new Paragraph(new Bold(new Run("OFFICIAL ORDER")))
        {
            FontSize = OfficialOrderTitleFontSize,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var headerTable = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 4)
        };
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(DocumentBoxGapWidth) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(PostInvoiceHeaderColumnWidth) });

        var headerGroup = new TableRowGroup();
        headerTable.RowGroups.Add(headerGroup);
        var headerRow = new TableRow();
        headerGroup.Rows.Add(headerRow);

        var logoImage = CreatePrintLogoImage(150, 76);
        var brandingCell = new TableCell
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            TextAlignment = TextAlignment.Left
        };
        if (logoImage != null)
        {
            brandingCell.Blocks.Add(new Paragraph(new InlineUIContainer(logoImage))
            {
                Margin = new Thickness(0, PrintLogoTopOffset, 0, 2)
            });
        }
        brandingCell.Blocks.Add(new Paragraph(new Bold(new Run(CompanyName)))
        {
            Margin = new Thickness(3, 0, 0, 0)
        });
        brandingCell.Blocks.Add(new Paragraph(new Run(CompanyRegistrationLine))
        {
            FontSize = 9,
            Margin = new Thickness(3, 0, 0, 0)
        });
        headerRow.Cells.Add(brandingCell);

        headerRow.Cells.Add(new TableCell(new Paragraph(new Run(string.Empty)))
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        });

        var postInvoicePanel = new StackPanel
        {
            Width = PostInvoiceBoxWidth
        };
        postInvoicePanel.Children.Add(new TextBlock
        {
            Text = "POST INVOICE TO:",
            FontWeight = System.Windows.FontWeights.Bold,
            LineHeight = PrintPostInvoiceLineHeight,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Margin = new Thickness(0)
        });
        foreach (var line in PostInvoiceLines)
        {
            postInvoicePanel.Children.Add(CreatePrintPostInvoiceElement(line));
        }

        var postInvoiceBorder = new System.Windows.Controls.Border
        {
            Width = PostInvoiceBoxWidth,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(PrintBorderThickness),
            Padding = new Thickness(PrintBoxPaddingHorizontal, PrintPostInvoicePaddingVertical, PrintBoxPaddingHorizontal, PrintPostInvoicePaddingVertical),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = postInvoicePanel
        };

        var postInvoiceCell = new TableCell
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };
        postInvoiceCell.Blocks.Add(new BlockUIContainer(postInvoiceBorder));
        headerRow.Cells.Add(postInvoiceCell);

        headerSection.Blocks.Add(headerTable);

        return headerSection;
    }

    private static iTextCell CreatePdfStackedCell(string text, PdfFont font, float fontSize, iTextTextAlignment alignment, bool includeTopBorder, float minHeight = PdfBoxRowMinHeight)
    {
        return new iTextCell()
            .Add(new iTextParagraph(text).SetFont(font).SetFontSize(fontSize).SetTextAlignment(alignment))
            .SetPadding(PdfBoxPadding)
            .SetMinHeight(minHeight)
            .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE)
            .SetBorderLeft(new SolidBorder(PdfBorderThickness))
            .SetBorderRight(new SolidBorder(PdfBorderThickness))
            .SetBorderBottom(new SolidBorder(PdfBorderThickness))
            .SetBorderTop(includeTopBorder ? new SolidBorder(PdfBorderThickness) : iText.Layout.Borders.Border.NO_BORDER);
    }

    private static double MeasurePrintRowWidth(string text, double fontSize, FontWeight fontWeight)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            TextWrapping = TextWrapping.NoWrap
        };
        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return textBlock.DesiredSize.Width;
    }

    private static double GetPrintOrderInfoBoxWidth(MainViewModel vm)
    {
        var widestContent = Math.Max(
            MeasurePrintRowWidth($"ORDER: {vm.CurrentOrder.OrderNumber}", 10, System.Windows.FontWeights.Bold),
            Math.Max(
                MeasurePrintRowWidth($"DATE: {vm.CurrentOrder.Date:dd/MM/yyyy}", 10, System.Windows.FontWeights.Normal),
                MeasurePrintRowWidth($"REF: {vm.CurrentOrder.Reference}", 10, System.Windows.FontWeights.Normal)));

        var boxWidth = widestContent + (PrintBoxPaddingHorizontal * 2) + 10;
        return Math.Clamp(boxWidth, OrderInfoBoxMinWidth, OrderInfoBoxMaxWidth);
    }

    private static float GetPdfOrderInfoBoxWidth(MainViewModel vm, PdfFont font, PdfFont boldFont)
    {
        var widestContent = Math.Max(
            boldFont.GetWidth($"ORDER: {vm.CurrentOrder.OrderNumber}", 10),
            Math.Max(
                font.GetWidth($"DATE: {vm.CurrentOrder.Date:dd/MM/yyyy}", 9),
                font.GetWidth($"REF: {vm.CurrentOrder.Reference}", 9)));

        var boxWidth = widestContent + (PdfBoxPadding * 2) + 8f;
        return Math.Clamp(boxWidth, (float)OrderInfoBoxMinWidth, (float)OrderInfoBoxMaxWidth);
    }

    private static float GetPdfScaledLayoutWidth(double printWidth, float pdfContentWidth)
    {
        if (pdfContentWidth <= 0)
        {
            return (float)printWidth;
        }

        return (float)(printWidth * (pdfContentWidth / PrintDocumentContentWidth));
    }

    private static System.Windows.Controls.Border CreatePrintRow(string text, TextAlignment alignment, bool includeBottomBorder, FontWeight? fontWeight = null)
    {
        return new System.Windows.Controls.Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 0, 0, includeBottomBorder ? PrintBorderThickness : 0),
            Padding = new Thickness(PrintBoxPaddingHorizontal, PrintBoxPaddingVertical, PrintBoxPaddingHorizontal, PrintBoxPaddingVertical),
            MinHeight = PrintBoxRowMinHeight,
            Child = new TextBlock
            {
                Text = text,
                TextAlignment = alignment,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10,
                FontWeight = fontWeight ?? System.Windows.FontWeights.Normal
            }
        };
    }

    private static Block CreatePrintRecipientAndOrderBlock(MainViewModel vm)
    {
        var layoutGrid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 4),
            Width = PrintDocumentContentWidth
        };
        layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PrintRecipientBoxWidth) });
        layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RecipientOrderGapWidth) });
        layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(OrderInfoBoxWidth) });

        var recipientGrid = new Grid();
        recipientGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        recipientGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        recipientGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var recipientRows = new[]
        {
            CreatePrintRow("TO:", TextAlignment.Left, true),
            CreatePrintRow(vm.CurrentOrder.BillTo ?? string.Empty, TextAlignment.Center, true),
            CreatePrintRow(vm.CurrentOrder.BillToAddress ?? string.Empty, TextAlignment.Center, false)
        };

        for (int i = 0; i < recipientRows.Length; i++)
        {
            Grid.SetRow(recipientRows[i], i);
            recipientGrid.Children.Add(recipientRows[i]);
        }

        var recipientBorder = new System.Windows.Controls.Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(PrintBorderThickness),
            Width = PrintRecipientBoxWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = recipientGrid
        };
        Grid.SetColumn(recipientBorder, 0);
        layoutGrid.Children.Add(recipientBorder);

        var orderGrid = new Grid();
        orderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        orderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        orderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var orderRows = new[]
        {
            CreatePrintRow($"ORDER: {vm.CurrentOrder.OrderNumber}", TextAlignment.Left, true, System.Windows.FontWeights.Bold),
            CreatePrintRow($"DATE: {vm.CurrentOrder.Date:dd/MM/yyyy}", TextAlignment.Left, true),
            CreatePrintRow($"REF: {vm.CurrentOrder.Reference}", TextAlignment.Left, false)
        };

        for (int i = 0; i < orderRows.Length; i++)
        {
            Grid.SetRow(orderRows[i], i);
            orderGrid.Children.Add(orderRows[i]);
        }

        var orderBorder = new System.Windows.Controls.Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(PrintBorderThickness),
            Width = GetPrintOrderInfoBoxWidth(vm),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = orderGrid
        };
        Grid.SetColumn(orderBorder, 2);
        layoutGrid.Children.Add(orderBorder);

        return new BlockUIContainer(layoutGrid);
    }

    private static string FormatOrderAmount(decimal amount)
    {
        return amount.ToString("N2", OrderAmountNumberFormat);
    }

    private static Block CreatePrintFooterBlock()
    {
        var footerSection = new Section
        {
            Margin = new Thickness(0, FooterSectionTopSpacing, 0, 0)
        };

        var signatureGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var signatureLabel = new TextBlock
        {
            Text = AuthorisedSignatureLabel,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(signatureLabel, 0);
        signatureGrid.Children.Add(signatureLabel);

        var signatureLine = new System.Windows.Controls.Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 0, 0, PrintBorderThickness),
            Height = 1,
            Margin = new Thickness(6, 0, 46, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(signatureLine, 1);
        signatureGrid.Children.Add(signatureLine);

        footerSection.Blocks.Add(new BlockUIContainer(signatureGrid));
        footerSection.Blocks.Add(new Paragraph(new Run(QuoteOrderFooterText))
        {
            FontSize = 10,
            Margin = new Thickness(0)
        });

        return footerSection;
    }

    private static void AddPdfFooterBlock(Document document, PdfFont font)
    {
        var signatureTable = new iTextTable(new float[] { 170f, 1f }).UseAllAvailableWidth().SetMarginTop((float)FooterSectionTopSpacing).SetMarginBottom(4);
        signatureTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);
        signatureTable.AddCell(new iTextCell()
            .Add(new iTextParagraph(AuthorisedSignatureLabel).SetFont(font).SetFontSize(11))
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetPadding(0)
            .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.BOTTOM));
        signatureTable.AddCell(new iTextCell()
            .Add(new iTextParagraph(" "))
            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
            .SetBorderBottom(new SolidBorder(PdfBorderThickness))
            .SetPadding(0)
            .SetPaddingLeft(6)
            .SetPaddingRight(46)
            .SetMinHeight(12));
        document.Add(signatureTable);

        document.Add(new iTextParagraph(QuoteOrderFooterText)
            .SetFont(font)
            .SetFontSize(10)
            .SetMarginTop(0)
            .SetMarginBottom(0));
    }

    private static (decimal VatAmount, decimal TotalAmount) GetOrderDocumentTotals(MainViewModel vm)
    {
        var subTotal = Math.Round(vm.Lines.Sum(line => line.UnitPrice), 2);
        var vatAmount = Math.Round(subTotal * (vm.CurrentOrder?.VATPercent ?? 0m) / 100m, 2);
        return (vatAmount, Math.Round(subTotal + vatAmount, 2));
    }

    private void OnPrintInvoice(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var doc = BuildInvoiceDocument(vm);

        var pd = new PrintDialog();
        if (pd.ShowDialog() == true)
        {
            doc.PagePadding = new Thickness(18);
            doc.ColumnGap = 0;
            doc.ColumnWidth = pd.PrintableAreaWidth;
            pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Purchase Order");
        }
    }

    private void OnExportPdf(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainViewModel vm) return;

            var dialog = new SaveFileDialog
            {
                FileName = $"PurchaseOrder_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                DefaultExt = ".pdf",
                Filter = "PDF documents (.pdf)|*.pdf"
            };

            if (dialog.ShowDialog() != true) return;

            var filePath = dialog.FileName;

            using var pdfWriter = new PdfWriter(filePath);
            using var pdfDocument = new PdfDocument(pdfWriter);
            using var document = new Document(pdfDocument);

            var font = CreatePdfDocumentFont("arial.ttf", StandardFonts.HELVETICA);
            var boldFont = CreatePdfDocumentFont("arialbd.ttf", StandardFonts.HELVETICA_BOLD);
            var border = new SolidBorder(PdfBorderThickness);

            document.SetFont(font);
            document.SetFontSize(10);

            document.Add(new iTextParagraph("OFFICIAL ORDER")
                .SetFont(boldFont)
                .SetFontSize(OfficialOrderTitleFontSize)
                .SetTextAlignment(iTextTextAlignment.CENTER)
                .SetMarginTop(6)
                .SetMarginBottom(16));

            var pdfContentWidth = pdfDocument.GetDefaultPageSize().GetWidth() - document.GetLeftMargin() - document.GetRightMargin();
            var pdfGapWidth = GetPdfScaledLayoutWidth(DocumentBoxGapWidth, pdfContentWidth);
            var pdfRecipientOrderGapWidth = GetPdfScaledLayoutWidth(RecipientOrderGapWidth, pdfContentWidth);
            var pdfPostInvoiceBoxWidth = GetPdfScaledLayoutWidth(PostInvoiceBoxWidth, pdfContentWidth);
            var pdfPostInvoiceHeaderColumnWidth = GetPdfScaledLayoutWidth(PostInvoiceHeaderColumnWidth, pdfContentWidth);
            var pdfOrderInfoBoxWidth = GetPdfScaledLayoutWidth(OrderInfoBoxWidth, pdfContentWidth);
            var pdfRecipientBoxWidth = GetPdfScaledLayoutWidth(PrintRecipientBoxWidth, pdfContentWidth);
            var pdfBrandingWidth = Math.Max(0f, pdfContentWidth - pdfGapWidth - pdfPostInvoiceHeaderColumnWidth);

            var headerContentTable = new iTextTable(iText.Layout.Properties.UnitValue.CreatePointArray(
                new float[] { pdfBrandingWidth, pdfGapWidth, pdfPostInvoiceHeaderColumnWidth }))
                .SetWidth(pdfContentWidth)
                .SetMarginBottom((float)HeaderToBoxesSpacing);
            headerContentTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            var brandingCell = new iTextCell()
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                .SetPadding(0);
            var pdfLogo = CreatePdfLogoImage(150, 76);
            if (pdfLogo != null)
            {
                pdfLogo.SetMarginTop(PdfLogoTopOffset);
                brandingCell.Add(pdfLogo);
            }
            brandingCell
                .Add(new iTextParagraph(CompanyName).SetFont(boldFont).SetFontSize(10).SetMarginTop(8).SetMarginBottom(0).SetMarginLeft(4))
                .Add(new iTextParagraph(CompanyRegistrationLine).SetFont(font).SetFontSize(7.5f).SetMarginTop(0).SetMarginLeft(4));

            var postInvoiceBox = new iTextTable(1)
                .SetWidth(pdfPostInvoiceBoxWidth)
                .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.LEFT);
            postInvoiceBox.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            var pdfPostInvoiceLabelWidth = Math.Min(GetPdfScaledLayoutWidth(PrintPostInvoiceLabelWidth, pdfContentWidth), pdfPostInvoiceBoxWidth - (PdfBoxPadding * 2));
            var pdfPostInvoiceValueWidth = Math.Max(0f, pdfPostInvoiceBoxWidth - (PdfBoxPadding * 2) - pdfPostInvoiceLabelWidth);

            var postInvoiceBoxCell = new iTextCell()
                .Add(new iTextParagraph("POST INVOICE TO:")
                    .SetFont(boldFont)
                    .SetFontSize(10)
                    .SetFixedLeading(10f)
                    .SetMarginTop(0)
                    .SetMarginBottom(0))
                .SetPaddingLeft(PdfBoxPadding)
                .SetPaddingRight(PdfBoxPadding)
                .SetPaddingTop(PdfPostInvoicePaddingVertical)
                .SetPaddingBottom(PdfPostInvoicePaddingVertical)
                .SetBorder(border)
                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.TOP);
            foreach (var line in PostInvoiceLines)
            {
                AddPdfPostInvoiceLine(postInvoiceBoxCell, line, font, boldFont, pdfPostInvoiceLabelWidth, pdfPostInvoiceValueWidth);
            }
            postInvoiceBox.AddCell(postInvoiceBoxCell);

            var postInvoiceCell = new iTextCell()
                .Add(postInvoiceBox)
                .SetPadding(0)
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            headerContentTable.AddCell(brandingCell);
            headerContentTable.AddCell(new iTextCell().SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            headerContentTable.AddCell(postInvoiceCell);
            document.Add(headerContentTable);

            // Bill To and Order Info section
            var infoLayoutTable = new iTextTable(iText.Layout.Properties.UnitValue.CreatePointArray(
                new float[] { pdfRecipientBoxWidth, pdfRecipientOrderGapWidth, pdfOrderInfoBoxWidth }))
                .SetWidth(pdfContentWidth)
                .SetMarginBottom(8);
            infoLayoutTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);

            var recipientTable = new iTextTable(1).UseAllAvailableWidth();
            recipientTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            recipientTable.AddCell(CreatePdfStackedCell("TO:", boldFont, 10, iTextTextAlignment.LEFT, true));
            recipientTable.AddCell(CreatePdfStackedCell(vm.CurrentOrder.BillTo ?? string.Empty, font, 10, iTextTextAlignment.CENTER, false));
            recipientTable.AddCell(CreatePdfStackedCell(vm.CurrentOrder.BillToAddress ?? string.Empty, font, 9, iTextTextAlignment.CENTER, false));

            var orderInfoTable = new iTextTable(1);
            orderInfoTable.SetBorder(iText.Layout.Borders.Border.NO_BORDER);
            orderInfoTable.SetWidth(GetPdfOrderInfoBoxWidth(vm, font, boldFont));
            orderInfoTable.SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.RIGHT);
            orderInfoTable.AddCell(CreatePdfStackedCell($"ORDER: {vm.CurrentOrder.OrderNumber}", boldFont, 10, iTextTextAlignment.LEFT, true));
            orderInfoTable.AddCell(CreatePdfStackedCell($"DATE: {vm.CurrentOrder.Date:dd/MM/yyyy}", font, 9, iTextTextAlignment.LEFT, false));
            orderInfoTable.AddCell(CreatePdfStackedCell($"REF: {vm.CurrentOrder.Reference}", font, 9, iTextTextAlignment.LEFT, false));

            infoLayoutTable.AddCell(new iTextCell().Add(recipientTable).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(0));
            infoLayoutTable.AddCell(new iTextCell().SetBorder(iText.Layout.Borders.Border.NO_BORDER));
            infoLayoutTable.AddCell(new iTextCell().Add(orderInfoTable).SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(0));
            document.Add(infoLayoutTable);

            // Items table
            var (documentVatAmount, documentTotalAmount) = GetOrderDocumentTotals(vm);
            var itemsTable = new iTextTable(new float[] { 68, 94, 316, 68, 92 }).UseAllAvailableWidth().SetMarginTop(12).SetMarginBottom(6);
            var headerBorder = new SolidBorder(PdfBorderThickness);

            var qtyHeaderCell = new iTextCell()
                .Add(new iTextParagraph("QUANTITY").SetFont(boldFont).SetFontSize(9))
                .SetBorder(headerBorder)
                .SetPadding(PdfItemHeaderPadding)
                .SetMinHeight(PdfItemHeaderMinHeight)
                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE)
                .SetTextAlignment(iTextTextAlignment.CENTER);

            var partHeaderCell = new iTextCell()
                .Add(new iTextParagraph("PART No.").SetFont(boldFont).SetFontSize(9))
                .SetBorder(headerBorder)
                .SetPadding(PdfItemHeaderPadding)
                .SetMinHeight(PdfItemHeaderMinHeight)
                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE)
                .SetTextAlignment(iTextTextAlignment.CENTER);

            var descHeaderCell = new iTextCell()
                .Add(new iTextParagraph("DESCRIPTION OF MATERIAL AND/OR\nSERVICES RENDERED").SetFont(boldFont).SetFontSize(9))
                .SetBorder(headerBorder)
                .SetPadding(PdfItemHeaderPadding)
                .SetMinHeight(PdfItemHeaderMinHeight)
                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE)
                .SetTextAlignment(iTextTextAlignment.CENTER);

            var priceHeaderCell = new iTextCell()
                .Add(new iTextParagraph("PRICE PER\nUNIT").SetFont(boldFont).SetFontSize(9))
                .SetBorder(headerBorder)
                .SetPadding(PdfItemHeaderPadding)
                .SetMinHeight(PdfItemHeaderMinHeight)
                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE)
                .SetTextAlignment(iTextTextAlignment.CENTER);

            var totalHeaderCell = new iTextCell()
                .Add(new iTextParagraph("PRICE").SetFont(boldFont).SetFontSize(9))
                .SetBorder(headerBorder)
                .SetPadding(PdfItemHeaderPadding)
                .SetMinHeight(PdfItemHeaderMinHeight)
                .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE)
                .SetTextAlignment(iTextTextAlignment.CENTER);

            itemsTable.AddCell(qtyHeaderCell);
            itemsTable.AddCell(partHeaderCell);
            itemsTable.AddCell(descHeaderCell);
            itemsTable.AddCell(priceHeaderCell);
            itemsTable.AddCell(totalHeaderCell);

            // Data rows
            foreach (var line in vm.Lines)
            {
                var cellBorder = new SolidBorder(PdfBorderThickness);

                itemsTable.AddCell(new iTextCell()
                    .Add(new iTextParagraph(line.Quantity.ToString()).SetFont(font).SetFontSize(9))
                    .SetBorder(cellBorder)
                    .SetPadding(PdfItemRowPadding)
                    .SetMinHeight(PdfItemRowMinHeight)
                    .SetTextAlignment(iTextTextAlignment.CENTER));

                itemsTable.AddCell(new iTextCell()
                    .Add(new iTextParagraph(line.PartNumber ?? "").SetFont(font).SetFontSize(9))
                    .SetBorder(cellBorder)
                    .SetPadding(PdfItemRowPadding)
                    .SetMinHeight(PdfItemRowMinHeight));

                itemsTable.AddCell(new iTextCell()
                    .Add(new iTextParagraph(line.Description ?? "").SetFont(font).SetFontSize(9))
                    .SetBorder(cellBorder)
                    .SetPadding(PdfItemRowPadding)
                    .SetMinHeight(PdfItemRowMinHeight));

                itemsTable.AddCell(new iTextCell()
                    .Add(new iTextParagraph(string.Empty).SetFont(font).SetFontSize(9))
                    .SetBorder(cellBorder)
                    .SetPadding(PdfItemRowPadding)
                    .SetMinHeight(PdfItemRowMinHeight)
                    .SetTextAlignment(iTextTextAlignment.CENTER));

                itemsTable.AddCell(new iTextCell()
                    .Add(new iTextParagraph(FormatOrderAmount(line.UnitPrice)).SetFont(font).SetFontSize(9))
                    .SetBorder(cellBorder)
                    .SetPadding(PdfItemRowPadding)
                    .SetMinHeight(PdfItemRowMinHeight)
                    .SetTextAlignment(iTextTextAlignment.RIGHT));
            }

            // Blank rows to pad to 16
            for (int i = vm.Lines.Count; i < 16; i++)
            {
                for (int col = 0; col < 5; col++)
                {
                    itemsTable.AddCell(new iTextCell()
                        .Add(new iTextParagraph(" ").SetFont(font).SetFontSize(9))
                        .SetBorder(border)
                        .SetPadding(PdfItemRowPadding)
                        .SetMinHeight(PdfItemRowMinHeight));
                }
            }

            // VAT row
            itemsTable.AddCell(new iTextCell(1, 3).SetBorder(border).Add(new iTextParagraph(" ")));
            itemsTable.AddCell(new iTextCell()
                .Add(new iTextParagraph("VAT").SetFont(font).SetFontSize(9))
                .SetBorder(border)
                .SetPadding(2)
                .SetTextAlignment(iTextTextAlignment.CENTER));
            itemsTable.AddCell(new iTextCell()
                .Add(new iTextParagraph(FormatOrderAmount(documentVatAmount)).SetFont(font).SetFontSize(9))
                .SetBorder(border)
                .SetPadding(2)
                .SetTextAlignment(iTextTextAlignment.RIGHT));

            // Total row
            itemsTable.AddCell(new iTextCell(1, 4)
                .Add(new iTextParagraph("TOTAL AMOUNT R").SetFont(boldFont).SetFontSize(9))
                .SetBorder(border)
                .SetPadding(2)
                .SetTextAlignment(iTextTextAlignment.RIGHT));
            itemsTable.AddCell(new iTextCell()
                .Add(new iTextParagraph(FormatOrderAmount(documentTotalAmount)).SetFont(boldFont).SetFontSize(9))
                .SetBorder(border)
                .SetPadding(2)
                .SetTextAlignment(iTextTextAlignment.RIGHT));

            document.Add(itemsTable);
            AddPdfFooterBlock(document, font);

            document.Close();

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "exception.log"), $"[{DateTime.Now:O}] Export to PDF exception: {ex}\n\n");
            }
            catch {
            }
            MessageBox.Show($"Export to PDF failed:\n{ex}", "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FlowDocument BuildInvoiceDocument(MainViewModel vm)
    {
        var doc = new FlowDocument
        {
            PageWidth = 840,
            PageHeight = 1188,
            FontFamily = new FontFamily("Arial"),
            FontSize = 11,
            PagePadding = new Thickness(18)
        };

        doc.Blocks.Add(CreatePrintHeaderBlock());

        doc.Blocks.Add(CreatePrintRecipientAndOrderBlock(vm));

        var (documentVatAmount, documentTotalAmount) = GetOrderDocumentTotals(vm);
        var itemsTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 12, 0, 6) };
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(68) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(96) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(320) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(74) });
        itemsTable.Columns.Add(new TableColumn { Width = new GridLength(96) });

        var itemsGroup = new TableRowGroup();
        itemsTable.RowGroups.Add(itemsGroup);

        var headerRow = new TableRow();
        headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("QUANTITY"))) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("PART No."))) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("DESCRIPTION OF MATERIAL AND/OR\nSERVICES RENDERED"))) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("PRICE PER\nUNIT"))) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("PRICE"))) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        itemsGroup.Rows.Add(headerRow);

        foreach (var line in vm.Lines)
        {
            var itemRow = new TableRow();
            itemRow.Cells.Add(new TableCell(new Paragraph(new Run(line.Quantity.ToString())) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, PrintBorderThickness, PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
            itemRow.Cells.Add(new TableCell(new Paragraph(new Run(line.PartNumber ?? string.Empty)) { FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, PrintBorderThickness, PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
            itemRow.Cells.Add(new TableCell(new Paragraph(new Run(line.Description ?? string.Empty)) { FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, PrintBorderThickness, PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
            itemRow.Cells.Add(new TableCell(new Paragraph(new Run(string.Empty)) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, PrintBorderThickness, PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
            itemRow.Cells.Add(new TableCell(new Paragraph(new Run(FormatOrderAmount(line.UnitPrice))) { TextAlignment = TextAlignment.Right, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
            itemsGroup.Rows.Add(itemRow);
        }

        var extraRows = Math.Max(0, 16 - vm.Lines.Count);
        for (int i = 0; i < extraRows; i++)
        {
            var blankRow = new TableRow();
            for (int c = 0; c < 5; c++)
            {
                blankRow.Cells.Add(new TableCell(new Paragraph(new Run(" ")))
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(c == 4 ? PrintBorderThickness : 0, 0, PrintBorderThickness, PrintBorderThickness),
                    Padding = new Thickness(PrintItemCellPadding)
                });
            }
            itemsGroup.Rows.Add(blankRow);
        }

        var totalRow = new TableRow();
        totalRow.Cells.Add(new TableCell(new Paragraph(new Run(""))) { ColumnSpan = 3, BorderThickness = new Thickness(0) });
        totalRow.Cells.Add(new TableCell(new Paragraph(new Run("VAT")) { TextAlignment = TextAlignment.Center, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        totalRow.Cells.Add(new TableCell(new Paragraph(new Run(FormatOrderAmount(documentVatAmount))) { TextAlignment = TextAlignment.Right, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        itemsGroup.Rows.Add(totalRow);

        var totalGrandRow = new TableRow();
        totalGrandRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("TOTAL AMOUNT R"))) { TextAlignment = TextAlignment.Right, FontSize = 9 }) { ColumnSpan = 4, BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        totalGrandRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run(FormatOrderAmount(documentTotalAmount)))) { TextAlignment = TextAlignment.Right, FontSize = 9 }) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(PrintBorderThickness), Padding = new Thickness(PrintItemCellPadding) });
        itemsGroup.Rows.Add(totalGrandRow);

        doc.Blocks.Add(itemsTable);
        doc.Blocks.Add(CreatePrintFooterBlock());

        return doc;
    }
}
