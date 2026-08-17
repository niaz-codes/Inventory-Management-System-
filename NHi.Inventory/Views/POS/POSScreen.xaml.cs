using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Views.POS;

public partial class POSScreen : UserControl
{
    private readonly ProductService _productService = new();
    private readonly SalesService _salesService = new();
    private readonly CustomerService _customerService = new();
    private List<CartViewModel> _cartItems = new();
    private List<Product> _allProducts = new();
    private bool _isInitialized;

    private static readonly List<HeldSale> _heldSales = new();

    private Sale? _lastCompletedSale;
    private Customer? _lastCompletedSaleCustomer;
    private List<ReceiptLineItem>? _lastCompletedSaleLines;

    public POSScreen()
    {
        InitializeComponent();
        _isInitialized = true;
        Loaded += POSScreen_Loaded;
        UpdateHeldSalesButton();
    }

    private void POSScreen_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProducts();
        LoadCustomers();
    }

    private void LoadProducts(string? categoryFilter = null)
    {
        _allProducts = _productService.GetAll();
        FilterProducts(txtSearch.Text, categoryFilter);
    }

    private void LoadCustomers()
    {
        var customers = _customerService.GetAll();
        var walkIn = new Customer { Id = 0, Name = LanguageManager.GetString("WalkInCustomer") };
        cmbCustomer.Items.Add(walkIn);
        foreach (var c in customers) cmbCustomer.Items.Add(c);
        cmbCustomer.SelectedIndex = 0;
    }

    private void FilterProducts(string searchText, string? categoryFilter = null)
    {
        var filtered = _allProducts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var text = searchText.ToLower();
            filtered = filtered.Where(p =>
                p.NameEnglish.ToLower().Contains(text) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(text)) ||
                (p.SKU != null && p.SKU.ToLower().Contains(text)));
        }
        if (!string.IsNullOrEmpty(categoryFilter))
            filtered = filtered.Where(p => p.Category?.Name == categoryFilter);

        productCardsPanel.Items.Clear();
        foreach (var product in filtered)
        {
            var card = CreateProductCard(product);
            productCardsPanel.Items.Add(card);
        }
    }

    private Button CreateProductCard(Product product)
    {
        var btn = new Button
        {
            Style = (Style)FindResource("ProductCardBtn"),
            Tag = product,
            ToolTip = product.NameEnglish
        };

        var panel = new StackPanel { Width = 135 };

        var iconBorder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(227, 242, 253)),
            CornerRadius = new CornerRadius(8),
            Height = 55,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var icon = new TextBlock
        {
            Text = "📦",
            FontSize = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = icon;
        panel.Children.Add(iconBorder);

        var nameBlock = new TextBlock
        {
            Text = product.NameEnglish,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 135,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 33, 33)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        panel.Children.Add(nameBlock);

        var priceBlock = new TextBlock
        {
            Text = $"Rs. {product.SalePrice:N0}",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 101, 192)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };
        panel.Children.Add(priceBlock);

        var stockPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var stockLabel = new TextBlock
        {
            Text = $"Stock: {product.CurrentStock}",
            FontSize = 11,
            Foreground = product.CurrentStock <= product.MinimumStock ?
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(117, 117, 117))
        };
        stockPanel.Children.Add(stockLabel);
        panel.Children.Add(stockPanel);

        btn.Content = panel;
        btn.Click += ProductCard_Click;
        return btn;
    }

    private void ProductCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Product product)
        {
            AddToCart(product);
        }
    }

    private void AddToCart(Product product)
    {
        var existing = _cartItems.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            _cartItems.Add(new CartViewModel
            {
                ProductId = product.Id,
                ProductName = product.NameEnglish,
                Quantity = 1,
                Price = product.SalePrice,
                PurchasePriceAtSale = product.PurchasePrice
            });
        }
        UpdateCartDisplay();
    }

    private void UpdateCartDisplay()
    {
        dgCart.ItemsSource = null;
        dgCart.ItemsSource = _cartItems;
        txtEmptyCart.Visibility = _cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtCartCount.Text = $"{_cartItems.Count} items";

        var subtotal = _cartItems.Sum(c => c.Quantity * c.Price);
        var discountPercent = decimal.TryParse(txtDiscountPercent.Text, out var dp) ? dp : 0;
        var discountAmount = subtotal * discountPercent / 100;
        var grandTotal = subtotal - discountAmount;

        txtSubtotal.Text = $"Rs. {subtotal:N0}";
        txtDiscountAmount.Text = $"- Rs. {discountAmount:N0}";
        txtGrandTotal.Text = $"Rs. {grandTotal:N0}";

        UpdatePaymentDisplay();
    }

    private void UpdatePaymentDisplay()
    {
        var subtotal = _cartItems.Sum(c => c.Quantity * c.Price);
        var discountPercent = decimal.TryParse(txtDiscountPercent.Text, out var dp) ? dp : 0;
        var discountAmount = subtotal * discountPercent / 100;
        var grandTotal = subtotal - discountAmount;
        var paid = decimal.TryParse(txtPaidAmount.Text, out var p) ? p : 0;

        if (rbCredit.IsChecked == true)
        {
            txtPaidAmount.Text = "0";
            pnlRemaining.Visibility = Visibility.Visible;
            txtRemaining.Text = $"Rs. {grandTotal:N0}";
        }
        else if (rbPartial.IsChecked == true)
        {
            pnlRemaining.Visibility = Visibility.Visible;
            var remaining = grandTotal - paid;
            txtRemaining.Text = $"Rs. {remaining:N0}";
        }
        else
        {
            pnlRemaining.Visibility = Visibility.Collapsed;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterProducts(txtSearch.Text);
    }

    private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var barcode = txtBarcode.Text.Trim();
            if (string.IsNullOrEmpty(barcode)) return;

            var product = _productService.GetByBarcode(barcode);
            if (product != null)
            {
                AddToCart(product);
                txtBarcode.Text = "";
            }
            txtBarcode.Focus();
        }
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            FilterProducts(txtSearch.Text, btn.Tag?.ToString());
        }
    }

    private void TxtDiscount_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateCartDisplay();
    }

    private void PaymentMethod_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdatePaymentDisplay();
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CartViewModel item)
        {
            _cartItems.Remove(item);
            UpdateCartDisplay();
        }
    }

    private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomerDialog();
        if (dialog.ShowDialog() == true)
        {
            LoadCustomers();
        }
    }

    private void BtnCompleteSale_Click(object sender, RoutedEventArgs e)
    {
        if (_cartItems.Count == 0)
        {
            MessageBox.Show(LanguageManager.GetString("EmptyCart"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var subtotal = _cartItems.Sum(c => c.Quantity * c.Price);
        var discountPercent = decimal.TryParse(txtDiscountPercent.Text, out var dp) ? dp : 0;
        var discountAmount = subtotal * discountPercent / 100;
        var grandTotal = subtotal - discountAmount;
        var paid = decimal.TryParse(txtPaidAmount.Text, out var p) ? p : 0;
        var isCredit = rbCredit.IsChecked == true;
        var isPartial = rbPartial.IsChecked == true;

        if (isPartial && paid <= 0)
        {
            MessageBox.Show("Please enter paid amount", "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (paid > grandTotal)
        {
            paid = grandTotal;
        }

        var customer = cmbCustomer.SelectedItem as Customer;
        var remaining = grandTotal - paid;

        var sale = new Sale
        {
            InvoiceNumber = _salesService.GenerateInvoiceNumber(),
            CustomerId = customer?.Id > 0 ? customer.Id : null,
            Subtotal = subtotal,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            GrandTotal = grandTotal,
            PaidAmount = paid,
            RemainingAmount = remaining,
            PaymentMethod = isCredit ? "Credit" : isPartial ? "Partial" : "Cash",
            IsCreditSale = isCredit || isPartial
        };

        var items = _cartItems.Select(c => new SaleItem
        {
            ProductId = c.ProductId,
            Quantity = c.Quantity,
            UnitPrice = c.Price,
            PurchasePriceAtSale = c.PurchasePriceAtSale
        }).ToList();

        try
        {
            var saleId = _salesService.CompleteSale(sale, items);
            sale.Id = saleId;

            var lines = _cartItems.Select(c => new ReceiptLineItem
            {
                ProductName = c.ProductName,
                Quantity = c.Quantity,
                Price = c.Price
            }).ToList();
            _lastCompletedSale = sale;
            _lastCompletedSaleCustomer = customer;
            _lastCompletedSaleLines = lines;

            var result = MessageBox.Show(
                $"{LanguageManager.GetString("SaleSuccessful")}\n\nInvoice: {sale.InvoiceNumber}\nTotal: Rs. {grandTotal:N0}\nPaid: Rs. {paid:N0}\n\n{LanguageManager.GetString("PrintReceipt")}?",
                LanguageManager.GetString("Success"),
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes && !ReceiptService.PrintReceipt(sale, customer, lines))
            {
                MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _cartItems.Clear();
            UpdateCartDisplay();
            LoadProducts();
            txtPaidAmount.Text = "0";
            txtDiscountPercent.Text = "0";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{LanguageManager.GetString("ErrorOccurred")}: {ex.Message}",
                LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---- Keyboard-shortcut entry points (invoked from MainWindow's global key handler) ----

    public void FocusSearch()
    {
        txtSearch.Focus();
        txtSearch.SelectAll();
    }

    public void FocusCustomer()
    {
        cmbCustomer.Focus();
        cmbCustomer.IsDropDownOpen = true;
    }

    public void FocusPayment()
    {
        txtPaidAmount.Focus();
        txtPaidAmount.SelectAll();
    }

    public void FocusDiscount()
    {
        txtDiscountPercent.Focus();
        txtDiscountPercent.SelectAll();
    }

    public void TriggerCompleteSale()
    {
        BtnCompleteSale_Click(this, new RoutedEventArgs());
    }

    public void PrintLastReceipt()
    {
        if (_lastCompletedSale == null || _lastCompletedSaleLines == null)
        {
            MessageBox.Show(LanguageManager.GetString("NoDataFound"), "", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ReceiptService.PrintReceipt(_lastCompletedSale, _lastCompletedSaleCustomer, _lastCompletedSaleLines);
    }

    public void CancelCurrentSale()
    {
        if (_cartItems.Count == 0) return;
        if (MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _cartItems.Clear();
        UpdateCartDisplay();
        txtPaidAmount.Text = "0";
        txtDiscountPercent.Text = "0";
    }

    public void HoldCurrentSale()
    {
        if (_cartItems.Count == 0)
        {
            MessageBox.Show(LanguageManager.GetString("EmptyCart"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var customer = cmbCustomer.SelectedItem as Customer;
        _heldSales.Add(new HeldSale
        {
            Items = _cartItems.ToList(),
            CustomerId = customer?.Id > 0 ? customer.Id : null,
            CustomerName = customer?.Name ?? LanguageManager.GetString("WalkInCustomer"),
            DiscountPercent = decimal.TryParse(txtDiscountPercent.Text, out var dp) ? dp : 0,
            HeldAt = DateTime.Now
        });

        _cartItems.Clear();
        UpdateCartDisplay();
        txtPaidAmount.Text = "0";
        txtDiscountPercent.Text = "0";
        UpdateHeldSalesButton();
    }

    private void UpdateHeldSalesButton()
    {
        if (btnHeldSales == null) return;
        btnHeldSales.Visibility = _heldSales.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        btnHeldSales.Content = $"{LanguageManager.GetString("HoldSale")} ({_heldSales.Count})";
    }

    private void BtnHeldSales_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HeldSalesDialog(_heldSales);
        if (dialog.ShowDialog() == true && dialog.SelectedHeldSale != null)
        {
            var held = dialog.SelectedHeldSale;
            if (_cartItems.Count > 0)
                HoldCurrentSale();

            _heldSales.Remove(held);
            _cartItems = held.Items.ToList();
            foreach (var c in cmbCustomer.Items)
            {
                if (c is Customer cust && held.CustomerId.HasValue && cust.Id == held.CustomerId.Value)
                {
                    cmbCustomer.SelectedItem = c;
                    break;
                }
            }
            txtDiscountPercent.Text = held.DiscountPercent.ToString();
            UpdateCartDisplay();
            UpdateHeldSalesButton();
        }
        else if (dialog.DeletedAny)
        {
            UpdateHeldSalesButton();
        }
    }
}

public class CartViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal PurchasePriceAtSale { get; set; }
    public decimal Total => Quantity * Price;
}

public class HeldSale
{
    public List<CartViewModel> Items { get; set; } = new();
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public DateTime HeldAt { get; set; }
    public decimal Total => Items.Sum(i => i.Total);
}
