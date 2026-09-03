using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Admin;

public partial class ShopManagementView : Window
{
    private readonly ShopService _shopService = new();

    public ShopManagementView()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        LoadShops();
    }

    private void LoadShops()
    {
        panelShops.Children.Clear();
        var shops = _shopService.GetAll();

        if (shops.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = LanguageManager.GetString("NoShopsFound"),
                FontSize = 13,
                Margin = new Thickness(4, 20, 0, 0)
            };
            emptyText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            panelShops.Children.Add(emptyText);
            return;
        }

        foreach (var shop in shops)
            panelShops.Children.Add(BuildShopCard(shop));
    }

    private Border BuildShopCard(Shop shop)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            BorderThickness = new Thickness(1)
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoPanel = new StackPanel();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock { Text = shop.Name, FontSize = 16, FontWeight = FontWeights.Bold });

        var badge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.SetResourceReference(Border.BackgroundProperty, shop.IsActive ? "SuccessBgBrush" : "DangerBgBrush");
        badge.Child = new TextBlock
        {
            Text = LanguageManager.GetString(shop.IsActive ? "Active" : "Inactive"),
            FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = shop.IsActive ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)) : new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28))
        };
        titleRow.Children.Add(badge);
        infoPanel.Children.Add(titleRow);

        var userCount = _shopService.GetUserCount(shop.Id);
        var subtitle = string.Join("  •  ", new[]
        {
            string.IsNullOrWhiteSpace(shop.OwnerName) ? null : shop.OwnerName,
            string.IsNullOrWhiteSpace(shop.Phone) ? null : shop.Phone,
            $"{userCount} {LanguageManager.GetString("Users")}"
        }.Where(s => s != null));
        var subtitleText = new TextBlock
        {
            Text = subtitle, FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };
        subtitleText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        infoPanel.Children.Add(subtitleText);

        Grid.SetColumn(infoPanel, 0);
        grid.Children.Add(infoPanel);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var btnUsers = new Button { Content = LanguageManager.GetString("ManageUsers"), Style = (Style)FindResource("ActionBtnBlue") };
        btnUsers.Click += (s, e) => { var w = new ShopUsersWindow(shop); w.Owner = this; w.ShowDialog(); LoadShops(); };
        actions.Children.Add(btnUsers);

        var btnEdit = new Button { Content = LanguageManager.GetString("EditShop"), Style = (Style)FindResource("ActionBtnGray") };
        btnEdit.Click += (s, e) => { var dlg = new ShopDialog(shop) { Owner = this }; if (dlg.ShowDialog() == true) LoadShops(); };
        actions.Children.Add(btnEdit);

        var btnToggle = new Button
        {
            Content = LanguageManager.GetString(shop.IsActive ? "DeactivateShop" : "ActivateShop"),
            Style = (Style)FindResource(shop.IsActive ? "ActionBtnRed" : "ActionBtnGreen")
        };
        btnToggle.Click += (s, e) => { _shopService.SetActive(shop.Id, !shop.IsActive); LoadShops(); };
        actions.Children.Add(btnToggle);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    private void BtnAddShop_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ShopDialog { Owner = this };
        if (dialog.ShowDialog() == true) LoadShops();
    }

    private void BtnBackupRestore_Click(object sender, RoutedEventArgs e)
    {
        var window = new BackupRestoreWindow { Owner = this };
        window.ShowDialog();
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(LanguageManager.GetString("ConfirmLogout"),
            LanguageManager.GetString("Logout"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            AuthService.Logout();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
