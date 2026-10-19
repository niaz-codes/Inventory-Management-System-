using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class DailyClosingService
{
    public DailyClosing CalculateDailyClosing()
    {
        using var db = new AppDbContext();
        var today = DateTime.Today;

        var opening = new CashService().GetOpeningCash();
        var cashSales = db.Sales.Where(s => !s.IsDeleted && s.SaleDate.Date == today && s.PaymentMethod == "Cash").ToList().Sum(s => s.GrandTotal);
        var creditSales = db.Sales.Where(s => !s.IsDeleted && s.SaleDate.Date == today && s.IsCreditSale).ToList().Sum(s => s.RemainingAmount);
        var customerPayments = db.CustomerPayments.Where(p => p.PaymentDate.Date == today).ToList().Sum(p => p.Amount);
        var purchases = db.Purchases.Where(p => !p.IsDeleted && p.PurchaseDate.Date == today).ToList().Sum(p => p.PaidAmount);
        var expenses = db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate.Date == today).ToList().Sum(e => e.Amount);
        var supplierPayments = db.SupplierPayments.Where(p => p.PaymentDate.Date == today).ToList().Sum(p => p.Amount);

        var expectedClosing = opening + cashSales + customerPayments - purchases - expenses - supplierPayments;

        var revenue = db.SaleItems.Where(s => s.Sale!.SaleDate.Date == today && !s.Sale!.IsDeleted)
            .ToList().Sum(s => (s.UnitPrice - s.PurchasePriceAtSale) * s.Quantity);
        var netProfit = revenue - expenses;

        return new DailyClosing
        {
            ShopId = AuthService.CurrentShopId ?? 1,
            ClosingDate = today,
            OpeningCash = opening,
            TotalCashSales = cashSales,
            TotalCreditSales = creditSales,
            TotalCustomerPayments = customerPayments,
            TotalPurchases = purchases,
            TotalExpenses = expenses,
            TotalSupplierPayments = supplierPayments,
            ExpectedClosingCash = expectedClosing,
            NetProfit = netProfit
        };
    }

    public int CloseDay(decimal actualClosingCash, string? notes = null)
    {
        var closing = CalculateDailyClosing();
        closing.ActualClosingCash = actualClosingCash;
        closing.Difference = actualClosingCash - closing.ExpectedClosingCash;
        closing.IsClosed = true;
        closing.ClosedBy = AuthService.CurrentUser?.Id;
        closing.Notes = notes;

        using var db = new AppDbContext();
        db.DailyClosings.Add(closing);
        db.SaveChanges();
        return closing.Id;
    }
}
