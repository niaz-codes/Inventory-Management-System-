using System.Windows;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class ProductDialog : Window
{
    private readonly ProductService _productService = new();
    private Product? _existingProduct;

    public ProductDialog(Product? existingProduct = null)
    {
        InitializeComponent();
        _existingProduct = existingProduct;
        LoadComboBoxes();
        if (existingProduct != null) LoadProduct(existingProduct);
    }

    private void LoadComboBoxes()
    {
        cmbCategory.ItemsSource = _productService.GetCategories();
        cmbBrand.ItemsSource = _productService.GetBrands();
        cmbUnit.ItemsSource = _productService.GetUnits();
    }

    private void LoadProduct(Product product)
    {
        txtTitle.Text = LanguageManager.GetString("EditProduct");
        txtName.Text = product.NameEnglish;
        txtNameUrdu.Text = product.NameUrdu ?? "";
        txtBarcode.Text = product.Barcode ?? "";
        txtSKU.Text = product.SKU ?? "";
        txtPurchasePrice.Text = product.PurchasePrice.ToString();
        txtSalePrice.Text = product.SalePrice.ToString();
        txtWholesalePrice.Text = product.WholesalePrice.ToString();
        txtOpeningStock.Text = product.OpeningStock.ToString();
        txtMinStock.Text = product.MinimumStock.ToString();
        txtMaxStock.Text = product.MaximumStock.ToString();
        txtDescription.Text = product.Description ?? "";

        if (product.Category != null)
            cmbCategory.SelectedItem = cmbCategory.Items.Cast<Category>().FirstOrDefault(c => c.Id == product.CategoryId);
        if (product.Brand != null)
            cmbBrand.SelectedItem = cmbBrand.Items.Cast<Brand>().FirstOrDefault(b => b.Id == product.BrandId);
        if (product.Unit != null)
            cmbUnit.SelectedItem = cmbUnit.Items.Cast<Unit>().FirstOrDefault(u => u.Id == product.UnitId);
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
        product.Barcode = txtBarcode.Text.Trim();
        product.SKU = txtSKU.Text.Trim();
        product.CategoryId = (cmbCategory.SelectedItem as Category)?.Id;
        product.BrandId = (cmbBrand.SelectedItem as Brand)?.Id;
        product.UnitId = (cmbUnit.SelectedItem as Unit)?.Id;
        product.PurchasePrice = purchasePrice;
        product.SalePrice = salePrice;
        product.WholesalePrice = wholesalePrice;
        product.OpeningStock = openingStock;
        product.CurrentStock = _existingProduct == null ? openingStock : _existingProduct.CurrentStock;
        product.MinimumStock = minStock;
        product.MaximumStock = maxStock;
        product.Description = txtDescription.Text.Trim();

        _productService.Save(product);
        this.DialogResult = true;
        this.Close();
    }
}
