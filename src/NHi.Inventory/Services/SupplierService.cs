using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class SupplierService
{
    public List<Supplier> GetAll(bool activeOnly = true)
    {
        using var db = new AppDbContext();
        var query = db.Suppliers.AsQueryable();
        if (activeOnly) query = query.Where(s => s.IsActive);
        return query.OrderBy(s => s.Name).ToList();
    }

    public Supplier? GetById(int id)
    {
        using var db = new AppDbContext();
        return db.Suppliers.Find(id);
    }

    public List<Supplier> Search(string searchText)
    {
        using var db = new AppDbContext();
        var text = searchText.ToLower();
        return db.Suppliers.Where(s => s.IsActive && (
                s.Name.ToLower().Contains(text) ||
                (s.CompanyName != null && s.CompanyName.ToLower().Contains(text)) ||
                (s.Phone != null && s.Phone.Contains(searchText)) ||
                (s.WhatsAppNumber != null && s.WhatsAppNumber.Contains(searchText))))
            .OrderBy(s => s.Name).ToList();
    }

    /// <summary>
    /// Creates or updates a supplier. On create, seeds CurrentBalance from OpeningBalance and
    /// writes a matching "Opening Balance" ledger row — mirrors CustomerService.Save so a
    /// supplier's opening balance is never lost / silently different from the ledger.
    /// </summary>
    public int Save(Supplier supplier)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            if (supplier.Id == 0)
            {
                supplier.ShopId = AuthService.CurrentShopId ?? 1;
                supplier.CreatedAt = DateTime.Now;
                supplier.CurrentBalance = supplier.OpeningBalance;
                db.Suppliers.Add(supplier);
                db.SaveChanges();

                if (supplier.OpeningBalance != 0)
                {
                    db.SupplierLedgers.Add(new SupplierLedger
                    {
                        ShopId = supplier.ShopId,
                        SupplierId = supplier.Id,
                        Description = "Opening Balance",
                        Debit = supplier.OpeningBalance > 0 ? supplier.OpeningBalance : 0,
                        Credit = supplier.OpeningBalance < 0 ? -supplier.OpeningBalance : 0,
                        Balance = supplier.OpeningBalance,
                        CreatedBy = AuthService.CurrentUser?.Id,
                        TransactionType = "OpeningBalance"
                    });
                    db.SaveChanges();
                }

                transaction.Commit();
                AuditLogService.Log("SUPPLIER_CREATE", "Supplier", supplier.Id, $"Created supplier '{supplier.Name}'");
            }
            else
            {
                var existing = db.Suppliers.Find(supplier.Id);
                if (existing == null) { transaction.Rollback(); return 0; }

                existing.Name = supplier.Name;
                existing.CompanyName = supplier.CompanyName;
                existing.ContactPerson = supplier.ContactPerson;
                existing.Phone = supplier.Phone;
                existing.WhatsAppNumber = supplier.WhatsAppNumber;
                existing.Address = supplier.Address;
                existing.Email = supplier.Email;
                existing.Notes = supplier.Notes;
                existing.IsActive = supplier.IsActive;
                db.SaveChanges();

                transaction.Commit();
                AuditLogService.Log("SUPPLIER_UPDATE", "Supplier", supplier.Id, $"Updated supplier '{supplier.Name}'");
            }
            return supplier.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var supplier = db.Suppliers.Find(id);
        if (supplier == null) return false;

        supplier.IsActive = false;
        db.SaveChanges();
        AuditLogService.Log("SUPPLIER_DELETE", "Supplier", id, $"Deactivated supplier '{supplier.Name}'");
        return true;
    }

    public List<SupplierLedger> GetLedger(int supplierId, DateTime? from = null, DateTime? to = null)
    {
        using var db = new AppDbContext();
        var query = db.SupplierLedgers.Where(l => l.SupplierId == supplierId);
        if (from.HasValue) query = query.Where(l => l.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(l => l.TransactionDate <= to.Value.AddDays(1));
        return query.OrderBy(l => l.TransactionDate).ToList();
    }

    /// <summary>
    /// Records a payment to the supplier. Mirrors CustomerService.ReceivePayment: writes the
    /// SupplierPayment row (with a generated receipt number), the matching ledger credit, and
    /// the cash-out CashTransaction, all in one transaction so the balance stays reliable.
    /// </summary>
    public SupplierPaymentResult? PaySupplier(int supplierId, decimal amount, DateTime paymentDate, string paymentMethod, string? referenceNumber, string notes, int? userId)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var supplier = db.Suppliers.Find(supplierId);
            if (supplier == null) return null;

            var previousBalance = supplier.CurrentBalance;
            var actingUserId = userId ?? AuthService.CurrentUser?.Id;
            var receiptNumber = GenerateReceiptNumber(db);

            var payment = new SupplierPayment
            {
                ShopId = supplier.ShopId,
                SupplierId = supplierId,
                Amount = amount,
                PaymentDate = paymentDate,
                PaymentMethod = paymentMethod,
                Notes = notes,
                CreatedBy = actingUserId,
                ReceiptNumber = receiptNumber,
                ReferenceNumber = referenceNumber
            };
            db.SupplierPayments.Add(payment);
            db.SaveChanges();

            supplier.CurrentBalance -= amount;
            db.SupplierLedgers.Add(new SupplierLedger
            {
                ShopId = supplier.ShopId,
                SupplierId = supplierId,
                TransactionDate = paymentDate,
                Description = "Payment Made",
                Debit = 0,
                Credit = amount,
                Balance = supplier.CurrentBalance,
                ReferenceId = payment.Id,
                ReferenceType = "Payment",
                CreatedBy = actingUserId,
                TransactionType = "Payment"
            });

            db.CashTransactions.Add(new CashTransaction
            {
                ShopId = supplier.ShopId,
                TransactionType = "SUPPLIER_PAYMENT",
                Amount = amount,
                IsInflow = false,
                Description = $"Payment to {supplier.Name}",
                ReferenceId = payment.Id,
                ReferenceType = "SupplierPayment"
            });

            db.SaveChanges();
            transaction.Commit();

            AuditLogService.Log("SUPPLIER_PAYMENT", "Supplier", supplierId, $"Paid Rs. {amount:N0} to {supplier.Name} (Receipt {receiptNumber})");

            return new SupplierPaymentResult
            {
                PaymentId = payment.Id,
                PreviousBalance = previousBalance,
                NewBalance = supplier.CurrentBalance,
                ReceiptNumber = receiptNumber
            };
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>
    /// Manual Khata entry: Manual Debit (increases what the shop owes the supplier) or
    /// Manual Credit (decreases it) — for corrections that aren't a Purchase or a Payment.
    /// </summary>
    public int AddManualEntry(int supplierId, string type, decimal amount, string description, int? userId)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var supplier = db.Suppliers.Find(supplierId);
            if (supplier == null) return 0;

            decimal debit = 0, credit = 0;
            switch (type)
            {
                case "ManualDebit": debit = amount; break;
                case "ManualCredit": credit = amount; break;
                default: throw new ArgumentException($"Unknown manual entry type '{type}'", nameof(type));
            }

            var actingUserId = userId ?? AuthService.CurrentUser?.Id;
            supplier.CurrentBalance += debit - credit;

            var ledger = new SupplierLedger
            {
                ShopId = supplier.ShopId,
                SupplierId = supplierId,
                Description = description,
                Debit = debit,
                Credit = credit,
                Balance = supplier.CurrentBalance,
                CreatedBy = actingUserId,
                TransactionType = type
            };
            db.SupplierLedgers.Add(ledger);
            db.SaveChanges();
            transaction.Commit();

            AuditLogService.Log("SUPPLIER_KHATA_MANUAL_ENTRY", "Supplier", supplierId, $"{type}: Rs. {amount:N0} - {description}");
            return ledger.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>Batch-resolves User.Id -> display name, used by the Supplier Khata ledger grid's
    /// "Created By" column.</summary>
    public Dictionary<int, string> GetUserNames(IEnumerable<int> userIds)
    {
        using var db = new AppDbContext();
        return db.Users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u.FullName ?? u.Username);
    }

    public decimal GetTotalPayables()
    {
        using var db = new AppDbContext();
        return db.Suppliers.Where(s => s.IsActive).ToList().Sum(s => s.CurrentBalance);
    }

    public DateTime? GetLastTransactionDate(int supplierId)
    {
        using var db = new AppDbContext();
        return db.SupplierLedgers.Where(l => l.SupplierId == supplierId)
            .OrderByDescending(l => l.TransactionDate).Select(l => (DateTime?)l.TransactionDate).FirstOrDefault();
    }

    /// <summary>Live Khata snapshot used by the Supplier Khata header and the WhatsApp reminder
    /// message — there's no persisted summary table, so this is always derived from the ledger
    /// + CurrentBalance, same approach as CustomerService.GetKhataSummary.</summary>
    public SupplierKhataSummary GetKhataSummary(int supplierId)
    {
        using var db = new AppDbContext();
        var supplier = db.Suppliers.Find(supplierId);
        var entries = db.SupplierLedgers.Where(l => l.SupplierId == supplierId).OrderBy(l => l.TransactionDate).ToList();

        var totalPurchases = entries.Where(e => e.TransactionType is "Purchase" or "CashPurchase").Sum(e => e.Debit);
        var totalPaid = entries.Where(e => e.TransactionType == "Payment").Sum(e => e.Credit);
        var remaining = supplier?.CurrentBalance ?? 0;
        var last = entries.LastOrDefault();

        return new SupplierKhataSummary
        {
            TotalPurchases = totalPurchases,
            TotalPaid = totalPaid,
            RemainingPayable = remaining,
            LastTransactionDescription = last?.Description,
            LastTransactionDate = last?.TransactionDate
        };
    }

    public SupplierDashboardStats GetSupplierDashboardStats()
    {
        using var db = new AppDbContext();
        var suppliers = db.Suppliers.Where(s => s.IsActive).ToList();
        var today = DateTime.Today;

        return new SupplierDashboardStats
        {
            TotalSuppliers = suppliers.Count,
            SuppliersWithBalance = suppliers.Count(s => s.CurrentBalance > 0),
            TotalPayables = suppliers.Sum(s => s.CurrentBalance),
            TodayPaid = db.SupplierPayments.Where(p => p.PaymentDate.Date == today).ToList().Sum(p => p.Amount)
        };
    }

    private static string GenerateReceiptNumber(AppDbContext db)
    {
        var year = DateTime.Now.Year;
        var prefix = $"SPY-{year}-";
        var last = db.SupplierPayments.Where(p => p.ReceiptNumber != null && p.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(p => p.ReceiptNumber).FirstOrDefault();
        if (last?.ReceiptNumber == null) return $"{prefix}000001";
        var lastNum = int.Parse(last.ReceiptNumber.Split('-').Last());
        return $"{prefix}{(lastNum + 1):D6}";
    }
}

public class SupplierPaymentResult
{
    public int PaymentId { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal NewBalance { get; set; }
    public string ReceiptNumber { get; set; } = "";
}

public class SupplierKhataSummary
{
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingPayable { get; set; }
    public string? LastTransactionDescription { get; set; }
    public DateTime? LastTransactionDate { get; set; }
}

public class SupplierDashboardStats
{
    public int TotalSuppliers { get; set; }
    public int SuppliersWithBalance { get; set; }
    public decimal TotalPayables { get; set; }
    public decimal TodayPaid { get; set; }
}
