using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class AddDueDialog : Window
{
    private readonly int _employeeId;

    public AddDueDialog(int employeeId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _employeeId = employeeId;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0 || string.IsNullOrWhiteSpace(txtDescription.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        new EmployeeService().AddDue(_employeeId, amount, txtDescription.Text.Trim());
        this.DialogResult = true;
        this.Close();
    }
}
