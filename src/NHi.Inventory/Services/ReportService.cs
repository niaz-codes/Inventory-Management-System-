using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class ReportService
{
    public decimal GetTodaySalesTotal()
    {
        using var db = new AppDbContext();
        return db.Sales.Where(s => !s.IsDeleted && s.SaleDate.Date == DateTime.Today).ToList().Sum(s => s.GrandTotal);
    }

    public decimal GetTodayCashSales()
    {
        using var db = new AppDbContext();
        return db.Sales.Where(s => !s.IsDeleted && s.SaleDate.Date == DateTime.Today && s.PaymentMethod == "Cash").ToList().Sum(s => s.GrandTotal);
    }

    public decimal GetTodayCreditSales()
    {
        using var db = new AppDbContext();
        return db.Sales.Where(s => !s.IsDeleted && s.SaleDate.Date == DateTime.Today && s.IsCreditSale).ToList().Sum(s => s.RemainingAmount);
    }

    public decimal GetTodayPurchases()
    {
        using var db = new AppDbContext();
        return db.Purchases.Where(p => !p.IsDeleted && p.PurchaseDate.Date == DateTime.Today).ToList().Sum(p => p.GrandTotal);
    }

    public decimal GetTodayProfit()
    {
        using var db = new AppDbContext();
        var revenue = db.SaleItems.Where(s => s.Sale!.SaleDate.Date == DateTime.Today && !s.Sale!.IsDeleted)
            .ToList().Sum(s => (s.UnitPrice - s.PurchasePriceAtSale) * s.Quantity);
        var expenses = db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate.Date == DateTime.Today).ToList().Sum(e => e.Amount);
        return revenue - expenses;
    }

    public decimal GetTotalReceivables()
    {
        using var db = new AppDbContext();
        return db.Customers.Where(c => c.IsActive).ToList().Sum(c => c.CurrentBalance);
    }

    public decimal GetTotalPayables()
    {
        using var db = new AppDbContext();
        return db.Suppliers.Where(s => s.IsActive).ToList().Sum(s => s.CurrentBalance);
    }

    public int GetTotalProducts()
    {
        using var db = new AppDbContext();
        return db.Products.Count(p => p.IsActive);
    }

    public int GetLowStockCount()
    {
        using var db = new AppDbContext();
        return db.Products.Count(p => p.IsActive && p.CurrentStock <= p.MinimumStock);
    }

    public int GetTotalCustomers()
    {
        using var db = new AppDbContext();
        return db.Customers.Count(c => c.IsActive);
    }

    public int GetTotalSuppliers()
    {
        using var db = new AppDbContext();
        return db.Suppliers.Count(s => s.IsActive);
    }

    public decimal GetCurrentCash()
    {
        using var db = new AppDbContext();
        var inflow = db.CashTransactions.Where(c => c.IsInflow).ToList().Sum(c => c.Amount);
        var outflow = db.CashTransactions.Where(c => !c.IsInflow).ToList().Sum(c => c.Amount);
        return inflow - outflow;
    }

    public decimal GetStockValue()
    {
        using var db = new AppDbContext();
        return db.Products.Where(p => p.IsActive).ToList().Sum(p => p.CurrentStock * p.PurchasePrice);
    }

    public decimal GetMonthlySales(DateTime month)
    {
        using var db = new AppDbContext();
        return db.Sales.Where(s => !s.IsDeleted && s.SaleDate.Year == month.Year && s.SaleDate.Month == month.Month)
            .ToList().Sum(s => s.GrandTotal);
    }

    public decimal GetMonthlyProfit(DateTime month)
    {
        using var db = new AppDbContext();
        var revenue = db.SaleItems.Where(s => s.Sale!.SaleDate.Year == month.Year && s.Sale!.SaleDate.Month == month.Month && !s.Sale!.IsDeleted)
            .ToList().Sum(s => (s.UnitPrice - s.PurchasePriceAtSale) * s.Quantity);
        var expenses = db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate.Year == month.Year && e.ExpenseDate.Month == month.Month).ToList().Sum(e => e.Amount);
        return revenue - expenses;
    }

    public decimal GetMonthlyPurchases(DateTime month)
    {
        using var db = new AppDbContext();
        return db.Purchases.Where(p => !p.IsDeleted && p.PurchaseDate.Year == month.Year && p.PurchaseDate.Month == month.Month)
            .ToList().Sum(p => p.GrandTotal);
    }

    public decimal GetMonthlyExpenses(DateTime month)
    {
        using var db = new AppDbContext();
        return db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate.Year == month.Year && e.ExpenseDate.Month == month.Month)
            .ToList().Sum(e => e.Amount);
    }

    public int GetMonthlyRecordsCount(DateTime month)
    {
        using var db = new AppDbContext();
        var sales = db.Sales.Count(s => !s.IsDeleted && s.SaleDate.Year == month.Year && s.SaleDate.Month == month.Month);
        var purchases = db.Purchases.Count(p => !p.IsDeleted && p.PurchaseDate.Year == month.Year && p.PurchaseDate.Month == month.Month);
        var expenses = db.Expenses.Count(e => !e.IsDeleted && e.ExpenseDate.Year == month.Year && e.ExpenseDate.Month == month.Month);
        return sales + purchases + expenses;
    }
}
