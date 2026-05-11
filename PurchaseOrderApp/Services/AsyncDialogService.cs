using System.Threading.Tasks;
using System.Windows;

namespace PurchaseOrderApp.Services;

public static class AsyncDialogService
{
    private static Window? _mainWindow;

    public static void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public static async Task ShowMessageAsync(string message, string title = "Information", MessageBoxImage icon = MessageBoxImage.Information)
    {
        var dialog = new MessageDialogWindow
        {
            Title = title,
            Message = message,
            Owner = _mainWindow
        };
        dialog.ConfigureButtons(MessageBoxButton.OK);

        await dialog.ShowDialogAsync();
    }

    public static async Task<MessageBoxResult> ShowConfirmationAsync(string message, string title = "Confirm", MessageBoxButton buttons = MessageBoxButton.YesNo, MessageBoxImage icon = MessageBoxImage.Question)
    {
        var dialog = new MessageDialogWindow
        {
            Title = title,
            Message = message,
            Owner = _mainWindow
        };
        dialog.ConfigureButtons(buttons);

        return await dialog.ShowDialogAsync();
    }
}