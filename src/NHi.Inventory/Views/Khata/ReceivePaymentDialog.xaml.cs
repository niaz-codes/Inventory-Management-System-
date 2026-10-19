using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class ReceivePaymentDialog : Window
{
    private readonly int _customerId;
    private readonly Customer? _customer;

    public ReceivePaymentDialog(int customerId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _customerId = customerId;
        _customer = new CustomerService().GetById(customerId);

        txtCustomerName.Text = _customer?.Name ?? "";
        txtCurrentBalance.Text = $"Rs. {_customer?.CurrentBalance ?? 0:N0}";
        UpdatePreview();
    }

    private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        var balance = _customer?.CurrentBalance ?? 0;
        var amount = decimal.TryParse(txtAmount.Text, out var a) ? a : 0;
        var newBalance = balance - amount;
        txtNewBalancePreview.Text = $"Rs. {newBalance:N0}";

        if (amount > balance && balance >= 0)
        {
            var extra = amount - balance;
            txtOverpayWarning.Text = $"{LanguageManager.GetString("OverpaymentWarning")}: Rs. {extra:N0}. {LanguageManager.GetString("StoredAsAdvance")}.";
            txtOverpayWarning.Visibility = Visibility.Visible;
        }
        else
        {
            txtOverpayWarning.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_customer == null) return;

        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var method = (cmbPaymentMethod.SelectedItem as ComboBoxItem)?.Tag as string ?? "Cash";
        var result = new CustomerService().ReceivePayment(_customerId, amount, txtNotes.Text.Trim(), method, AuthService.CurrentUser?.Id);
        if (result == null)
        {
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (chkPrintReceipt.IsChecked == true)
            ReceiptService.PrintKhataPaymentReceipt(_customer, result.PreviousBalance, amount, result.NewBalance, result.ReceiptNumber);

        var message = $"{LanguageManager.GetString("PaymentReceived")}\n\n" +
                      $"{LanguageManager.GetString("PreviousBalance")}: Rs. {result.PreviousBalance:N0}\n" +
                      $"{LanguageManager.GetString("AmountReceived")}: Rs. {amount:N0}\n" +
                      $"{LanguageManager.GetString("RemainingBalance")}: Rs. {result.NewBalance:N0}";
        MessageBox.Show(message, LanguageManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);

        this.DialogResult = true;
        this.Close();
    }
}
