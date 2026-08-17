using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class SupplierPayment
{
    [Key]
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    [MaxLength(20)]
    public string PaymentMethod { get; set; } = "Cash";
    [MaxLength(300)]
    public string? Notes { get; set; }
    public int? CreatedBy { get; set; }
}
