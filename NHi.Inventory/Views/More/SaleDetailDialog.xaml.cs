using System.Windows;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class SaleDetailDialog : Window
{
    private readonly Sale _sale;

    public SaleDetailDialog(Sale sale)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _sale = sale;

        txtInvoice.Text = sale.InvoiceNumber;
        txtDate.Text = sale.SaleDate.ToString("dd-MMM-yyyy hh:mm tt");
        txtCustomer.Text = $"{LanguageManager.GetString("Customer")}: {sale.Customer?.Name ?? LanguageManager.GetString("WalkInCustomer")}";
        txtMethod.Text = $"{LanguageManager.GetString("PaymentMethod")}: {sale.PaymentMethod}";

        dgItems.ItemsSource = sale.Items.Select(i => new SaleDetailLine
        {
            ProductName = i.Product?.NameEnglish ?? "",
            Quantity = i.Quantity,
            Price = i.UnitPrice,
            DiscountAmount = i.DiscountAmount,
            Total = i.Total
        }).ToList();

        txtSubtotal.Text = $"Rs. {sale.Subtotal:N0}";
        txtGrandTotal.Text = $"Rs. {sale.GrandTotal:N0}";
        txtPaid.Text = $"Rs. {sale.PaidAmount:N0}";
        txtRemaining.Text = $"Rs. {sale.RemainingAmount:N0}";

        if (sale.CustomerId.HasValue) btnViewKhata.Visibility = Visibility.Visible;
    }

    private void BtnViewKhata_Click(object sender, RoutedEventArgs e)
    {
        if (!_sale.CustomerId.HasValue) return;
        var dialog = new Khata.CustomerKhataView(_sale.CustomerId.Value) { Owner = this };
        dialog.ShowDialog();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}

public class SaleDetailLine
{
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
}
