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

    public int Save(Supplier supplier)
    {
        using var db = new AppDbContext();
        if (supplier.Id == 0) { supplier.CreatedAt = DateTime.Now; db.Suppliers.Add(supplier); }
        else db.Suppliers.Update(supplier);
        db.SaveChanges();
        return supplier.Id;
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var supplier = db.Suppliers.Find(id);
        if (supplier != null) { supplier.IsActive = false; db.SaveChanges(); return true; }
        return false;
    }

    public List<SupplierLedger> GetLedger(int supplierId)
    {
        using var db = new AppDbContext();
        return db.SupplierLedgers.Where(l => l.SupplierId == supplierId).OrderBy(l => l.TransactionDate).ToList();
    }

    public int MakePayment(int supplierId, decimal amount, string notes = "")
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var supplier = db.Suppliers.Find(supplierId);
            if (supplier == null) return 0;

            var payment = new SupplierPayment
            {
                SupplierId = supplierId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                Notes = notes,
                CreatedBy = AuthService.CurrentUser?.Id
            };
            db.SupplierPayments.Add(payment);
            db.SaveChanges();

            supplier.CurrentBalance -= amount;
            db.SupplierLedgers.Add(new SupplierLedger
            {
                SupplierId = supplierId,
                Description = "Payment Made",
                Debit = 0,
                Credit = amount,
                Balance = supplier.CurrentBalance,
                ReferenceId = payment.Id,
                ReferenceType = "Payment"
            });

            db.CashTransactions.Add(new CashTransaction
            {
                TransactionType = "SUPPLIER_PAYMENT",
                Amount = amount,
                IsInflow = false,
                Description = $"Payment to {supplier.Name}",
                ReferenceId = payment.Id,
                ReferenceType = "SupplierPayment"
            });

            db.SaveChanges();
            transaction.Commit();
            return payment.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public decimal GetTotalPayables()
    {
        using var db = new AppDbContext();
        return db.Suppliers.Where(s => s.IsActive).ToList().Sum(s => s.CurrentBalance);
    }
}
