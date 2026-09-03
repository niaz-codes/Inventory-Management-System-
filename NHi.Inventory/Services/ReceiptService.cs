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
    public string? ProductNameUrdu { get; set; }
    /// <summary>Unit label (symbol or custom unit) shown next to the quantity, e.g. "pc", "درجن".</summary>
    public string? UnitLabel { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total => (Quantity * Price) - DiscountAmount;

    /// <summary>"2" or "2 درجن" — quantity with its unit appended when one is set.</summary>
    public string QuantityDisplay =>
        string.IsNullOrWhiteSpace(UnitLabel) ? $"{Quantity:0.##}" : $"{Quantity:0.##} {UnitLabel}";
}

public static class ReceiptService
{
    /// <summary>Must match the font family name embedded in
    /// Resources/Fonts/Jameel Noori Nastaleeq Kasheeda.ttf (registered in App.xaml.cs).
    /// If that file/family isn't present, QuestPDF falls back to its default font automatically.</summary>
    public const string UrduFontFamilyName = "Jameel Noori Nastaleeq";

    /// <summary>Brand-red used to make the shop's first/short name (e.g. "الرشید" in "الرشید
    /// كارپوریشن اینڈ بلڈنگ میٹریل سپلریز") stand out on every printed header, letterhead-style —
    /// matches the app's own DangerTextBrush/#C62828 used for emphasis elsewhere in the UI.</summary>
    private const string ShopBrandRed = "#C62828";

    private static string L(string key) => LanguageManager.GetString(key);

    /// <summary>Shared shop-name header for every receipt/statement: the first word (the shop's
    /// short/brand name) renders larger, bold, and in ShopBrandRed; the remainder of the name
    /// renders smaller so the whole thing reads as one flowing, letterhead-style line instead of
    /// uniform-size text that's more likely to wrap awkwardly at typical receipt widths.</summary>
    private static void ShopNameText(TextDescriptor text, string? shopName, float baseFontSize)
    {
        var name = string.IsNullOrWhiteSpace(shopName) ? "NHI Inventory Management System" : shopName.Trim();
        var spaceIndex = name.IndexOf(' ');
        var firstWord = spaceIndex > 0 ? name[..spaceIndex] : name;
        var rest = spaceIndex > 0 ? name[spaceIndex..] : "";

        text.Span(firstWord).FontSize(baseFontSize * 1.3f).Bold().FontColor(ShopBrandRed)
            .FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
        if (rest.Length > 0)
            text.Span(rest).FontSize(baseFontSize * 0.75f).SemiBold()
                .FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
    }

    public static byte[] GenerateReceiptPdf(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
        => BuildReceiptDocument(sale, customer, lines).GeneratePdf();

    /// <summary>Muted grey used for section-divider lines and small-caps section labels —
    /// deliberately not pure black, so the eye reads them as structure rather than content.</summary>
    private const string DividerColor = "#BBBBBB";
    private const string SectionLabelColor = "#757575";

    private static void SectionDivider(QuestPDF.Infrastructure.IContainer container)
        => container.PaddingVertical(6).LineHorizontal(1f).LineColor(DividerColor);

    private static void SectionHeading(QuestPDF.Infrastructure.IContainer container, string text)
        => container.PaddingBottom(3).Text(text).FontSize(7).Bold().FontColor(SectionLabelColor).LetterSpacing(0.05f);

    /// <summary>Applies a resolved Print Layout geometry to a QuestPDF page: fixed size (or
    /// auto-fit continuous height when unset) plus per-side margins, all in millimetres — shared
    /// by every receipt-style document so the Print Layout dialog's preview matches what prints.</summary>
    private static void ApplyPageLayout(QuestPDF.Fluent.PageDescriptor page, PrintLayoutService.Geometry geometry, double? fallbackFixedHeightMm = null)
    {
        const QuestPDF.Infrastructure.Unit mm = QuestPDF.Infrastructure.Unit.Millimetre;
        var heightMm = geometry.HeightMm ?? fallbackFixedHeightMm;
        if (heightMm is { } fixedHeight)
            page.Size((float)geometry.WidthMm, (float)fixedHeight, mm);
        else
            page.ContinuousSize((float)geometry.WidthMm, mm);

        page.MarginTop((float)geometry.MarginTopMm, mm);
        page.MarginBottom((float)geometry.MarginBottomMm, mm);
        page.MarginLeft((float)geometry.MarginLeftMm, mm);
        page.MarginRight((float)geometry.MarginRightMm, mm);
    }

    private static IDocument BuildReceiptDocument(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var cashierName = sale.CreatedBy.HasValue ? db.Users.FirstOrDefault(u => u.Id == sale.CreatedBy.Value)?.FullName : null;
        var hasItemDiscounts = lines.Any(l => l.DiscountAmount > 0);
        var urdu = LanguageManager.IsRTL;
        var geometry = PrintLayoutService.Resolve(company);
        var change = sale.PaidAmount - sale.GrandTotal;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageLayout(page, geometry);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    // --- 1. Logo ---
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(45).Image(company.Logo).FitHeight(); }
                        catch { /* A corrupted/unsupported logo image must never block receipt printing. */ }
                    }

