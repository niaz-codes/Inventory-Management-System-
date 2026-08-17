using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class SalesService
{
    private readonly ProductService _productService = new();

    public string GenerateInvoiceNumber()
    {
        using var db = new AppDbContext();
        var year = DateTime.Now.Year;
        var prefix = $"NHI-{year}-";
        var lastSale = db.Sales.Where(s => s.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(s => s.InvoiceNumber).FirstOrDefault();
        if (lastSale == null) return $"{prefix}000001";
        var lastNum = int.Parse(lastSale.InvoiceNumber.Split('-').Last());
        return $"{prefix}{(lastNum + 1):D6}";
    }

    public int CompleteSale(Sale sale, List<SaleItem> items)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            sale.CreatedAt = DateTime.Now;
            sale.CreatedBy = AuthService.CurrentUser?.Id;
            db.Sales.Add(sale);
            db.SaveChanges();

            foreach (var item in items)
            {
                item.SaleId = sale.Id;
                db.SaleItems.Add(item);

                var product = db.Products.Find(item.ProductId);
                if (product != null)
                {
                    var prevStock = product.CurrentStock;
                    product.CurrentStock -= item.Quantity;
                    db.StockTransactions.Add(new StockTransaction
                    {
                        ProductId = item.ProductId,
                        TransactionType = "SALE",
                        Quantity = -item.Quantity,
                        PreviousStock = prevStock,
                        NewStock = product.CurrentStock,
                        Description = $"Sale Invoice: {sale.InvoiceNumber}",
                        ReferenceId = sale.Id,
                        ReferenceType = "Sale"
                    });
                }
            }

            if (sale.IsCreditSale && sale.CustomerId.HasValue && sale.RemainingAmount > 0)
            {
                var customer = db.Customers.Find(sale.CustomerId);
                if (customer != null)
                {
                    customer.CurrentBalance += sale.RemainingAmount;
                    db.CustomerLedgers.Add(new CustomerLedger
                    {
                        CustomerId = sale.CustomerId.Value,
                        Description = $"Credit Sale - {sale.InvoiceNumber}",
                        Debit = sale.RemainingAmount,
                        Credit = 0,
                        Balance = customer.CurrentBalance,
                        ReferenceId = sale.Id,
                        ReferenceType = "Sale"
                    });
                }
            }

            db.CashTransactions.Add(new CashTransaction
            {
                TransactionType = "SALE",
                Amount = sale.PaidAmount,
                IsInflow = true,
                Description = $"Sale - {sale.InvoiceNumber}",
                ReferenceId = sale.Id,
                ReferenceType = "Sale"
            });

            db.SaveChanges();
            transaction.Commit();
            return sale.Id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<Sale> GetSales(DateTime? from = null, DateTime? to = null)
    {
        using var db = new AppDbContext();
        var query = db.Sales.Include(s => s.Customer).Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => !s.IsDeleted).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.SaleDate >= from.Value);
        if (to.HasValue) query = query.Where(s => s.SaleDate <= to.Value.AddDays(1));
        return query.OrderByDescending(s => s.SaleDate).ToList();
    }

    public List<Sale> GetTodaySales()
    {
        var today = DateTime.Today;
        return GetSales(today, today);
    }

    public Sale? GetSaleById(int id)
    {
        using var db = new AppDbContext();
        return db.Sales.Include(s => s.Customer).Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefault(s => s.Id == id);
    }

    public bool VoidSale(int saleId)
    {
        if (!AuthService.IsAdmin()) return false;
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var sale = db.Sales.Include(s => s.Items).FirstOrDefault(s => s.Id == saleId);
            if (sale == null) return false;

            sale.IsDeleted = true;
            foreach (var item in sale.Items)
            {
                var product = db.Products.Find(item.ProductId);
                if (product != null)
                {
                    var prev = product.CurrentStock;
                    product.CurrentStock += item.Quantity;
                    db.StockTransactions.Add(new StockTransaction
                    {
                        ProductId = item.ProductId,
                        TransactionType = "VOID",
                        Quantity = item.Quantity,
                        PreviousStock = prev,
                        NewStock = product.CurrentStock,
                        Description = $"Void Sale: {sale.InvoiceNumber}",
                        ReferenceId = saleId,
                        ReferenceType = "Sale"
                    });
                }
            }

            if (sale.IsCreditSale && sale.CustomerId.HasValue && sale.RemainingAmount > 0)
            {
                var customer = db.Customers.Find(sale.CustomerId);
                if (customer != null)
                {
                    customer.CurrentBalance -= sale.RemainingAmount;
                    db.CustomerLedgers.Add(new CustomerLedger
                    {
                        CustomerId = sale.CustomerId.Value,
                        Description = $"Void Sale Credit Reversal - {sale.InvoiceNumber}",
                        Debit = 0,
                        Credit = sale.RemainingAmount,
                        Balance = customer.CurrentBalance,
                        ReferenceId = saleId,
                        ReferenceType = "Sale"
                    });
                }
            }

            db.SaveChanges();
            transaction.Commit();
            AuditLogService.Log("VOID_SALE", "Sale", saleId, $"Voided invoice {sale.InvoiceNumber}");
            return true;
        }
        catch { transaction.Rollback(); throw; }
    }

    public decimal GetTodaySalesTotal()
    {
        using var db = new AppDbContext();
        return db.Sales.Where(s => !s.IsDeleted && s.SaleDate.Date == DateTime.Today).ToList().Sum(s => s.GrandTotal);
    }

    public decimal GetTodayProfit()
    {
        using var db = new AppDbContext();
        var todaySales = db.SaleItems.Where(s => s.Sale!.SaleDate.Date == DateTime.Today && !s.Sale!.IsDeleted)
            .ToList().Sum(s => (s.UnitPrice - s.PurchasePriceAtSale) * s.Quantity);
        var todayExpenses = db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate.Date == DateTime.Today).ToList().Sum(e => e.Amount);
        return todaySales - todayExpenses;
    }
}
