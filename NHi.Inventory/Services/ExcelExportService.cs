using NHi.Inventory.Data;
using NHi.Inventory.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public static class ExcelExportService
{
    public static bool ExportSales(string filePath, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sales");

            var company = db.CompanySettings.FirstOrDefault();
            ws.Cell(1, 1).Value = company?.CompanyName ?? "NHi Inventory";
            ws.Range(1, 1, 1, 8).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 8).Style.Font.FontSize = 14;

            ws.Cell(2, 1).Value = $"Sales Report: {DateTime.Now:dd-MMM-yyyy}";
            ws.Range(2, 1, 2, 8).Merge();

            var headers = new[] { "Invoice #", "Date", "Customer", "Subtotal", "Discount", "Total", "Paid", "Remaining" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(4, i + 1).Value = headers[i];
                ws.Cell(4, i + 1).Style.Font.Bold = true;
                ws.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }

            var query = db.Sales.Include(s => s.Customer).Where(s => !s.IsDeleted).AsQueryable();
            if (from.HasValue) query = query.Where(s => s.SaleDate >= from.Value);
            if (to.HasValue) query = query.Where(s => s.SaleDate <= to.Value.AddDays(1));

            int row = 5;
            foreach (var sale in query.OrderByDescending(s => s.SaleDate).ToList())
            {
                ws.Cell(row, 1).Value = sale.InvoiceNumber;
                ws.Cell(row, 2).Value = sale.SaleDate.ToString("dd-MMM-yyyy");
                ws.Cell(row, 3).Value = sale.Customer?.Name ?? "Walk-in";
                ws.Cell(row, 4).Value = (double)sale.Subtotal;
                ws.Cell(row, 5).Value = (double)sale.DiscountAmount;
                ws.Cell(row, 6).Value = (double)sale.GrandTotal;
                ws.Cell(row, 7).Value = (double)sale.PaidAmount;
                ws.Cell(row, 8).Value = (double)sale.RemainingAmount;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportProducts(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Products");

            ws.Cell(1, 1).Value = "Products Report";
            ws.Range(1, 1, 1, 8).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 8).Style.Font.FontSize = 14;

            var headers = new[] { "Name", "Barcode", "Category", "Purchase Price", "Sale Price", "Stock", "Min Stock", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
            }

            var products = db.Products.Include(p => p.Category).Where(p => p.IsActive).OrderBy(p => p.NameEnglish).ToList();
            int row = 4;
            foreach (var p in products)
            {
                ws.Cell(row, 1).Value = p.NameEnglish;
                ws.Cell(row, 2).Value = p.Barcode ?? "";
                ws.Cell(row, 3).Value = p.Category?.Name ?? "";
                ws.Cell(row, 4).Value = (double)p.PurchasePrice;
                ws.Cell(row, 5).Value = (double)p.SalePrice;
                ws.Cell(row, 6).Value = (double)p.CurrentStock;
                ws.Cell(row, 7).Value = (double)p.MinimumStock;
                ws.Cell(row, 8).Value = p.CurrentStock <= p.MinimumStock ? "LOW STOCK" : "OK";
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportStock(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Stock");

            ws.Cell(1, 1).Value = "Stock Report";
            ws.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 6).Style.Font.FontSize = 14;

            var headers = new[] { "Product", "Barcode", "Purchase Price", "Current Stock", "Stock Value", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightCyan;
            }

            var products = db.Products.Where(p => p.IsActive).OrderBy(p => p.NameEnglish).ToList();
            int row = 4;
            foreach (var p in products)
            {
                ws.Cell(row, 1).Value = p.NameEnglish;
                ws.Cell(row, 2).Value = p.Barcode ?? "";
                ws.Cell(row, 3).Value = (double)p.PurchasePrice;
                ws.Cell(row, 4).Value = (double)p.CurrentStock;
                ws.Cell(row, 5).Value = (double)(p.CurrentStock * p.PurchasePrice);
                ws.Cell(row, 6).Value = p.CurrentStock <= p.MinimumStock ? "LOW STOCK" : "OK";
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }
}
