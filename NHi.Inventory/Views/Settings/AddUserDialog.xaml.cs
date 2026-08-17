using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

public partial class AddUserDialog : Window
{
    public AddUserDialog() { InitializeComponent(); FlowDirection = LanguageManager.CurrentFlowDirection; }

    private void BtnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Password))
        { MessageBox.Show("Fill required fields"); return; }
        using var db = new AppDbContext();
        db.Users.Add(new User
        {
            Username = txtUsername.Text.Trim(),
            FullName = txtFullName.Text.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(txtPassword.Password),
            Role = (cmbRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "USER"
        });
        db.SaveChanges();
        this.DialogResult = true; this.Close();
    }
}
