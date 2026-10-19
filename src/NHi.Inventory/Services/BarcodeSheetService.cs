using System;
using System.Collections.Generic;
using System.Linq;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Views.Printing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NHi.Inventory.Services;

/// <summary>
/// Lays product barcode labels out as a grid on standard paper (A4 / A5 / Letter /
/// custom), automatically working out how many labels fit per page and letting QuestPDF
/// break to a new page when a page is full. Nothing is clipped, overlapped, or pushed
/// past the printable area — every cell is a fixed size with margins and gutters.
/// </summary>
public static class BarcodeSheetService
{
    public enum PaperKind { A4, A5, Letter, Custom }

    public sealed class Options
    {
        public PaperKind Paper = PaperKind.A4;
        public double CustomWidthMm = 100;
        public double CustomHeightMm = 150;
        public double LabelWidthMm = 55;
        public double LabelHeightMm = 32;
        public double MarginMm = 8;
        public double GutterMm = 3;
        public bool ShowShopName;
        public bool ShowPrice = true;
    }

    public static (double w, double h) PaperMm(Options o) => o.Paper switch
    {
        PaperKind.A4 => (210.0, 297.0),
        PaperKind.A5 => (148.0, 210.0),
        PaperKind.Letter => (215.9, 279.4),
        _ => (System.Math.Max(20.0, o.CustomWidthMm), System.Math.Max(20.0, o.CustomHeightMm)),
    };

    public static (int cols, int rows, int perPage) GridFor(Options o)
    {
        var (pw, ph) = PaperMm(o);
        var usableW = pw - 2 * o.MarginMm;
        var usableH = ph - 2 * o.MarginMm;
        var cols = System.Math.Max(1, (int)System.Math.Floor((usableW + o.GutterMm) / (o.LabelWidthMm + o.GutterMm)));
        var rows = System.Math.Max(1, (int)System.Math.Floor((usableH + o.GutterMm) / (o.LabelHeightMm + o.GutterMm)));
        return (cols, rows, cols * rows);
    }

    public static int PageCount(int labelCount, Options o)
    {
        var (_, _, perPage) = GridFor(o);
        if (perPage <= 0) return 1;
        return labelCount <= 0 ? 1 : (labelCount + perPage - 1) / perPage;
    }

    /// <summary>
    /// Opens the app-wide print preview in "barcode sheet" mode: the toolbar exposes
    /// paper size + label size + copies, and the grid re-flows live on every change.
    /// </summary>
    public static UniversalPrintPreviewWindow BuildPreviewWindow(List<(Product product, int copies)> items)
    {
        UniversalPrintPreviewWindow window = null!;

        IDocument Build(UniversalPrintPreviewWindow win)
        {
            var opts = new Options
            {
                Paper = PaperKind.Custom,
                CustomWidthMm = win.PaperWidthMm,
                CustomHeightMm = win.PaperHeightMm,
                LabelWidthMm = win.LabelWidthMm,
                LabelHeightMm = win.LabelHeightMm,
            };
            var labels = items
                .SelectMany(it => Enumerable.Repeat(it.product, Math.Max(1, it.copies) * Math.Max(1, win.Copies)))
                .ToList();
            return BarcodeSheetService.Build(labels, opts);
        }

        window = new UniversalPrintPreviewWindow(
            LanguageManager.GetString("PrintBarcodes"),
            Build,
            showBarcodeControls: true,
            itemCount: () => items.Sum(it => Math.Max(1, it.copies)) * Math.Max(1, window.Copies));

        return window;
    }

    public static IDocument Build(List<Product> labels, Options o)
    {
        const QuestPDF.Infrastructure.Unit mm = QuestPDF.Infrastructure.Unit.Millimetre;
        var (pw, ph) = PaperMm(o);
        var (cols, _, _) = GridFor(o);

        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var shopName = string.IsNullOrWhiteSpace(company?.Name) ? "" : company!.Name.Trim();

        var pngCache = new Dictionary<string, byte[]?>();
        byte[]? Png(string code)
        {
            if (!pngCache.TryGetValue(code, out var p))
            {
                p = BarcodeService.RenderPng(code, moduleWidth: 2, barHeight: 74, withText: false);
                pngCache[code] = p;
            }
            return p;
        }

        static string Short(string? s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..(max - 1)] + "…");

        var list = labels.Count == 0 ? new List<Product> { new() { NameEnglish = "-" } } : labels;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size((float)pw, (float)ph, mm);
                page.Margin((float)o.MarginMm, mm);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Content().Column(col =>
                {
                    col.Spacing((float)o.GutterMm, mm);

                    for (var i = 0; i < list.Count; i += cols)
                    {
                        var rowItems = list.Skip(i).Take(cols).ToList();
                        // MinHeight (not a rigid Height): keeps rows uniform without ever
                        // throwing "conflicting size constraints" if a cell needs a hair more.
                        col.Item().MinHeight((float)o.LabelHeightMm, mm).Row(row =>
                        {
                            row.Spacing((float)o.GutterMm, mm);

                            foreach (var p in rowItems)
                            {
                                row.RelativeItem().Border(0.5f).BorderColor("#C8C8C8").Padding(2)
                                   .AlignMiddle().Column(cell =>
                                {
                                    if (o.ShowShopName && shopName.Length > 0)
                                        cell.Item().AlignCenter().Text(Short(shopName, 34)).FontSize(5.5f);

                                    // Product name — sits directly above the barcode.
                                    cell.Item().AlignCenter().Text(Short(p.NameEnglish, 34)).FontSize(6.5f).Bold();

                                    if (o.ShowPrice)
                                        cell.Item().AlignCenter().Text($"Rs. {p.SalePrice:N0}").FontSize(6f);

                                    var png = Png(p.Barcode ?? "");
                                    if (png != null)
                                        // FitWidth: the barcode fills the cell width and its height
                                        // follows proportionally, so it can never overflow the cell.
                                        cell.Item().PaddingTop(1).AlignCenter().Image(png).FitWidth();
                                    else
                                        cell.Item().PaddingTop(1).AlignCenter().Text(p.Barcode ?? "").FontSize(6f);

                                    cell.Item().AlignCenter().Text(p.Barcode ?? "").FontSize(5.5f);
                                });
                            }

                            // Keep cell widths uniform when the last row isn't full.
                            for (var k = rowItems.Count; k < cols; k++)
                                row.RelativeItem().Text("");
                        });
                    }
                });
            });
        });
    }
}
