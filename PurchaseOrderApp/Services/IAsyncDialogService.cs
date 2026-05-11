using System.Threading.Tasks;
using System.Windows;

namespace PurchaseOrderApp.Services;

public interface IAsyncDialogService
{
    Task ShowMessageAsync(string message, string title = "Information", MessageBoxImage icon = MessageBoxImage.Information);
    Task<MessageBoxResult> ShowConfirmationAsync(string message, string title = "Confirm", MessageBoxButton buttons = MessageBoxButton.YesNo, MessageBoxImage icon = MessageBoxImage.Question);
}