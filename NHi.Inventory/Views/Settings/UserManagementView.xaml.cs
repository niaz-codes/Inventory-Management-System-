using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Data;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

public partial class UserManagementView : UserControl
{
    public UserManagementView() { InitializeComponent(); Loaded += (s, e) => LoadUsers(); }

    private void LoadUsers()
    {
        using var db = new AppDbContext();
        dg.ItemsSource = db.Users.ToList();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddUserDialog();
        if (dialog.ShowDialog() == true) LoadUsers();
    }

    private void BtnResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int userId)
        {
            var dialog = new ResetPasswordDialog(userId);
            dialog.ShowDialog();
        }
    }
}
