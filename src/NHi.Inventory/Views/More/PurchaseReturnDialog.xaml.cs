using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public class PurchaseReturnableLine
{
    public int ProductId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal PurchasedQuantity { get; set; }
    public decimal AlreadyReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Available => PurchasedQuantity - AlreadyReturned;
}

public partial class PurchaseReturnDialog : Window
{
    private readonly PurchaseService _purchaseService = new();
    private readonly int _purchaseId;
    private readonly Purchase _purchase;

    public PurchaseReturnDialog(int purchaseId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _purchaseId = purchaseId;
        using var db = new AppDbContext();
        _purchase = db.Purchases.Include(p => p.Items).ThenInclude(i => i.Product).Include(p => p.Supplier)
            .First(p => p.Id == purchaseId);
        txtTitle.Text = $"{LanguageManager.GetString("Return")} - {_purchase.InvoiceNumber}";
        LoadReturnableLines();
    }

    private void LoadReturnableLines()
    {
        using var db = new AppDbContext();
        var returned = db.PurchaseReturns.Where(r => r.PurchaseId == _purchaseId)
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

        var lines = _purchase.Items.Select(i => new PurchaseReturnableLine
        {
            ProductId = i.ProductId,
            DisplayName = i.Product?.NameEnglish ?? "",
            PurchasedQuantity = i.Quantity,
            AlreadyReturned = returned.TryGetValue(i.ProductId, out var r) ? r : 0,
            UnitPrice = i.UnitPrice
        }).Where(l => l.Available > 0).ToList();

        cmbProduct.ItemsSource = lines;
        if (lines.Count > 0) cmbProduct.SelectedIndex = 0;
    }

    private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbProduct.SelectedItem is PurchaseReturnableLine line)
        {
            txtAvailable.Text = $"{LanguageManager.GetString("Quantity")} purchased: {line.PurchasedQuantity:0.##} | Available to return: {line.Available:0.##}";
            txtQuantity.Text = line.Available.ToString("0.##");
            UpdateRefundPreview();
        }
    }

    private void TxtQuantity_Changed(object sender, TextChangedEventArgs e) => UpdateRefundPreview();

    private void UpdateRefundPreview()
    {
        if (cmbProduct.SelectedItem is PurchaseReturnableLine line && decimal.TryParse(txtQuantity.Text, out var qty))
        {
            txtRefundPreview.Text = $"{LanguageManager.GetString("Amount")}: Rs. {qty * line.UnitPrice:N0}";
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (cmbProduct.SelectedItem is not PurchaseReturnableLine line)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(txtQuantity.Text, out var quantity) || quantity <= 0 || quantity > line.Available)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (success, error) = _purchaseService.ProcessPurchaseReturn(_purchaseId, line.ProductId, quantity, txtReason.Text?.Trim());
        if (success)
        {
            MessageBox.Show(LanguageManager.GetString("PurchaseReturned"));
            DialogResult = true;
        }
        else
        {
            MessageBox.Show($"{LanguageManager.GetString("ErrorOccurred")}: {error}", LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
