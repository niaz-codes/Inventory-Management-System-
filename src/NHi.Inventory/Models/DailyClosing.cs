using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class DailyClosing
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public DateTime ClosingDate { get; set; } = DateTime.Today;
    public decimal OpeningCash { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCreditSales { get; set; }
    public decimal TotalCustomerPayments { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalSupplierPayments { get; set; }
    public decimal ExpectedClosingCash { get; set; }
    public decimal ActualClosingCash { get; set; }
    public decimal Difference { get; set; }
    public decimal NetProfit { get; set; }
    public bool IsClosed { get; set; }
    public int? ClosedBy { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}
