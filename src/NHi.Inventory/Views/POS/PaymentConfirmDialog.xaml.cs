using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.POS;

public partial class PaymentConfirmDialog : Window
{
    public decimal CashReceived { get; private set; }

    private readonly decimal _grandTotal;
    private readonly bool _isCash;

    public PaymentConfirmDialog(decimal subtotal, decimal discountAmount, decimal grandTotal, string paymentMethodLabel,
        bool isCash, decimal paidAmount, decimal remaining, string? customerName)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _grandTotal = grandTotal;
        _isCash = isCash;

        txtCustomerName.Text = customerName ?? "";
        txtSubtotal.Text = $"Rs. {subtotal:N0}";
        txtDiscount.Text = $"- Rs. {discountAmount:N0}";
        txtGrandTotal.Text = $"Rs. {grandTotal:N0}";
        txtPaymentMethod.Text = paymentMethodLabel;

        if (isCash)
        {
            pnlCash.Visibility = Visibility.Visible;
            pnlCreditSummary.Visibility = Visibility.Collapsed;
            txtCashReceived.Text = grandTotal.ToString("0.##");
        }
        else
        {
            pnlCash.Visibility = Visibility.Collapsed;
            pnlCreditSummary.Visibility = Visibility.Visible;
            txtPaidSummary.Text = $"Rs. {paidAmount:N0}";
            txtRemainingSummary.Text = $"Rs. {remaining:N0}";
        }
    }

    private void TxtCashReceived_TextChanged(object sender, TextChangedEventArgs e)
    {
        var received = decimal.TryParse(txtCashReceived.Text, out var r) ? r : 0;
        var change = received - _grandTotal;
        txtChange.Text = $"Rs. {Math.Max(0, change):N0}";
        btnConfirm.IsEnabled = !_isCash || received >= _grandTotal;
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        btnConfirm.IsEnabled = false;

        if (_isCash)
        {
            CashReceived = decimal.TryParse(txtCashReceived.Text, out var r) ? r : _grandTotal;
            if (CashReceived < _grandTotal) { btnConfirm.IsEnabled = true; return; }
        }

        DialogResult = true;
        Close();
    }
}
