using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class SaleItem
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total => (Quantity * UnitPrice) - DiscountAmount;
    public decimal PurchasePriceAtSale { get; set; }
}
