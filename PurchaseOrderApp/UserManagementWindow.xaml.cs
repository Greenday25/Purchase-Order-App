using System.Windows;
using System.Windows.Controls;
using PurchaseOrderApp.Models;
using PurchaseOrderApp.Services;

namespace PurchaseOrderApp;

public partial class UserManagementWindow : Window
{
    private readonly UserAccessService userAccessService = new();
    private AppUser? selectedUser;

    public UserManagementWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReloadAccessLists();
        ResetUserForm();
    }

    private void ReloadAccessLists()
    {
        var roles = userAccessService.GetRoles();
        var users = userAccessService.GetUsers();

        UserRoleComboBox.ItemsSource = roles;
        RolesGrid.ItemsSource = roles;
        UsersGrid.ItemsSource = users;

        if (UserRoleComboBox.SelectedItem is null)
        {
            UserRoleComboBox.SelectedItem = roles.FirstOrDefault();
        }

        StatusTextBlock.Text = $"{users.Count} user(s), {roles.Count} role(s) loaded.";
    }

    private void OnSelectedUserChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedUser = UsersGrid.SelectedItem as AppUser;
        if (selectedUser is null)
        {
            return;
        }

        UserNameTextBox.Text = selectedUser.DisplayName;
        UserPasswordBox.Password = string.Empty;
        UserConfirmPasswordBox.Password = string.Empty;
        UserRoleComboBox.SelectedValue = selectedUser.AppRoleId;
        UserActiveCheckBox.IsChecked = selectedUser.IsActive;
    }

    private void OnNewUser(object sender, RoutedEventArgs e)
    {
        ResetUserForm();
    }

    private void OnSaveUser(object sender, RoutedEventArgs e)
    {
        try
        {
            if (UserRoleComboBox.SelectedValue is not int roleId)
            {
                throw new InvalidOperationException("Select a role before saving the user.");
            }

            if (selectedUser is null)
            {
                ValidatePasswordFields(requirePassword: true);
                userAccessService.CreateUser(UserNameTextBox.Text, roleId, UserPasswordBox.Password);
                StatusTextBlock.Text = "User created.";
            }
            else
            {
                ValidatePasswordFields(requirePassword: false);
                userAccessService.UpdateUser(
                    selectedUser.AppUserId,
                    UserNameTextBox.Text,
                    roleId,
                    UserActiveCheckBox.IsChecked == true,
                    string.IsNullOrWhiteSpace(UserPasswordBox.Password) ? null : UserPasswordBox.Password);
                StatusTextBlock.Text = "User updated.";
            }

            ReloadAccessLists();
            ResetUserForm();
        }
        catch (Exception ex)
        {
            ShowAccessError(ex.Message);
        }
    }

    private void OnDeleteUser(object sender, RoutedEventArgs e)
    {
        if (selectedUser is null)
        {
            ShowAccessError("Select a user before deleting.");
            return;
        }

        var confirmation = MessageBox.Show(
            $"Delete {selectedUser.DisplayName}?",
            "Delete User",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            userAccessService.DeleteUser(selectedUser.AppUserId);
            ReloadAccessLists();
            ResetUserForm();
            StatusTextBlock.Text = "User deleted.";
        }
        catch (Exception ex)
        {
            ShowAccessError(ex.Message);
        }
    }

    private void OnAddRole(object sender, RoutedEventArgs e)
    {
        try
        {
            userAccessService.CreateRole(new AppRole
            {
                Name = RoleNameTextBox.Text,
                CanAccessPurchaseOrders = RolePurchaseOrdersCheckBox.IsChecked == true,
                CanAccessJobCards = RoleJobCardsCheckBox.IsChecked == true,
                CanAccessWialonUnits = RoleWialonUnitsCheckBox.IsChecked == true,
                CanAccessTrackingCertificates = RoleTrackingCertificatesCheckBox.IsChecked == true,
                CanAccessInventory = RoleInventoryCheckBox.IsChecked == true,
                CanAccessConnectivitySettings = RoleConnectivityCheckBox.IsChecked == true,
                CanManageUsers = RoleManageUsersCheckBox.IsChecked == true
            });

            ClearRoleForm();
            ReloadAccessLists();
            StatusTextBlock.Text = "Role created.";
        }
        catch (Exception ex)
        {
            ShowAccessError(ex.Message);
        }
    }

    private void OnSaveRoles(object sender, RoutedEventArgs e)
    {
        RolesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RolesGrid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            if (RolesGrid.ItemsSource is IEnumerable<AppRole> roles)
            {
                foreach (var role in roles)
                {
                    userAccessService.UpdateRole(role);
                }
            }

            ReloadAccessLists();
            StatusTextBlock.Text = "Role permissions saved.";
        }
        catch (Exception ex)
        {
            ShowAccessError(ex.Message);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ResetUserForm()
    {
        selectedUser = null;
        UsersGrid.SelectedItem = null;
        UserNameTextBox.Text = string.Empty;
        UserPasswordBox.Password = string.Empty;
        UserConfirmPasswordBox.Password = string.Empty;
        UserActiveCheckBox.IsChecked = true;
        UserRoleComboBox.SelectedIndex = UserRoleComboBox.Items.Count > 0 ? 0 : -1;
    }

    private void ValidatePasswordFields(bool requirePassword)
    {
        if (requirePassword && string.IsNullOrWhiteSpace(UserPasswordBox.Password))
        {
            throw new InvalidOperationException("Password is required for new users.");
        }

        if (!string.Equals(UserPasswordBox.Password, UserConfirmPasswordBox.Password, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password and confirmation do not match.");
        }
    }

    private void ClearRoleForm()
    {
        RoleNameTextBox.Text = string.Empty;
        RolePurchaseOrdersCheckBox.IsChecked = false;
        RoleJobCardsCheckBox.IsChecked = false;
        RoleWialonUnitsCheckBox.IsChecked = false;
        RoleTrackingCertificatesCheckBox.IsChecked = false;
        RoleInventoryCheckBox.IsChecked = false;
        RoleConnectivityCheckBox.IsChecked = false;
        RoleManageUsersCheckBox.IsChecked = false;
    }

    private void ShowAccessError(string message)
    {
        StatusTextBlock.Text = message;
        MessageBox.Show(message, "User Access", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
