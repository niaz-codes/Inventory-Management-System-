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
        if (e.Key == Key.F1) NavTo("POS");
        if (e.Key == Key.F8) NavTo("Khata");
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
