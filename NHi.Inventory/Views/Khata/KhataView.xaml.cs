using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class KhataView : UserControl
{
    private readonly CustomerService _customerService = new();
    private readonly SupplierService _supplierService = new();

    public KhataView()
    {
        InitializeComponent();
        Loaded += KhataView_Loaded;
    }

    private void KhataView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadCustomerLedger();
        LoadSupplierLedger();
    }

    private void LoadCustomerLedger()
    {
        var customers = _customerService.GetAll();
        var allEntries = new List<KhataEntry>();
        foreach (var c in customers)
        {
            var ledger = _customerService.GetLedger(c.Id);
            foreach (var l in ledger)
            {
                allEntries.Add(new KhataEntry
                {
                    Date = l.TransactionDate,
                    CustomerName = c.Name,
                    CustomerId = c.Id,
                    Description = l.Description,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Balance = l.Balance
                });
            }
        }
        dgCustomerLedger.ItemsSource = allEntries.OrderByDescending(e => e.Date).ToList();
        txtCustomerBalance.Text = $"Total Receivables: Rs. {_customerService.GetTotalReceivables():N0}";
    }

    private void LoadSupplierLedger()
    {
        var suppliers = _supplierService.GetAll();
        var allEntries = new List<SupplierKhataEntry>();
        foreach (var s in suppliers)
        {
            var ledger = _supplierService.GetLedger(s.Id);
            foreach (var l in ledger)
            {
                allEntries.Add(new SupplierKhataEntry
                {
                    Date = l.TransactionDate,
                    SupplierName = s.Name,
                    SupplierId = s.Id,
                    Description = l.Description,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Balance = l.Balance
                });
            }
        }
        dgSupplierLedger.ItemsSource = allEntries.OrderByDescending(e => e.Date).ToList();
        txtSupplierBalance.Text = $"Total Payables: Rs. {_supplierService.GetTotalPayables():N0}";
    }

    private void TxtCustomerSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = txtCustomerSearch.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            LoadCustomerLedger();
            return;
        }
        var customers = _customerService.Search(text);
        var entries = new List<KhataEntry>();
        foreach (var c in customers)
        {
            foreach (var l in _customerService.GetLedger(c.Id))
            {
                entries.Add(new KhataEntry
                {
                    Date = l.TransactionDate,
                    CustomerName = c.Name,
                    CustomerId = c.Id,
                    Description = l.Description,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Balance = l.Balance
                });
            }
        }
        dgCustomerLedger.ItemsSource = entries.OrderByDescending(e => e.Date).ToList();
    }

    private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new POS.CustomerDialog();
        if (dialog.ShowDialog() == true) LoadCustomerLedger();
    }

    private void BtnAddSupplier_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SupplierDialog();
        if (dialog.ShowDialog() == true) LoadSupplierLedger();
    }

    private void BtnReceivePayment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int customerId)
        {
            var dialog = new PaymentDialog(customerId, "customer");
            if (dialog.ShowDialog() == true)
            {
                LoadCustomerLedger();
                LoadSupplierLedger();
            }
        }
    }

    private void BtnMakePayment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int supplierId)
        {
            var dialog = new PaymentDialog(supplierId, "supplier");
            if (dialog.ShowDialog() == true)
            {
                LoadCustomerLedger();
                LoadSupplierLedger();
            }
        }
    }
}

public class KhataEntry
{
    public DateTime Date { get; set; }
    public string CustomerName { get; set; } = "";
    public int CustomerId { get; set; }
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

public class SupplierKhataEntry
{
    public DateTime Date { get; set; }
    public string SupplierName { get; set; } = "";
    public int SupplierId { get; set; }
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}
