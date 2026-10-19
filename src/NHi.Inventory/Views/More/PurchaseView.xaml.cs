using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class PurchaseView : UserControl
{
    private readonly PurchaseService _service = new();
    public PurchaseView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData() { dg.ItemsSource = _service.GetPurchases(); }
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Purchases.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportPurchases(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PurchaseDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void BtnReturn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Models.Purchase purchaseRow) return;
        var dialog = new PurchaseReturnDialog(purchaseRow.Id);
        if (dialog.ShowDialog() == true) LoadData();
    }
}
