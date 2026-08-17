using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView() { InitializeComponent(); Loaded += (s, e) => LoadCompanySettings(); }

    private void LoadCompanySettings()
    {
        using var db = new AppDbContext();
        var settings = db.CompanySettings.FirstOrDefault();
        if (settings != null)
        {
            txtCompanyName.Text = settings.CompanyName;
            txtPhone.Text = settings.Phone ?? "";
            txtAddress.Text = settings.Address ?? "";
            txtCity.Text = settings.City ?? "";
            txtEmail.Text = settings.Email ?? "";
            txtCurrency.Text = settings.Currency;
        }
    }

    private void BtnSaveCompany(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsAdmin()) { MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired")); return; }
        using var db = new AppDbContext();
        var settings = db.CompanySettings.FirstOrDefault();
        if (settings == null) { settings = new CompanySettings(); db.CompanySettings.Add(settings); }
        settings.CompanyName = txtCompanyName.Text.Trim();
        settings.Phone = txtPhone.Text.Trim();
        settings.Address = txtAddress.Text.Trim();
        settings.City = txtCity.Text.Trim();
        settings.Email = txtEmail.Text.Trim();
        settings.Currency = txtCurrency.Text.Trim();
        settings.UpdatedAt = DateTime.Now;
        db.SaveChanges();
        MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
    }

    private void BtnChangePassword(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(txtCurrentPass.Password) || string.IsNullOrEmpty(txtNewPass.Password))
        { MessageBox.Show(LanguageManager.GetString("FillRequired")); return; }
        if (txtNewPass.Password != txtConfirmPass.Password)
        { MessageBox.Show("Passwords do not match"); return; }
        if (AuthService.ChangePassword(txtCurrentPass.Password, txtNewPass.Password))
        { MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); txtCurrentPass.Clear(); txtNewPass.Clear(); txtConfirmPass.Clear(); }
        else MessageBox.Show("Current password is incorrect");
    }

    private void Lang_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        LanguageManager.SetLanguage(rbUrdu.IsChecked == true ? "ur" : "en");
    }
}
