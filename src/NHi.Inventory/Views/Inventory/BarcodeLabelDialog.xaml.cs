using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class BarcodeLabelDialog : Window
{
    private readonly Product _product;

    public BarcodeLabelDialog(Product product)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _product = product;

        txtProductName.Text = product.NameEnglish;
        txtProductMeta.Text =
            $"{LanguageManager.GetString("Barcode")}: {product.Barcode}   |   Rs. {product.SalePrice:N0}   |   " +
            $"{LanguageManager.GetString("CurrentStock")}: {product.CurrentStock:N0}";

        var png = BarcodeService.RenderPng(product.Barcode ?? "", moduleWidth: 2, barHeight: 90);
        if (png != null)
        {
            using var ms = new MemoryStream(png);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            imgBarcode.Source = bmp;
        }
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtCopies.Text, out var copies) || copies < 1) copies = 1;
        copies = System.Math.Min(copies, 200);

        var ok = ReceiptService.PrintProductLabels(new() { (_product, copies) });
        if (!ok)
        {
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        Close();
    }

    private void BtnSheet_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtCopies.Text, out var copies) || copies < 1) copies = 1;
        var win = BarcodeSheetService.BuildPreviewWindow(new() { (_product, copies) });
        if (Owner != null) win.Owner = Owner;
        Close();
        win.ShowDialog();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
