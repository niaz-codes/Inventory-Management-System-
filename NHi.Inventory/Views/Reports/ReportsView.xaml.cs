using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Reports;

public partial class ReportsView : UserControl
{
    private readonly ReportService _reportService = new();
    private readonly SalesService _salesService = new();
    private readonly ProductService _productService = new();
    private string _stockFilterTag = "All";

    public ReportsView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }

    private void LoadData()
    {
        dgSales.ItemsSource = _salesService.GetSales();
        LoadStockGrid();
        LoadCategoryValuation();
        txtGrossProfit.Text = $"Rs. {_reportService.GetTodayProfit() + new ExpenseService().GetTodayExpenses():N0}";
        txtExpenses.Text = $"Rs. {new ExpenseService().GetTodayExpenses():N0}";
        txtNetProfit.Text = $"Rs. {_reportService.GetTodayProfit():N0}";
        txtStockValue.Text = $"Rs. {_reportService.GetStockValue():N0}";
    }

    private void LoadStockGrid()
    {
        // cmbStockFilter's SelectionChanged can fire while InitializeComponent is still
        // running (before dgStock is assigned) — ignore those premature calls; LoadData()
        // re-runs this once the view is fully built.
        if (cmbStockFilter is null || dgStock is null) return;

        _stockFilterTag = (cmbStockFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        dgStock.ItemsSource = _stockFilterTag switch
        {
            "LowStock" => _productService.GetLowStock(),
            "Expired" => _productService.GetExpiredProducts(),
            "Expiring" => _productService.GetExpiringProducts(GetExpiryWarningDays()),
            _ => _productService.GetAll(true)
        };
    }

    private int GetExpiryWarningDays()
    {
        using var db = new AppDbContext();
        return db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId)?.ExpiryWarningDays ?? 30;
    }

    private void LoadCategoryValuation()
    {
        var rows = _productService.GetStockValueByCategory()
            .Select(c => new CategoryValuationRow { CategoryName = c.CategoryName, Summary = $"{c.ProductCount} - Rs. {c.StockValue:N0}" })
            .ToList();
        icCategoryValuation.ItemsSource = rows;
    }

    private void StockFilter_Changed(object sender, SelectionChangedEventArgs e) => LoadStockGrid();

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
        var (fileName, exportFn) = _stockFilterTag switch
        {
            "LowStock" => ("LowStockReport.xlsx", (Func<string, bool>)ExcelExportService.ExportLowStock),
            "Expired" => ("ExpiredProductsReport.xlsx", (Func<string, bool>)ExcelExportService.ExportExpiredProducts),
            "Expiring" => ("ExpiringProductsReport.xlsx", (Func<string, bool>)(path => ExcelExportService.ExportExpiringProducts(path, GetExpiryWarningDays()))),
            _ => ("StockReport.xlsx", (Func<string, bool>)ExcelExportService.ExportStock)
        };

        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = fileName };
        if (dialog.ShowDialog() == true)
        { exportFn(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }

    private void BtnPrintStock(object sender, RoutedEventArgs e)
    {
        var items = (dgStock.ItemsSource as IEnumerable<Product>)?.ToList() ?? new List<Product>();
        var headers = new[] { LanguageManager.GetString("ProductName"), LanguageManager.GetString("Barcode"),
            LanguageManager.GetString("CurrentStock"), LanguageManager.GetString("StockValueColumn") };
        var rows = items.Select(p => new[] { p.NameEnglish, p.Barcode ?? "", $"{p.CurrentStock:N0}", $"Rs. {p.StockValue:N0}" }).ToList();

        if (!ReceiptService.PrintSimpleTable(LanguageManager.GetString("InventoryReports"), headers, rows))
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnExportCategoryValuation(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "StockValuationByCategory.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportStockValuationByCategory(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }

    private void BtnExportAdjustmentHistory(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "StockAdjustmentHistory.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportStockAdjustmentHistory(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }

    private void BtnExportProfit(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "ProfitSummary.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportProfitSummary(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
}

public class CategoryValuationRow
{
    public string CategoryName { get; set; } = "";
    public string Summary { get; set; } = "";
}
