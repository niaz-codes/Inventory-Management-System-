using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class EmployeeService
{
    public List<Employee> GetAll(bool activeOnly = true)
    {
        using var db = new AppDbContext();
        var query = db.Employees.AsQueryable();
        if (activeOnly) query = query.Where(e => e.IsActive);
        return query.OrderBy(e => e.Name).ToList();
    }

    public Employee? GetById(int id)
    {
        using var db = new AppDbContext();
        return db.Employees.Find(id);
    }

    public int Save(Employee employee)
    {
        using var db = new AppDbContext();
        if (employee.Id == 0) { employee.CreatedAt = DateTime.Now; db.Employees.Add(employee); }
        else db.Employees.Update(employee);
        db.SaveChanges();
        AuditLogService.Log(employee.Id == 0 ? "ADD_EMPLOYEE" : "UPDATE_EMPLOYEE", "Employee", employee.Id, employee.Name);
        return employee.Id;
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var employee = db.Employees.Find(id);
        if (employee != null)
        {
            employee.IsActive = false;
            db.SaveChanges();
            AuditLogService.Log("DELETE_EMPLOYEE", "Employee", id, employee.Name);
            return true;
        }
        return false;
    }

    public List<EmployeeLedger> GetLedger(int employeeId)
    {
        using var db = new AppDbContext();
        return db.EmployeeLedgers.Where(l => l.EmployeeId == employeeId).OrderBy(l => l.TransactionDate).ToList();
    }

    /// <summary>Records a new due owed to the employee (e.g. a period's salary or a bonus).</summary>
    public int AddDue(int employeeId, decimal amount, string description)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var employee = db.Employees.Find(employeeId);
            if (employee == null) return 0;

            employee.CurrentBalance += amount;
            var entry = new EmployeeLedger
            {
                EmployeeId = employeeId,
                Description = description,
                Debit = amount,
                Credit = 0,
                Balance = employee.CurrentBalance,
                ReferenceType = "Due"
            };
            db.EmployeeLedgers.Add(entry);
            db.SaveChanges();
            transaction.Commit();
            AuditLogService.Log("ADD_EMPLOYEE_DUE", "Employee", employeeId, $"{description} - Rs. {amount:N0}");
            return entry.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>Records a payment made to the employee against their outstanding dues.</summary>
    public int MakePayment(int employeeId, decimal amount, string notes = "")
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var employee = db.Employees.Find(employeeId);
            if (employee == null) return 0;

            var payment = new EmployeePayment
            {
                EmployeeId = employeeId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                Notes = notes,
                CreatedBy = AuthService.CurrentUser?.Id
            };
            db.EmployeePayments.Add(payment);
            db.SaveChanges();

            employee.CurrentBalance -= amount;
            db.EmployeeLedgers.Add(new EmployeeLedger
            {
                EmployeeId = employeeId,
                Description = "Payment Made",
                Debit = 0,
                Credit = amount,
                Balance = employee.CurrentBalance,
                ReferenceId = payment.Id,
                ReferenceType = "Payment"
            });

            db.CashTransactions.Add(new CashTransaction
            {
                TransactionType = "EMPLOYEE_PAYMENT",
                Amount = amount,
                IsInflow = false,
                Description = $"Payment to {employee.Name}",
                ReferenceId = payment.Id,
                ReferenceType = "EmployeePayment"
            });

            db.SaveChanges();
            transaction.Commit();
            AuditLogService.Log("PAY_EMPLOYEE", "Employee", employeeId, $"Paid Rs. {amount:N0} to {employee.Name}");
            return payment.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public decimal GetTotalDues()
    {
        using var db = new AppDbContext();
        return db.Employees.Where(e => e.IsActive).ToList().Sum(e => e.CurrentBalance);
    }
}
