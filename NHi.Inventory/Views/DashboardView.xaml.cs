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

public partial class DashboardView : UserControl
{
    private readonly ReportService _reportService = new();

    public DashboardView()
    {
        InitializeComponent();
        Loaded += DashboardView_Loaded;
    }

    private void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadGreeting();
        LoadStats();
    }

    private void LoadGreeting()
    {
        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? LanguageManager.GetString("GoodMorning") :
                       hour < 17 ? LanguageManager.GetString("GoodAfternoon") :
                       LanguageManager.GetString("GoodEvening");
        txtGreeting.Text = $"{greeting} 👋";
        txtDate.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy");
    }

    private void LoadStats()
    {
        var currency = "Rs.";
        txtTodaySales.Text = $"{currency} {_reportService.GetTodaySalesTotal():N0}";
        txtTodayProfit.Text = $"{currency} {_reportService.GetTodayProfit():N0}";
        txtReceivables.Text = $"{currency} {_reportService.GetTotalReceivables():N0}";
        txtPayables.Text = $"{currency} {_reportService.GetTotalPayables():N0}";
        txtTodayPurchases.Text = $"{currency} {_reportService.GetTodayPurchases():N0}";
        txtTodayExpenses.Text = $"{currency} {new ExpenseService().GetTodayExpenses():N0}";
        txtCurrentCash.Text = $"{currency} {_reportService.GetCurrentCash():N0}";
        txtLowStock.Text = _reportService.GetLowStockCount().ToString();
        txtTotalProducts.Text = _reportService.GetTotalProducts().ToString();
        txtTotalCustomers.Text = _reportService.GetTotalCustomers().ToString();
        txtTotalSuppliers.Text = _reportService.GetTotalSuppliers().ToString();
    }

    private void QuickPOS_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("POS");
    }

    private void QuickCustomer_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("Khata");
    }

    private void QuickKhata_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("Khata");
    }

    private void QuickReports_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("More");
    }

    private void QuickExpenses_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("More");
    }

    private void QuickClosing_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("More");
    }

    private void LowStock_Click(object sender, MouseButtonEventArgs e)
    {
        NavigateTo("Inventory");
    }

    private void NavigateTo(string section)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavTo(section);
    }
}