                    // --- 2. Shop name: the most prominent element on the receipt ---
                    col.Item().PaddingTop(company?.Logo is { Length: > 0 } ? 4 : 0)
                        .AlignCenter().Text(t => ShopNameText(t, company?.Name, 18));

                    // --- 3. Address ---
                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().PaddingTop(2).AlignCenter().Text(company.Address).FontSize(8)
                            .FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();

                    // --- 4. Phone + WhatsApp, forced onto one flowing line/paragraph ---
                    var hasPhone = !string.IsNullOrWhiteSpace(company?.Phone);
                    var hasWhatsApp = !string.IsNullOrWhiteSpace(company?.WhatsApp);
                    if (hasPhone || hasWhatsApp)
                    {
                        col.Item().PaddingTop(3).AlignCenter().Text(text =>
                        {
                            if (hasPhone)
                            {
                                text.Span("☎ ").FontSize(8);
                                text.Span(company!.Phone).FontSize(8).SemiBold();
                            }
                            if (hasPhone && hasWhatsApp) text.Span("   |   ").FontSize(8).FontColor(SectionLabelColor);
                            if (hasWhatsApp)
                            {
                                text.Span($"{L("WhatsApp")}: ").FontSize(8);
                                text.Span(company!.WhatsApp).FontSize(8).SemiBold();
                            }
                        });
                    }

                    // --- 5. Divider ---
                    col.Item().Element(SectionDivider);

