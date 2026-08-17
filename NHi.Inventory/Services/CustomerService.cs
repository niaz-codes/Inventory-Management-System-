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
        return db.Customers.Where(c => c.IsActive && (c.Name.ToLower().Contains(text) || (c.Phone != null && c.Phone.Contains(searchText))))
            .OrderBy(c => c.Name).ToList();
    }

    public int Save(Customer customer)
    {
        using var db = new AppDbContext();
        if (customer.Id == 0)
        {
            customer.CreatedAt = DateTime.Now;
            db.Customers.Add(customer);
        }
        else
        {
            db.Customers.Update(customer);
        }
        db.SaveChanges();
        return customer.Id;
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var customer = db.Customers.Find(id);
        if (customer != null) { customer.IsActive = false; db.SaveChanges(); return true; }
        return false;
    }

    public List<CustomerLedger> GetLedger(int customerId)
    {
        using var db = new AppDbContext();
        return db.CustomerLedgers.Where(l => l.CustomerId == customerId).OrderBy(l => l.TransactionDate).ToList();
    }

    public int ReceivePayment(int customerId, decimal amount, string notes = "")
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var customer = db.Customers.Find(customerId);
            if (customer == null) return 0;

            var payment = new CustomerPayment
            {
                CustomerId = customerId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                Notes = notes,
                CreatedBy = AuthService.CurrentUser?.Id
            };
            db.CustomerPayments.Add(payment);
            db.SaveChanges();

            customer.CurrentBalance -= amount;
            db.CustomerLedgers.Add(new CustomerLedger
            {
                CustomerId = customerId,
                Description = $"Payment Received",
                Debit = 0,
                Credit = amount,
                Balance = customer.CurrentBalance,
                ReferenceId = payment.Id,
                ReferenceType = "Payment"
            });

            db.CashTransactions.Add(new CashTransaction
            {
                TransactionType = "CUSTOMER_PAYMENT",
                Amount = amount,
                IsInflow = true,
                Description = $"Payment from {customer.Name}",
                ReferenceId = payment.Id,
                ReferenceType = "CustomerPayment"
            });

            db.SaveChanges();
            transaction.Commit();
            return payment.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public decimal GetTotalReceivables()
    {
        using var db = new AppDbContext();
        return db.Customers.Where(c => c.IsActive).ToList().Sum(c => c.CurrentBalance);
    }
}
