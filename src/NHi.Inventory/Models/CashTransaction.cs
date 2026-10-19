using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class CashTransaction
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    [MaxLength(50)]
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsInflow { get; set; }
    [MaxLength(300)]
    public string? Description { get; set; }
    public int? ReferenceId { get; set; }
    [MaxLength(50)]
    public string? ReferenceType { get; set; }
    public decimal RunningBalance { get; set; }
}
