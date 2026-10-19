using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class CashService
{
    public decimal GetOpeningCash()
    {
        using var db = new AppDbContext();
        var todayClosing = db.DailyClosings.Where(d => d.ClosingDate < DateTime.Today && d.IsClosed)
            .OrderByDescending(d => d.ClosingDate).FirstOrDefault();
        return todayClosing?.ActualClosingCash ?? 0;
    }

    public decimal GetClosingCash()
    {
        using var db = new AppDbContext();
        var inflow = db.CashTransactions.Where(c => c.IsInflow).ToList().Sum(c => c.Amount);
        var outflow = db.CashTransactions.Where(c => !c.IsInflow).ToList().Sum(c => c.Amount);
        return inflow - outflow;
    }

    public decimal GetTodayInflow()
    {
        using var db = new AppDbContext();
        return db.CashTransactions.Where(c => c.IsInflow && c.TransactionDate.Date == DateTime.Today).ToList().Sum(c => c.Amount);
    }

    public decimal GetTodayOutflow()
    {
        using var db = new AppDbContext();
        return db.CashTransactions.Where(c => !c.IsInflow && c.TransactionDate.Date == DateTime.Today).ToList().Sum(c => c.Amount);
    }
}
