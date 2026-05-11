using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace PurchaseOrderApp;

public partial class MessageDialogWindow : Window, INotifyPropertyChanged
{
    private string _title = "Information";
    private string _message = "";
    private string _okButtonText = "OK";
    private string _cancelButtonText = "Cancel";
    private string _yesButtonText = "Yes";
    private string _noButtonText = "No";
    private Visibility _okButtonVisibility = Visibility.Visible;
    private Visibility _cancelButtonVisibility = Visibility.Collapsed;
    private Visibility _yesButtonVisibility = Visibility.Collapsed;
    private Visibility _noButtonVisibility = Visibility.Collapsed;

    public MessageDialogWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string OkButtonText
    {
        get => _okButtonText;
        set => SetProperty(ref _okButtonText, value);
    }

    public string CancelButtonText
    {
        get => _cancelButtonText;
        set => SetProperty(ref _cancelButtonText, value);
    }

    public string YesButtonText
    {
        get => _yesButtonText;
        set => SetProperty(ref _yesButtonText, value);
    }

    public string NoButtonText
    {
        get => _noButtonText;
        set => SetProperty(ref _noButtonText, value);
    }

    public Visibility OkButtonVisibility
    {
        get => _okButtonVisibility;
        set => SetProperty(ref _okButtonVisibility, value);
    }

    public Visibility CancelButtonVisibility
    {
        get => _cancelButtonVisibility;
        set => SetProperty(ref _cancelButtonVisibility, value);
    }

    public Visibility YesButtonVisibility
    {
        get => _yesButtonVisibility;
        set => SetProperty(ref _yesButtonVisibility, value);
    }

    public Visibility NoButtonVisibility
    {
        get => _noButtonVisibility;
        set => SetProperty(ref _noButtonVisibility, value);
    }

    public void ConfigureButtons(MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OK:
                OkButtonVisibility = Visibility.Visible;
                CancelButtonVisibility = Visibility.Collapsed;
                YesButtonVisibility = Visibility.Collapsed;
                NoButtonVisibility = Visibility.Collapsed;
                break;
            case MessageBoxButton.OKCancel:
                OkButtonVisibility = Visibility.Visible;
                CancelButtonVisibility = Visibility.Visible;
                YesButtonVisibility = Visibility.Collapsed;
                NoButtonVisibility = Visibility.Collapsed;
                break;
            case MessageBoxButton.YesNo:
                OkButtonVisibility = Visibility.Collapsed;
                CancelButtonVisibility = Visibility.Collapsed;
                YesButtonVisibility = Visibility.Visible;
                NoButtonVisibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNoCancel:
                OkButtonVisibility = Visibility.Collapsed;
                CancelButtonVisibility = Visibility.Visible;
                YesButtonVisibility = Visibility.Visible;
                NoButtonVisibility = Visibility.Visible;
                break;
        }
    }

    private MessageBoxResult _result = MessageBoxResult.None;

    public Task<MessageBoxResult> ShowDialogAsync()
    {
        var tcs = new TaskCompletionSource<MessageBoxResult>();
        Closed += (s, e) => tcs.TrySetResult(_result);
        Show();
        return tcs.Task;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.OK;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.Cancel;
        Close();
    }

    private void OnYesClick(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.Yes;
        Close();
    }

    private void OnNoClick(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.No;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}