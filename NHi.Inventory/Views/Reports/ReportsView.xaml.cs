using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Data;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Reports;

public partial class ReportsView : UserControl
{
    private readonly ReportService _reportService = new();
    private readonly SalesService _salesService = new();
    public ReportsView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }

    private void LoadData()
    {
        dgSales.ItemsSource = _salesService.GetSales();
        using var db = new AppDbContext();
        var products = db.Products.Where(p => p.IsActive).Select(p => new { p.NameEnglish, p.Barcode, p.PurchasePrice, p.SalePrice, p.CurrentStock, StockValue = p.CurrentStock * p.PurchasePrice }).ToList();
        dgStock.ItemsSource = products;
        txtGrossProfit.Text = $"Rs. {_reportService.GetTodayProfit() + new ExpenseService().GetTodayExpenses():N0}";
        txtExpenses.Text = $"Rs. {new ExpenseService().GetTodayExpenses():N0}";
        txtNetProfit.Text = $"Rs. {_reportService.GetTodayProfit():N0}";
        txtStockValue.Text = $"Rs. {_reportService.GetStockValue():N0}";
    }

    private void BtnFilterSales(object sender, RoutedEventArgs e)
    { dgSales.ItemsSource = _salesService.GetSales(dpFrom.SelectedDate, dpTo.SelectedDate); }

    private void BtnExportSales(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "SalesReport.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportSales(dialog.FileName, dpFrom.SelectedDate, dpTo.SelectedDate); MessageBox.Show("Exported!"); }
    }

    private void BtnExportStock(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "StockReport.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportStock(dialog.FileName); MessageBox.Show("Exported!"); }
    }

    private void BtnExportProfit(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "ProfitSummary.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportProfitSummary(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
}
