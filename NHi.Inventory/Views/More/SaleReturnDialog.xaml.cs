using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public class ReturnableLine
{
    public int ProductId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal SoldQuantity { get; set; }
    public decimal AlreadyReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Available => SoldQuantity - AlreadyReturned;
}

public partial class SaleReturnDialog : Window
{
    private readonly SalesService _salesService = new();
    private readonly int _saleId;
    private readonly Sale _sale;

    public SaleReturnDialog(int saleId)
    {
        InitializeComponent();
        _saleId = saleId;
        _sale = _salesService.GetSaleById(saleId)!;
        txtTitle.Text = $"{LanguageManager.GetString("Return")} - {_sale.InvoiceNumber}";
        LoadReturnableLines();
    }

    private void LoadReturnableLines()
    {
        using var db = new AppDbContext();
        var returned = db.SaleReturns.Where(r => r.SaleId == _saleId)
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

        var lines = _sale.Items.Select(i => new ReturnableLine
        {
            ProductId = i.ProductId,
            DisplayName = i.Product?.NameEnglish ?? "",
            SoldQuantity = i.Quantity,
            AlreadyReturned = returned.TryGetValue(i.ProductId, out var r) ? r : 0,
            UnitPrice = i.UnitPrice
        }).Where(l => l.Available > 0).ToList();

        cmbProduct.ItemsSource = lines;
        if (lines.Count > 0) cmbProduct.SelectedIndex = 0;
    }

    private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbProduct.SelectedItem is ReturnableLine line)
        {
            txtAvailable.Text = $"{LanguageManager.GetString("Quantity")} sold: {line.SoldQuantity:0.##} | Available to return: {line.Available:0.##}";
            txtQuantity.Text = line.Available.ToString("0.##");
            UpdateRefundPreview();
        }
    }

    private void TxtQuantity_Changed(object sender, TextChangedEventArgs e) => UpdateRefundPreview();

    private void UpdateRefundPreview()
    {
        if (cmbProduct.SelectedItem is ReturnableLine line && decimal.TryParse(txtQuantity.Text, out var qty))
        {
            txtRefundPreview.Text = $"{LanguageManager.GetString("Amount")}: Rs. {qty * line.UnitPrice:N0}";
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (cmbProduct.SelectedItem is not ReturnableLine line)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(txtQuantity.Text, out var quantity) || quantity <= 0 || quantity > line.Available)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (success, error) = _salesService.ProcessSaleReturn(_saleId, line.ProductId, quantity, txtReason.Text?.Trim());
        if (success)
        {
            MessageBox.Show(LanguageManager.GetString("SaleReturned"));
            DialogResult = true;
        }
        else
        {
            MessageBox.Show($"{LanguageManager.GetString("ErrorOccurred")}: {error}", LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
