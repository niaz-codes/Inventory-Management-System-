using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class KhataView : UserControl
{
    private readonly CustomerService _customerService = new();
    private readonly SupplierService _supplierService = new();
    private readonly WhatsAppService _whatsApp = new();
    private List<Customer> _allCustomers = new();
    private bool _isInitialized;

    public KhataView()
    {
        InitializeComponent();
        _isInitialized = true;
        Loaded += KhataView_Loaded;
    }

    private List<Supplier> _allSuppliers = new();

    private void KhataView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadDashboard();
        LoadCustomerList();
        LoadSupplierDashboard();
        LoadSupplierList();
    }

    private void LoadDashboard()
    {
        var stats = _customerService.GetDashboardStats();
        txtStatTotalCustomers.Text = stats.TotalCustomers.ToString();
        txtStatWithBalance.Text = stats.CustomersWithBalance.ToString();
        txtStatReceivable.Text = $"Rs. {stats.TotalReceivable:N0}";
        txtStatTodayReceived.Text = $"Rs. {stats.TodayReceived:N0}";
        txtStatTodayCredit.Text = $"Rs. {stats.TodayCreditSales:N0}";
        txtStatOverdue.Text = stats.OverdueCustomers.ToString();
        txtStatNearLimit.Text = stats.NearLimitCustomers.ToString();
    }

    private void LoadCustomerList()
    {
        _allCustomers = _customerService.GetAll();
        ApplyCustomerFilterAndSort();
    }

    private void ApplyCustomerFilterAndSort()
    {
        if (!_isInitialized) return;

        var searchText = txtCustomerSearch?.Text ?? "";
        IEnumerable<Customer> source = string.IsNullOrWhiteSpace(searchText)
            ? _allCustomers
            : _customerService.Search(searchText);

        var filterTag = (cmbFilter?.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        var rows = source.Select(BuildRow).ToList();

        rows = filterTag switch
        {
            "WithBalance" => rows.Where(r => r.Balance > 0).ToList(),
            "Paid" => rows.Where(r => r.Balance == 0).ToList(),
            "Overdue" => rows.Where(r => r.StatusKey == "Overdue").ToList(),
            "Advance" => rows.Where(r => r.Balance < 0).ToList(),
            _ => rows
        };

        var sortTag = (cmbSort?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Name";
        rows = sortTag switch
        {
            "HighestBalance" => rows.OrderByDescending(r => r.Balance).ToList(),
            "LowestBalance" => rows.OrderBy(r => r.Balance).ToList(),
            "Recent" => rows.OrderByDescending(r => _customerService.GetLastTransactionDate(r.Id) ?? DateTime.MinValue).ToList(),
            _ => rows.OrderBy(r => r.Name).ToList()
        };

        dgCustomers.ItemsSource = rows;
    }

    private CustomerRow BuildRow(Customer c)
    {
        var (overdueAmount, _) = _customerService.GetOverdueInfo(c.Id);
        string statusKey = c.CurrentBalance == 0 ? "Paid"
            : c.CurrentBalance < 0 ? "Advance"
            : overdueAmount > 0 ? "Overdue"
            : "Balance";

        return new CustomerRow
        {
            Id = c.Id,
            Name = c.Name,
            Phone = c.Phone ?? "",
            Balance = c.CurrentBalance,
            StatusKey = statusKey,
            StatusText = LanguageManager.GetString("Status" + statusKey)
        };
    }

    private void LoadSupplierDashboard()
    {
        var stats = _supplierService.GetSupplierDashboardStats();
        txtStatTotalSuppliers.Text = stats.TotalSuppliers.ToString();
        txtStatSuppliersWithBalance.Text = stats.SuppliersWithBalance.ToString();
        txtStatTotalPayables.Text = $"Rs. {stats.TotalPayables:N0}";
        txtStatTodayPaid.Text = $"Rs. {stats.TodayPaid:N0}";
    }

    private void LoadSupplierList()
    {
        _allSuppliers = _supplierService.GetAll();
        ApplySupplierFilterAndSort();
    }

    private void ApplySupplierFilterAndSort()
    {
        if (!_isInitialized) return;

        var searchText = txtSupplierSearch?.Text ?? "";
        IEnumerable<Supplier> source = string.IsNullOrWhiteSpace(searchText)
            ? _allSuppliers
            : _supplierService.Search(searchText);

        var filterTag = (cmbSupplierFilter?.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        var rows = source.Select(BuildSupplierRow).ToList();

        rows = filterTag switch
        {
            "WithBalance" => rows.Where(r => r.Balance > 0).ToList(),
            "Paid" => rows.Where(r => r.Balance == 0).ToList(),
            "Advance" => rows.Where(r => r.Balance < 0).ToList(),
            _ => rows
        };

        var sortTag = (cmbSupplierSort?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Name";
        rows = sortTag switch
        {
            "HighestBalance" => rows.OrderByDescending(r => r.Balance).ToList(),
            "LowestBalance" => rows.OrderBy(r => r.Balance).ToList(),
            "Recent" => rows.OrderByDescending(r => _supplierService.GetLastTransactionDate(r.Id) ?? DateTime.MinValue).ToList(),
            _ => rows.OrderBy(r => r.Name).ToList()
        };

        dgSuppliers.ItemsSource = rows;
    }

    private SupplierRow BuildSupplierRow(Supplier s)
    {
        string statusKey = s.CurrentBalance == 0 ? "Paid" : s.CurrentBalance < 0 ? "Advance" : "Balance";
        return new SupplierRow
        {
            Id = s.Id,
            Name = s.Name,
            Phone = s.Phone ?? "",
            Balance = s.CurrentBalance,
            StatusKey = statusKey,
            StatusText = LanguageManager.GetString("Status" + statusKey)
        };
    }

    private void TxtCustomerSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyCustomerFilterAndSort();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyCustomerFilterAndSort();

    private void TxtSupplierSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplySupplierFilterAndSort();

    private void SupplierFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplySupplierFilterAndSort();

    private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new POS.CustomerDialog();
        if (dialog.ShowDialog() == true) { LoadDashboard(); LoadCustomerList(); }
    }

    private void BtnAddSupplier_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SupplierDialog();
        if (dialog.ShowDialog() == true) { LoadSupplierDashboard(); LoadSupplierList(); }
    }

    private void OpenSupplierKhata(int supplierId)
    {
        var dialog = new SupplierKhataView(supplierId) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        if (dialog.DataChanged) { LoadSupplierDashboard(); LoadSupplierList(); }
    }

    private void DgSuppliers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgSuppliers.SelectedItem is SupplierRow row) OpenSupplierKhata(row.Id);
    }

    private void BtnViewSupplierKhata_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int supplierId) OpenSupplierKhata(supplierId);
    }

    /// <summary>Opens a prefilled WhatsApp chat with this supplier's Khata balance via the
    /// wa.me Click-to-Chat link — no auto-send, no API. The button is disabled for the
    /// duration of the call so a double-click can't open the link twice.</summary>
    private void BtnSupplierReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int supplierId) return;
        var supplier = _supplierService.GetById(supplierId);
        if (supplier == null) return;

        btn.IsEnabled = false;
        try
        {
            var result = _whatsApp.SendSupplierKhataReminder(supplier);
            MessageBox.Show(
                result.Success ? LanguageManager.GetString("WhatsAppOpenedStatus") : result.ErrorMessage,
                LanguageManager.GetString("PaymentReminder"),
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally { btn.IsEnabled = true; }
    }

    private void OpenCustomerKhata(int customerId)
    {
        var dialog = new CustomerKhataView(customerId) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        if (dialog.DataChanged) { LoadDashboard(); LoadCustomerList(); }
    }

    private void DgCustomers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgCustomers.SelectedItem is CustomerRow row) OpenCustomerKhata(row.Id);
    }

    private void BtnViewKhata_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int customerId) OpenCustomerKhata(customerId);
    }

    private void BtnReceivePayment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int customerId)
        {
            var dialog = new ReceivePaymentDialog(customerId) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true) { LoadDashboard(); LoadCustomerList(); }
        }
    }

    /// <summary>Opens a prefilled WhatsApp chat with this customer's Khata balance via the
    /// wa.me Click-to-Chat link — no auto-send, no API. The button is disabled for the
    /// duration of the call so a double-click can't open the link twice.</summary>
    private void BtnSendReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int customerId) return;
        var customer = _customerService.GetById(customerId);
        if (customer == null) return;

        btn.IsEnabled = false;
        try
        {
            var result = _whatsApp.SendKhataReminder(customer);
            MessageBox.Show(
                result.Success ? LanguageManager.GetString("WhatsAppOpenedStatus") : result.ErrorMessage,
                LanguageManager.GetString("SendReminder"),
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally { btn.IsEnabled = true; }
    }

    private void BtnMakePayment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int supplierId)
        {
            var dialog = new PaySupplierDialog(supplierId) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true) { LoadSupplierDashboard(); LoadSupplierList(); }
        }
    }

    private void BtnExportCustomerLedger_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "CustomerKhata.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportCustomerLedger(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }

    private void BtnExportSupplierLedger_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "SupplierLedger.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportSupplierLedger(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
}

public class CustomerRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal Balance { get; set; }
    public string StatusKey { get; set; } = "";
    public string StatusText { get; set; } = "";
}

public class SupplierRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal Balance { get; set; }
    public string StatusKey { get; set; } = "";
    public string StatusText { get; set; } = "";
}

public class KhataStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = (value as string) switch
        {
            "Paid" => "#4CAF50",
            "Overdue" => "#F44336",
            "Advance" => "#2196F3",
            _ => "#FF9800"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
