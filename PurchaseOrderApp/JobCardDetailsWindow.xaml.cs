using Microsoft.Win32;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.ViewModels;
using System.IO;
using System.Windows;

namespace PurchaseOrderApp;

/// <summary>
/// Interaction logic for JobCardDetailsWindow.xaml
/// </summary>
public partial class JobCardDetailsWindow : Window
{
    private static readonly string[] SupportedImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff"
    ];

    private readonly JobCardDetailsViewModel viewModel;
    private readonly int jobCardRecordId;

    public JobCardDetailsWindow(int jobCardRecordId)
    {
        InitializeComponent();
        this.jobCardRecordId = jobCardRecordId;
        viewModel = new JobCardDetailsViewModel(jobCardRecordId);
        DataContext = viewModel;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        TryRun(() => viewModel.Refresh(), "Refresh failed");
    }

    private void OnUploadPhoto(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select installation photo",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All Files|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        TryRun(() => viewModel.UploadPhoto(ParsePhotoType((FrameworkElement)sender), dialog.FileName), "Upload failed");
    }

    private void OnPhotoDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedImagePath(e, out _)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPhotoDrop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (!TryGetDroppedImagePath(e, out var filePath))
        {
            MessageBox.Show("Drop a single image file onto the photo panel.", "Unsupported file", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TryRun(() => viewModel.UploadPhoto(ParsePhotoType(element), filePath), "Upload failed");
        e.Handled = true;
    }

    private void OnOpenPhoto(object sender, RoutedEventArgs e)
    {
        TryRun(() => viewModel.OpenEvidence(ParsePhotoType((FrameworkElement)sender)), "Open photo");
    }

    private void OnExportPdf(object sender, RoutedEventArgs e)
    {
        TryRun(() =>
        {
            viewModel.ExportPdf(openAfterSave: true);
        }, "Export failed");
    }

    private void OnOpenPdf(object sender, RoutedEventArgs e)
    {
        TryRun(() => viewModel.OpenPdf(), "Open PDF");
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        TryRun(() => viewModel.OpenJobCardFolder(), "Open folder");
    }

    private void OnBillingCardMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        TryRun(() =>
        {
            var billingWindow = new JobCardBillingEditorWindow(jobCardRecordId)
            {
                Owner = this
            };

            if (billingWindow.ShowDialog() == true)
            {
                viewModel.Refresh();
            }
        }, "Edit billing");

        e.Handled = true;
    }

    private static JobCardEvidencePhotoType ParsePhotoType(FrameworkElement element)
    {
        return Enum.TryParse<JobCardEvidencePhotoType>(element.Tag?.ToString(), ignoreCase: true, out var photoType)
            ? photoType
            : throw new InvalidOperationException("I couldn't determine which photo slot you selected.");
    }

    private static bool TryGetDroppedImagePath(DragEventArgs e, out string filePath)
    {
        filePath = string.Empty;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length != 1)
        {
            return false;
        }

        var candidate = files[0];
        var extension = Path.GetExtension(candidate);
        if (string.IsNullOrWhiteSpace(extension) ||
            !SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        filePath = candidate;
        return true;
    }

    private static void TryRun(Action action, string title)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
