using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class CustomerService
{
    public List<Customer> GetAll(bool activeOnly = true)
    {
        using var db = new AppDbContext();
        var query = db.Customers.AsQueryable();
        if (activeOnly) query = query.Where(c => c.IsActive);
        return query.OrderBy(c => c.Name).ToList();
    }

    public Customer? GetById(int id)
    {
        using var db = new AppDbContext();
        return db.Customers.Find(id);
    }

    public List<Customer> Search(string searchText)
    {
        using var db = new AppDbContext();
        var text = searchText.ToLower();
        var isNumeric = int.TryParse(searchText, out var idVal);
        return db.Customers.Where(c => c.IsActive && (
                c.Name.ToLower().Contains(text) ||
                (c.Phone != null && c.Phone.Contains(searchText)) ||
                (c.WhatsAppNumber != null && c.WhatsAppNumber.Contains(searchText)) ||
                (isNumeric && c.Id == idVal)))
            .OrderBy(c => c.Name).ToList();
    }

    /// <summary>
    /// Creates or updates a customer. On create, seeds CurrentBalance from OpeningBalance and
    /// writes a matching "Opening Balance" ledger row. On update, profile fields only are
    /// changed — balance/ledger are never touched here (all money movement goes through
    /// ReceivePayment/AddManualEntry/the sales flow, so the ledger stays the single source of truth).
    /// </summary>
    public int Save(Customer customer)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            if (customer.Id == 0)
            {
                customer.ShopId = AuthService.CurrentShopId ?? 1;
                customer.CreatedAt = DateTime.Now;
                customer.CurrentBalance = customer.OpeningBalance;
                db.Customers.Add(customer);
                db.SaveChanges();

                if (customer.OpeningBalance != 0)
                {
                    db.CustomerLedgers.Add(new CustomerLedger
                    {
                        ShopId = customer.ShopId,
                        CustomerId = customer.Id,
                        Description = "Opening Balance",
                        Debit = customer.OpeningBalance > 0 ? customer.OpeningBalance : 0,
                        Credit = customer.OpeningBalance < 0 ? -customer.OpeningBalance : 0,
                        Balance = customer.OpeningBalance,
                        CreatedBy = AuthService.CurrentUser?.Id,
                        TransactionType = "OpeningBalance"
                    });
                    db.SaveChanges();
                }

                transaction.Commit();
                AuditLogService.Log("CUSTOMER_CREATE", "Customer", customer.Id, $"Created customer '{customer.Name}'");
            }
            else
            {
                var existing = db.Customers.Find(customer.Id);
                if (existing == null) { transaction.Rollback(); return 0; }

                existing.Name = customer.Name;
                existing.FatherName = customer.FatherName;
                existing.Phone = customer.Phone;
                existing.WhatsAppNumber = customer.WhatsAppNumber;
                existing.WhatsAppSameAsPhone = customer.WhatsAppSameAsPhone;
                existing.Address = customer.Address;
                existing.Email = customer.Email;
                existing.CreditLimit = customer.CreditLimit;
                existing.Notes = customer.Notes;
                existing.IsActive = customer.IsActive;
                db.SaveChanges();

                transaction.Commit();
                AuditLogService.Log("CUSTOMER_UPDATE", "Customer", customer.Id, $"Updated customer '{customer.Name}'");
            }
            return customer.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var customer = db.Customers.Find(id);
        if (customer == null) return false;

        customer.IsActive = false;
        db.SaveChanges();
        AuditLogService.Log("CUSTOMER_DELETE", "Customer", id, $"Deactivated customer '{customer.Name}'");
        return true;
    }

    public List<CustomerLedger> GetLedger(int customerId, DateTime? from = null, DateTime? to = null)
    {
        using var db = new AppDbContext();
        var query = db.CustomerLedgers.Where(l => l.CustomerId == customerId);
        if (from.HasValue) query = query.Where(l => l.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(l => l.TransactionDate <= to.Value.AddDays(1));
        return query.OrderBy(l => l.TransactionDate).ToList();
    }

    /// <summary>
    /// Records a payment from the customer. If the amount exceeds the current balance the
    /// excess is simply left as a negative CurrentBalance (an advance/credit for the customer) —
    /// the ledger is still the single source of truth, no separate "advance" table needed.
    /// </summary>
    public PaymentResult? ReceivePayment(int customerId, decimal amount, string notes = "", string paymentMethod = "Cash", int? userId = null)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var customer = db.Customers.Find(customerId);
            if (customer == null) return null;

            var previousBalance = customer.CurrentBalance;
            var overpaidBy = amount > previousBalance ? amount - previousBalance : 0;
            var actingUserId = userId ?? AuthService.CurrentUser?.Id;
            var receiptNumber = GenerateReceiptNumber(db);

            var payment = new CustomerPayment
            {
                ShopId = customer.ShopId,
                CustomerId = customerId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = paymentMethod,
                Notes = notes,
                CreatedBy = actingUserId,
                ReceiptNumber = receiptNumber
            };
            db.CustomerPayments.Add(payment);
            db.SaveChanges();

            customer.CurrentBalance -= amount;
            db.CustomerLedgers.Add(new CustomerLedger
            {
                ShopId = customer.ShopId,
                CustomerId = customerId,
                Description = "Payment Received",
                Debit = 0,
                Credit = amount,
                Balance = customer.CurrentBalance,
                ReferenceId = payment.Id,
                ReferenceType = "Payment",
                CreatedBy = actingUserId,
                TransactionType = "Payment"
            });

            db.CashTransactions.Add(new CashTransaction
            {
                ShopId = customer.ShopId,
                TransactionType = "CUSTOMER_PAYMENT",
                Amount = amount,
                IsInflow = true,
                Description = $"Payment from {customer.Name}",
                ReferenceId = payment.Id,
                ReferenceType = "CustomerPayment"
            });

            db.SaveChanges();
            transaction.Commit();

            AuditLogService.Log("PAYMENT_RECEIVED", "Customer", customerId, $"Received Rs. {amount:N0} from {customer.Name} (Receipt {receiptNumber})");

            return new PaymentResult
            {
                PaymentId = payment.Id,
                PreviousBalance = previousBalance,
                NewBalance = customer.CurrentBalance,
                OverpaidBy = overpaidBy,
                ReceiptNumber = receiptNumber
            };
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>
    /// Pure read: does CurrentBalance + additionalDebit stay within CreditLimit?
    /// CreditLimit == 0 means unlimited credit (no cap). Never writes anything —
    /// callers (POS) use this to decide whether to block/confirm before completing a credit sale.
    /// </summary>
    public (bool WithinLimit, decimal Available) CheckCreditLimit(int customerId, decimal additionalDebit)
    {
        using var db = new AppDbContext();
        var customer = db.Customers.Find(customerId);
        if (customer == null) return (true, 0);
        if (customer.CreditLimit <= 0) return (true, decimal.MaxValue);

        var available = customer.CreditLimit - customer.CurrentBalance;
        var withinLimit = customer.CurrentBalance + additionalDebit <= customer.CreditLimit;
        return (withinLimit, available);
    }

    /// <summary>
    /// Manual Khata entry (point 10): Credit/Udhaar, Payment Received, or Adjustment.
    /// Adjustment direction is taken from the sign of amount (positive = increases what the
    /// customer owes, negative = decreases it) — callers must already have verified the acting
    /// user is authorized (Adjustment is gated behind AuthService.HasPermission("KhataAdjustment")
    /// at the call site, matching how VoidSale gates on AuthService.IsAdmin()).
    /// </summary>
    public int AddManualEntry(int customerId, string type, decimal amount, string description, DateTime? dueDate, int? userId)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var customer = db.Customers.Find(customerId);
            if (customer == null) return 0;

            decimal debit = 0, credit = 0;
            string txType;
            switch (type)
            {
                case "Credit":
                    debit = amount;
                    txType = "CreditSale";
                    break;
                case "Payment":
                    credit = amount;
                    txType = "Payment";
                    break;
                case "Adjustment":
                    if (amount >= 0) debit = amount; else credit = -amount;
                    txType = "Adjustment";
                    break;
                default:
                    throw new ArgumentException($"Unknown manual entry type '{type}'", nameof(type));
            }

            var actingUserId = userId ?? AuthService.CurrentUser?.Id;
            customer.CurrentBalance += debit - credit;

            var ledger = new CustomerLedger
            {
                ShopId = customer.ShopId,
                CustomerId = customerId,
                Description = description,
                Debit = debit,
                Credit = credit,
                Balance = customer.CurrentBalance,
                DueDate = debit > 0 ? dueDate : null,
                CreatedBy = actingUserId,
                TransactionType = txType
            };
            db.CustomerLedgers.Add(ledger);
            db.SaveChanges();
            transaction.Commit();

            AuditLogService.Log("KHATA_MANUAL_ENTRY", "Customer", customerId, $"{txType}: Rs. {amount:N0} - {description}");
            return ledger.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>
    /// Corrects a ledger entry by posting an offsetting row rather than deleting anything
    /// (point 20: transactions are never destroyed, only reversed). Admin-only — callers must
    /// have already checked AuthService.IsAdmin() before calling this.
    /// </summary>
    public bool ReverseEntry(int ledgerId, string reason, int? userId)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var original = db.CustomerLedgers.Find(ledgerId);
            if (original == null || original.IsReversed) return false;

            var customer = db.Customers.Find(original.CustomerId);
            if (customer == null) return false;

            var actingUserId = userId ?? AuthService.CurrentUser?.Id;
            var reversalDebit = original.Credit;
            var reversalCredit = original.Debit;
            customer.CurrentBalance += reversalDebit - reversalCredit;

            var reversal = new CustomerLedger
            {
                ShopId = customer.ShopId,
                CustomerId = original.CustomerId,
                Description = $"Reversal of #{original.Id} - {reason}",
                Debit = reversalDebit,
                Credit = reversalCredit,
                Balance = customer.CurrentBalance,
                CreatedBy = actingUserId,
                TransactionType = "Reversal"
            };
            db.CustomerLedgers.Add(reversal);
            db.SaveChanges();

            original.IsReversed = true;
            original.ReversedByLedgerId = reversal.Id;
            db.SaveChanges();

            transaction.Commit();
            AuditLogService.Log("KHATA_REVERSAL", "CustomerLedger", ledgerId, reason);
            return true;
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>
    /// Aging/overdue calculation for display only (dashboard, status badges) — never alters the
    /// ledger. Walks the customer's CreditSale debits oldest-first and applies later
    /// Payment/SaleReturn credits against them FIFO, so a due date on an old sale that's since
    /// been paid off (even if by a later, undirected payment) doesn't wrongly show as overdue.
    /// </summary>
    public (decimal OverdueAmount, DateTime? OldestDueDate) GetOverdueInfo(int customerId)
    {
        using var db = new AppDbContext();
        var entries = db.CustomerLedgers.Where(l => l.CustomerId == customerId).OrderBy(l => l.TransactionDate).ToList();
        return CalculateOverdue(entries);
    }

    private static (decimal OverdueAmount, DateTime? OldestDueDate) CalculateOverdue(List<CustomerLedger> entries)
    {
        var openDebits = new List<(decimal Amount, DateTime? DueDate)>();
        decimal creditPool = 0;

        foreach (var e in entries)
        {
            if (e.Debit > 0)
            {
                var remaining = e.Debit;
                if (creditPool > 0)
                {
                    var applied = Math.Min(creditPool, remaining);
                    creditPool -= applied;
                    remaining -= applied;
                }
                if (remaining > 0) openDebits.Add((remaining, e.DueDate));
            }
            else if (e.Credit > 0)
            {
                var toApply = e.Credit;
                for (int i = 0; i < openDebits.Count && toApply > 0; i++)
                {
                    var (amt, due) = openDebits[i];
                    var applied = Math.Min(amt, toApply);
                    amt -= applied;
                    toApply -= applied;
                    openDebits[i] = (amt, due);
                }
                openDebits.RemoveAll(d => d.Amount <= 0);
                if (toApply > 0) creditPool += toApply;
            }
        }

        var today = DateTime.Today;
        var overdue = openDebits.Where(d => d.DueDate.HasValue && d.DueDate.Value.Date < today).ToList();
        if (overdue.Count == 0) return (0, null);
        return (overdue.Sum(d => d.Amount), overdue.Min(d => d.DueDate));
    }

    public KhataDashboardStats GetDashboardStats()
    {
        using var db = new AppDbContext();
        var customers = db.Customers.Where(c => c.IsActive).ToList();
        var today = DateTime.Today;

        var stats = new KhataDashboardStats
        {
            TotalCustomers = customers.Count,
            CustomersWithBalance = customers.Count(c => c.CurrentBalance > 0),
            TotalReceivable = customers.Sum(c => c.CurrentBalance),
            TodayReceived = db.CustomerPayments.Where(p => p.PaymentDate.Date == today).ToList().Sum(p => p.Amount),
            TodayCreditSales = db.CustomerLedgers.Where(l => l.TransactionType == "CreditSale" && l.TransactionDate.Date == today).ToList().Sum(l => l.Debit),
            NearLimitCustomers = customers.Count(c => c.CreditLimit > 0 && c.CurrentBalance >= c.CreditLimit * 0.9m)
        };

        var balancesOwed = customers.Where(c => c.CurrentBalance > 0).ToList();
        var allLedgers = db.CustomerLedgers.Where(l => balancesOwed.Select(c => c.Id).Contains(l.CustomerId))
            .OrderBy(l => l.TransactionDate).ToList().GroupBy(l => l.CustomerId).ToDictionary(g => g.Key, g => g.ToList());
        stats.OverdueCustomers = balancesOwed.Count(c => allLedgers.TryGetValue(c.Id, out var entries) && CalculateOverdue(entries).OverdueAmount > 0);

        return stats;
    }

    /// <summary>Computes the live Khata snapshot (Total/Paid/Remaining + last transaction) used
    /// by the Customer Khata screen and the WhatsApp reminder message — there's no persisted
    /// Khata table, so this is always derived from the ledger + CurrentBalance.</summary>
    public KhataSummary GetKhataSummary(int customerId)
    {
        using var db = new AppDbContext();
        var customer = db.Customers.Find(customerId);
        var entries = db.CustomerLedgers.Where(l => l.CustomerId == customerId).OrderBy(l => l.TransactionDate).ToList();

        var paid = entries.Sum(e => e.Credit);
        var remaining = customer?.CurrentBalance ?? 0;
        var last = entries.LastOrDefault();

        return new KhataSummary
        {
            PaidAmount = paid,
            RemainingBalance = remaining,
            TotalAmount = paid + remaining,
            LastTransactionDescription = last?.Description,
            LastTransactionDate = last?.TransactionDate
        };
    }

    public DateTime? GetLastTransactionDate(int customerId)
    {
        using var db = new AppDbContext();
        return db.CustomerLedgers.Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.TransactionDate).Select(l => (DateTime?)l.TransactionDate).FirstOrDefault();
    }

    public decimal GetTotalReceivables()
    {
        using var db = new AppDbContext();
        return db.Customers.Where(c => c.IsActive).ToList().Sum(c => c.CurrentBalance);
    }

    private static string GenerateReceiptNumber(AppDbContext db)
    {
        var year = DateTime.Now.Year;
        var prefix = $"RCP-{year}-";
        var last = db.CustomerPayments.Where(p => p.ReceiptNumber != null && p.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(p => p.ReceiptNumber).FirstOrDefault();
        if (last?.ReceiptNumber == null) return $"{prefix}000001";
        var lastNum = int.Parse(last.ReceiptNumber.Split('-').Last());
        return $"{prefix}{(lastNum + 1):D6}";
    }
}

public class PaymentResult
{
    public int PaymentId { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal NewBalance { get; set; }
    public decimal OverpaidBy { get; set; }
    public string ReceiptNumber { get; set; } = "";
}

public class KhataDashboardStats
{
    public int TotalCustomers { get; set; }
    public int CustomersWithBalance { get; set; }
    public decimal TotalReceivable { get; set; }
    public decimal TodayReceived { get; set; }
    public decimal TodayCreditSales { get; set; }
    public int OverdueCustomers { get; set; }
    public int NearLimitCustomers { get; set; }
}
