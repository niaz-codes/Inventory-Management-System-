using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class SalesHistoryView : UserControl
{
    private readonly SalesService _service = new();
    private readonly int? _customerId;
    private List<Sale> _allSales = new();
    public SalesHistoryView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    public SalesHistoryView(int customerId) : this() { _customerId = customerId; }

    private void LoadData()
    {
        _allSales = _service.GetSales(customerId: _customerId);
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var text = txtSearch.Text;
        dg.ItemsSource = string.IsNullOrWhiteSpace(text)
            ? _allSales
            : _allSales.Where(s => s.InvoiceNumber.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                                    (s.Customer != null && s.Customer.Name.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplySearchFilter();

    private void BtnFilter_Click(object sender, RoutedEventArgs e)
    {
        _allSales = _service.GetSales(dpFrom.SelectedDate, dpTo.SelectedDate, _customerId);
        ApplySearchFilter();
    }
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
            ProductNameUrdu = i.Product?.NameUrdu,
            UnitLabel = i.Product?.Unit?.Symbol,
            Quantity = i.Quantity,
            Price = i.UnitPrice,
            DiscountAmount = i.DiscountAmount
        }).ToList();

        if (!ReceiptService.PrintReceipt(sale, sale.Customer, lines))
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnViewSale_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Sale saleRow) return;
        var sale = _service.GetSaleById(saleRow.Id);
        if (sale == null) return;

        var dialog = new SaleDetailDialog(sale) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }

    private void BtnReturn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Sale saleRow) return;
        var dialog = new SaleReturnDialog(saleRow.Id);
        if (dialog.ShowDialog() == true) LoadData();
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
