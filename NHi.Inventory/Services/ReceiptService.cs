using System.Diagnostics;
using System.IO;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NHi.Inventory.Services;

public class ReceiptLineItem
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total => Quantity * Price;
}

public static class ReceiptService
{
    public static byte[] GenerateReceiptPdf(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        using var db = new AppDbContext();
        var company = db.CompanySettings.FirstOrDefault();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(80, lines.Count * 16 + 220, QuestPDF.Infrastructure.Unit.Millimetre);
                page.Margin(10);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text(company?.CompanyName ?? "NHi Inventory Management System")
                        .FontSize(13).Bold();

                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(8);
                    if (!string.IsNullOrWhiteSpace(company?.Phone))
                        col.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(8);

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().Text($"Invoice: {sale.InvoiceNumber}").FontSize(8);
                    col.Item().Text($"Date: {sale.SaleDate:dd-MMM-yyyy hh:mm tt}").FontSize(8);
                    col.Item().Text($"Customer: {customer?.Name ?? "Walk-in Customer"}").FontSize(8);

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem(4).Text("Item").Bold().FontSize(8);
                        row.RelativeItem(1).AlignRight().Text("Qty").Bold().FontSize(8);
                        row.RelativeItem(2).AlignRight().Text("Price").Bold().FontSize(8);
                        row.RelativeItem(2).AlignRight().Text("Total").Bold().FontSize(8);
                    });
                    col.Item().PaddingBottom(2).LineHorizontal(0.5f);

                    foreach (var line in lines)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem(4).Text(line.ProductName).FontSize(8);
                            row.RelativeItem(1).AlignRight().Text($"{line.Quantity:0.##}").FontSize(8);
                            row.RelativeItem(2).AlignRight().Text($"{line.Price:N0}").FontSize(8);
                            row.RelativeItem(2).AlignRight().Text($"{line.Total:N0}").FontSize(8);
                        });
                    }

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Subtotal");
                        row.RelativeItem().AlignRight().Text($"Rs. {sale.Subtotal:N0}");
                    });
                    if (sale.DiscountAmount > 0)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Discount");
                            row.RelativeItem().AlignRight().Text($"- Rs. {sale.DiscountAmount:N0}");
                        });
                    }
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Grand Total").Bold().FontSize(11);
                        row.RelativeItem().AlignRight().Text($"Rs. {sale.GrandTotal:N0}").Bold().FontSize(11);
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Paid");
                        row.RelativeItem().AlignRight().Text($"Rs. {sale.PaidAmount:N0}");
                    });
                    if (sale.RemainingAmount > 0)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Remaining (Khata)").Bold();
                            row.RelativeItem().AlignRight().Text($"Rs. {sale.RemainingAmount:N0}").Bold();
                        });
                    }
                    col.Item().Text($"Payment: {sale.PaymentMethod}").FontSize(8);

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    if (!string.IsNullOrWhiteSpace(company?.InvoiceFooter))
                        col.Item().AlignCenter().Text(company.InvoiceFooter).FontSize(8);

                    col.Item().AlignCenter().PaddingTop(3).Text("Thank you for shopping with us!").FontSize(8).Italic();
                });
            });
        });

        return document.GeneratePdf();
    }

    public static string SaveToTempFile(byte[] pdfBytes, string invoiceNumber)
    {
        var dir = Path.Combine(Path.GetTempPath(), "NHiInventory", "Receipts");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"Receipt_{invoiceNumber.Replace('/', '-')}.pdf");
        File.WriteAllBytes(path, pdfBytes);
        return path;
    }

    /// <summary>
    /// Sends the receipt to the OS's default PDF handler with the "print" verb,
    /// which opens the system print dialog for the user to pick a printer.
    /// </summary>
    public static bool PrintReceipt(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        try
        {
            var pdf = GenerateReceiptPdf(sale, customer, lines);
            var path = SaveToTempFile(pdf, sale.InvoiceNumber);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "print" });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool SaveReceiptAs(Sale sale, Customer? customer, List<ReceiptLineItem> lines, string destinationPath)
    {
        try
        {
            var pdf = GenerateReceiptPdf(sale, customer, lines);
            File.WriteAllBytes(destinationPath, pdf);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
