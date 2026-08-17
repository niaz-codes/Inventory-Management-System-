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
            txtSalary.Text = existing.MonthlySalary.ToString("0.##");
        }
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
        employee.MonthlySalary = decimal.TryParse(txtSalary.Text, out var salary) ? salary : 0;

        new EmployeeService().Save(employee);
        this.DialogResult = true;
        this.Close();
    }
}
