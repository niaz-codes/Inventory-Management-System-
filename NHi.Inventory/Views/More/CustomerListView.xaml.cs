using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class CustomerListView : UserControl
{
    private readonly CustomerService _service = new();
    public CustomerListView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData() { dg.ItemsSource = string.IsNullOrWhiteSpace(txtSearch?.Text) ? _service.GetAll() : _service.Search(txtSearch.Text); }
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => LoadData();

    private void Dg_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dg.SelectedItem is Customer customer)
        {
            var dialog = new Khata.CustomerKhataView(customer.Id) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            if (dialog.DataChanged) LoadData();
        }
    }
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Customers.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportCustomers(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.POS.CustomerDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
            if (MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _service.Delete(id); LoadData(); }
    }

    private void BtnView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var dialog = new Khata.CustomerKhataView(id) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            if (dialog.DataChanged) LoadData();
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id && _service.GetById(id) is Customer customer)
        {
            var dialog = new Views.POS.CustomerDialog(customer) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true) LoadData();
        }
    }
}
