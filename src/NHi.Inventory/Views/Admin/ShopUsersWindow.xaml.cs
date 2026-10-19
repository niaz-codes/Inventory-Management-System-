using System.Windows;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;
using NHi.Inventory.Views.Settings;

namespace NHi.Inventory.Views.Admin;

/// <summary>Main-Admin-only per-shop user list, reached by drilling into a shop from
/// ShopManagementView. Reuses AddUserDialog/ResetPasswordDialog rather than duplicating them.</summary>
public partial class ShopUsersWindow : Window
{
    private readonly Shop _shop;

    public ShopUsersWindow(Shop shop)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _shop = shop;
        txtTitle.Text = $"{LanguageManager.GetString("ShopUsers")} — {shop.Name}";
        LoadUsers();
    }

    private void LoadUsers()
    {
        using var db = new AppDbContext();
        dg.ItemsSource = db.Users.Where(u => u.ShopId == _shop.Id).ToList();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddUserDialog(_shop.Id) { Owner = this };
        if (dialog.ShowDialog() == true) LoadUsers();
    }

    private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is int userId)
        {
            var dialog = new UserDetailsDialog(userId) { Owner = this };
            dialog.ShowDialog();
            LoadUsers();
        }
    }
}
