using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Expense
{
    [Key]
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Now;
    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    [MaxLength(20)]
    public string PaymentMethod { get; set; } = "Cash";
    public bool IsDeleted { get; set; }
    public int? CreatedBy { get; set; }
}
