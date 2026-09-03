using System.Windows;
using Microsoft.EntityFrameworkCore;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;
using NHi.Inventory.Views.Settings;

namespace NHi.Inventory.Views.Admin;

/// <summary>Main-Admin-only read/act view for one shop user — approve/reject a pending
/// account, activate/deactivate, edit, reset password, or view their activity log. Reached
/// by clicking a row in ShopUsersWindow; never opens the shop's own operational screens.</summary>
public partial class UserDetailsDialog : Window
{
    private int _userId;

    public UserDetailsDialog(int userId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _userId = userId;
        Load();
    }

    private void Load()
    {
        using var db = new AppDbContext();
        var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == _userId);
        if (user == null) { this.Close(); return; }

        var shop = user.ShopId.HasValue ? db.Shops.Find(user.ShopId.Value) : null;

        txtShopName.Text = shop?.Name ?? "—";
        txtOwnerName.Text = shop?.OwnerName ?? "—";
        txtUsername.Text = user.Username;
        txtPhone.Text = shop?.Phone ?? "—";
        txtAddress.Text = shop?.Address ?? "—";
        txtRole.Text = user.Role;
        txtCreatedDate.Text = user.CreatedAt.ToString("dd-MMM-yyyy");
        txtLastLogin.Text = user.LastLogin?.ToString("dd-MMM-yyyy hh:mm tt") ?? LanguageManager.GetString("Never");

        var statusKey = user.Status switch { "Pending" => "StatusPending", "Rejected" => "StatusRejected", _ => "StatusApproved" };
        txtStatusBadge.Text = $"{LanguageManager.GetString(statusKey)} · {LanguageManager.GetString(user.IsActive ? "Active" : "Inactive")}";

        btnApprove.Visibility = user.Status == "Pending" ? Visibility.Visible : Visibility.Collapsed;
        btnReject.Visibility = user.Status == "Pending" ? Visibility.Visible : Visibility.Collapsed;
        btnToggleActive.Content = LanguageManager.GetString(user.IsActive ? "DeactivateShop" : "ActivateShop");
        btnToggleActive.Visibility = user.Status == "Pending" ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateStatus(string status, string auditAction)
    {
        using var db = new AppDbContext();
        var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == _userId);
        if (user == null) return;
        user.Status = status;
        db.SaveChanges();
        AuditLogService.Log(auditAction, "User", user.Id, $"'{user.Username}' -> {status}");
        Load();
    }

    private void BtnApprove_Click(object sender, RoutedEventArgs e) => UpdateStatus("Approved", "USER_APPROVE");
    private void BtnReject_Click(object sender, RoutedEventArgs e) => UpdateStatus("Rejected", "USER_REJECT");

    private void BtnToggleActive_Click(object sender, RoutedEventArgs e)
    {
        using var db = new AppDbContext();
        var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == _userId);
        if (user == null) return;
        user.IsActive = !user.IsActive;
        db.SaveChanges();
        AuditLogService.Log(user.IsActive ? "USER_ACTIVATE" : "USER_DEACTIVATE", "User", user.Id, user.Username);
        Load();
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        using var db = new AppDbContext();
        var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == _userId);
        if (user == null) return;
        var dialog = new AddUserDialog(user.ShopId, user) { Owner = this };
        if (dialog.ShowDialog() == true) Load();
    }

    private void BtnResetPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ResetPasswordDialog(_userId) { Owner = this };
        dialog.ShowDialog();
    }

    private void BtnActivityLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UserActivityLogWindow(_userId, txtUsername.Text) { Owner = this };
        dialog.ShowDialog();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        using var db = new AppDbContext();
        var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Id == _userId);
        if (user == null) { this.Close(); return; }

        var username = user.Username;
        db.Users.Remove(user);
        db.SaveChanges();
        AuditLogService.Log("USER_DELETE", "User", _userId, username);
        this.Close();
    }
}
