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

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int userId) return;

        if (userId == AuthService.CurrentUser?.Id)
        {
            MessageBox.Show(LanguageManager.GetString("CannotDeleteOwnAccount"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return;

        var username = user.Username;
        db.Users.Remove(user);
        db.SaveChanges();
        AuditLogService.Log("USER_DELETE", "User", userId, username);
        LoadUsers();
    }
}
