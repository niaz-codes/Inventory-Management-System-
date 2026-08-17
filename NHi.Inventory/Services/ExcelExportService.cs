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

    public static bool ExportPurchases(string filePath, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Purchases");

            ws.Cell(1, 1).Value = "Purchases Report";
            ws.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 6).Style.Font.FontSize = 14;

            var headers = new[] { "Invoice #", "Date", "Supplier", "Total", "Paid", "Remaining" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }

            var query = db.Purchases.Include(p => p.Supplier).Where(p => !p.IsDeleted).AsQueryable();
            if (from.HasValue) query = query.Where(p => p.PurchaseDate >= from.Value);
            if (to.HasValue) query = query.Where(p => p.PurchaseDate <= to.Value.AddDays(1));

            int row = 4;
            foreach (var p in query.OrderByDescending(p => p.PurchaseDate).ToList())
            {
                ws.Cell(row, 1).Value = p.InvoiceNumber;
                ws.Cell(row, 2).Value = p.PurchaseDate.ToString("dd-MMM-yyyy");
                ws.Cell(row, 3).Value = p.Supplier?.Name ?? "";
                ws.Cell(row, 4).Value = (double)p.GrandTotal;
                ws.Cell(row, 5).Value = (double)p.PaidAmount;
                ws.Cell(row, 6).Value = (double)p.RemainingAmount;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportCustomers(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Customers");

            ws.Cell(1, 1).Value = "Customers Report";
            ws.Range(1, 1, 1, 5).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 5).Style.Font.FontSize = 14;

            var headers = new[] { "Name", "Father Name", "Phone", "Address", "Balance" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
            }

            int row = 4;
            foreach (var c in db.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList())
            {
                ws.Cell(row, 1).Value = c.Name;
                ws.Cell(row, 2).Value = c.FatherName ?? "";
                ws.Cell(row, 3).Value = c.Phone ?? "";
                ws.Cell(row, 4).Value = c.Address ?? "";
                ws.Cell(row, 5).Value = (double)c.CurrentBalance;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportSuppliers(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Suppliers");

            ws.Cell(1, 1).Value = "Suppliers Report";
            ws.Range(1, 1, 1, 4).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 4).Style.Font.FontSize = 14;

            var headers = new[] { "Name", "Contact Person", "Phone", "Balance" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
            }

            int row = 4;
            foreach (var s in db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList())
            {
                ws.Cell(row, 1).Value = s.Name;
                ws.Cell(row, 2).Value = s.ContactPerson ?? "";
                ws.Cell(row, 3).Value = s.Phone ?? "";
                ws.Cell(row, 4).Value = (double)s.CurrentBalance;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportCustomerLedger(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Customer Khata");

            ws.Cell(1, 1).Value = "Customer Khata";
            ws.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 6).Style.Font.FontSize = 14;

            var headers = new[] { "Date", "Customer", "Description", "Debit", "Credit", "Balance" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightCyan;
            }

            var entries = db.CustomerLedgers.Include(l => l.Customer).OrderByDescending(l => l.TransactionDate).ToList();
            int row = 4;
            foreach (var l in entries)
            {
                ws.Cell(row, 1).Value = l.TransactionDate.ToString("dd-MMM-yyyy");
                ws.Cell(row, 2).Value = l.Customer?.Name ?? "";
                ws.Cell(row, 3).Value = l.Description;
                ws.Cell(row, 4).Value = (double)l.Debit;
                ws.Cell(row, 5).Value = (double)l.Credit;
                ws.Cell(row, 6).Value = (double)l.Balance;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportSupplierLedger(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Supplier Ledger");

            ws.Cell(1, 1).Value = "Supplier Ledger";
            ws.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 6).Style.Font.FontSize = 14;

            var headers = new[] { "Date", "Supplier", "Description", "Debit", "Credit", "Balance" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightCyan;
            }

            var entries = db.SupplierLedgers.Include(l => l.Supplier).OrderByDescending(l => l.TransactionDate).ToList();
            int row = 4;
            foreach (var l in entries)
            {
                ws.Cell(row, 1).Value = l.TransactionDate.ToString("dd-MMM-yyyy");
                ws.Cell(row, 2).Value = l.Supplier?.Name ?? "";
                ws.Cell(row, 3).Value = l.Description;
                ws.Cell(row, 4).Value = (double)l.Debit;
                ws.Cell(row, 5).Value = (double)l.Credit;
                ws.Cell(row, 6).Value = (double)l.Balance;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportExpenses(string filePath, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Expenses");

            ws.Cell(1, 1).Value = "Expenses Report";
            ws.Range(1, 1, 1, 4).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 4).Style.Font.FontSize = 14;

            var headers = new[] { "Date", "Category", "Amount", "Description" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightPink;
            }

            var query = db.Expenses.Where(e => !e.IsDeleted).AsQueryable();
            if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value);
            if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.AddDays(1));

            int row = 4;
            foreach (var e in query.OrderByDescending(e => e.ExpenseDate).ToList())
            {
                ws.Cell(row, 1).Value = e.ExpenseDate.ToString("dd-MMM-yyyy");
                ws.Cell(row, 2).Value = e.Category;
                ws.Cell(row, 3).Value = (double)e.Amount;
                ws.Cell(row, 4).Value = e.Description ?? "";
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportDailyClosings(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Daily Closing");

            ws.Cell(1, 1).Value = "Daily Closing History";
            ws.Range(1, 1, 1, 9).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 9).Style.Font.FontSize = 14;

            var headers = new[] { "Date", "Opening Cash", "Cash Sales", "Credit Sales", "Purchases", "Expenses", "Expected Closing", "Actual Closing", "Difference" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(3, i + 1).Value = headers[i];
                ws.Cell(3, i + 1).Style.Font.Bold = true;
                ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }

            int row = 4;
            foreach (var d in db.DailyClosings.OrderByDescending(d => d.ClosingDate).ToList())
            {
                ws.Cell(row, 1).Value = d.ClosingDate.ToString("dd-MMM-yyyy");
                ws.Cell(row, 2).Value = (double)d.OpeningCash;
                ws.Cell(row, 3).Value = (double)d.TotalCashSales;
                ws.Cell(row, 4).Value = (double)d.TotalCreditSales;
                ws.Cell(row, 5).Value = (double)d.TotalPurchases;
                ws.Cell(row, 6).Value = (double)d.TotalExpenses;
                ws.Cell(row, 7).Value = (double)d.ExpectedClosingCash;
                ws.Cell(row, 8).Value = (double)d.ActualClosingCash;
                ws.Cell(row, 9).Value = (double)d.Difference;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }

    public static bool ExportProfitSummary(string filePath)
    {
        try
        {
            using var db = new AppDbContext();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Profit Summary");
            var report = new ReportService();

            ws.Cell(1, 1).Value = "Profit Summary Report";
            ws.Range(1, 1, 1, 2).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 2).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Value = $"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}";

            var todayExpenses = new ExpenseService().GetTodayExpenses();
            var todayProfit = report.GetTodayProfit();
            var monthlyProfit = report.GetMonthlyProfit(DateTime.Today);
            var stockValue = report.GetStockValue();

            var rows = new (string Label, decimal Value)[]
            {
                ("Today's Gross Profit", todayProfit + todayExpenses),
                ("Today's Expenses", todayExpenses),
                ("Today's Net Profit", todayProfit),
                ("This Month's Net Profit", monthlyProfit),
                ("Current Stock Value", stockValue),
            };

            int row = 4;
            foreach (var (label, value) in rows)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 2).Value = (double)value;
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
