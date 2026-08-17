using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class PurchaseService
{
    public string GenerateInvoiceNumber()
    {
        using var db = new AppDbContext();
        var year = DateTime.Now.Year;
        var prefix = $"PUR-{year}-";
        var last = db.Purchases.Where(p => p.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(p => p.InvoiceNumber).FirstOrDefault();
        if (last == null) return $"{prefix}000001";
        var lastNum = int.Parse(last.InvoiceNumber.Split('-').Last());
        return $"{prefix}{(lastNum + 1):D6}";
    }

    public int CompletePurchase(Purchase purchase, List<PurchaseItem> items)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            purchase.CreatedAt = DateTime.Now;
            purchase.CreatedBy = AuthService.CurrentUser?.Id;
            db.Purchases.Add(purchase);
            db.SaveChanges();

            foreach (var item in items)
            {
                item.PurchaseId = purchase.Id;
                db.PurchaseItems.Add(item);

                var product = db.Products.Find(item.ProductId);
                if (product != null)
                {
                    var prev = product.CurrentStock;
                    product.CurrentStock += item.Quantity;
                    product.PurchasePrice = item.UnitPrice;
                    db.StockTransactions.Add(new StockTransaction
                    {
                        ProductId = item.ProductId,
                        TransactionType = "PURCHASE",
                        Quantity = item.Quantity,
                        PreviousStock = prev,
                        NewStock = product.CurrentStock,
                        Description = $"Purchase: {purchase.InvoiceNumber}",
                        ReferenceId = purchase.Id,
                        ReferenceType = "Purchase"
                    });
                }
            }

            if (purchase.SupplierId.HasValue && purchase.RemainingAmount > 0)
            {
                var supplier = db.Suppliers.Find(purchase.SupplierId);
                if (supplier != null)
                {
                    supplier.CurrentBalance += purchase.RemainingAmount;
                    db.SupplierLedgers.Add(new SupplierLedger
                    {
                        SupplierId = purchase.SupplierId.Value,
                        Description = $"Purchase - {purchase.InvoiceNumber}",
                        Debit = purchase.RemainingAmount,
                        Credit = 0,
                        Balance = supplier.CurrentBalance,
                        ReferenceId = purchase.Id,
                        ReferenceType = "Purchase"
                    });
                }
            }

            if (purchase.PaidAmount > 0)
            {
                db.CashTransactions.Add(new CashTransaction
                {
                    TransactionType = "PURCHASE",
                    Amount = purchase.PaidAmount,
                    IsInflow = false,
                    Description = $"Purchase - {purchase.InvoiceNumber}",
                    ReferenceId = purchase.Id,
                    ReferenceType = "Purchase"
                });
            }

            db.SaveChanges();
            transaction.Commit();
            return purchase.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public List<Purchase> GetPurchases(DateTime? from = null, DateTime? to = null)
    {
        using var db = new AppDbContext();
        var query = db.Purchases.Include(p => p.Supplier).Include(p => p.Items).AsQueryable();
        if (from.HasValue) query = query.Where(p => p.PurchaseDate >= from.Value);
        if (to.HasValue) query = query.Where(p => p.PurchaseDate <= to.Value.AddDays(1));
        return query.Where(p => !p.IsDeleted).OrderByDescending(p => p.PurchaseDate).ToList();
    }

    /// <summary>
    /// Processes a partial or full return of one product from a completed purchase:
    /// decreases stock, updates the supplier's payable ledger, and records the return.
    /// Runs as a single transaction.
    /// </summary>
    public (bool success, string? error) ProcessPurchaseReturn(int purchaseId, int productId, decimal quantity, string? reason)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var purchase = db.Purchases.Include(p => p.Items).Include(p => p.Supplier).FirstOrDefault(p => p.Id == purchaseId);
            if (purchase == null) return (false, "Purchase not found");

            var purchaseItem = purchase.Items.FirstOrDefault(i => i.ProductId == productId);
            if (purchaseItem == null) return (false, "Product not found on this invoice");

            var alreadyReturned = db.PurchaseReturns.Where(r => r.PurchaseId == purchaseId && r.ProductId == productId).Sum(r => r.Quantity);
            if (quantity <= 0 || quantity > purchaseItem.Quantity - alreadyReturned)
                return (false, "Return quantity exceeds what was purchased");

            var refundAmount = quantity * purchaseItem.UnitPrice;

            var product = db.Products.Find(productId);
            if (product != null)
            {
                var prevStock = product.CurrentStock;
                product.CurrentStock -= quantity;
                db.StockTransactions.Add(new StockTransaction
                {
                    ProductId = productId,
                    TransactionType = "PURCHASE_RETURN",
                    Quantity = -quantity,
                    PreviousStock = prevStock,
                    NewStock = product.CurrentStock,
                    Description = $"Purchase Return: {purchase.InvoiceNumber}",
                    ReferenceId = purchaseId,
                    ReferenceType = "Purchase"
                });
            }

            if (purchase.SupplierId.HasValue)
            {
                var supplier = db.Suppliers.Find(purchase.SupplierId);
                if (supplier != null)
                {
                    supplier.CurrentBalance -= refundAmount;
                    db.SupplierLedgers.Add(new SupplierLedger
                    {
                        SupplierId = purchase.SupplierId.Value,
                        Description = $"Purchase Return - {purchase.InvoiceNumber}",
                        Debit = 0,
                        Credit = refundAmount,
                        Balance = supplier.CurrentBalance,
                        ReferenceId = purchaseId,
                        ReferenceType = "PurchaseReturn"
                    });
                }
            }

            db.PurchaseReturns.Add(new PurchaseReturn
            {
                PurchaseId = purchaseId,
                ProductId = productId,
                Quantity = quantity,
                RefundAmount = refundAmount,
                Reason = reason,
                CreatedBy = AuthService.CurrentUser?.Id
            });

            db.SaveChanges();
            transaction.Commit();
            AuditLogService.Log("PURCHASE_RETURN", "Purchase", purchaseId, $"Returned {quantity} of product #{productId} from {purchase.InvoiceNumber}");
            return (true, null);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return (false, ex.Message);
        }
    }

    public decimal GetTodayPurchasesTotal()
    {
        using var db = new AppDbContext();
        return db.Purchases.Where(p => !p.IsDeleted && p.PurchaseDate.Date == DateTime.Today).ToList().Sum(p => p.GrandTotal);
    }
}
