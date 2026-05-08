using System.Windows;
using System.Windows.Input;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;

namespace PurchaseOrderApp;

public partial class LoginWindow : Window
{
    private readonly UserAccessService userAccessService = new();

    public AppUser? AuthenticatedUser { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UserNameTextBox.Focus();
        StatusTextBlock.Text = "Default first login: Admin / admin";
    }

    private void OnLogin(object sender, RoutedEventArgs e)
    {
        TryLogin();
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryLogin();
        }
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TryLogin()
    {
        try
        {
            AuthenticatedUser = userAccessService.AuthenticateUser(UserNameTextBox.Text, PasswordBox.Password);
            if (AuthenticatedUser is null)
            {
                StatusTextBlock.Text = "Invalid user name or password.";
                PasswordBox.SelectAll();
                PasswordBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = ex.Message;
        }
    }
}
