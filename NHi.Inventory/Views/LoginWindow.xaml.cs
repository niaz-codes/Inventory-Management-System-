using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        txtUsername.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        var username = txtUsername.Text.Trim();
        var password = chkShowPassword.IsChecked == true ? txtPasswordVisible.Text : txtPassword.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            txtError.Text = LanguageManager.GetString("FillRequired");
            return;
        }

        if (AuthService.Login(username, password))
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
        else
        {
            txtError.Text = LanguageManager.GetString("LoginFailed");
        }
    }

    private void ChkShowPassword_Click(object sender, RoutedEventArgs e)
    {
        if (chkShowPassword.IsChecked == true)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPasswordVisible.Visibility = Visibility.Visible;
            txtPassword.Visibility = Visibility.Collapsed;
        }
        else
        {
            txtPassword.Password = txtPasswordVisible.Text;
            txtPassword.Visibility = Visibility.Visible;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
        }
    }

    private void Lang_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var lang = rbUrdu.IsChecked == true ? "ur" : "en";
        LanguageManager.SetLanguage(lang);
    }
}
