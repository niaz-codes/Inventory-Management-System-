using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;
using NHi.Inventory.Views.Inventory;
using Microsoft.EntityFrameworkCore;

// Customer Search (POS Sale only): replaces the old eagerly-loaded "every customer in a
// ComboBox" picker with a search-as-you-type box backed by an in-memory snapshot of every
// active customer (_allCustomersCache, refreshed on load and whenever a customer is added).
// Focusing the empty box shows the COMPLETE list; typing even one character filters it live by
// Name / Phone / WhatsApp number. _selectedCustomer == null directly means walk-in, replacing
// the old fake Customer{Id=0} sentinel - every existing "customer?.Id > 0" check downstream
// still works unchanged.

namespace NHi.Inventory.Views.POS;

public partial class POSScreen : UserControl
{
    private readonly ProductService _productService = new();
    private readonly SalesService _salesService = new();
    private readonly CustomerService _customerService = new();
    private List<CartViewModel> _cartItems = new();
    private List<Product> _allProducts = new();
    private bool _isInitialized;
    private bool _invoiceDiscountIsFixed;
    private string? _activeCategoryFilter = "";
    private bool _isProcessingSale;
    private readonly DispatcherTimer _searchDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _customerSearchDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private Customer? _selectedCustomer;
    // In-memory snapshot of every active customer so the search box can show the full list on
    // focus and filter instantly on each keystroke without a DB round-trip.
    private List<Customer> _allCustomersCache = new();

    private static readonly List<HeldSale> _heldSales = new();

    private Sale? _lastCompletedSale;
    private Customer? _lastCompletedSaleCustomer;
    private List<ReceiptLineItem>? _lastCompletedSaleLines;

    public POSScreen()
    {
        InitializeComponent();
        _isInitialized = true;
        UpdateDiscountModeVisuals();
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        _customerSearchDebounceTimer.Tick += CustomerSearchDebounceTimer_Tick;
        Loaded += POSScreen_Loaded;
        UpdateHeldSalesButton();
    }

    private void POSScreen_Loaded(object sender, RoutedEventArgs e)
    {
        LoadCategoryFilterButtons();
        LoadProducts();
        LoadCustomersCache();
        cartColumn.Width = new GridLength(AppPreferences.PosCartWidth);
        // The barcode scanner acts as a keyboard — keep its input box focused so the cashier
        // can scan immediately without clicking anything.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FocusBarcode));
    }

    /// <summary>Refreshes the in-memory list of active customers that backs the search box.</summary>
    private void LoadCustomersCache() => _allCustomersCache = _customerService.GetAll();

