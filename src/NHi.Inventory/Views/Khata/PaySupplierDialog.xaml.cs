using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class PaySupplierDialog : Window
{
    private readonly int _supplierId;
    private readonly Supplier? _supplier;

    public PaySupplierDialog(int supplierId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _supplierId = supplierId;
        _supplier = new SupplierService().GetById(supplierId);

        txtSupplierName.Text = _supplier?.Name ?? "";
        txtCurrentBalance.Text = $"Rs. {_supplier?.CurrentBalance ?? 0:N0}";
        dpPaymentDate.SelectedDate = DateTime.Today;
        UpdatePreview();
    }

    private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        var balance = _supplier?.CurrentBalance ?? 0;
        var amount = decimal.TryParse(txtAmount.Text, out var a) ? a : 0;
        var newBalance = balance - amount;
        txtNewBalancePreview.Text = $"Rs. {newBalance:N0}";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier == null) return;

        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var paymentDate = dpPaymentDate.SelectedDate ?? DateTime.Now;
        var method = (cmbPaymentMethod.SelectedItem as ComboBoxItem)?.Tag as string ?? "Cash";
        var result = new SupplierService().PaySupplier(_supplierId, amount, paymentDate, method,
            txtReferenceNumber.Text.Trim(), txtNotes.Text.Trim(), AuthService.CurrentUser?.Id);
        if (result == null)
        {
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (chkPrintReceipt.IsChecked == true)
            ReceiptService.PrintSupplierPaymentReceipt(_supplier, result.PreviousBalance, amount, result.NewBalance, result.ReceiptNumber);

        var message = $"{LanguageManager.GetString("PaymentMade")}\n\n" +
                      $"{LanguageManager.GetString("PreviousBalance")}: Rs. {result.PreviousBalance:N0}\n" +
                      $"{LanguageManager.GetString("AmountReceived")}: Rs. {amount:N0}\n" +
                      $"{LanguageManager.GetString("CurrentPayableLabel")}: Rs. {result.NewBalance:N0}";
        MessageBox.Show(message, LanguageManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);

        this.DialogResult = true;
        this.Close();
    }
}
