using System;
using System.Collections.Generic;
using System.Linq;
using NHi.Inventory.Data;
using NHi.Inventory.Views.Printing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NHi.Inventory.Services;

/// <summary>
/// Centralised, paper-size-aware report printing. Builds a paged table document for
/// any paper (A4 / A5 / Letter / custom), repeating the column header on every page
/// and letting QuestPDF break pages automatically so nothing is ever cut. Everything
/// is shown through the app-wide <see cref="UniversalPrintPreviewWindow"/>.
/// </summary>
public static class ReportPrintService
{
    private const string UrduFont = ReceiptService.UrduFontFamilyName;

    /// <summary>Opens the universal preview for a tabular report; the user can switch the
    /// paper size and the whole table re-flows live.</summary>
    public static void PreviewTable(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<double>? columnWeights = null)
    {
        var window = new UniversalPrintPreviewWindow(
            title,
            win => BuildTableDocument(title, headers, rows, columnWeights, win.PaperWidthMm, win.PaperHeightMm),
            showBarcodeControls: false,
            itemCount: () => rows.Count);

        SetOwner(window);
        window.ShowDialog();
    }

    /// <summary>Opens the universal preview for any pre-built QuestPDF document (receipts,
    /// statements, etc.) — no paper selector, since the document defines its own size.</summary>
    public static void PreviewDocument(IDocument document, string title)
    {
        var window = new UniversalPrintPreviewWindow(document, title);
        SetOwner(window);
        window.ShowDialog();
    }

    private static void SetOwner(System.Windows.Window window)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;
        foreach (System.Windows.Window w in app.Windows)
            if (w.IsActive && w != window) { window.Owner = w; return; }
    }

    private static IDocument BuildTableDocument(string title, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<double>? weights,
        double paperWidthMm, double paperHeightMm)
    {
        const QuestPDF.Infrastructure.Unit mm = QuestPDF.Infrastructure.Unit.Millimetre;

        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var shopName = string.IsNullOrWhiteSpace(company?.Name) ? "NHI Inventory Management System" : company!.Name.Trim();
        var urdu = LanguageManager.IsRTL;

        // Smaller paper => smaller base font so columns still fit.
        var narrow = paperWidthMm < 165;
        var bodyFont = narrow ? 7f : 9f;
        var headFont = narrow ? 7.5f : 9.5f;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size((float)paperWidthMm, (float)paperHeightMm, mm);
                page.Margin(12, mm);
                page.DefaultTextStyle(x => x.FontSize(bodyFont));

                page.Header().Column(col =>
                {
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(38).Image(company.Logo).FitHeight(); }
                        catch { /* a bad logo must never block a report */ }
                    }
                    col.Item().AlignCenter().Text(shopName).FontSize(narrow ? 11f : 15f).Bold()
                        .FontFamily(UrduFont).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company!.Address).FontSize(bodyFont - 1)
                            .FontFamily(UrduFont).DirectionFromRightToLeft();
                    var t = col.Item().PaddingTop(5).AlignCenter().Text(title).FontSize(narrow ? 11f : 13f).Bold();
                    if (urdu) t.FontFamily(UrduFont);
                    col.Item().AlignCenter().Text($"{LanguageManager.GetString("GeneratedLabel")}: {DateTime.Now:dd-MMM-yyyy hh:mm tt}")
                        .FontSize(bodyFont - 1);
                    col.Item().PaddingTop(6).LineHorizontal(1);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        for (var i = 0; i < headers.Count; i++)
                        {
                            var wgt = (weights != null && i < weights.Count) ? (float)weights[i] : 1f;
                            cd.RelativeColumn(wgt);
                        }
                    });

                    table.Header(h =>
                    {
                        foreach (var head in headers)
                        {
                            var c = h.Cell().PaddingVertical(3).PaddingHorizontal(2).Background("#F0F0F0")
                                .Text(head).Bold().FontSize(headFont);
                            if (urdu) c.FontFamily(UrduFont);
                        }
                    });

                    var alt = false;
                    foreach (var row in rows)
                    {
                        var bg = alt ? "#FAFAFA" : "#FFFFFF";
                        alt = !alt;
                        foreach (var cell in row)
                            table.Cell().Background(bg).PaddingVertical(2).PaddingHorizontal(2)
                                .Text(cell ?? "").FontFamily(UrduFont).DirectionFromRightToLeft();
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(bodyFont - 1);
                    t.Span(" / ").FontSize(bodyFont - 1);
                    t.TotalPages().FontSize(bodyFont - 1);
                });
            });
        });
    }
}
