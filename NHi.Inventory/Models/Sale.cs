using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Sale
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; } = DateTime.Now;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    [MaxLength(20)]
    public string PaymentMethod { get; set; } = "Cash";
    [MaxLength(500)]
    public string? Notes { get; set; }
    public bool IsCreditSale { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? CreatedBy { get; set; }
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
