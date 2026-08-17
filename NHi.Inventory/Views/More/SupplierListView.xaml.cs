using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class SupplierListView : UserControl
{
    private readonly SupplierService _service = new();
    public SupplierListView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData() { dg.ItemsSource = _service.GetAll(); }
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Suppliers.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportSuppliers(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.Khata.SupplierDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
            if (MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _service.Delete(id); LoadData(); }
    }
}
