using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class InventoryView : UserControl
{
    private readonly ProductService _productService = new();

    public InventoryView()
    {
        InitializeComponent();
        Loaded += InventoryView_Loaded;
    }

    private void InventoryView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void LoadData()
    {
        var products = _productService.GetAll(false);
        dgProducts.ItemsSource = products;
        cmbCategory.ItemsSource = _productService.GetCategories();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = txtSearch.Text;
        if (string.IsNullOrWhiteSpace(text))
            dgProducts.ItemsSource = _productService.GetAll(false);
        else
            dgProducts.ItemsSource = _productService.Search(text);
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
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

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var result = MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _productService.Delete(id);
                LoadData();
            }
        }
    }

    private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryBrandDialog("Category");
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void BtnAddBrand_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryBrandDialog("Brand");
        if (dialog.ShowDialog() == true) LoadData();
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = "Products.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            if (ExcelExportService.ExportProducts(dialog.FileName))
                MessageBox.Show("Exported successfully!", "", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
