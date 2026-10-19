using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class SalesService
{
    private readonly ProductService _productService = new();

    /// <summary>Preview only — the authoritative number is assigned atomically inside CompleteSale's own transaction to avoid a read-then-format race on rapid/duplicate submissions.</summary>
    public string GenerateInvoiceNumber()
    {
        using var db = new AppDbContext();
        return GenerateInvoiceNumberInternal(db);
    }

    private static string GenerateInvoiceNumberInternal(AppDbContext db)
    {
        var year = DateTime.Now.Year;
        var prefix = $"NHI-{year}-";
        var lastSale = db.Sales.Where(s => s.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(s => s.InvoiceNumber).FirstOrDefault();
        if (lastSale == null) return $"{prefix}000001";
        var lastNum = int.Parse(lastSale.InvoiceNumber.Split('-').Last());
        return $"{prefix}{(lastNum + 1):D6}";
    }

    /// <summary>
    /// Validates stock, saves the sale/items/stock/Khata/cash movements as one transaction,
    /// and assigns the invoice number atomically within it. Returns an error resource key
    /// (not a pre-built message) on failure, matching ProductService.Save's convention, so
    /// nothing is written unless every line item can actually be fulfilled.
    /// </summary>
    public (bool success, string? errorKey, int saleId) CompleteSale(Sale sale, List<SaleItem> items, DateTime? creditDueDate = null)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            if (items.Count == 0) { transaction.Rollback(); return (false, "EmptyCart", 0); }

            foreach (var item in items)
            {
                if (item.Quantity <= 0) { transaction.Rollback(); return (false, "InvalidStock", 0); }
                var product = db.Products.Find(item.ProductId);
                if (product == null) { transaction.Rollback(); return (false, "NoDataFound", 0); }
                if (product.CurrentStock < item.Quantity) { transaction.Rollback(); return (false, "InsufficientStock", 0); }
            }

            sale.ShopId = AuthService.CurrentShopId ?? 1;
            sale.InvoiceNumber = GenerateInvoiceNumberInternal(db);
            sale.CreatedAt = DateTime.Now;
            sale.CreatedBy = AuthService.CurrentUser?.Id;
            db.Sales.Add(sale);
            db.SaveChanges();

            foreach (var item in items)
            {
                item.ShopId = sale.ShopId;
                item.SaleId = sale.Id;
                db.SaleItems.Add(item);

                var product = db.Products.Find(item.ProductId)!;
                var prevStock = product.CurrentStock;
                product.CurrentStock -= item.Quantity;
                db.StockTransactions.Add(new StockTransaction
                {
                    ShopId = sale.ShopId,
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

            if (sale.IsCreditSale && sale.CustomerId.HasValue && sale.RemainingAmount > 0)
            {
                var customer = db.Customers.Find(sale.CustomerId);
                if (customer != null)
                {
                    customer.CurrentBalance += sale.RemainingAmount;
                    db.CustomerLedgers.Add(new CustomerLedger
                    {
                        ShopId = sale.ShopId,
                        CustomerId = sale.CustomerId.Value,
                        Description = $"Credit Sale - {sale.InvoiceNumber}",
                        Debit = sale.RemainingAmount,
                        Credit = 0,
                        Balance = customer.CurrentBalance,
                        ReferenceId = sale.Id,
                        ReferenceType = "Sale",
                        DueDate = creditDueDate,
                        CreatedBy = sale.CreatedBy,
                        TransactionType = "CreditSale"
                    });
                }
            }

            db.CashTransactions.Add(new CashTransaction
            {
                ShopId = sale.ShopId,
                TransactionType = "SALE",
                Amount = sale.PaidAmount,
                IsInflow = true,
                Description = $"Sale - {sale.InvoiceNumber}",
                ReferenceId = sale.Id,
                ReferenceType = "Sale"
            });

            db.SaveChanges();
            transaction.Commit();

            // Sending the invoice over WhatsApp is the caller's responsibility — POSScreen opens
            // ReceiptActionsDialog after a successful sale, which calls WhatsAppService only if
            // the employee explicitly clicks "Send WhatsApp". Keeping it out of this
            // transactional method keeps CompleteSale free of UI/messaging side effects
            // (TestDataSeeder's demo sales use this method too, with no customer interaction).

            return (true, null, sale.Id);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<Sale> GetSales(DateTime? from = null, DateTime? to = null, int? customerId = null)
    {
        using var db = new AppDbContext();
        var query = db.Sales.Include(s => s.Customer).Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => !s.IsDeleted).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.SaleDate >= from.Value);
        if (to.HasValue) query = query.Where(s => s.SaleDate <= to.Value.AddDays(1));
        if (customerId.HasValue) query = query.Where(s => s.CustomerId == customerId.Value);
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
        return db.Sales.Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Unit)
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
                        ShopId = sale.ShopId,
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
                        ShopId = sale.ShopId,
                        CustomerId = sale.CustomerId.Value,
                        Description = $"Void Sale Credit Reversal - {sale.InvoiceNumber}",
                        Debit = 0,
                        Credit = sale.RemainingAmount,
                        Balance = customer.CurrentBalance,
                        ReferenceId = saleId,
                        ReferenceType = "Sale",
                        CreatedBy = AuthService.CurrentUser?.Id,
                        TransactionType = "Reversal"
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

    /// <summary>
    /// Processes a partial or full return of one product from a completed sale:
    /// restores stock, reverses the customer's credit ledger (or refunds cash),
    /// and records the return. Runs as a single transaction.
    /// </summary>
    public (bool success, string? error) ProcessSaleReturn(int saleId, int productId, decimal quantity, string? reason)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var sale = db.Sales.Include(s => s.Items).Include(s => s.Customer).FirstOrDefault(s => s.Id == saleId);
            if (sale == null) return (false, "Sale not found");

            var saleItem = sale.Items.FirstOrDefault(i => i.ProductId == productId);
            if (saleItem == null) return (false, "Product not found on this invoice");

            var alreadyReturned = db.SaleReturns.Where(r => r.SaleId == saleId && r.ProductId == productId).Sum(r => r.Quantity);
            if (quantity <= 0 || quantity > saleItem.Quantity - alreadyReturned)
                return (false, "Return quantity exceeds what was sold");

            var refundAmount = quantity * saleItem.UnitPrice;

            var product = db.Products.Find(productId);
            if (product != null)
            {
                var prevStock = product.CurrentStock;
                product.CurrentStock += quantity;
                db.StockTransactions.Add(new StockTransaction
                {
                    ShopId = sale.ShopId,
                    ProductId = productId,
                    TransactionType = "SALE_RETURN",
                    Quantity = quantity,
                    PreviousStock = prevStock,
                    NewStock = product.CurrentStock,
                    Description = $"Sale Return: {sale.InvoiceNumber}",
                    ReferenceId = saleId,
                    ReferenceType = "Sale"
                });
            }

            if (sale.CustomerId.HasValue)
            {
                var customer = db.Customers.Find(sale.CustomerId);
                if (customer != null)
                {
                    customer.CurrentBalance -= refundAmount;
                    db.CustomerLedgers.Add(new CustomerLedger
                    {
                        ShopId = sale.ShopId,
                        CustomerId = sale.CustomerId.Value,
                        Description = $"Sale Return - {sale.InvoiceNumber}",
                        Debit = 0,
                        Credit = refundAmount,
                        Balance = customer.CurrentBalance,
                        ReferenceId = saleId,
                        ReferenceType = "SaleReturn",
                        CreatedBy = AuthService.CurrentUser?.Id,
                        TransactionType = "SaleReturn"
                    });
                }
            }
            else
            {
                db.CashTransactions.Add(new CashTransaction
                {
                    ShopId = sale.ShopId,
                    TransactionType = "SALE_RETURN",
                    Amount = refundAmount,
                    IsInflow = false,
                    Description = $"Refund - {sale.InvoiceNumber}",
                    ReferenceId = saleId,
                    ReferenceType = "SaleReturn"
                });
            }

            db.SaleReturns.Add(new SaleReturn
            {
                ShopId = sale.ShopId,
                SaleId = saleId,
                ProductId = productId,
                Quantity = quantity,
                RefundAmount = refundAmount,
                Reason = reason,
                CreatedBy = AuthService.CurrentUser?.Id
            });

            db.SaveChanges();
            transaction.Commit();
            AuditLogService.Log("SALE_RETURN", "Sale", saleId, $"Returned {quantity} of product #{productId} from {sale.InvoiceNumber}");
            return (true, null);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return (false, ex.Message);
        }
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
