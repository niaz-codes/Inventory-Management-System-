using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class StockAdjustmentDialog : Window
{
    private readonly ProductService _productService = new();
    private Product? _selectedProduct;

    public StockAdjustmentDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
    }

    private void TxtProductSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = txtProductSearch.Text;
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            lstProductResults.Visibility = Visibility.Collapsed;
            return;
        }

        var results = _productService.Search(text);
        lstProductResults.ItemsSource = results;
        lstProductResults.DisplayMemberPath = "NameEnglish";
        lstProductResults.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LstProductResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstProductResults.SelectedItem is not Product product) return;

        _selectedProduct = product;
        txtSelectedProduct.Text = product.NameEnglish;
        txtCurrentStock.Text = $"{LanguageManager.GetString("CurrentStock")}: {product.CurrentStock:0.##} {product.Unit?.Symbol ?? ""}";
        lstProductResults.Visibility = Visibility.Collapsed;
        txtProductSearch.Text = product.NameEnglish;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.HasPermission("StockAdjustment"))
        {
            MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedProduct == null)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(txtQuantity.Text, out var quantity) || quantity <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("InvalidStock"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var delta = rbIncrease.IsChecked == true ? quantity : -quantity;
        var reasonKey = (cmbReason.SelectedItem as ComboBoxItem)?.Tag as string ?? "Other";
        var reasonText = LanguageManager.GetString(reasonKey == "Damaged" ? "ReasonDamaged" : reasonKey == "Lost" ? "ReasonLost" : reasonKey == "Correction" ? "ReasonCorrection" : "Other");

        try
        {
            var ok = _productService.AdjustStock(_selectedProduct.Id, delta, reasonText, txtNotes.Text.Trim(), AuthService.CurrentUser?.Id);
            if (!ok)
            {
                MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        catch
        {
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(LanguageManager.GetString("StockUpdated"), LanguageManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        this.DialogResult = true;
        this.Close();
    }
}
