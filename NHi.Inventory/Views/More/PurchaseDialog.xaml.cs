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

    public PurchaseDialog()
    {
        InitializeComponent();
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
        txtTotal.Text = $"Total: Rs. {_items.Sum(i => i.Quantity * i.Price):N0}";
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PurchaseItemViewModel item)
        {
            _items.Remove(item); dgItems.ItemsSource = null; dgItems.ItemsSource = _items;
            txtTotal.Text = $"Total: Rs. {_items.Sum(i => i.Quantity * i.Price):N0}";
        }
    }

    private void BtnComplete_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0) { MessageBox.Show("Add items first"); return; }
        var total = _items.Sum(i => i.Quantity * i.Price);
        var purchase = new Purchase
        {
            InvoiceNumber = txtInvoice.Text,
            SupplierId = (cmbSupplier.SelectedItem as Supplier)?.Id,
            Subtotal = total, GrandTotal = total, PaidAmount = total, RemainingAmount = 0
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
