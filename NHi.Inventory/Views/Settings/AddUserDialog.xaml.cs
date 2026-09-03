using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

public partial class AddUserDialog : Window
{
    /// <summary>The shop the new user belongs to — always a concrete shop, never null (Main
    /// Admin isn't created from this dialog). Defaults to the caller's own shop; a Main Admin
    /// drilling into a specific shop's user list passes that shop's Id explicitly.</summary>
    private readonly int? _shopId;
    private readonly int? _existingUserId;

    public AddUserDialog(int? shopId = null, User? existingUser = null)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _shopId = shopId ?? AuthService.CurrentShopId;

        if (existingUser != null)
        {
            _existingUserId = existingUser.Id;
            txtTitle.Text = LanguageManager.GetString("EditUser");
            txtUsername.Text = existingUser.Username;
            txtUsername.IsReadOnly = true;
            txtFullName.Text = existingUser.FullName;
            txtPasswordHint.Visibility = Visibility.Visible;
            foreach (ComboBoxItem item in cmbRole.Items)
                if ((string)item.Content == existingUser.Role) { cmbRole.SelectedItem = item; break; }
        }
    }

    private void BtnSave(object sender, RoutedEventArgs e)
    {
        var isEdit = _existingUserId.HasValue;
        if (string.IsNullOrWhiteSpace(txtUsername.Text) || (!isEdit && string.IsNullOrWhiteSpace(txtPassword.Password)))
        { MessageBox.Show(LanguageManager.GetString("FillRequired")); return; }

        using var db = new AppDbContext();
        var role = (cmbRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "USER";

        if (isEdit)
        {
            var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == _existingUserId!.Value);
            if (user == null) { this.Close(); return; }
            user.FullName = txtFullName.Text.Trim();
            user.Role = role;
            if (!string.IsNullOrWhiteSpace(txtPassword.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(txtPassword.Password);
            db.SaveChanges();
            AuditLogService.Log("USER_UPDATE", "User", user.Id, $"Updated user '{user.Username}'");
        }
        else
        {
            if (db.Users.IgnoreQueryFilters().Any(u => u.Username == txtUsername.Text.Trim()))
            { MessageBox.Show(LanguageManager.GetString("DuplicateUsername")); return; }

            var user = new User
            {
                ShopId = _shopId,
                Username = txtUsername.Text.Trim(),
                FullName = txtFullName.Text.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(txtPassword.Password),
                Role = role,
                Status = "Pending"
            };
            db.Users.Add(user);
            db.SaveChanges();
            AuditLogService.Log("USER_CREATE", "User", user.Id, $"Created user '{user.Username}' (pending approval)");
        }

        this.DialogResult = true; this.Close();
    }
}
