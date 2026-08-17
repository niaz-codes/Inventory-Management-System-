using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Services;
using NHi.Inventory.Views.Reports;
using NHi.Inventory.Views.Settings;

namespace NHi.Inventory.Views.More;

public partial class MoreView : UserControl
{
    public MoreView()
    {
        InitializeComponent();
    }

    private void NavigateTo(UserControl view)
    {
        var mainWindow = Window.GetWindow(this) as Views.MainWindow;
        if (mainWindow != null) mainWindow.mainContent.Content = view;
    }

    private void BtnCustomers_Click(object sender, RoutedEventArgs e) => NavigateTo(new CustomerListView());
    private void BtnSuppliers_Click(object sender, RoutedEventArgs e) => NavigateTo(new SupplierListView());
    private void BtnPurchases_Click(object sender, RoutedEventArgs e) => NavigateTo(new PurchaseView());
    private void BtnSalesHistory_Click(object sender, RoutedEventArgs e) => NavigateTo(new SalesHistoryView());
    private void BtnExpenses_Click(object sender, RoutedEventArgs e) => NavigateTo(new ExpenseView());
    private void BtnReports_Click(object sender, RoutedEventArgs e) => NavigateTo(new ReportsView());
    private void BtnDailyClosing_Click(object sender, RoutedEventArgs e) => NavigateTo(new DailyClosingView());
    private void BtnBackup_Click(object sender, RoutedEventArgs e) => NavigateTo(new BackupRestoreView());
    private void BtnSettings_Click(object sender, RoutedEventArgs e) => NavigateTo(new SettingsView());
    private void BtnUsers_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsAdmin())
        {
            MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        NavigateTo(new UserManagementView());
    }
}
