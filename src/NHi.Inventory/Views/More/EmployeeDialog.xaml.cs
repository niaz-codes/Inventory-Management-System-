using System.Windows;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class EmployeeDialog : Window
{
    private readonly Employee? _existing;

    public EmployeeDialog(Employee? existing = null)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _existing = existing;

        if (existing != null)
        {
            txtTitle.Text = LanguageManager.GetString("EditEmployee");
            txtName.Text = existing.Name;
            txtPosition.Text = existing.Position ?? "";
            txtPhone.Text = existing.Phone ?? "";
            txtAddress.Text = existing.Address ?? "";
            txtSalary.Text = existing.DailyWage.ToString("0.##");
            txtMonthly.Text = existing.MonthlySalary.ToString("0.##");
            rbMonthly.IsChecked = existing.SalaryType == "Monthly";
            rbDaily.IsChecked = existing.SalaryType != "Monthly";
        }

        UpdateSalaryPanels();
    }

    private void SalType_Changed(object sender, RoutedEventArgs e) => UpdateSalaryPanels();

    private void UpdateSalaryPanels()
    {
        if (pnlDaily == null || pnlMonthly == null) return;
        var monthly = rbMonthly.IsChecked == true;
        pnlDaily.Visibility = monthly ? Visibility.Collapsed : Visibility.Visible;
        pnlMonthly.Visibility = monthly ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var employee = _existing ?? new Employee();
        employee.Name = txtName.Text.Trim();
        employee.Position = txtPosition.Text.Trim();
        employee.Phone = txtPhone.Text.Trim();
        employee.Address = txtAddress.Text.Trim();
        employee.SalaryType = rbMonthly.IsChecked == true ? "Monthly" : "Daily";
        employee.DailyWage = decimal.TryParse(txtSalary.Text, out var wage) ? wage : 0;
        employee.MonthlySalary = decimal.TryParse(txtMonthly.Text, out var monthly) ? monthly : 0;

        new EmployeeService().Save(employee);
        this.DialogResult = true;
        this.Close();
    }
}
