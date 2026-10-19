using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class StockTransaction
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    [MaxLength(50)]
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal NewStock { get; set; }
    [MaxLength(300)]
    public string? Description { get; set; }
    public int? ReferenceId { get; set; }
    [MaxLength(50)]
    public string? ReferenceType { get; set; }
}
