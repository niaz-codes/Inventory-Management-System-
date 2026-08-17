using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class DailyClosingView : UserControl
{
    private readonly DailyClosingService _service = new();
    public DailyClosingView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData()
    {
        var c = _service.CalculateDailyClosing();
        txtOpening.Text = $"Rs. {c.OpeningCash:N0}";
        txtCashSales.Text = $"Rs. {c.TotalCashSales:N0}";
        txtCreditSales.Text = $"Rs. {c.TotalCreditSales:N0}";
        txtCustPayments.Text = $"Rs. {c.TotalCustomerPayments:N0}";
        txtPurchases.Text = $"Rs. {c.TotalPurchases:N0}";
        txtExpenses.Text = $"Rs. {c.TotalExpenses:N0}";
        txtSupPayments.Text = $"Rs. {c.TotalSupplierPayments:N0}";
        txtExpected.Text = $"Rs. {c.ExpectedClosingCash:N0}";
        txtProfit.Text = $"Rs. {c.NetProfit:N0}";
    }
    private void BtnCloseDay_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsAdmin()) { MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired")); return; }
        if (!decimal.TryParse(txtActualClosing.Text, out var actual)) { MessageBox.Show("Enter valid amount"); return; }
        var closingId = _service.CloseDay(actual);
        AuditLogService.Log("CLOSE_DAY", "DailyClosing", closingId, $"Day closed with actual cash Rs. {actual:N0}");
        MessageBox.Show("Day closed successfully!", "", MessageBoxButton.OK, MessageBoxImage.Information);
        LoadData();
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "DailyClosingHistory.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportDailyClosings(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
}
