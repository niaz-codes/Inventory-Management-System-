using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class InventoryView : UserControl
{
    private const int PageSize = 50;

    private readonly ProductService _productService = new();
    private int _page = 1;
    private int _totalCount;
    private bool _isInitialized;

    public InventoryView()
    {
        InitializeComponent();
        _isInitialized = true;
        Loaded += InventoryView_Loaded;
    }

    private void InventoryView_Loaded(object sender, RoutedEventArgs e)
    {
        cmbCategory.ItemsSource = BuildCategoryFilterList();
        cmbCategory.SelectedIndex = 0;
        LoadData();
    }

    private List<Category> BuildCategoryFilterList()
    {
        var all = new Category { Id = 0, Name = LanguageManager.GetString("All") };
        var list = new List<Category> { all };
        list.AddRange(_productService.GetCategories());
        return list;
    }

    private void LoadData()
    {
        if (!_isInitialized) return;

        var categoryId = (cmbCategory.SelectedItem as Category)?.Id;
        if (categoryId is 0) categoryId = null;
        var lowStockOnly = chkLowStockOnly.IsChecked == true;
        var searchText = txtSearch.Text;

        var (items, total) = _productService.GetAllPaged(_page, PageSize, true, searchText, categoryId, lowStockOnly);
        _totalCount = total;

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        if (_page > totalPages) { _page = totalPages; (items, total) = _productService.GetAllPaged(_page, PageSize, true, searchText, categoryId, lowStockOnly); }

        dgProducts.ItemsSource = items;
        txtPageInfo.Text = $"{LanguageManager.GetString("Page")} {_page} / {totalPages} ({total})";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _page = 1;
        LoadData();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        _page = 1;
        LoadData();
    }

    private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_page <= 1) return;
        _page--;
        LoadData();
    }

    private void BtnNextPage_Click(object sender, RoutedEventArgs e)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
        if (_page >= totalPages) return;
        _page++;
        LoadData();
    }

    private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProductDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void DgProducts_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgProducts.SelectedItem is Product product)
        {
            var dialog = new ProductDialog(product);
            if (dialog.ShowDialog() == true) LoadData();
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var product = _productService.GetById(id);
            if (product != null)
            {
                var dialog = new ProductDialog(product);
                if (dialog.ShowDialog() == true) LoadData();
            }
        }
    }

    private void BtnPrintLabel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var product = _productService.GetById(id);
            if (product != null)
                new BarcodeLabelDialog(product) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
    }

    private void BtnPrintAllBarcodes_Click(object sender, RoutedEventArgs e)
    {
        var products = _productService.GetAll(activeOnly: true);
        if (products.Count == 0)
        {
            MessageBox.Show(LanguageManager.GetString("NoProductsToPrint"), LanguageManager.GetString("PrintBarcodes"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var items = products.Select(p => (p, 1)).ToList();
        var win = BarcodeSheetService.BuildPreviewWindow(items);
        win.Owner = Window.GetWindow(this);
        win.ShowDialog();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var result = MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                if (_productService.Delete(id)) LoadData();
                else MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryBrandDialog("Category") { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        cmbCategory.ItemsSource = BuildCategoryFilterList();
        cmbCategory.SelectedIndex = 0;
        LoadData();
    }

    private void BtnAddBrand_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryBrandDialog("Brand") { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        LoadData();
    }

    private void BtnManageUnits_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UnitManagementDialog { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        if (dialog.DataChanged) LoadData();
    }

    private void BtnStockAdjustment_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.HasPermission("StockAdjustment"))
        {
            MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dialog = new StockAdjustmentDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = "Products.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            if (ExcelExportService.ExportProducts(dialog.FileName))
                MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"), "", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

/// <summary>Derives an OK/LOW/OUT/EXPIRED badge from CurrentStock, MinimumStock, and ExpiryDate — same low-stock definition used everywhere else in the app (CurrentStock &lt;= MinimumStock), not just an exact-zero check.</summary>
public class ProductStatusConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var currentStock = values.Length > 0 && values[0] is decimal cs ? cs : 0;
        var minStock = values.Length > 1 && values[1] is decimal ms ? ms : 0;
        var expiry = values.Length > 2 ? values[2] as DateTime? : null;

        string key;
        string hex;
        if (expiry.HasValue && expiry.Value.Date < DateTime.Today) { key = "StatusExpired"; hex = "#9C27B0"; }
        else if (currentStock <= 0) { key = "StatusOut"; hex = "#F44336"; }
        else if (currentStock <= minStock) { key = "StatusLow"; hex = "#FF9800"; }
        else { key = "StatusOk"; hex = "#4CAF50"; }

        if (parameter as string == "brush")
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        return LanguageManager.GetString(key);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
