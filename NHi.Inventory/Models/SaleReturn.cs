using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class SaleReturn
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    public DateTime ReturnDate { get; set; } = DateTime.Now;
    [MaxLength(300)]
    public string? Reason { get; set; }
    public int? CreatedBy { get; set; }
}