                    // --- 6. Invoice / sale details ---
                    col.Item().Element(c => SectionHeading(c, L("InvoiceDetailsHeading")));
                    col.Item().Text($"{L("InvoiceLabel")}: {sale.InvoiceNumber}").FontSize(8);
                    col.Item().Text($"{L("Date")}: {sale.SaleDate:dd-MMM-yyyy hh:mm tt}").FontSize(8);
                    col.Item().Text($"{L("Customer")}: {customer?.Name ?? L("WalkInCustomer")}").FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(cashierName))
                        col.Item().Text($"{L("CashierLabel")}: {cashierName}").FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    col.Item().Text($"{L("PaymentLabel")}: {sale.PaymentMethod}").FontSize(8);

                    // --- 7. Divider ---
                    col.Item().Element(SectionDivider);

                    // --- 8. Products ---
                    col.Item().Element(c => SectionHeading(c, L("ProductsHeading")));
                    col.Item().Row(row =>
                    {
                        var item = row.RelativeItem(4).Text(L("ItemLabel")).Bold().FontSize(8);
                        var qty = row.RelativeItem(1).AlignRight().Text(L("QtyLabel")).Bold().FontSize(8);
                        var price = row.RelativeItem(2).AlignRight().Text(L("Price")).Bold().FontSize(8);
                        if (urdu) { item.FontFamily(UrduFontFamilyName); qty.FontFamily(UrduFontFamilyName); price.FontFamily(UrduFontFamilyName); }
                        if (hasItemDiscounts)
                        {
                            var disc = row.RelativeItem(2).AlignRight().Text(L("DiscLabel")).Bold().FontSize(8);
                            if (urdu) disc.FontFamily(UrduFontFamilyName);
                        }
                        var total = row.RelativeItem(2).AlignRight().Text(L("Total")).Bold().FontSize(8);
                        if (urdu) total.FontFamily(UrduFontFamilyName);
                    });
                    col.Item().PaddingBottom(2).LineHorizontal(0.5f).LineColor(DividerColor);

                    foreach (var line in lines)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem(4).Column(nameCol =>
                            {
                                nameCol.Item().Text(line.ProductName).FontSize(8);
                                if (!string.IsNullOrWhiteSpace(line.ProductNameUrdu))
                                    nameCol.Item().Text(line.ProductNameUrdu).FontSize(7).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                            });
                            row.RelativeItem(1).AlignRight().Text(line.QuantityDisplay).FontSize(8);
                            row.RelativeItem(2).AlignRight().Text($"{line.Price:N0}").FontSize(8);
                            if (hasItemDiscounts) row.RelativeItem(2).AlignRight().Text(line.DiscountAmount > 0 ? $"{line.DiscountAmount:N0}" : "-").FontSize(8);
                            row.RelativeItem(2).AlignRight().Text($"{line.Total:N0}").FontSize(8);
                        });
                    }

                    // --- 9. Divider ---
                    col.Item().Element(SectionDivider);

                    // --- 10. Payment summary ---
                    col.Item().Element(c => SectionHeading(c, L("PaymentSummaryHeading")));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("Subtotal"));
                        row.RelativeItem().AlignRight().Text($"Rs. {sale.Subtotal:N0}");
                    });
                    if (sale.DiscountAmount > 0)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(L("Discount"));
                            row.RelativeItem().AlignRight().Text($"- Rs. {sale.DiscountAmount:N0}");
                        });
                    }
                    col.Item().PaddingTop(2).Row(row =>
                    {
                        row.RelativeItem().Text(L("GrandTotal")).Bold().FontSize(12);
                        row.RelativeItem().AlignRight().Text($"Rs. {sale.GrandTotal:N0}").Bold().FontSize(12);
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("PaidAmount"));
                        row.RelativeItem().AlignRight().Text($"Rs. {sale.PaidAmount:N0}");
                    });
                    if (change > 0)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(L("Change")).Bold();
                            row.RelativeItem().AlignRight().Text($"Rs. {change:N0}").Bold();
                        });
                    }
                    else if (sale.RemainingAmount > 0)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(L("RemainingKhataLabel")).Bold();
                            row.RelativeItem().AlignRight().Text($"Rs. {sale.RemainingAmount:N0}").Bold();
                        });
                    }

                    // --- 11. Divider ---
                    col.Item().Element(SectionDivider);

                    // --- 12. Footer: return/exchange policy (if configured), then thank-you ---
                    if (!string.IsNullOrWhiteSpace(company?.InvoiceFooter))
                        col.Item().AlignCenter().Text(company.InvoiceFooter).FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();

                    col.Item().AlignCenter().PaddingTop(3).Text(L("ThankYouMessage")).FontSize(9).Italic().Bold();
                });
            });
        });
    }

    public static byte[] GenerateKhataPaymentReceiptPdf(Customer customer, decimal previousBalance, decimal amountReceived, decimal newBalance, string receiptNumber)
        => BuildKhataPaymentReceiptDocument(customer, previousBalance, amountReceived, newBalance, receiptNumber).GeneratePdf();

    private static IDocument BuildKhataPaymentReceiptDocument(Customer customer, decimal previousBalance, decimal amountReceived, decimal newBalance, string receiptNumber)
    {
        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var urdu = LanguageManager.IsRTL;
        var geometry = PrintLayoutService.Resolve(company);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageLayout(page, geometry, fallbackFixedHeightMm: 190);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(35).Image(company.Logo).FitHeight(); }
                        catch { /* A corrupted/unsupported logo image must never block receipt printing. */ }
                    }

                    col.Item().AlignCenter().Text(t => ShopNameText(t, company?.Name, 13));

                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(company?.Phone))
                        col.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(8);

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);
                    var title = col.Item().AlignCenter().Text(L("PaymentReceiptTitle")).Bold().FontSize(11);
                    if (urdu) title.FontFamily(UrduFontFamilyName);
                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().Text($"{L("ReceiptNo")}: {receiptNumber}").FontSize(8);
                    col.Item().Text($"{L("Date")}: {DateTime.Now:dd-MMM-yyyy}").FontSize(8);
                    col.Item().Text($"{L("TimeLabel")}: {DateTime.Now:hh:mm tt}").FontSize(8);
                    col.Item().Text($"{L("Customer")}: {customer.Name}").FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("PreviousBalance"));
                        row.RelativeItem().AlignRight().Text($"Rs. {previousBalance:N0}");
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("AmountReceived")).Bold();
                        row.RelativeItem().AlignRight().Text($"Rs. {amountReceived:N0}").Bold();
                    });
                    col.Item().PaddingVertical(3).LineHorizontal(0.5f);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("RemainingBalance")).Bold().FontSize(11);
                        row.RelativeItem().AlignRight().Text($"Rs. {newBalance:N0}").Bold().FontSize(11);
                    });

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);
                    col.Item().AlignCenter().PaddingTop(3).Text(L("ThankYouShort")).FontSize(8).Italic();
                });
            });
        });
    }

    public static bool PrintKhataPaymentReceipt(Customer customer, decimal previousBalance, decimal amountReceived, decimal newBalance, string receiptNumber)
    {
        try
        {
            var document = BuildKhataPaymentReceiptDocument(customer, previousBalance, amountReceived, newBalance, receiptNumber);
            PrintingService.ShowPrintPreview(document, $"Receipt_{receiptNumber}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static byte[] GenerateCustomerStatementPdf(Customer customer, List<CustomerLedger> entries, DateTime? from, DateTime? to)
        => BuildCustomerStatementDocument(customer, entries, from, to).GeneratePdf();

    private static IDocument BuildCustomerStatementDocument(Customer customer, List<CustomerLedger> entries, DateTime? from, DateTime? to)
    {
        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var urdu = LanguageManager.IsRTL;

        var totalCredit = entries.Where(e => e.TransactionType == "CreditSale").Sum(e => e.Debit);
        var totalPayments = entries.Where(e => e.TransactionType == "Payment").Sum(e => e.Credit);
        var totalReturns = entries.Where(e => e.TransactionType == "SaleReturn").Sum(e => e.Credit);
        var totalAdjustments = entries.Where(e => e.TransactionType == "Adjustment" || e.TransactionType == "Reversal").Sum(e => e.Debit - e.Credit);
        var openingBalance = entries.FirstOrDefault(e => e.TransactionType == "OpeningBalance")?.Balance ?? 0;
        var currentBalance = entries.Count > 0 ? entries[^1].Balance : customer.CurrentBalance;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(45).Image(company.Logo).FitHeight(); }
                        catch { /* A corrupted/unsupported logo image must never block statement printing. */ }
                    }

                    col.Item().AlignCenter().Text(t => ShopNameText(t, company?.Name, 16));
                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(9).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(company?.Phone))
                        col.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(9);
                    var title = col.Item().PaddingTop(8).AlignCenter().Text(L("StatementTitle")).FontSize(13).Bold();
                    if (urdu) title.FontFamily(UrduFontFamilyName);
                    col.Item().PaddingTop(10).LineHorizontal(1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Line($"{L("Customer")}: {customer.Name}").Bold().FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                            if (!string.IsNullOrWhiteSpace(customer.FatherName)) t.Line($"{L("FatherName")}: {customer.FatherName}").FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                            if (!string.IsNullOrWhiteSpace(customer.Phone)) t.Line($"{L("Phone")}: {customer.Phone}");
                            if (!string.IsNullOrWhiteSpace(customer.Address)) t.Line($"{L("Address")}: {customer.Address}").FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                        });
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Line($"{L("StatementDateLabel")}: {DateTime.Now:dd-MMM-yyyy}");
                            if (from.HasValue || to.HasValue)
                                t.Line($"{L("PeriodLabel")}: {from?.ToString("dd-MMM-yyyy") ?? "Start"} to {to?.ToString("dd-MMM-yyyy") ?? "Today"}");
                            if (customer.CreditLimit > 0) t.Line($"{L("CreditLimit")}: Rs. {customer.CreditLimit:N0}");
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(4);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(L("Date")).Bold();
                            header.Cell().Text(L("Description")).Bold();
                            header.Cell().AlignRight().Text(L("Debit")).Bold();
                            header.Cell().AlignRight().Text(L("Credit")).Bold();
                            header.Cell().AlignRight().Text(L("Balance")).Bold();
                        });

                        foreach (var e in entries)
                        {
                            table.Cell().Text(e.TransactionDate.ToString("dd-MMM-yyyy"));
                            table.Cell().Text(e.Description).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                            table.Cell().AlignRight().Text(e.Debit > 0 ? $"{e.Debit:N0}" : "-");
                            table.Cell().AlignRight().Text(e.Credit > 0 ? $"{e.Credit:N0}" : "-");
                            table.Cell().AlignRight().Text($"{e.Balance:N0}");
                        }
                    });

                    col.Item().PaddingTop(15).LineHorizontal(0.5f);

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{L("OpeningBalance")}: Rs. {openingBalance:N0}");
                            c.Item().Text($"{L("TotalCreditSalesLabel")}: Rs. {totalCredit:N0}");
                            c.Item().Text($"{L("TotalPaymentsLabel")}: Rs. {totalPayments:N0}");
                            c.Item().Text($"{L("TotalReturnsLabel")}: Rs. {totalReturns:N0}");
                            c.Item().Text($"{L("TotalAdjustmentsLabel")}: Rs. {totalAdjustments:N0}");
                        });
                        row.RelativeItem().AlignRight().Text($"{L("CurrentBalanceLabel")}: Rs. {currentBalance:N0}").Bold().FontSize(13);
                    });
                });
            });
        });
    }

    public static bool PrintCustomerStatement(Customer customer, List<CustomerLedger> entries, DateTime? from, DateTime? to)
    {
        try
        {
            var document = BuildCustomerStatementDocument(customer, entries, from, to);
            PrintingService.ShowPrintPreview(document, $"Statement_{customer.Name}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static byte[] GenerateSupplierPaymentReceiptPdf(Supplier supplier, decimal previousBalance, decimal amountPaid, decimal newBalance, string receiptNumber)
        => BuildSupplierPaymentReceiptDocument(supplier, previousBalance, amountPaid, newBalance, receiptNumber).GeneratePdf();

    public static bool PrintSupplierPaymentReceipt(Supplier supplier, decimal previousBalance, decimal amountPaid, decimal newBalance, string receiptNumber)
    {
        try
        {
            var document = BuildSupplierPaymentReceiptDocument(supplier, previousBalance, amountPaid, newBalance, receiptNumber);
            PrintingService.ShowPrintPreview(document, $"Receipt_{receiptNumber}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IDocument BuildSupplierPaymentReceiptDocument(Supplier supplier, decimal previousBalance, decimal amountPaid, decimal newBalance, string receiptNumber)
    {
        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var urdu = LanguageManager.IsRTL;
        var geometry = PrintLayoutService.Resolve(company);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageLayout(page, geometry, fallbackFixedHeightMm: 190);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(35).Image(company.Logo).FitHeight(); }
                        catch { /* A corrupted/unsupported logo image must never block receipt printing. */ }
                    }

                    col.Item().AlignCenter().Text(t => ShopNameText(t, company?.Name, 13));

                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(company?.Phone))
                        col.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(8);

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);
                    var title = col.Item().AlignCenter().Text(L("PaymentReceiptTitle")).Bold().FontSize(11);
                    if (urdu) title.FontFamily(UrduFontFamilyName);
                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().Text($"{L("ReceiptNo")}: {receiptNumber}").FontSize(8);
                    col.Item().Text($"{L("Date")}: {DateTime.Now:dd-MMM-yyyy}").FontSize(8);
                    col.Item().Text($"{L("TimeLabel")}: {DateTime.Now:hh:mm tt}").FontSize(8);
                    col.Item().Text($"{L("Supplier")}: {supplier.Name}").FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("PreviousBalance"));
                        row.RelativeItem().AlignRight().Text($"Rs. {previousBalance:N0}");
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("AmountReceived")).Bold();
                        row.RelativeItem().AlignRight().Text($"Rs. {amountPaid:N0}").Bold();
                    });
                    col.Item().PaddingVertical(3).LineHorizontal(0.5f);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L("CurrentPayableLabel")).Bold().FontSize(11);
                        row.RelativeItem().AlignRight().Text($"Rs. {newBalance:N0}").Bold().FontSize(11);
                    });

                    col.Item().PaddingVertical(5).LineHorizontal(0.5f);
                    col.Item().AlignCenter().PaddingTop(3).Text(L("ThankYouShort")).FontSize(8).Italic();
                });
            });
        });
    }

    public static byte[] GenerateSupplierStatementPdf(Supplier supplier, List<SupplierLedger> entries, DateTime? from, DateTime? to)
        => BuildSupplierStatementDocument(supplier, entries, from, to).GeneratePdf();

    public static bool PrintSupplierStatement(Supplier supplier, List<SupplierLedger> entries, DateTime? from, DateTime? to)
    {
        try
        {
            var document = BuildSupplierStatementDocument(supplier, entries, from, to);
            PrintingService.ShowPrintPreview(document, $"Statement_{supplier.Name}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IDocument BuildSupplierStatementDocument(Supplier supplier, List<SupplierLedger> entries, DateTime? from, DateTime? to)
    {
        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var urdu = LanguageManager.IsRTL;

        var totalPurchases = entries.Where(e => e.TransactionType is "Purchase" or "CashPurchase").Sum(e => e.Debit);
        var totalPayments = entries.Where(e => e.TransactionType == "Payment").Sum(e => e.Credit);
        var totalReturns = entries.Where(e => e.TransactionType == "PurchaseReturn").Sum(e => e.Credit);
        var totalAdjustments = entries.Where(e => e.TransactionType is "ManualDebit" or "ManualCredit").Sum(e => e.Debit - e.Credit);
        var openingBalance = entries.FirstOrDefault(e => e.TransactionType == "OpeningBalance")?.Balance ?? 0;
        var currentBalance = entries.Count > 0 ? entries[^1].Balance : supplier.CurrentBalance;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(45).Image(company.Logo).FitHeight(); }
                        catch { /* A corrupted/unsupported logo image must never block statement printing. */ }
                    }

                    col.Item().AlignCenter().Text(t => ShopNameText(t, company?.Name, 16));
                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(9).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(company?.Phone))
                        col.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(9);
                    var title = col.Item().PaddingTop(8).AlignCenter().Text(L("SupplierStatementTitle")).FontSize(13).Bold();
                    if (urdu) title.FontFamily(UrduFontFamilyName);
                    col.Item().PaddingTop(10).LineHorizontal(1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Line($"{L("Supplier")}: {supplier.Name}").Bold().FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                            if (!string.IsNullOrWhiteSpace(supplier.CompanyName)) t.Line($"{L("CompanyName")}: {supplier.CompanyName}").FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                            if (!string.IsNullOrWhiteSpace(supplier.Phone)) t.Line($"{L("Phone")}: {supplier.Phone}");
                            if (!string.IsNullOrWhiteSpace(supplier.Address)) t.Line($"{L("Address")}: {supplier.Address}").FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                        });
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Line($"{L("StatementDateLabel")}: {DateTime.Now:dd-MMM-yyyy}");
                            if (from.HasValue || to.HasValue)
                                t.Line($"{L("PeriodLabel")}: {from?.ToString("dd-MMM-yyyy") ?? "Start"} to {to?.ToString("dd-MMM-yyyy") ?? "Today"}");
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(4);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(L("Date")).Bold();
                            header.Cell().Text(L("Description")).Bold();
                            header.Cell().AlignRight().Text(L("Debit")).Bold();
                            header.Cell().AlignRight().Text(L("Credit")).Bold();
                            header.Cell().AlignRight().Text(L("Balance")).Bold();
                        });

                        foreach (var e in entries)
                        {
                            table.Cell().Text(e.TransactionDate.ToString("dd-MMM-yyyy"));
                            table.Cell().Text(e.Description).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                            table.Cell().AlignRight().Text(e.Debit > 0 ? $"{e.Debit:N0}" : "-");
                            table.Cell().AlignRight().Text(e.Credit > 0 ? $"{e.Credit:N0}" : "-");
                            table.Cell().AlignRight().Text($"{e.Balance:N0}");
                        }
                    });

                    col.Item().PaddingTop(15).LineHorizontal(0.5f);

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{L("OpeningBalance")}: Rs. {openingBalance:N0}");
                            c.Item().Text($"{L("TotalPurchases")}: Rs. {totalPurchases:N0}");
                            c.Item().Text($"{L("TotalPaymentsLabel")}: Rs. {totalPayments:N0}");
                            c.Item().Text($"{L("TotalReturnsLabel")}: Rs. {totalReturns:N0}");
                            c.Item().Text($"{L("TotalAdjustmentsLabel")}: Rs. {totalAdjustments:N0}");
                        });
                        row.RelativeItem().AlignRight().Text($"{L("CurrentPayableLabel")}: Rs. {currentBalance:N0}").Bold().FontSize(13);
                    });
                });
            });
        });
    }

    public static byte[] GenerateEmployeeStatementPdf(Employee employee, List<EmployeeLedger> ledgerEntries, List<EmployeeAttendance> attendance, Dictionary<int, string> statusByAttendance)
        => BuildEmployeeStatementDocument(employee, ledgerEntries, attendance, statusByAttendance).GeneratePdf();

    private static IDocument BuildEmployeeStatementDocument(Employee employee, List<EmployeeLedger> ledgerEntries, List<EmployeeAttendance> attendance, Dictionary<int, string> statusByAttendance)
    {
        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var urdu = LanguageManager.IsRTL;

        var totalPayable = attendance.Sum(a => a.Amount);
        var totalPaid = ledgerEntries.Where(l => l.ReferenceType == "Payment").Sum(l => l.Credit);
        var currentBalance = employee.CurrentBalance;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(45).Image(company.Logo).FitHeight(); }
                        catch { /* A corrupted/unsupported logo image must never block statement printing. */ }
                    }

                    col.Item().AlignCenter().Text(t => ShopNameText(t, company?.Name, 16));
                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(9).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    var title = col.Item().PaddingTop(8).AlignCenter().Text(L("EmployeeStatement")).FontSize(13).Bold();
                    if (urdu) title.FontFamily(UrduFontFamilyName);
                    col.Item().PaddingTop(10).LineHorizontal(1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Text(t =>
                    {
                        t.Line($"{L("EmployeeName")}: {employee.Name}").Bold().FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                        if (!string.IsNullOrWhiteSpace(employee.Position)) t.Line($"{L("Position")}: {employee.Position}");
                        if (!string.IsNullOrWhiteSpace(employee.Phone)) t.Line($"{L("Phone")}: {employee.Phone}");
                        t.Line($"{L("DailyWage")}: Rs. {employee.DailyWage:N0}");
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(0.5f);
                    var workedTitle = col.Item().Text(L("WorkedDays")).Bold().FontSize(12);
                    if (urdu) workedTitle.FontFamily(UrduFontFamilyName);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(L("Date")).Bold();
                            header.Cell().AlignRight().Text(L("Amount")).Bold();
                            header.Cell().AlignRight().Text(L("PaymentStatus")).Bold();
                        });

                        foreach (var a in attendance.OrderBy(a => a.WorkDate))
                        {
                            var status = statusByAttendance.GetValueOrDefault(a.Id, "Unpaid");
                            var statusText = status switch
                            {
                                "Paid" => L("StatusPaid"),
                                "Partial" => L("StatusPartiallyPaid"),
                                _ => L("StatusUnpaid")
                            };
                            table.Cell().Text(a.WorkDate.ToString("dd-MMM-yyyy"));
                            table.Cell().AlignRight().Text($"{a.Amount:N0}");
                            table.Cell().AlignRight().Text(statusText);
                        }
                    });

                    col.Item().PaddingTop(15).LineHorizontal(0.5f);
                    var ledgerTitle = col.Item().PaddingTop(10).Text(L("LedgerTab")).Bold().FontSize(12);
                    if (urdu) ledgerTitle.FontFamily(UrduFontFamilyName);

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(4);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text(L("Date")).Bold();
                            header.Cell().Text(L("Description")).Bold();
                            header.Cell().AlignRight().Text(L("Debit")).Bold();
                            header.Cell().AlignRight().Text(L("Credit")).Bold();
                            header.Cell().AlignRight().Text(L("Balance")).Bold();
                        });

                        foreach (var e in ledgerEntries)
                        {
                            table.Cell().Text(e.TransactionDate.ToString("dd-MMM-yyyy"));
                            table.Cell().Text(e.Description);
                            table.Cell().AlignRight().Text(e.Debit > 0 ? $"{e.Debit:N0}" : "-");
                            table.Cell().AlignRight().Text(e.Credit > 0 ? $"{e.Credit:N0}" : "-");
                            table.Cell().AlignRight().Text($"{e.Balance:N0}");
                        }
                    });

                    col.Item().PaddingTop(15).LineHorizontal(0.5f);
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{L("WorkedDays")}: {attendance.Count}");
                            c.Item().Text($"{L("TotalPayable")}: Rs. {totalPayable:N0}");
                            c.Item().Text($"{L("TotalPaid")}: Rs. {totalPaid:N0}");
                        });
                        row.RelativeItem().AlignRight().Text($"{L("RemainingBalance")}: Rs. {currentBalance:N0}").Bold().FontSize(13);
                    });
                });
            });
        });
    }

    public static bool PrintEmployeeStatement(Employee employee, List<EmployeeLedger> ledgerEntries, List<EmployeeAttendance> attendance, Dictionary<int, string> statusByAttendance)
    {
        try
        {
            var document = BuildEmployeeStatementDocument(employee, ledgerEntries, attendance, statusByAttendance);
            PrintingService.ShowPrintPreview(document, $"Statement_{employee.Name}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Small, reusable QuestPDF table generator for the new Inventory reports (low stock, expired, expiring, category valuation) — one generic layout instead of a bespoke renderer per report.</summary>
    public static byte[] GenerateSimpleTablePdf(string title, string[] headers, List<string[]> rows)
        => BuildSimpleTableDocument(title, headers, rows).GeneratePdf();

    private static IDocument BuildSimpleTableDocument(string title, string[] headers, List<string[]> rows)
    {
        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var urdu = LanguageManager.IsRTL;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    if (company?.Logo is { Length: > 0 })
                    {
                        try { col.Item().AlignCenter().MaxHeight(40).Image(company.Logo).FitHeight(); }
                        catch { /* A corrupted/unsupported logo image must never block report printing. */ }
                    }

                    col.Item().AlignCenter().Text(t => ShopNameText(t, company?.Name, 15));
                    if (!string.IsNullOrWhiteSpace(company?.Address))
                        col.Item().AlignCenter().Text(company.Address).FontSize(8).FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();
                    if (!string.IsNullOrWhiteSpace(company?.Phone))
                        col.Item().AlignCenter().Text($"Ph: {company.Phone}").FontSize(8);
                    var title2 = col.Item().PaddingTop(6).AlignCenter().Text(title).FontSize(12).Bold();
                    if (urdu) title2.FontFamily(UrduFontFamilyName);
                    col.Item().AlignCenter().Text($"{L("GeneratedLabel")}: {DateTime.Now:dd-MMM-yyyy hh:mm tt}").FontSize(8);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in headers) c.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers) header.Cell().Text(h).Bold();
                    });

                    foreach (var row in rows)
                        foreach (var cell in row)
                            table.Cell().Text(cell);
                });
            });
        });
    }

    public static bool PrintSimpleTable(string title, string[] headers, List<string[]> rows)
    {
        try
        {
            // Routed through the centralised, paper-size-aware report printer so the user
            // can pick A4 / A5 / Letter / custom and the table re-flows live.
            ReportPrintService.PreviewTable(title, headers, rows);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prints product barcode labels to the thermal (receipt) printer — one label per
    /// requested copy. Each label shows the product name (directly above the barcode),
    /// the sale price, and the Code 128 barcode image with its digits underneath.
    /// </summary>
    public static bool PrintProductLabels(List<(Product product, int copies)> items)
    {
        try
        {
            var document = BuildProductLabelsDocument(items);
            PrintingService.ShowPrintPreview(document, "Barcode_Labels");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IDocument BuildProductLabelsDocument(List<(Product product, int copies)> items)
    {
        const QuestPDF.Infrastructure.Unit mm = QuestPDF.Infrastructure.Unit.Millimetre;

        using var db = new AppDbContext();
        var company = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        var geometry = PrintLayoutService.Resolve(company);
        var shopName = string.IsNullOrWhiteSpace(company?.Name) ? "" : company!.Name.Trim();

        // Uniform, generous label height so a long / wrapped product name can never push the
        // barcode off the page (which would abort the whole print run).
        var labelWidthMm = (float)geometry.WidthMm;
        const float labelHeightMm = 62f;

        var labels = new List<Product>();
        foreach (var (product, copies) in items)
            for (var i = 0; i < Math.Max(1, copies); i++)
                labels.Add(product);
        if (labels.Count == 0) labels.Add(new Product { NameEnglish = "-" });

        // Render each distinct barcode once; repeated copies reuse the same PNG.
        var pngCache = new Dictionary<string, byte[]?>();
        byte[]? PngFor(string code)
        {
            if (!pngCache.TryGetValue(code, out var png))
            {
                png = BarcodeService.RenderPng(code, moduleWidth: 2, barHeight: 50);
                pngCache[code] = png;
            }
            return png;
        }

        return Document.Create(container =>
        {
            foreach (var p in labels)
            {
                container.Page(page =>
                {
                    page.Size(labelWidthMm, labelHeightMm, mm);
                    page.Margin(3, mm);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Content().Column(col =>
                    {
                        col.Spacing(1);

                        if (!string.IsNullOrWhiteSpace(shopName))
                            col.Item().AlignCenter().Text(shopName).FontSize(6.5f).SemiBold()
                                .FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();

                        // Product name — the label's headline, sitting right above the barcode.
                        col.Item().AlignCenter().Text(p.NameEnglish ?? "").FontSize(10.5f).Bold();
                        if (!string.IsNullOrWhiteSpace(p.NameUrdu))
                            col.Item().AlignCenter().Text(p.NameUrdu).FontSize(8)
                                .FontFamily(UrduFontFamilyName).DirectionFromRightToLeft();

                        col.Item().AlignCenter().Text($"Rs. {p.SalePrice:N0}").FontSize(11).Bold();

                        var code = p.Barcode ?? "";
                        var png = PngFor(code);
                        if (png != null)
                            col.Item().PaddingTop(1).AlignCenter().MaxHeight(58).Image(png).FitHeight();
                        else
                            col.Item().PaddingTop(1).AlignCenter().Text(code).FontSize(10).Bold();
                    });
                });
            }
        });
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
    /// Opens an in-app print preview (page layout + printer selection) and prints
    /// through the Windows print pipeline directly, via <see cref="PrintingService"/>.
    /// </summary>
    public static bool PrintReceipt(Sale sale, Customer? customer, List<ReceiptLineItem> lines)
    {
        try
        {
            var document = BuildReceiptDocument(sale, customer, lines);
            PrintingService.ShowPrintPreview(document, $"Receipt_{sale.InvoiceNumber}");
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
