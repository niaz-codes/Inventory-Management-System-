using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class SupplierLedger
{
    [Key]
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public int? ReferenceId { get; set; }
    [MaxLength(50)]
    public string? ReferenceType { get; set; }
}
