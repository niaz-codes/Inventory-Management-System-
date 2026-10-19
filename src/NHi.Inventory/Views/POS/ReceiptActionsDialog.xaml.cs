using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.POS;

/// <summary>Post-sale actions: print the receipt and/or open a prefilled WhatsApp chat with the
/// customer's invoice summary via the wa.me Click-to-Chat link. There's no auto-send and no
/// API — the employee always presses the button here, then presses Send inside WhatsApp
/// themselves, so every attempt is reported as "Opened", never "Sent".</summary>
public partial class ReceiptActionsDialog : Window
{
    private readonly Sale _sale;
    private readonly Customer? _customer;
    private readonly List<ReceiptLineItem> _lines;
    private readonly WhatsAppService _whatsApp = new();

    public ReceiptActionsDialog(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _sale = sale;
        _customer = customer;
        _lines = lines;

        txtTitle.Text = LanguageManager.GetString("SaleSuccessful");
        txtCustomer.Text = $"{LanguageManager.GetString("Customer")}: {customer?.Name ?? LanguageManager.GetString("WalkInCustomer")}";
        txtSubtitle.Text = $"{LanguageManager.GetString("InvoiceLabel")}: {sale.InvoiceNumber}   •   Rs. {sale.GrandTotal:N0}";

        var hasNumber = customer != null && (!string.IsNullOrWhiteSpace(customer.WhatsAppNumber) || !string.IsNullOrWhiteSpace(customer.Phone));
        btnWhatsApp.Visibility = hasNumber ? Visibility.Visible : Visibility.Collapsed;
        btnPrintAndSend.Visibility = hasNumber ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PosPrintService.PrintSale(_sale, _customer, _lines);
        }
        catch
        {
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tag
            || !System.Enum.TryParse<ReceiptExportFormat>(tag, out var fmt)) return;

        var ext = fmt switch
        {
            ReceiptExportFormat.Pdf => "pdf",
            ReceiptExportFormat.Excel => "xlsx",
            ReceiptExportFormat.Csv => "csv",
            _ => "png",
        };
        var dlg = new SaveFileDialog
        {
            Filter = $"{fmt} (*.{ext})|*.{ext}",
            FileName = $"Receipt_{_sale.InvoiceNumber.Replace('/', '-')}.{ext}",
        };
        if (dlg.ShowDialog() != true) return;

        if (PosPrintService.Export(_sale, _customer, _lines, fmt, dlg.FileName))
            MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
        else
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnWhatsApp_Click(object sender, RoutedEventArgs e) => SendWhatsApp();

    private void BtnPrintAndSend_Click(object sender, RoutedEventArgs e)
    {
        BtnPrint_Click(sender, e);
        SendWhatsApp();
    }

    /// <summary>Disables the WhatsApp buttons for the duration of the (synchronous, local-only)
    /// send so a double-click can't open the wa.me link twice — WhatsAppService itself also
    /// guards against re-entrancy for the same invoice, independent of this UI-level guard.</summary>
    private void SendWhatsApp()
    {
        if (_customer == null) return;
        SetButtonsEnabled(false);
        var result = _whatsApp.SendInvoiceMessage(_customer, _sale);
        statusPanel.Children.Add(BuildStatusRow(result));
        SetButtonsEnabled(true);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        btnWhatsApp.IsEnabled = enabled;
        btnPrintAndSend.IsEnabled = enabled;
    }

    private UIElement BuildStatusRow(WhatsAppActionResult result)
    {
        var text = result.Success ? LanguageManager.GetString("WhatsAppOpenedStatus") : (result.ErrorMessage ?? LanguageManager.GetString("StatusFailed"));
        var colorResourceKey = result.Success ? "SuccessBrush" : "DangerBrush";

        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, colorResourceKey);

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 6),
            Child = textBlock
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
