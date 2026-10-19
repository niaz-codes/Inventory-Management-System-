using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Views.Printing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using GdiImage = System.Drawing.Image;

namespace NHi.Inventory.Services;

public enum ReceiptExportFormat { Pdf, Excel, Csv, Png }

/// <summary>
/// The complete POS receipt printing engine: builds Customer / Shop copies for either a
/// thermal roll (58 / 80 / custom mm, continuous auto-height) or a standard page invoice
/// (A4 / A5 / Letter / custom, auto-paginated), shows the universal live preview (zoom,
/// page navigation, print current / range / all, Windows print dialog), auto-prints after
/// a sale when configured, and exports to PDF / Excel / CSV / PNG.
/// </summary>
public static class PosPrintService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private const string UrduFont = ReceiptService.UrduFontFamilyName;

    // ---------- settings ----------

    public static PosPrintSettings Load()
    {
        using var db = new AppDbContext();
        var shop = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        return Parse(shop?.PosPrintJson);
    }

    public static PosPrintSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new PosPrintSettings();
        try { return JsonSerializer.Deserialize<PosPrintSettings>(json, JsonOpts) ?? new PosPrintSettings(); }
        catch { return new PosPrintSettings(); }
    }

    public static void Save(PosPrintSettings settings)
    {
        using var db = new AppDbContext();
        var shop = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (shop == null) return;
        shop.PosPrintJson = JsonSerializer.Serialize(settings, JsonOpts);
        shop.UpdatedAt = DateTime.Now;
        shop.UpdatedBy = AuthService.CurrentUser?.Id;
        db.SaveChanges();
        AuditLogService.Log("UPDATE_POS_PRINT_SETTINGS", "Shop", shop.Id, "POS print settings updated");
    }

    public static string[] InstalledPrinters()
    {
        try { return PrinterSettings.InstalledPrinters.Cast<string>().OrderBy(n => n).ToArray(); }
        catch { return Array.Empty<string>(); }
    }

    private static double ThermalWidthMm(PosPrintSettings s) => s.ThermalWidth switch
    {
        "58mm" => 58,
        "80mm" => 80,
        _ => Math.Clamp(s.CustomThermalWidthMm, 40, 120),
    };

    private static (double w, double h) StandardMm(PosPrintSettings s) => s.StandardPaper switch
    {
        "A5" => (148, 210),
        "Letter" => (215.9, 279.4),
        "Custom" => (Math.Clamp(s.CustomWidthMm, 60, 1000), Math.Clamp(s.CustomHeightMm, 60, 2000)),
        _ => (210, 297),
    };

    private static IEnumerable<string> CopyLabels(PosPrintSettings s)
    {
        var labels = s.CopyMode switch
        {
            "Shop" => new[] { "SHOP COPY" },
            "Both" => new[] { "CUSTOMER COPY", "SHOP COPY" },
            _ => new[] { "CUSTOMER COPY" },
        };
        var copies = Math.Clamp(s.Copies, 1, 20);
        foreach (var label in labels)
            for (var i = 0; i < copies; i++)
                yield return label;
    }

    // ---------- public entry points ----------

    public static void PrintSale(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        var s = Load();
        var doc = BuildDocument(sale, customer, lines, s, CopyLabels(s).ToList());
        var title = $"Receipt_{sale.InvoiceNumber}";

        if (s.AutoPrint && !s.LivePreview)
        {
            SilentPrint(doc, s.PrinterName, title);
            return;
        }
        OpenPreview(doc, title);
    }

    /// <summary>Called right after a sale completes; only does anything when AutoPrint is on.</summary>
    public static void AutoPrintAfterSale(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        var s = Load();
        if (!s.AutoPrint) return;
        var doc = BuildDocument(sale, customer, lines, s, CopyLabels(s).ToList());
        var title = $"Receipt_{sale.InvoiceNumber}";
        if (s.LivePreview) OpenPreview(doc, title);
        else SilentPrint(doc, s.PrinterName, title);
    }

    public static void TestPrint(PosPrintSettings s)
    {
        var sale = new Sale
        {
            InvoiceNumber = "TEST-0001",
            SaleDate = DateTime.Now,
            Subtotal = 1250,
            DiscountAmount = 50,
            GrandTotal = 1200,
            PaidAmount = 1500,
            RemainingAmount = 0,
            PaymentMethod = "Cash",
        };
        var lines = new List<ReceiptLineItem>
        {
            new() { ProductName = "Sample Product A", Quantity = 2, Price = 300 },
            new() { ProductName = "Sample Product B", Quantity = 1, Price = 450, DiscountAmount = 50 },
            new() { ProductName = "Sample Product C", Quantity = 5, Price = 40 },
        };
        var doc = BuildDocument(sale, null, lines, s, CopyLabels(s).ToList());
        OpenPreview(doc, "Test_Print");
    }

    public static bool Export(Sale sale, Customer? customer, List<ReceiptLineItem> lines,
        ReceiptExportFormat format, string path)
    {
        try
        {
            var s = Load();
            switch (format)
            {
                case ReceiptExportFormat.Pdf:
                    File.WriteAllBytes(path, BuildDocument(sale, customer, lines, s, new List<string> { "CUSTOMER COPY" }).GeneratePdf());
                    return true;
                case ReceiptExportFormat.Png:
                    var img = BuildDocument(sale, customer, lines, s, new List<string> { "CUSTOMER COPY" })
                        .GenerateImages(new ImageGenerationSettings { RasterDpi = 200 }).FirstOrDefault();
                    if (img == null) return false;
                    File.WriteAllBytes(path, img);
                    return true;
                case ReceiptExportFormat.Csv:
                    File.WriteAllText(path, BuildCsv(sale, customer, lines), Encoding.UTF8);
                    return true;
                case ReceiptExportFormat.Excel:
                    BuildExcel(sale, customer, lines).SaveAs(path);
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    // ---------- preview / silent print ----------

    private static void OpenPreview(IDocument doc, string title)
    {
        var window = new UniversalPrintPreviewWindow(doc, title);
        var app = System.Windows.Application.Current;
        if (app != null)
            foreach (System.Windows.Window w in app.Windows)
                if (w.IsActive && w != window) { window.Owner = w; break; }
        window.ShowDialog();
    }

    private static void SilentPrint(IDocument doc, string? printerName, string title)
    {
        var pages = doc.GenerateImages(new ImageGenerationSettings { RasterDpi = 200 }).ToList();
        if (pages.Count == 0) return;

        var gdi = pages.Select(b => GdiImage.FromStream(new MemoryStream(b))).ToList();
        var pd = new PrintDocument { DocumentName = title };
        try
        {
            if (!string.IsNullOrWhiteSpace(printerName)) pd.PrinterSettings.PrinterName = printerName;
            pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            var first = gdi[0];
            var w = (int)Math.Round(first.Width / 200.0 * 100);
            var h = (int)Math.Round(first.Height / 200.0 * 100);
            if (w > 0 && h > 0) pd.DefaultPageSettings.PaperSize = new PaperSize("Receipt", w, h);

            var k = 0;
            pd.PrintPage += (_, e) =>
            {
                var image = gdi[k];
                var b = e.MarginBounds;
                var scale = Math.Min((float)b.Width / image.Width, (float)b.Height / image.Height);
                e.Graphics!.DrawImage(image, b.Left + (b.Width - image.Width * scale) / 2f, b.Top,
                    image.Width * scale, image.Height * scale);
                k++;
                e.HasMorePages = k < gdi.Count;
            };
            pd.Print();
        }
        finally
        {
            foreach (var g in gdi) g.Dispose();
            pd.Dispose();
        }
    }

    // ---------- document building ----------

    public static IDocument BuildDocument(Sale sale, Customer? customer, List<ReceiptLineItem> lines,
        PosPrintSettings s, List<string> copyLabels)
    {
        using var db = new AppDbContext();
        var shop = db.Shops.FirstOrDefault(x => x.Id == AuthService.CurrentShopId);
        var cashier = sale.CreatedBy.HasValue ? db.Users.FirstOrDefault(u => u.Id == sale.CreatedBy.Value)?.FullName : null;
        var thermal = s.PrinterType != "Standard";
        if (copyLabels.Count == 0) copyLabels = new List<string> { "CUSTOMER COPY" };

        return Document.Create(container =>
        {
            foreach (var label in copyLabels)
            {
                container.Page(page =>
                {
                    if (thermal)
                    {
                        page.ContinuousSize((float)ThermalWidthMm(s), QuestPDF.Infrastructure.Unit.Millimetre);
                        page.MarginHorizontal((float)Math.Max(1, s.MarginMm), QuestPDF.Infrastructure.Unit.Millimetre);
                        page.MarginVertical((float)Math.Max(1, s.MarginMm), QuestPDF.Infrastructure.Unit.Millimetre);
                        page.DefaultTextStyle(x => x.FontSize(8));
                        page.Content().Element(c => ThermalReceipt(c, shop, sale, customer, cashier, lines, label, s));
                    }
                    else
                    {
                        var (w, h) = StandardMm(s);
                        page.Size((float)w, (float)h, QuestPDF.Infrastructure.Unit.Millimetre);
                        page.Margin((float)Math.Max(3, s.MarginMm), QuestPDF.Infrastructure.Unit.Millimetre);
                        page.DefaultTextStyle(x => x.FontSize(9));
                        page.Content().Element(c => StandardInvoice(c, shop, sale, customer, cashier, lines, label));
                        page.Footer().AlignRight().Text(t =>
                        {
                            t.CurrentPageNumber().FontSize(7);
                            t.Span(" / ").FontSize(7);
                            t.TotalPages().FontSize(7);
                        });
                    }
                });
            }
        });
    }

    private static void ThermalReceipt(IContainer container, Shop? shop, Sale sale, Customer? customer,
        string? cashier, List<ReceiptLineItem> lines, string copyLabel, PosPrintSettings s)
    {
        var change = sale.PaidAmount - sale.GrandTotal;

        container.Column(col =>
        {
            col.Item().AlignCenter().Border(1).BorderColor("#000").PaddingVertical(1).PaddingHorizontal(6)
                .Text($"*** {copyLabel} ***").FontSize(8).Bold();

            if (shop?.Logo is { Length: > 0 })
            {
                try { col.Item().PaddingTop(3).AlignCenter().MaxHeight(38).Image(shop.Logo).FitHeight(); }
                catch { /* bad logo must never block a receipt */ }
            }

            col.Item().PaddingTop(2).AlignCenter().Text(shop?.Name ?? "NHI Inventory").FontSize(13).Bold()
                .FontFamily(UrduFont).DirectionFromRightToLeft();
            if (!string.IsNullOrWhiteSpace(shop?.Address))
                col.Item().AlignCenter().Text(shop!.Address).FontSize(7).FontFamily(UrduFont).DirectionFromRightToLeft();
            var contact = string.Join("   ", new[]
            {
                string.IsNullOrWhiteSpace(shop?.Phone) ? null : $"Ph: {shop!.Phone}",
                string.IsNullOrWhiteSpace(shop?.WhatsApp) ? null : $"WA: {shop!.WhatsApp}",
            }.Where(x => x != null));
            if (contact.Length > 0) col.Item().AlignCenter().Text(contact).FontSize(7);

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#BBB");

            col.Item().Text($"Invoice: {sale.InvoiceNumber}").FontSize(7.5f);
            col.Item().Text($"Date: {sale.SaleDate:dd-MMM-yyyy hh:mm tt}").FontSize(7.5f);
            col.Item().Text($"Customer: {customer?.Name ?? "Walk-in"}").FontSize(7.5f)
                .FontFamily(UrduFont).DirectionFromRightToLeft();
            if (!string.IsNullOrWhiteSpace(cashier))
                col.Item().Text($"Cashier: {cashier}").FontSize(7.5f);
            col.Item().Text($"Payment: {sale.PaymentMethod}").FontSize(7.5f);

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#BBB");

            col.Item().Row(r =>
            {
                r.RelativeItem(4).Text("Item").Bold().FontSize(7.5f);
                r.RelativeItem(1).AlignRight().Text("Qty").Bold().FontSize(7.5f);
                r.RelativeItem(2).AlignRight().Text("Price").Bold().FontSize(7.5f);
                r.RelativeItem(2).AlignRight().Text("Total").Bold().FontSize(7.5f);
            });
            col.Item().PaddingBottom(1).LineHorizontal(0.5f).LineColor("#BBB");

            foreach (var l in lines)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem(4).Column(nc =>
                    {
                        nc.Item().Text(l.ProductName).FontSize(7.5f);
                        if (!string.IsNullOrWhiteSpace(l.ProductNameUrdu))
                            nc.Item().Text(l.ProductNameUrdu).FontSize(7f).FontFamily(UrduFont).DirectionFromRightToLeft();
                    });
                    r.RelativeItem(1).AlignRight().Text(l.QuantityDisplay).FontSize(7.5f);
                    r.RelativeItem(2).AlignRight().Text($"{l.Price:N0}").FontSize(7.5f);
                    r.RelativeItem(2).AlignRight().Text($"{l.Total:N0}").FontSize(7.5f);
                });
            }

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#BBB");

            void Line(string k, string v, bool bold = false, float size = 8f)
                => col.Item().Row(r =>
                {
                    var a = r.RelativeItem().Text(k).FontSize(size);
                    var b = r.RelativeItem().AlignRight().Text(v).FontSize(size);
                    if (bold) { a.Bold(); b.Bold(); }
                });

            Line("Subtotal", $"Rs. {sale.Subtotal:N0}");
            if (sale.DiscountAmount > 0) Line("Discount", $"- Rs. {sale.DiscountAmount:N0}");
            Line("Grand Total", $"Rs. {sale.GrandTotal:N0}", bold: true, size: 11f);
            Line("Paid", $"Rs. {sale.PaidAmount:N0}");
            if (change > 0) Line("Change", $"Rs. {change:N0}", bold: true);
            else if (sale.RemainingAmount > 0) Line("Balance (Khata)", $"Rs. {sale.RemainingAmount:N0}", bold: true);

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor("#BBB");
            if (!string.IsNullOrWhiteSpace(shop?.InvoiceFooter))
                col.Item().AlignCenter().Text(shop!.InvoiceFooter).FontSize(7)
                    .FontFamily(UrduFont).DirectionFromRightToLeft();
            col.Item().AlignCenter().PaddingTop(2).Text("Thank you for shopping!").FontSize(8).Italic().Bold();

            if (s.AutoCut)
                col.Item().PaddingTop(6).AlignCenter().Text("- - - - - - - - - - - -").FontSize(7).FontColor("#999");
        });
    }

    private static void StandardInvoice(IContainer container, Shop? shop, Sale sale, Customer? customer,
        string? cashier, List<ReceiptLineItem> lines, string copyLabel)
    {
        var change = sale.PaidAmount - sale.GrandTotal;

        container.Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    if (shop?.Logo is { Length: > 0 })
                    {
                        try { c.Item().MaxHeight(55).Image(shop.Logo).FitHeight(); }
                        catch { }
                    }
                    c.Item().PaddingTop(3).Text(shop?.Name ?? "NHI Inventory").FontSize(17).Bold()
                        .FontFamily(UrduFont).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(shop?.Address))
                        c.Item().Text(shop!.Address).FontSize(8).FontFamily(UrduFont).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(shop?.Phone))
                        c.Item().Text($"Phone: {shop!.Phone}").FontSize(8);
                    if (!string.IsNullOrWhiteSpace(shop?.Email))
                        c.Item().Text($"Email: {shop!.Email}").FontSize(8);
                });
                r.ConstantItem(190).Column(c =>
                {
                    c.Item().AlignRight().Text("INVOICE").FontSize(22).Bold().FontColor("#1565C0");
                    c.Item().AlignRight().Text(copyLabel).FontSize(9).Bold().FontColor("#C62828");
                    c.Item().PaddingTop(6).AlignRight().Text($"# {sale.InvoiceNumber}").FontSize(10).SemiBold();
                    c.Item().AlignRight().Text($"{sale.SaleDate:dd-MMM-yyyy  hh:mm tt}").FontSize(8);
                });
            });

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#1565C0");

            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("Bill To").FontSize(8).Bold().FontColor("#757575");
                    c.Item().Text(customer?.Name ?? "Walk-in Customer").FontSize(11).Bold()
                        .FontFamily(UrduFont).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(customer?.Phone))
                        c.Item().Text(customer!.Phone).FontSize(9);
                    if (!string.IsNullOrWhiteSpace(customer?.Address))
                        c.Item().Text(customer!.Address).FontSize(9).FontFamily(UrduFont).DirectionFromRightToLeft();
                });
                r.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text($"Payment: {sale.PaymentMethod}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(cashier)) c.Item().Text($"Cashier: {cashier}").FontSize(9);
                });
            });

            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(1);
                    cd.RelativeColumn(6);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    void HC(string t) => h.Cell().Background("#1565C0").PaddingVertical(4).PaddingHorizontal(3)
                        .Text(t).FontColor("#FFFFFF").Bold().FontSize(8.5f);
                    HC("#"); HC("Description"); HC("Qty"); HC("Price"); HC("Disc"); HC("Total");
                });

                var i = 1;
                foreach (var l in lines)
                {
                    var bg = i % 2 == 0 ? "#F5F7FA" : "#FFFFFF";
                    void C(string t, bool right = false)
                    {
                        var cell = table.Cell().Background(bg).PaddingVertical(3).PaddingHorizontal(3);
                        (right ? cell.AlignRight() : cell).Text(t).FontSize(8.5f)
                            .FontFamily(UrduFont).DirectionFromRightToLeft();
                    }
                    C(i.ToString());
                    C(string.IsNullOrWhiteSpace(l.ProductNameUrdu) ? l.ProductName : $"{l.ProductName}\n{l.ProductNameUrdu}");
                    C(l.QuantityDisplay, true);
                    C($"{l.Price:N0}", true);
                    C(l.DiscountAmount > 0 ? $"{l.DiscountAmount:N0}" : "-", true);
                    C($"{l.Total:N0}", true);
                    i++;
                }
            });

            col.Item().PaddingTop(12).Row(r =>
            {
                r.RelativeItem();
                r.ConstantItem(230).Border(1).BorderColor("#DDD").Padding(10).Column(c =>
                {
                    void KV(string k, string v, bool bold = false, float size = 9f)
                        => c.Item().Row(rr =>
                        {
                            var a = rr.RelativeItem().Text(k).FontSize(size);
                            var b = rr.RelativeItem().AlignRight().Text(v).FontSize(size);
                            if (bold) { a.Bold(); b.Bold(); }
                        });
                    KV("Subtotal", $"Rs. {sale.Subtotal:N0}");
                    if (sale.DiscountAmount > 0) KV("Discount", $"- Rs. {sale.DiscountAmount:N0}");
                    c.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#DDD");
                    KV("Grand Total", $"Rs. {sale.GrandTotal:N0}", bold: true, size: 12f);
                    KV("Paid", $"Rs. {sale.PaidAmount:N0}");
                    if (change > 0) KV("Change", $"Rs. {change:N0}", bold: true);
                    else if (sale.RemainingAmount > 0) KV("Balance (Khata)", $"Rs. {sale.RemainingAmount:N0}", bold: true);
                });
            });

            if (!string.IsNullOrWhiteSpace(shop?.InvoiceFooter))
                col.Item().PaddingTop(14).AlignCenter().Text(shop!.InvoiceFooter).FontSize(8)
                    .FontFamily(UrduFont).DirectionFromRightToLeft();
            col.Item().PaddingTop(4).AlignCenter().Text("Thank you for your business!").FontSize(9).Italic();
        });
    }

    // ---------- exports ----------

    private static string BuildCsv(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine($"Invoice,{Q(sale.InvoiceNumber)}");
        sb.AppendLine($"Date,{Q(sale.SaleDate.ToString("dd-MMM-yyyy HH:mm"))}");
        sb.AppendLine($"Customer,{Q(customer?.Name ?? "Walk-in")}");
        sb.AppendLine($"Payment,{Q(sale.PaymentMethod)}");
        sb.AppendLine();
        sb.AppendLine("Item,Qty,Price,Discount,Total");
        foreach (var l in lines)
            sb.AppendLine($"{Q(l.ProductName)},{l.Quantity},{l.Price},{l.DiscountAmount},{l.Total}");
        sb.AppendLine();
        sb.AppendLine($"Subtotal,{sale.Subtotal}");
        sb.AppendLine($"Discount,{sale.DiscountAmount}");
        sb.AppendLine($"Grand Total,{sale.GrandTotal}");
        sb.AppendLine($"Paid,{sale.PaidAmount}");
        sb.AppendLine($"Remaining,{sale.RemainingAmount}");
        return sb.ToString();
    }

    private static XLWorkbook BuildExcel(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Receipt");
        ws.Cell(1, 1).Value = "Invoice"; ws.Cell(1, 2).Value = sale.InvoiceNumber;
        ws.Cell(2, 1).Value = "Date"; ws.Cell(2, 2).Value = sale.SaleDate.ToString("dd-MMM-yyyy HH:mm");
        ws.Cell(3, 1).Value = "Customer"; ws.Cell(3, 2).Value = customer?.Name ?? "Walk-in";
        ws.Cell(4, 1).Value = "Payment"; ws.Cell(4, 2).Value = sale.PaymentMethod;

        var r = 6;
        ws.Cell(r, 1).Value = "Item"; ws.Cell(r, 2).Value = "Qty"; ws.Cell(r, 3).Value = "Price";
        ws.Cell(r, 4).Value = "Discount"; ws.Cell(r, 5).Value = "Total";
        ws.Range(r, 1, r, 5).Style.Font.Bold = true;
        r++;
        foreach (var l in lines)
        {
            ws.Cell(r, 1).Value = l.ProductName;
            ws.Cell(r, 2).Value = l.Quantity;
            ws.Cell(r, 3).Value = l.Price;
            ws.Cell(r, 4).Value = l.DiscountAmount;
            ws.Cell(r, 5).Value = l.Total;
            r++;
        }
        r++;
        ws.Cell(r, 4).Value = "Subtotal"; ws.Cell(r++, 5).Value = sale.Subtotal;
        ws.Cell(r, 4).Value = "Discount"; ws.Cell(r++, 5).Value = sale.DiscountAmount;
        ws.Cell(r, 4).Value = "Grand Total"; ws.Cell(r, 5).Value = sale.GrandTotal;
        ws.Range(r, 4, r, 5).Style.Font.Bold = true; r++;
        ws.Cell(r, 4).Value = "Paid"; ws.Cell(r++, 5).Value = sale.PaidAmount;
        ws.Cell(r, 4).Value = "Remaining"; ws.Cell(r, 5).Value = sale.RemainingAmount;

        ws.Columns().AdjustToContents();
        return wb;
    }
}
