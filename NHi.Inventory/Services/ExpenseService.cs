using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class ExpenseService
{
    public List<Expense> GetAll(DateTime? from = null, DateTime? to = null)
    {
        using var db = new AppDbContext();
        var query = db.Expenses.Where(e => !e.IsDeleted).AsQueryable();
        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.ExpenseDate <= to.Value.AddDays(1));
        return query.OrderByDescending(e => e.ExpenseDate).ToList();
    }

    public int Save(Expense expense)
    {
        using var db = new AppDbContext();
        if (expense.Id == 0) db.Expenses.Add(expense);
        else db.Expenses.Update(expense);
        db.SaveChanges();
        return expense.Id;
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var expense = db.Expenses.Find(id);
        if (expense != null) { expense.IsDeleted = true; db.SaveChanges(); return true; }
        return false;
    }

    public decimal GetTodayExpenses()
    {
        using var db = new AppDbContext();
        return db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate.Date == DateTime.Today).ToList().Sum(e => e.Amount);
    }

    public List<ExpenseCategory> GetCategories()
    {
        using var db = new AppDbContext();
        return db.ExpenseCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
    }
}
