using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class SalesHistoryView : UserControl
{
    private readonly SalesService _service = new();
    public SalesHistoryView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData() { dg.ItemsSource = _service.GetSales(); }
    private void BtnFilter_Click(object sender, RoutedEventArgs e)
    { dg.ItemsSource = _service.GetSales(dpFrom.SelectedDate, dpTo.SelectedDate); }
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Sales.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportSales(dialog.FileName, dpFrom.SelectedDate, dpTo.SelectedDate); MessageBox.Show("Exported!"); }
    }

    private void BtnReprint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Sale saleRow) return;
        var sale = _service.GetSaleById(saleRow.Id);
        if (sale == null) return;

        var lines = sale.Items.Select(i => new ReceiptLineItem
        {
            ProductName = i.Product?.NameEnglish ?? "",
            Quantity = i.Quantity,
            Price = i.UnitPrice
        }).ToList();

        if (!ReceiptService.PrintReceipt(sale, sale.Customer, lines))
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnVoid_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Sale saleRow) return;
        if (!AuthService.IsAdmin())
        {
            MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (_service.VoidSale(saleRow.Id))
        {
            MessageBox.Show(LanguageManager.GetString("DeletedSuccessfully"));
            LoadData();
        }
    }
}
