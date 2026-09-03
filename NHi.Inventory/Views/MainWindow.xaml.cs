using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NHi.Inventory.Data;
using NHi.Inventory.Services;
using NHi.Inventory.Views.POS;
using NHi.Inventory.Views.Inventory;
using NHi.Inventory.Views.Khata;
using NHi.Inventory.Views.More;
using NHi.Inventory.Views.Reports;
using NHi.Inventory.Views.Settings;

// Global POS keyboard shortcuts (spec section 30):
// F1 New Sale, F2 Search, F3 Customer, F4 Payment, F5 Hold Sale,
// F6 Discount, F7 Sales History, F8 Khata, F9 Print, F10 Complete Sale, ESC Cancel.

namespace NHi.Inventory.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadUserInfo();
        UpdateThemeIcon();
        // User management is an admin-only destination — hide the tab entirely for regular users.
        navUsers.Visibility = AuthService.IsAdmin() ? Visibility.Visible : Visibility.Collapsed;
        mainContent.Content = new DashboardView();
        this.KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // Never let global F-key shortcuts fire while the user is typing — a shortcut hijacking
        // keystrokes mid-entry (especially Escape wiping the cart) is worse than not firing at all.
        if (Keyboard.FocusedElement is TextBoxBase or PasswordBox)
        {
            if (e.Key == Key.Escape)
            {
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.F1) { NavTo("POS"); return; }
        if (e.Key == Key.F8) { NavTo("Khata"); return; }
        if (e.Key == Key.F7) { NavTo("SalesHistory"); return; }

        if (mainContent.Content is not POSScreen pos) return;

        switch (e.Key)
        {
            case Key.F2: pos.FocusBarcode(); e.Handled = true; break;
            case Key.F3: pos.FocusCustomer(); e.Handled = true; break;
            case Key.F4: pos.TriggerCompleteSale(); e.Handled = true; break;
            case Key.F5: pos.HoldCurrentSale(); e.Handled = true; break;
            case Key.F6: pos.FocusDiscount(); e.Handled = true; break;
            case Key.F9: pos.PrintLastReceipt(); e.Handled = true; break;
            case Key.F10: pos.TriggerCompleteSale(); e.Handled = true; break;
            case Key.Escape: pos.CancelCurrentSale(); e.Handled = true; break;
        }
    }

    private void LoadUserInfo()
    {
        if (AuthService.CurrentUser != null)
        {
            txtCurrentUser.Text = AuthService.CurrentUser.FullName;
            txtUserRole.Text = AuthService.CurrentUser.Role;
        }

        using var db = new AppDbContext();
        var shop = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (shop != null)
        {
            txtBrandName.Text = shop.Name;
            if (shop.Logo is { Length: > 0 })
            {
                try
                {
                    using var ms = new System.IO.MemoryStream(shop.Logo);
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    imgShopLogo.Source = bitmap;
                    imgShopLogo.Visibility = Visibility.Visible;
                    txtBrandBadge.Visibility = Visibility.Collapsed;
                }
                catch { /* A corrupted/unsupported logo must never block the header from rendering. */ }
            }
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            NavTo(tag);
        }
    }

    public void NavTo(string section)
    {
        switch (section)
        {
            case "Home":
                SetContent(new DashboardView(), navHome);
                break;
            case "POS":
                SetContent(new POSScreen(), navPOS);
                break;
            case "Inventory":
                SetContent(new InventoryView(), navInventory);
                break;
            case "Khata":
                SetContent(new KhataView(), navKhata);
                break;
            case "Customers":
                SetContent(new CustomerListView(), navCustomers);
                break;
            case "Suppliers":
                SetContent(new SupplierListView(), navSuppliers);
                break;
            case "Purchases":
                SetContent(new PurchaseView(), navPurchases);
                break;
            case "SalesHistory":
                SetContent(new SalesHistoryView(), navSalesHistory);
                break;
            case "Expenses":
                SetContent(new ExpenseView(), navExpenses);
                break;
            case "Reports":
                SetContent(new ReportsView(), navReports);
                break;
            case "DailyClosing":
                SetContent(new DailyClosingView(), navDailyClosing);
                break;
            case "Employees":
                SetContent(new EmployeeListView(), navEmployees);
                break;
            case "Users":
                if (!AuthService.IsAdmin())
                {
                    MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SetContent(new UserManagementView(), navUsers);
                break;
            case "Settings":
                SetContent(new SettingsView(), navSettings);
                break;
            // "More" is retained as an alias so any older caller still lands somewhere sensible.
            case "More":
                SetContent(new MoreView(), null);
                break;
        }
    }

    /// <summary>Swaps the main content and plays a short fade + slide-up so tab changes feel animated.</summary>
    private void SetContent(UserControl view, RadioButton? tab)
    {
        if (tab != null) tab.IsChecked = true;

        mainContent.Content = view;

        var slide = new TranslateTransform(0, 14);
        mainContent.RenderTransform = slide;
        mainContent.Opacity = 0;

        mainContent.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
        UpdateThemeIcon();

        using var db = new AppDbContext();
        var shop = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (shop != null)
        {
            shop.Theme = ThemeManager.CurrentTheme;
            db.SaveChanges();
        }
    }

    private void UpdateThemeIcon()
    {
        iconTheme.Kind = ThemeManager.IsDark
            ? MaterialDesignThemes.Wpf.PackIconKind.WhiteBalanceSunny
            : MaterialDesignThemes.Wpf.PackIconKind.MoonWaningCrescent;
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

public static class MainWindowCommands
{
    public static RoutedCommand NewSaleCommand = new RoutedCommand();
    public static RoutedCommand SearchProductCommand = new RoutedCommand();
    public static RoutedCommand PrintCommand = new RoutedCommand();
    public static RoutedCommand CompleteSaleCommand = new RoutedCommand();
}
