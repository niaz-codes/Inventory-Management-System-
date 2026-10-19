using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class EmployeeListView : UserControl
{
    private readonly EmployeeService _service = new();

    public EmployeeListView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }

    private void LoadData()
    {
        dgEmployees.ItemsSource = string.IsNullOrWhiteSpace(txtSearch?.Text) ? _service.GetAll() : _service.Search(txtSearch.Text);
        txtTotalDues.Text = $"{LanguageManager.GetString("TotalDues")}: Rs. {_service.GetTotalDues():N0}";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => LoadData();

    private void OpenDetail(int employeeId)
    {
        var dialog = new EmployeeDetailView(employeeId) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        if (dialog.DataChanged) LoadData();
    }

    private void DgEmployees_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgEmployees.SelectedItem is Employee employee) OpenDetail(employee.Id);
    }

    private void BtnView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int employeeId) OpenDetail(employeeId);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EmployeeDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void BtnPay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int employeeId) return;
        var dialog = new Khata.PaymentDialog(employeeId, "employee");
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Employees.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportEmployees(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
}
