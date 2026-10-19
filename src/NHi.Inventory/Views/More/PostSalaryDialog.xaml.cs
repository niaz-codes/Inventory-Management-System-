using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class PostSalaryDialog : Window
{
    private readonly int _employeeId;

    public PostSalaryDialog(int employeeId, decimal defaultMonthlySalary)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _employeeId = employeeId;

        cmbMonth.ItemsSource = Enumerable.Range(1, 12)
            .Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m)).ToList();
        cmbMonth.SelectedIndex = DateTime.Today.Month - 1;

        var thisYear = DateTime.Today.Year;
        cmbYear.ItemsSource = Enumerable.Range(thisYear - 3, 5).ToList();
        cmbYear.SelectedItem = thisYear;

        txtAmount.Text = defaultMonthlySalary.ToString("0.##");
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (cmbMonth.SelectedIndex < 0 || cmbYear.SelectedItem is not int year
            || !decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            new EmployeeService().PostMonthlySalary(_employeeId, year, cmbMonth.SelectedIndex + 1,
                amount, txtNotes.Text.Trim());
            DialogResult = true;
            Close();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
