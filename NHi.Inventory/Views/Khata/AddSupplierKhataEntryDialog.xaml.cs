using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class AddSupplierKhataEntryDialog : Window
{
    private readonly int _supplierId;

    public AddSupplierKhataEntryDialog(int supplierId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _supplierId = supplierId;

        var supplier = new SupplierService().GetById(supplierId);
        txtSupplierName.Text = supplier?.Name ?? "";

        txtCreatedBy.Text = $"{LanguageManager.GetString("CreatedByLabel")}: {AuthService.CurrentUser?.FullName ?? AuthService.CurrentUser?.Username}";
        txtCreatedAt.Text = $"{LanguageManager.GetString("CreatedAtLabel")}: {DateTime.Now:dd-MMM-yyyy hh:mm tt}";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtDescription.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var type = rbDebit.IsChecked == true ? "ManualDebit" : "ManualCredit";
        new SupplierService().AddManualEntry(_supplierId, type, amount, txtDescription.Text.Trim(), AuthService.CurrentUser?.Id);

        MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"), LanguageManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        this.DialogResult = true;
        this.Close();
    }
}
