using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class PurchaseDialog : Window
{
    private readonly ProductService _productService = new();
    private readonly PurchaseService _purchaseService = new();
    private readonly SupplierService _supplierService = new();
    private List<PurchaseItemViewModel> _items = new();
    private bool _paidAmountEdited;

    public PurchaseDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        cmbSupplier.ItemsSource = _supplierService.GetAll();
        cmbProduct.ItemsSource = _productService.GetAll();
        txtInvoice.Text = _purchaseService.GenerateInvoiceNumber();
    }

    private void CmbProduct_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (cmbProduct.SelectedItem is Product p) txtPrice.Text = p.PurchasePrice.ToString();
    }

    private void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (cmbProduct.SelectedItem is not Product p) return;
        if (!decimal.TryParse(txtQty.Text, out var qty) || qty <= 0) return;
        if (!decimal.TryParse(txtPrice.Text, out var price)) return;
        _items.Add(new PurchaseItemViewModel { ProductId = p.Id, ProductName = p.NameEnglish, Quantity = qty, Price = price });
        dgItems.ItemsSource = null; dgItems.ItemsSource = _items;
        UpdateTotals();
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PurchaseItemViewModel item)
        {
            _items.Remove(item); dgItems.ItemsSource = null; dgItems.ItemsSource = _items;
            UpdateTotals();
        }
    }

    /// <summary>Refreshes the Total display and defaults Paid Amount to the new total — only
    /// while the user hasn't started typing their own Paid Amount, so adding/removing items
    /// after entering a partial payment doesn't silently overwrite it.</summary>
    private void UpdateTotals()
    {
        var total = _items.Sum(i => i.Quantity * i.Price);
        txtTotal.Text = $"Total: Rs. {total:N0}";
        if (!_paidAmountEdited)
        {
            txtPaidAmount.Text = total.ToString("0.##"); // fires TxtPaidAmount_TextChanged, which marks _paidAmountEdited true
            _paidAmountEdited = false; // ...undo that: this was a programmatic default, not a real user edit
        }
    }

    private void TxtPaidAmount_TextChanged(object sender, TextChangedEventArgs e)
    {
        _paidAmountEdited = true;
        var total = _items.Sum(i => i.Quantity * i.Price);
        var paid = decimal.TryParse(txtPaidAmount.Text, out var p) ? p : 0;
        var remaining = Math.Max(0, total - paid);
        txtRemainingPreview.Text = remaining > 0
            ? $"{LanguageManager.GetString("RemainingPayable")}: Rs. {remaining:N0}"
            : "";
    }

    private void BtnComplete_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) { MessageBox.Show("Add items first"); return; }
        var total = _items.Sum(i => i.Quantity * i.Price);

        // Blank/invalid Paid Amount defaults to paying in full, matching this dialog's
        // previous (pre-credit-purchase) always-fully-paid behavior.
        var paid = decimal.TryParse(txtPaidAmount.Text, out var p) ? p : total;
        paid = Math.Clamp(paid, 0, total);
        var remaining = total - paid;

        var purchase = new Purchase
        {
            InvoiceNumber = txtInvoice.Text,
            SupplierId = (cmbSupplier.SelectedItem as Supplier)?.Id,
            Subtotal = total, GrandTotal = total, PaidAmount = paid, RemainingAmount = remaining
        };
        var items = _items.Select(i => new PurchaseItem { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.Price }).ToList();
        _purchaseService.CompletePurchase(purchase, items);
        MessageBox.Show("Purchase saved!", "", MessageBoxButton.OK, MessageBoxImage.Information);
        this.DialogResult = true; this.Close();
    }
}

public class PurchaseItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total => Quantity * Price;
}
