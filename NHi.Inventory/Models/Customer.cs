using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Customer
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? FatherName { get; set; }
    [MaxLength(20)]
    public string? Phone { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CreditLimit { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public decimal CurrentBalance { get; set; }
}
