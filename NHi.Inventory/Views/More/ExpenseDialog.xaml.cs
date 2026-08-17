using System.Windows;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class ExpenseDialog : Window
{
    private readonly ExpenseService _service = new();
    public ExpenseDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        cmbCategory.ItemsSource = _service.GetCategories();
        if (cmbCategory.Items.Count > 0) cmbCategory.SelectedIndex = 0;
    }
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0) { MessageBox.Show("Enter valid amount"); return; }
        _service.Save(new Expense { ExpenseDate = DateTime.Now, Category = (cmbCategory.SelectedItem as ExpenseCategory)?.Name ?? "Other", Amount = amount, Description = txtDescription.Text.Trim(), CreatedBy = AuthService.CurrentUser?.Id });
        this.DialogResult = true; this.Close();
    }
}
