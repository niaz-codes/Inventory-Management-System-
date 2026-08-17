using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        mainContent.Content = new DashboardView();
        this.KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { NavTo("POS"); return; }
        if (e.Key == Key.F8) { NavTo("Khata"); return; }
        if (e.Key == Key.F7) { mainContent.Content = new SalesHistoryView(); return; }

        if (mainContent.Content is not POSScreen pos) return;

        switch (e.Key)
        {
            case Key.F2: pos.FocusSearch(); e.Handled = true; break;
            case Key.F3: pos.FocusCustomer(); e.Handled = true; break;
            case Key.F4: pos.FocusPayment(); e.Handled = true; break;
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
                mainContent.Content = new DashboardView();
                navHome.IsChecked = true;
                break;
            case "POS":
                mainContent.Content = new POSScreen();
                navPOS.IsChecked = true;
                break;
            case "Inventory":
                mainContent.Content = new InventoryView();
                navInventory.IsChecked = true;
                break;
            case "Khata":
                mainContent.Content = new KhataView();
                navKhata.IsChecked = true;
                break;
            case "More":
                mainContent.Content = new MoreView();
                navMore.IsChecked = true;
                break;
        }
    }

    private void BtnMenu_Click(object sender, RoutedEventArgs e)
    {
        NavTo("More");
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
