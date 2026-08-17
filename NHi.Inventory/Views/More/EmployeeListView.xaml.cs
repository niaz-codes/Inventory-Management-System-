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
        dgEmployees.ItemsSource = _service.GetAll();
        txtTotalDues.Text = $"{LanguageManager.GetString("TotalDues")}: Rs. {_service.GetTotalDues():N0}";
        dgLedger.ItemsSource = null;
    }

    private void DgEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgEmployees.SelectedItem is Employee employee)
            dgLedger.ItemsSource = _service.GetLedger(employee.Id);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EmployeeDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void BtnAddDue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int employeeId) return;
        var dialog = new AddDueDialog(employeeId);
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
