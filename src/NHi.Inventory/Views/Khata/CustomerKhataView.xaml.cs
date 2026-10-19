using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class CustomerKhataView : Window
{
    private readonly int _customerId;
    private readonly CustomerService _service = new();
    private readonly WhatsAppService _whatsApp = new();
    private Customer? _customer;
    private List<CustomerLedger> _entries = new();
    private bool _dataChanged;

    public CustomerKhataView(int customerId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _customerId = customerId;

        LoadHeader();
        LoadLedger(null, null);
    }

    /// <summary>True if any action taken while this window was open changed the customer's data, so the caller knows to refresh its own list.</summary>
    public bool DataChanged => _dataChanged;

    private void LoadHeader()
    {
        _customer = _service.GetById(_customerId);
        if (_customer == null) { Close(); return; }

        Title = $"{LanguageManager.GetString("CustomerKhata")} - {_customer.Name}";
        txtName.Text = _customer.Name;

        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_customer.FatherName)) detailParts.Add(_customer.FatherName);
        if (!string.IsNullOrWhiteSpace(_customer.Phone)) detailParts.Add(_customer.Phone);
        if (!string.IsNullOrWhiteSpace(_customer.WhatsAppNumber)) detailParts.Add($"WhatsApp: {_customer.WhatsAppNumber}");
        if (!string.IsNullOrWhiteSpace(_customer.Address)) detailParts.Add(_customer.Address);
        txtDetails.Text = string.Join("   |   ", detailParts);

        var balance = _customer.CurrentBalance;
        txtBalance.Text = $"Rs. {Math.Abs(balance):N0}";

        var (overdueAmount, _) = _service.GetOverdueInfo(_customerId);

        string statusKey;
        string badgeColor;
        string? balanceHex;
        if (balance == 0) { statusKey = "StatusPaid"; badgeColor = "#4CAF50"; balanceHex = null; }
        else if (balance < 0) { statusKey = "StatusAdvance"; badgeColor = "#2196F3"; balanceHex = "#2196F3"; }
        else if (overdueAmount > 0) { statusKey = "StatusOverdue"; badgeColor = "#F44336"; balanceHex = "#C62828"; }
        else { statusKey = "StatusBalance"; badgeColor = "#FF9800"; balanceHex = "#E65100"; }

        txtStatusBadge.Text = LanguageManager.GetString(statusKey);
        badgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeColor));
        if (balanceHex == null)
            txtBalance.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        else
            txtBalance.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(balanceHex));

        txtCreditLimitInfo.Text = _customer.CreditLimit > 0
            ? $"{LanguageManager.GetString("CreditLimit")}: Rs. {_customer.CreditLimit:N0}"
            : "";

        var hasAnyNumber = !string.IsNullOrWhiteSpace(_customer.WhatsAppNumber) || !string.IsNullOrWhiteSpace(_customer.Phone);
        btnWhatsApp.Visibility = hasAnyNumber ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadLedger(DateTime? from, DateTime? to)
    {
        _entries = _service.GetLedger(_customerId, from, to);
        dgLedger.ItemsSource = _entries;

        var opening = _entries.FirstOrDefault(e => e.TransactionType == "OpeningBalance")?.Balance ?? 0;
        var totalCredit = _entries.Where(e => e.TransactionType == "CreditSale").Sum(e => e.Debit);
        var totalPayments = _entries.Where(e => e.TransactionType == "Payment").Sum(e => e.Credit);
        var totalReturns = _entries.Where(e => e.TransactionType == "SaleReturn").Sum(e => e.Credit);
        var totalAdjustments = _entries.Where(e => e.TransactionType is "Adjustment" or "Reversal").Sum(e => e.Debit - e.Credit);

        txtOpening.Text = $"Rs. {opening:N0}";
        txtTotalCredit.Text = $"Rs. {totalCredit:N0}";
        txtTotalPayments.Text = $"Rs. {totalPayments:N0}";
        txtTotalReturns.Text = $"Rs. {totalReturns:N0}";
        txtTotalAdjustments.Text = $"Rs. {totalAdjustments:N0}";
    }

    private void BtnFilter_Click(object sender, RoutedEventArgs e) => LoadLedger(dpFrom.SelectedDate, dpTo.SelectedDate);

    private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        dpFrom.SelectedDate = null;
        dpTo.SelectedDate = null;
        LoadLedger(null, null);
    }

    private void BtnAddCredit_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddKhataEntryDialog(_customerId, "Credit") { Owner = this };
        if (dialog.ShowDialog() == true) { _dataChanged = true; LoadHeader(); LoadLedger(dpFrom.SelectedDate, dpTo.SelectedDate); }
    }

    private void BtnReceivePayment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ReceivePaymentDialog(_customerId) { Owner = this };
        if (dialog.ShowDialog() == true) { _dataChanged = true; LoadHeader(); LoadLedger(dpFrom.SelectedDate, dpTo.SelectedDate); }
    }

    private void BtnViewSales_Click(object sender, RoutedEventArgs e)
    {
        var win = new Window
        {
            Title = $"{_customer?.Name} - {LanguageManager.GetString("ViewSales")}",
            Width = 950,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Content = new More.SalesHistoryView(_customerId),
            FlowDirection = LanguageManager.CurrentFlowDirection
        };
        win.ShowDialog();
    }

    private void BtnPrintKhata_Click(object sender, RoutedEventArgs e)
    {
        if (_customer == null) return;
        if (!ReceiptService.PrintCustomerStatement(_customer, _entries, dpFrom.SelectedDate, dpTo.SelectedDate))
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_customer == null) return;
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Khata_{_customer.Name}.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            ExcelExportService.ExportCustomerStatement(dialog.FileName, _customerId, dpFrom.SelectedDate, dpTo.SelectedDate);
            MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
        }
    }

    private void BtnEditCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (_customer == null) return;
        var dialog = new POS.CustomerDialog(_customer) { Owner = this };
        if (dialog.ShowDialog() == true) { _dataChanged = true; LoadHeader(); }
    }

    /// <summary>Opens a prefilled WhatsApp chat with this customer's Khata balance via the
    /// wa.me Click-to-Chat link — no auto-send, no API. The button is disabled for the
    /// duration of the call so a double-click can't open the link twice.</summary>
    private void BtnWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        if (_customer == null) return;
        btnWhatsApp.IsEnabled = false;
        try
        {
            var result = _whatsApp.SendKhataReminder(_customer);
            MessageBox.Show(
                result.Success ? LanguageManager.GetString("WhatsAppOpenedStatus") : result.ErrorMessage,
                LanguageManager.GetString("SendReminder"),
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally { btnWhatsApp.IsEnabled = true; }
    }

    private void BtnMessageHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_customer == null) return;
        var dialog = new MessageHistoryWindow(_customer) { Owner = this };
        dialog.ShowDialog();
    }
}
