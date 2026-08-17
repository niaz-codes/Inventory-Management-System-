using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

public partial class ResetPasswordDialog : Window
{
    private readonly int _userId;
    public ResetPasswordDialog(int userId) { InitializeComponent(); _userId = userId; }

    private void BtnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtNewPass.Password)) return;
        AuthService.ChangeUserPassword(_userId, txtNewPass.Password);
        MessageBox.Show("Password updated!");
        this.Close();
    }
}