    /// <summary>POS-wide shortcuts that must work even while a text box (e.g. the barcode box)
    /// has focus, so they use PreviewKeyDown (tunnels before the text box consumes the key).</summary>
    private void POSScreen_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                FocusBarcode();
                e.Handled = true;
                break;
            case Key.F4:
                if (!_isProcessingSale) TriggerCompleteSale();
                e.Handled = true;
                break;
            case Key.Escape when txtBarcode.IsKeyboardFocused:
                txtBarcode.Clear();
                e.Handled = true;
                break;
        }
    }

    public void FocusBarcode()
    {
        txtBarcode.Focus();
        txtBarcode.SelectAll();
    }

    private void LoadProducts()
    {
        _allProducts = _productService.GetAll();
        FilterProducts(txtSearch.Text, _activeCategoryFilter);
    }

    /// <summary>Attaches an existing customer record to the current sale (never creates a new
    /// one) and switches the customer bar from search mode to the compact "selected" chip.</summary>
    private void SelectCustomer(Customer customer)
    {
        _selectedCustomer = customer;
        popupCustomerResults.IsOpen = false;
        txtCustomerSearch.Text = "";

        txtSelectedCustomerName.Text = customer.Name;
        txtSelectedCustomerPhone.Text = customer.Phone ?? "";
        txtSelectedCustomerPhone.Visibility = string.IsNullOrWhiteSpace(customer.Phone) ? Visibility.Collapsed : Visibility.Visible;
        txtPrevBalance.Text = customer.CurrentBalance != 0
            ? $"{LanguageManager.GetString("PreviousBalance")}: Rs. {customer.CurrentBalance:N0}"
            : "";

        pnlCustomerSearch.Visibility = Visibility.Collapsed;
        pnlCustomerSelected.Visibility = Visibility.Visible;
    }

    /// <summary>Detaches the customer from the sale (back to walk-in) and returns the bar to search mode.</summary>
    private void ClearSelectedCustomer()
    {
        _selectedCustomer = null;
        txtPrevBalance.Text = "";
        pnlCustomerSelected.Visibility = Visibility.Collapsed;
        pnlCustomerSearch.Visibility = Visibility.Visible;
    }

    private void TxtCustomerSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _customerSearchDebounceTimer.Stop();
        _customerSearchDebounceTimer.Start();
    }

    private void TxtCustomerSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        RunCustomerSearch();
        popupCustomerResults.IsOpen = true;
    }

    /// <summary>A click on the field always (re)opens the full list — even when the field is
    /// already focused, so GotFocus wouldn't fire. Deferred to Background priority so it runs
    /// after the click's own mouse-up, which would otherwise dismiss the just-opened popup.</summary>
    private void TxtCustomerSearch_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RunCustomerSearch));
    }

    private void TxtCustomerSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            popupCustomerResults.IsOpen = false;
        }
        else if (e.Key == Key.Enter)
        {
            // Fast path for a shop scanning/typing an exact phone number or Customer ID: Enter
            // picks the (usually only) match instead of requiring a mouse click.
            if (lstCustomerResults.Items.Count > 0 && lstCustomerResults.Items[0] is CustomerSearchResult first)
                SelectCustomer(first.Customer);
            e.Handled = true;
        }
    }

    private void CustomerSearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _customerSearchDebounceTimer.Stop();
        RunCustomerSearch();
    }

    /// <summary>Filters the in-memory customer snapshot and fills the results popup. An empty box
    /// shows the complete list; one or more characters filter live by Name / Phone / WhatsApp
    /// number (case-insensitive, substring). Runs off _allCustomersCache so there's no DB hit per
    /// keystroke - the popup stays instant even with a large customer base.</summary>
    private void RunCustomerSearch()
    {
        if (_allCustomersCache.Count == 0) LoadCustomersCache();

        var searchText = txtCustomerSearch.Text.Trim();
        bool hasQuery = searchText.Length > 0;

        IEnumerable<Customer> matches = _allCustomersCache;
        if (hasQuery)
        {
            var t = searchText.ToLowerInvariant();
            matches = _allCustomersCache.Where(c =>
                (!string.IsNullOrEmpty(c.Name) && c.Name.ToLowerInvariant().Contains(t)) ||
                (!string.IsNullOrEmpty(c.Phone) && c.Phone.ToLowerInvariant().Contains(t)) ||
                (!string.IsNullOrEmpty(c.WhatsAppNumber) && c.WhatsAppNumber.ToLowerInvariant().Contains(t)));
        }

        var results = matches.Select(c => new CustomerSearchResult
        {
            Customer = c,
            Name = c.Name,
            Phone = c.Phone ?? "",
            BalanceText = c.CurrentBalance == 0 ? LanguageManager.GetString("StatusPaid") : $"Rs. {c.CurrentBalance:N0}",
            BalanceBrush = c.CurrentBalance > 0 ? _balanceOwedBrush : c.CurrentBalance < 0 ? _balanceAdvanceBrush : _balanceZeroBrush
        }).ToList();

        lstCustomerResults.ItemsSource = results;
        // "No customer found" only makes sense once something has been typed - an empty list on an
        // empty box just means this shop has no customers yet.
        txtNoCustomerResults.Visibility = results.Count == 0 && hasQuery ? Visibility.Visible : Visibility.Collapsed;
        popupCustomerResults.IsOpen = true;
    }

    private static readonly SolidColorBrush _balanceOwedBrush = new((Color)ColorConverter.ConvertFromString("#E65100"));
    private static readonly SolidColorBrush _balanceAdvanceBrush = new((Color)ColorConverter.ConvertFromString("#1565C0"));
    private static readonly SolidColorBrush _balanceZeroBrush = new((Color)ColorConverter.ConvertFromString("#4CAF50"));

    private void CustomerResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CustomerSearchResult result)
            SelectCustomer(result.Customer);
    }

    private void BtnChangeCustomer_Click(object sender, RoutedEventArgs e)
    {
        ClearSelectedCustomer();
        txtCustomerSearch.Focus();
    }

    private void LoadCategoryFilterButtons()
    {
        categoryFilterPanel.Children.Clear();
        categoryFilterPanel.Children.Add(CreateCategoryButton(LanguageManager.GetString("All"), "", true));
        foreach (var cat in _productService.GetCategories())
            categoryFilterPanel.Children.Add(CreateCategoryButton(cat.Name, cat.Name, false));
    }

    private Button CreateCategoryButton(string label, string tag, bool isActive)
    {
        var btn = new Button
        {
            Content = label,
            Tag = tag,
            Style = (Style)FindResource("CategoryFilterBtn")
        };
        if (isActive)
        {
            btn.SetResourceReference(Button.BackgroundProperty, "PrimaryBrush");
            btn.Foreground = Brushes.White;
        }
        btn.Click += CategoryFilter_Click;
        return btn;
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked) return;
        foreach (var child in categoryFilterPanel.Children)
        {
            if (child is not Button b) continue;
            b.SetResourceReference(Button.BackgroundProperty, "HoverBrush");
            b.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
        }
        clicked.SetResourceReference(Button.BackgroundProperty, "PrimaryBrush");
        clicked.Foreground = Brushes.White;

        _activeCategoryFilter = clicked.Tag?.ToString() ?? "";
        FilterProducts(txtSearch.Text, _activeCategoryFilter);
    }

    private void FilterProducts(string searchText, string? categoryFilter = null)
    {
        var filtered = _allProducts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var text = searchText.ToLower();
            filtered = filtered.Where(p =>
                p.NameEnglish.ToLower().Contains(text) ||
                (p.NameUrdu != null && p.NameUrdu.Contains(searchText)) ||
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
            CornerRadius = new CornerRadius(8),
            Height = 55,
            Margin = new Thickness(0, 0, 0, 8),
            ClipToBounds = true
        };
        iconBorder.SetResourceReference(Border.BackgroundProperty, "InfoBgBrush");
        if (TryLoadProductImage(product.ImageData) is BitmapImage bmp)
        {
            iconBorder.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
        }
        else
        {
            var placeholderIcon = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = MaterialDesignThemes.Wpf.PackIconKind.PackageVariantClosed,
                Width = 26,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            placeholderIcon.SetResourceReference(MaterialDesignThemes.Wpf.PackIcon.ForegroundProperty, "TextSecondaryBrush");
            iconBorder.Child = placeholderIcon;
        }
        panel.Children.Add(iconBorder);

        var displayName = LanguageManager.IsRTL && !string.IsNullOrWhiteSpace(product.NameUrdu) ? product.NameUrdu : product.NameEnglish;
        var nameBlock = new TextBlock
        {
            Text = displayName,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 135,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
            FlowDirection = LanguageManager.CurrentFlowDirection
        };
        nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        panel.Children.Add(nameBlock);

        var priceBlock = new TextBlock
        {
            Text = $"Rs. {product.SalePrice:N0}",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };
        priceBlock.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");
        panel.Children.Add(priceBlock);

        var stockPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var unitSuffix = product.Unit?.Symbol;
        var stockLabel = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(unitSuffix) ? $"Stock: {product.CurrentStock:0.##}" : $"Stock: {product.CurrentStock:0.##} {unitSuffix}",
            FontSize = 11
        };
        stockLabel.SetResourceReference(TextBlock.ForegroundProperty,
            product.CurrentStock <= product.MinimumStock ? "DangerBrush" : "TextSecondaryBrush");
        stockPanel.Children.Add(stockLabel);
        panel.Children.Add(stockPanel);

        btn.Content = panel;
        btn.Click += ProductCard_Click;
        return btn;
    }

    private static BitmapImage? TryLoadProductImage(byte[]? data)
    {
        if (data is not { Length: > 0 }) return null;
        try
        {
            using var ms = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void ProductCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Product product)
        {
            var row = AddToCart(product);
            if (row != null) HighlightCartRow(row);
            // Manual add still leaves the scanner ready for the next scan.
            FocusBarcode();
        }
    }

    /// <summary>Adds one unit of <paramref name="product"/> to the cart — incrementing the
    /// existing row instead of creating a duplicate — and returns the affected cart row (or
    /// null if the add was rejected by a stock / expiry rule).</summary>
    private CartViewModel? AddToCart(Product product)
    {
        var existing = _cartItems.FirstOrDefault(c => c.ProductId == product.Id);
        var currentCartQty = existing?.Quantity ?? 0;

        using var settingsDb = new AppDbContext();
        var shop = settingsDb.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);

        var allowNegative = shop?.AllowNegativeStock == true;
        if (!allowNegative && currentCartQty + 1 > product.CurrentStock)
        {
            MessageBox.Show(LanguageManager.GetString("InsufficientStock"), LanguageManager.GetString("Warning"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (shop?.BlockExpiredSale == true && product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date < DateTime.Today)
        {
            MessageBox.Show(LanguageManager.GetString("ProductExpiredBlocked"), LanguageManager.GetString("Warning"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            existing = new CartViewModel
            {
                ProductId = product.Id,
                ProductName = product.NameEnglish,
                Quantity = 1,
                Price = product.SalePrice,
                PurchasePriceAtSale = product.PurchasePrice
            };
            _cartItems.Add(existing);
        }
        UpdateCartDisplay();
        return existing;
    }

    /// <summary>Selects, scrolls to, and briefly flashes the cart row for a just-added product.</summary>
    private void HighlightCartRow(CartViewModel vm)
    {
        dgCart.SelectedItem = vm;
        dgCart.ScrollIntoView(vm);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (dgCart.ItemContainerGenerator.ContainerFromItem(vm) is not DataGridRow row) return;
            var brush = new SolidColorBrush(Colors.Transparent);
            row.Background = brush;
            var flash = (Color)ColorConverter.ConvertFromString("#FFF59D");
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(flash, Colors.Transparent, TimeSpan.FromMilliseconds(900))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
        }));
    }

    private (decimal subtotal, decimal discountAmount, decimal grandTotal) CalculateTotals()
    {
        var subtotal = _cartItems.Sum(c => c.Total);
        decimal discountAmount;
        if (_invoiceDiscountIsFixed)
        {
            decimal.TryParse(txtDiscountPercent.Text, out var fixedAmt);
            discountAmount = Math.Clamp(fixedAmt, 0, subtotal);
        }
        else
        {
            decimal.TryParse(txtDiscountPercent.Text, out var pct);
            pct = Math.Clamp(pct, 0, 100);
            discountAmount = subtotal * pct / 100;
        }
        var grandTotal = subtotal - discountAmount;
        return (subtotal, discountAmount, grandTotal);
    }

    private void UpdateCartDisplay()
    {
        dgCart.ItemsSource = null;
        dgCart.ItemsSource = _cartItems;
        txtEmptyCart.Visibility = _cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        txtCartCount.Text = $"{_cartItems.Count} items";

        var (subtotal, discountAmount, grandTotal) = CalculateTotals();
        txtSubtotal.Text = $"Rs. {subtotal:N0}";
        txtDiscountAmount.Text = $"- Rs. {discountAmount:N0}";
        txtGrandTotal.Text = $"Rs. {grandTotal:N0}";

        UpdatePaymentDisplay();
    }

    private void UpdatePaymentDisplay()
    {
        var (_, _, grandTotal) = CalculateTotals();
        var paid = decimal.TryParse(txtPaidAmount.Text, out var p) ? p : 0;

        if (rbCredit.IsChecked == true)
        {
            txtPaidAmount.Text = "0";
            pnlRemaining.Visibility = Visibility.Visible;
            pnlDueDate.Visibility = Visibility.Visible;
            txtRemaining.Text = $"Rs. {grandTotal:N0}";
        }
        else if (rbPartial.IsChecked == true)
        {
            pnlRemaining.Visibility = Visibility.Visible;
            pnlDueDate.Visibility = Visibility.Visible;
            var remaining = grandTotal - paid;
            txtRemaining.Text = $"Rs. {remaining:N0}";
        }
        else
        {
            pnlRemaining.Visibility = Visibility.Collapsed;
            pnlDueDate.Visibility = Visibility.Collapsed;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        FilterProducts(txtSearch.Text, _activeCategoryFilter);
    }

    private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        var barcode = txtBarcode.Text.Trim();
        txtBarcode.Clear();
        ProcessScannedBarcode(barcode, fromCamera: false);
    }

    /// <summary>Shared by the physical scanner (keyboard-wedge) and the camera scanner: look the
    /// barcode up, add to cart (or bump quantity if already there), and give audio + on-screen
    /// feedback. An unknown barcode offers the "add new product" dialog.</summary>
    private Window? _cameraScanWindow;

    private void ProcessScannedBarcode(string barcode, bool fromCamera)
    {
        if (string.IsNullOrEmpty(barcode)) { if (!fromCamera) FocusBarcode(); return; }

        var product = _productService.GetByBarcode(barcode);
        if (product != null)
        {
            var row = AddToCart(product);
            if (row != null)
            {
                HighlightCartRow(row);
                try { SystemSounds.Beep.Play(); } catch { /* audio is best-effort */ }
                ShowScanInfo(
                    $"{product.NameEnglish}   •   Rs. {product.SalePrice:N0}   •   " +
                    $"{LanguageManager.GetString("CurrentStock")}: {product.CurrentStock:N0}   •   " +
                    $"{LanguageManager.GetString("QtyLabel")}: {row.Quantity:0.##}   •   {product.Barcode}",
                    found: true);
            }
            if (!fromCamera) FocusBarcode();
            return;
        }

        // Not found — beep + offer to add it.
        try { SystemSounds.Exclamation.Play(); } catch { }
        ShowScanInfo($"{LanguageManager.GetString("ProductNotFound")}: {barcode}", found: false);

        var owner = fromCamera && _cameraScanWindow != null ? _cameraScanWindow : Window.GetWindow(this);
        var dlg = new BarcodeNotFoundDialog(barcode) { Owner = owner };
        dlg.ShowDialog();

        if (dlg.Choice == BarcodeNotFoundChoice.AddNew)
        {
            var pd = new ProductDialog(new Product { Barcode = barcode }) { Owner = owner };
            if (pd.ShowDialog() == true)
            {
                LoadProducts();
                var created = _productService.GetByBarcode(barcode);
                if (created != null)
                {
                    var row = AddToCart(created);
                    if (row != null)
                    {
                        HighlightCartRow(row);
                        try { SystemSounds.Beep.Play(); } catch { }
                        ShowScanInfo($"{created.NameEnglish}   •   Rs. {created.SalePrice:N0}   •   {created.Barcode}", found: true);
                    }
                }
            }
        }

        if (!fromCamera) FocusBarcode();
    }

    // ---- Camera scan ----
    // Both input methods are always live: the barcode box takes a USB/Bluetooth/wireless
    // scanner (keyboard wedge), and this button opens the camera scanner. No mode toggle.

    private void BtnCameraScan_Click(object sender, RoutedEventArgs e) => OpenCameraScanner();

    /// <summary>Persist the cart width the cashier dragged to (clamped to the column's Min/Max).</summary>
    private void CartSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        AppPreferences.PosCartWidth = cartColumn.ActualWidth;
        cartColumn.Width = new GridLength(AppPreferences.PosCartWidth);
    }

    private void OpenCameraScanner()
    {
        if (_cameraScanWindow != null) { _cameraScanWindow.Activate(); return; }

        var win = new CameraScanWindow { Owner = Window.GetWindow(this) };
        win.BarcodeScanned += code => ProcessScannedBarcode(code, fromCamera: true);
        _cameraScanWindow = win;
        try { win.ShowDialog(); }
        finally { _cameraScanWindow = null; FocusBarcode(); }
    }

    private void ShowScanInfo(string text, bool found)
    {
        txtScanInfo.Text = text;
        scanInfoBorder.Visibility = Visibility.Visible;
        scanInfoBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, found ? "SuccessBgBrush" : "DangerBgBrush");
        txtScanInfo.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, found ? "SuccessTextBrush" : "DangerTextBrush");
    }

    private void TxtDiscount_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateCartDisplay();
    }

    private void UpdateDiscountModeVisuals()
    {
        SetDiscountModeButtonVisual(btnDiscountPercentMode, isActive: !_invoiceDiscountIsFixed);
        SetDiscountModeButtonVisual(btnDiscountFixedMode, isActive: _invoiceDiscountIsFixed);
    }

    private static void SetDiscountModeButtonVisual(Button btn, bool isActive)
    {
        if (isActive)
        {
            btn.SetResourceReference(Button.BackgroundProperty, "PrimaryBrush");
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = Brushes.Transparent;
            btn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
        }
    }

    private void SetDiscountMode(bool isFixed)
    {
        _invoiceDiscountIsFixed = isFixed;
        txtDiscountPercent.Text = "0";
        UpdateDiscountModeVisuals();
        UpdateCartDisplay();
    }

    private void BtnDiscountPercentMode_Click(object sender, RoutedEventArgs e) => SetDiscountMode(false);
    private void BtnDiscountFixedMode_Click(object sender, RoutedEventArgs e) => SetDiscountMode(true);

    private void PaymentMethod_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdatePaymentDisplay();
    }

    private void BtnQtyIncrease_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not CartViewModel item) return;
        var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
        if (product != null && item.Quantity + 1 > product.CurrentStock)
        {
            MessageBox.Show(LanguageManager.GetString("InsufficientStock"), LanguageManager.GetString("Warning"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        item.Quantity++;
        UpdateCartDisplay();
    }

    private void BtnQtyDecrease_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not CartViewModel item) return;
        if (item.Quantity > 1) item.Quantity--;
        UpdateCartDisplay();
    }

    private void TxtQty_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not CartViewModel item) return;
        var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
        var maxStock = product?.CurrentStock ?? decimal.MaxValue;

        if (!decimal.TryParse(tb.Text, out var qty) || qty <= 0) qty = 1;
        if (qty > maxStock)
        {
            qty = maxStock;
            MessageBox.Show(LanguageManager.GetString("InsufficientStock"), LanguageManager.GetString("Warning"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        item.Quantity = qty;
        UpdateCartDisplay();
    }

    private void TxtPrice_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not CartViewModel item) return;
        if (!decimal.TryParse(tb.Text, out var price) || price < 0) price = item.Price;
        item.Price = price;
        UpdateCartDisplay();
    }

    private void TxtItemDiscount_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not CartViewModel item) return;
        if (!decimal.TryParse(tb.Text, out var amt)) amt = 0;
        item.DiscountAmount = Math.Clamp(amt, 0, item.Price);
        UpdateCartDisplay();
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CartViewModel item)
        {
            _cartItems.Remove(item);
            UpdateCartDisplay();
        }
    }

    private void BtnClearCart_Click(object sender, RoutedEventArgs e) => CancelCurrentSale();

    private void BtnAddCustomer_Click(object sender, RoutedEventArgs e)
    {
        popupCustomerResults.IsOpen = false;
        var dialog = new CustomerDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.SavedCustomer != null)
        {
            // Newly added customer is attached to the sale immediately - the dialog is modal
            // over POS, so the cashier never leaves this screen and the sale continues normally.
            LoadCustomersCache();
            SelectCustomer(dialog.SavedCustomer);
        }
    }

    /// <summary>Re-checks every cart line against a fresh DB stock read (not the possibly-stale in-memory catalog) — catches stock that changed since cards loaded or since a held sale was resumed.</summary>
    private List<string> FindStockShortages()
    {
        var shortages = new List<string>();
        foreach (var item in _cartItems)
        {
            var product = _productService.GetById(item.ProductId);
            if (product == null || product.CurrentStock < item.Quantity)
                shortages.Add(item.ProductName);
        }
        return shortages;
    }

    private void BtnCompleteSale_Click(object sender, RoutedEventArgs e)
    {
        if (_isProcessingSale) return;

        if (_cartItems.Count == 0)
        {
            MessageBox.Show(LanguageManager.GetString("EmptyCart"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (subtotal, discountAmount, grandTotal) = CalculateTotals();
        var paid = decimal.TryParse(txtPaidAmount.Text, out var p) ? p : 0;
        var isCredit = rbCredit.IsChecked == true;
        var isPartial = rbPartial.IsChecked == true;
        var isCash = !isCredit && !isPartial;

        if (isPartial && paid <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (paid > grandTotal)
        {
            paid = grandTotal;
        }

        var customer = _selectedCustomer;
        var remaining = grandTotal - paid;

        var shortages = FindStockShortages();
        if (shortages.Count > 0)
        {
            MessageBox.Show($"{LanguageManager.GetString("InsufficientStock")}: {string.Join(", ", shortages)}",
                LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if ((isCredit || isPartial) && customer != null && customer.Id > 0 && remaining > 0)
        {
            var (withinLimit, _) = _customerService.CheckCreditLimit(customer.Id, remaining);
            if (!withinLimit)
            {
                if (!AuthService.HasPermission("OverrideCreditLimit"))
                {
                    MessageBox.Show(LanguageManager.GetString("CreditLimitExceeded"), LanguageManager.GetString("Warning"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var proceed = MessageBox.Show(
                    $"{LanguageManager.GetString("CreditLimitExceeded")}\n{LanguageManager.GetString("CreditLimitExceededConfirm")}",
                    LanguageManager.GetString("Warning"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes) return;

                AuditLogService.Log("CREDIT_LIMIT_OVERRIDE", "Customer", customer.Id,
                    $"Overrode credit limit for '{customer.Name}' to allow additional Rs. {remaining:N0}");
            }
        }

        var paymentMethodLabel = isCredit ? LanguageManager.GetString("Credit") : isPartial ? LanguageManager.GetString("Partial") : LanguageManager.GetString("Cash");
        var confirmDialog = new PaymentConfirmDialog(subtotal, discountAmount, grandTotal, paymentMethodLabel, isCash, paid, remaining, customer?.Name)
        {
            Owner = Window.GetWindow(this)
        };
        if (confirmDialog.ShowDialog() != true) return;

        _isProcessingSale = true;
        btnCompleteSale.IsEnabled = false;
        try
        {
            var sale = new Sale
            {
                CustomerId = customer?.Id > 0 ? customer.Id : null,
                Subtotal = subtotal,
                DiscountPercent = _invoiceDiscountIsFixed ? 0 : (decimal.TryParse(txtDiscountPercent.Text, out var pctVal) ? pctVal : 0),
                DiscountAmount = discountAmount,
                GrandTotal = grandTotal,
                PaidAmount = paid,
                RemainingAmount = remaining,
                PaymentMethod = isCredit ? "Credit" : isPartial ? "Partial" : "Cash",
                IsCreditSale = isCredit || isPartial
            };

            // c.DiscountAmount is a per-unit Rs discount (what the cart's Discount column edits);
            // SaleItem.DiscountAmount/Total expect the line's total discount, so scale by Quantity here.
            var items = _cartItems.Select(c => new SaleItem
            {
                ProductId = c.ProductId,
                Quantity = c.Quantity,
                UnitPrice = c.Price,
                PurchasePriceAtSale = c.PurchasePriceAtSale,
                DiscountPercent = c.DiscountPercent,
                DiscountAmount = c.DiscountAmount * c.Quantity
            }).ToList();

            var (success, errorKey, saleId) = _salesService.CompleteSale(sale, items, dpDueDate.SelectedDate);
            if (!success)
            {
                MessageBox.Show(LanguageManager.GetString(errorKey ?? "ErrorOccurred"), LanguageManager.GetString("Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            sale.Id = saleId;

            var lines = _cartItems.Select(c =>
            {
                var p = _allProducts.FirstOrDefault(x => x.Id == c.ProductId);
                return new ReceiptLineItem
                {
                    ProductName = c.ProductName,
                    ProductNameUrdu = p?.NameUrdu,
                    UnitLabel = p?.Unit?.Symbol,
                    Quantity = c.Quantity,
                    Price = c.Price,
                    DiscountAmount = c.DiscountAmount * c.Quantity
                };
            }).ToList();
            _lastCompletedSale = sale;
            _lastCompletedSaleCustomer = customer;
            _lastCompletedSaleLines = lines;

            // Fire-and-forget auto print (does nothing unless the shop enabled Auto Print).
            try { PosPrintService.AutoPrintAfterSale(sale, customer, lines); } catch { /* never block the sale flow */ }

            var receiptDialog = new ReceiptActionsDialog(sale, customer, lines) { Owner = Window.GetWindow(this) };
            receiptDialog.ShowDialog();

            ApplyLocalStockDecrement(_cartItems);
            _cartItems.Clear();
            UpdateCartDisplay();
            FilterProducts(txtSearch.Text, _activeCategoryFilter);
            txtPaidAmount.Text = "0";
            txtDiscountPercent.Text = "0";
            dpDueDate.SelectedDate = null;
            // Keep the customer-search balance badges current after a credit sale.
            LoadCustomersCache();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{LanguageManager.GetString("ErrorOccurred")}: {ex.Message}",
                LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isProcessingSale = false;
            btnCompleteSale.IsEnabled = true;
        }
    }

    /// <summary>Reflects a just-completed sale's stock changes into the already-loaded catalog instead of a full DB reload, so the product cards refresh instantly without an unnecessary round-trip.</summary>
    private void ApplyLocalStockDecrement(List<CartViewModel> soldItems)
    {
        foreach (var sold in soldItems)
        {
            var product = _allProducts.FirstOrDefault(p => p.Id == sold.ProductId);
            if (product != null) product.CurrentStock -= sold.Quantity;
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
        if (_selectedCustomer != null) ClearSelectedCustomer();
        txtCustomerSearch.Focus();
        popupCustomerResults.IsOpen = true;
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
        PosPrintService.PrintSale(_lastCompletedSale, _lastCompletedSaleCustomer, _lastCompletedSaleLines);
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
        dpDueDate.SelectedDate = null;
    }

    public void HoldCurrentSale()
    {
        if (_cartItems.Count == 0)
        {
            MessageBox.Show(LanguageManager.GetString("EmptyCart"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var customer = _selectedCustomer;
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
            var heldCustomer = held.CustomerId.HasValue ? _customerService.GetById(held.CustomerId.Value) : null;
            if (heldCustomer != null) SelectCustomer(heldCustomer); else ClearSelectedCustomer();

            txtDiscountPercent.Text = held.DiscountPercent.ToString();
            UpdateCartDisplay();
            UpdateHeldSalesButton();

            var shortages = FindStockShortages();
            if (shortages.Count > 0)
            {
                MessageBox.Show($"{LanguageManager.GetString("InsufficientStock")}:\n{string.Join("\n", shortages)}",
                    LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else if (dialog.DeletedAny)
        {
            UpdateHeldSalesButton();
        }
    }
}

public class CustomerSearchResult
{
    public Customer Customer { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string BalanceText { get; set; } = "";
    public Brush BalanceBrush { get; set; } = Brushes.Gray;
}

public class CartViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal PurchasePriceAtSale { get; set; }
    /// <summary>Per-unit discount in Rs (not a flat line total) — the cart's Discount column
    /// edits this directly. Multiply by Quantity to get the line's total discount amount.</summary>
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercent => Price == 0 ? 0 : DiscountAmount / Price * 100;
    public decimal Total => Quantity * (Price - DiscountAmount);
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
