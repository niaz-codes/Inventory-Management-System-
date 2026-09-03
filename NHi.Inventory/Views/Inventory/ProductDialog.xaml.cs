using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class ProductDialog : Window
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    private const long MaxImageBytes = 2 * 1024 * 1024; // 2 MB

    private readonly ProductService _productService = new();
    private Product? _existingProduct;
    private byte[]? _imageData;

    public ProductDialog(Product? existingProduct = null)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _existingProduct = existingProduct;
        LoadComboBoxes();
        if (existingProduct != null) LoadProduct(existingProduct);
    }

    // Sentinel appended to the unit list; picking it reveals the free-text custom-unit field.
    private const int OtherUnitId = -1;

    private void LoadComboBoxes()
    {
        cmbCategory.ItemsSource = _productService.GetCategories();
        cmbBrand.ItemsSource = _productService.GetBrands();

        var units = _productService.GetUnits();
        units.Add(new Unit { Id = OtherUnitId, Name = LanguageManager.GetString("UnitOther") });
        cmbUnit.ItemsSource = units;
    }

    private void CmbUnit_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var isOther = (cmbUnit.SelectedItem as Unit)?.Id == OtherUnitId;
        pnlCustomUnit.Visibility = isOther ? Visibility.Visible : Visibility.Collapsed;
        if (isOther) txtCustomUnit.Focus();
        else txtCustomUnit.Clear();
    }

    private void LoadProduct(Product product)
    {
        txtTitle.Text = LanguageManager.GetString("EditProduct");
        txtName.Text = product.NameEnglish;
        txtNameUrdu.Text = product.NameUrdu ?? "";
        txtBarcode.Text = product.Barcode ?? "";
        btnPrintLabel.Visibility = Visibility.Visible;
        txtPurchasePrice.Text = product.PurchasePrice.ToString();
        txtSalePrice.Text = product.SalePrice.ToString();
        txtWholesalePrice.Text = product.WholesalePrice.ToString();
        txtOpeningStock.Text = product.OpeningStock.ToString();
        txtMinStock.Text = product.MinimumStock.ToString();
        txtMaxStock.Text = product.MaximumStock.ToString();
        txtDescription.Text = product.Description ?? "";
        dpExpiryDate.SelectedDate = product.ExpiryDate;
        chkActive.IsChecked = product.IsActive;
        _imageData = product.ImageData;
        ShowImagePreview(_imageData);

        if (product.Category != null)
            cmbCategory.SelectedItem = cmbCategory.Items.Cast<Category>().FirstOrDefault(c => c.Id == product.CategoryId);
        if (product.Brand != null)
            cmbBrand.SelectedItem = cmbBrand.Items.Cast<Brand>().FirstOrDefault(b => b.Id == product.BrandId);
        if (product.Unit != null)
            cmbUnit.SelectedItem = cmbUnit.Items.Cast<Unit>().FirstOrDefault(u => u.Id == product.UnitId);
    }

    private void ShowImagePreview(byte[]? data)
    {
        if (data == null || data.Length == 0) { imgPreview.Source = null; return; }
        try
        {
            using var ms = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            imgPreview.Source = bitmap;
        }
        catch
        {
            // A corrupted/unsupported image should never block the rest of the dialog from working.
            imgPreview.Source = null;
        }
    }

    private void BtnChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif" };
        if (dialog.ShowDialog() != true) return;

        var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            MessageBox.Show(LanguageManager.GetString("InvalidImageFile"), LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > MaxImageBytes)
            {
                MessageBox.Show(LanguageManager.GetString("ImageTooLarge"), LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _imageData = File.ReadAllBytes(dialog.FileName);
            ShowImagePreview(_imageData);
        }
        catch
        {
            MessageBox.Show(LanguageManager.GetString("InvalidImageFile"), LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnGenerateBarcode_Click(object sender, RoutedEventArgs e)
    {
        txtBarcode.Text = BarcodeService.GenerateUnique();
    }

    private void BtnPrintLabel_Click(object sender, RoutedEventArgs e)
    {
        if (_existingProduct == null) return;
        // Reflect any just-typed barcode so the label matches what's on screen.
        if (!string.IsNullOrWhiteSpace(txtBarcode.Text)) _existingProduct.Barcode = txtBarcode.Text.Trim();
        new BarcodeLabelDialog(_existingProduct) { Owner = this }.ShowDialog();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(txtPurchasePrice.Text, out var purchasePrice);
        decimal.TryParse(txtSalePrice.Text, out var salePrice);
        decimal.TryParse(txtWholesalePrice.Text, out var wholesalePrice);
        decimal.TryParse(txtOpeningStock.Text, out var openingStock);
        decimal.TryParse(txtMinStock.Text, out var minStock);
        decimal.TryParse(txtMaxStock.Text, out var maxStock);

        var product = _existingProduct ?? new Product();
        product.NameEnglish = txtName.Text.Trim();
        product.NameUrdu = txtNameUrdu.Text.Trim();
        // Blank barcode is fine here — ProductService.Save mints a unique one on save.
        product.Barcode = string.IsNullOrWhiteSpace(txtBarcode.Text) ? null : txtBarcode.Text.Trim();
        // SKU/BatchNumber stay untouched on edit / null for a new product (not part of quick-add).
        product.CategoryId = (cmbCategory.SelectedItem as Category)?.Id;
        product.BrandId = (cmbBrand.SelectedItem as Brand)?.Id;

        var selectedUnit = cmbUnit.SelectedItem as Unit;
        if (selectedUnit?.Id == OtherUnitId)
        {
            if (string.IsNullOrWhiteSpace(txtCustomUnit.Text))
            {
                MessageBox.Show(LanguageManager.GetString("CustomUnitRequired"), LanguageManager.GetString("Warning"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCustomUnit.Focus();
                return;
            }
            // Reuse-or-create so the typed unit becomes a normal Unit row (shows everywhere).
            product.UnitId = _productService.GetOrCreateUnit(txtCustomUnit.Text);
        }
        else
        {
            product.UnitId = selectedUnit?.Id;
        }
        product.PurchasePrice = purchasePrice;
        product.SalePrice = salePrice;
        product.WholesalePrice = wholesalePrice;
        product.OpeningStock = openingStock;
        product.CurrentStock = _existingProduct == null ? openingStock : _existingProduct.CurrentStock;
        product.MinimumStock = minStock;
        product.MaximumStock = maxStock;
        product.Description = txtDescription.Text.Trim();
        product.ExpiryDate = dpExpiryDate.SelectedDate;
        product.IsActive = chkActive.IsChecked == true;
        product.ImageData = _imageData;

        try
        {
            var (success, errorKey, _) = _productService.Save(product);
            if (!success)
            {
                MessageBox.Show(LanguageManager.GetString(errorKey ?? "ErrorOccurred"), LanguageManager.GetString("Warning"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        catch
        {
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        this.DialogResult = true;
        this.Close();
    }
}
