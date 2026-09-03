using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class SupplierKhataView : Window
{
    private readonly int _supplierId;
    private readonly SupplierService _service = new();
    private readonly PurchaseService _purchaseService = new();
    private readonly WhatsAppService _whatsApp = new();
    private Supplier? _supplier;
    private List<SupplierLedger> _entries = new();
    private bool _dataChanged;

    public SupplierKhataView(int supplierId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _supplierId = supplierId;

        LoadHeader();
        LoadLedger(null, null);
    }

    /// <summary>True if any action taken while this window was open changed the supplier's data, so the caller knows to refresh its own list.</summary>
    public bool DataChanged => _dataChanged;

    private void LoadHeader()
    {
        _supplier = _service.GetById(_supplierId);
        if (_supplier == null) { Close(); return; }

        Title = $"{LanguageManager.GetString("SupplierKhata")} - {_supplier.Name}";
        txtName.Text = _supplier.Name;

        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_supplier.CompanyName)) detailParts.Add(_supplier.CompanyName);
        if (!string.IsNullOrWhiteSpace(_supplier.Phone)) detailParts.Add(_supplier.Phone);
        if (!string.IsNullOrWhiteSpace(_supplier.WhatsAppNumber)) detailParts.Add($"WhatsApp: {_supplier.WhatsAppNumber}");
        if (!string.IsNullOrWhiteSpace(_supplier.Address)) detailParts.Add(_supplier.Address);
        txtDetails.Text = string.Join("   |   ", detailParts);

        var balance = _supplier.CurrentBalance;
        txtBalance.Text = $"Rs. {Math.Abs(balance):N0}";

        string statusKey;
        string badgeColor;
        string? balanceHex;
        if (balance == 0) { statusKey = "StatusPaid"; badgeColor = "#4CAF50"; balanceHex = null; }
        else if (balance < 0) { statusKey = "StatusAdvance"; badgeColor = "#2196F3"; balanceHex = "#2196F3"; }
        else { statusKey = "StatusBalance"; badgeColor = "#FF9800"; balanceHex = "#E65100"; }

        txtStatusBadge.Text = LanguageManager.GetString(statusKey);
        badgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeColor));
        if (balanceHex == null)
            txtBalance.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        else
            txtBalance.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(balanceHex));

        var hasAnyNumber = !string.IsNullOrWhiteSpace(_supplier.WhatsAppNumber) || !string.IsNullOrWhiteSpace(_supplier.Phone);
        btnWhatsApp.Visibility = hasAnyNumber ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadLedger(DateTime? from, DateTime? to)
    {
        _entries = _service.GetLedger(_supplierId, from, to);

        var purchaseIds = _entries.Where(e => e.ReferenceType is "Purchase" or "PurchaseReturn" && e.ReferenceId.HasValue)
            .Select(e => e.ReferenceId!.Value).Distinct().ToList();
        var invoiceNumbers = purchaseIds.Count > 0 ? _purchaseService.GetInvoiceNumbers(purchaseIds) : new Dictionary<int, string>();

        var userIds = _entries.Where(e => e.CreatedBy.HasValue).Select(e => e.CreatedBy!.Value).Distinct().ToList();
        var userNames = userIds.Count > 0 ? _service.GetUserNames(userIds) : new Dictionary<int, string>();

        var rows = _entries.Select(e => new SupplierLedgerRow
        {
            TransactionDate = e.TransactionDate,
            TypeText = LanguageManager.GetString(e.TransactionType),
            ReferenceText = e.ReferenceId.HasValue && e.ReferenceType is "Purchase" or "PurchaseReturn" && invoiceNumbers.TryGetValue(e.ReferenceId.Value, out var inv)
                ? inv
                : (e.ReferenceType ?? ""),
            Description = e.Description,
            Debit = e.Debit,
            Credit = e.Credit,
            Balance = e.Balance,
            CreatedByName = e.CreatedBy.HasValue && userNames.TryGetValue(e.CreatedBy.Value, out var name) ? name : ""
        }).ToList();
        dgLedger.ItemsSource = rows;

        var opening = _entries.FirstOrDefault(e => e.TransactionType == "OpeningBalance")?.Balance ?? 0;
        var totalPurchases = _entries.Where(e => e.TransactionType is "Purchase" or "CashPurchase").Sum(e => e.Debit);
        var totalPayments = _entries.Where(e => e.TransactionType == "Payment").Sum(e => e.Credit);
        var totalAdjustments = _entries.Where(e => e.TransactionType is "ManualDebit" or "ManualCredit" or "PurchaseReturn").Sum(e => e.Debit - e.Credit);
        var current = _entries.Count > 0 ? _entries[^1].Balance : (_supplier?.CurrentBalance ?? 0);

        txtOpening.Text = $"Rs. {opening:N0}";
        txtTotalPurchases.Text = $"Rs. {totalPurchases:N0}";
        txtTotalPayments.Text = $"Rs. {totalPayments:N0}";
        txtTotalAdjustments.Text = $"Rs. {totalAdjustments:N0}";
        txtCurrentPayable.Text = $"Rs. {current:N0}";
    }

    private void BtnFilter_Click(object sender, RoutedEventArgs e) => LoadLedger(dpFrom.SelectedDate, dpTo.SelectedDate);

    private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        dpFrom.SelectedDate = null;
        dpTo.SelectedDate = null;
        LoadLedger(null, null);
    }

    private void BtnPaySupplier_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PaySupplierDialog(_supplierId) { Owner = this };
        if (dialog.ShowDialog() == true) { _dataChanged = true; LoadHeader(); LoadLedger(dpFrom.SelectedDate, dpTo.SelectedDate); }
    }

    private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddSupplierKhataEntryDialog(_supplierId) { Owner = this };
        if (dialog.ShowDialog() == true) { _dataChanged = true; LoadHeader(); LoadLedger(dpFrom.SelectedDate, dpTo.SelectedDate); }
    }

    private void BtnPrintStatement_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier == null) return;
        if (!ReceiptService.PrintSupplierStatement(_supplier, _entries, dpFrom.SelectedDate, dpTo.SelectedDate))
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier == null) return;
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Khata_{_supplier.Name}.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            ExcelExportService.ExportSupplierStatement(dialog.FileName, _supplierId, dpFrom.SelectedDate, dpTo.SelectedDate);
            MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
        }
    }

    private void BtnEditSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier == null) return;
        var dialog = new SupplierDialog(_supplier) { Owner = this };
        if (dialog.ShowDialog() == true) { _dataChanged = true; LoadHeader(); }
    }

    /// <summary>Opens a prefilled WhatsApp chat with this supplier's Khata balance via the wa.me
    /// Click-to-Chat link — no auto-send, no API. The button is disabled for the duration of the
    /// call so a double-click can't open the link twice.</summary>
    private void BtnWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier == null) return;
        btnWhatsApp.IsEnabled = false;
        try
        {
            var result = _whatsApp.SendSupplierKhataReminder(_supplier);
            MessageBox.Show(
                result.Success ? LanguageManager.GetString("WhatsAppOpenedStatus") : result.ErrorMessage,
                LanguageManager.GetString("PaymentReminder"),
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally { btnWhatsApp.IsEnabled = true; }
    }
}

public class SupplierLedgerRow
{
    public DateTime TransactionDate { get; set; }
    public string TypeText { get; set; } = "";
    public string ReferenceText { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public string CreatedByName { get; set; } = "";
}
