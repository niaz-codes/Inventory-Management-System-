using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
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
}
