using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Customer
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? FatherName { get; set; }
    [MaxLength(20)]
    public string? Phone { get; set; }
    [MaxLength(20)]
    public string? WhatsAppNumber { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    /// <summary>When true, WhatsAppNumber is kept in sync with Phone (same number, two channels
    /// on one line) instead of being entered/edited separately.</summary>
    public bool WhatsAppSameAsPhone { get; set; } = true;
    public decimal OpeningBalance { get; set; }
    public decimal CreditLimit { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public decimal CurrentBalance { get; set; }
}
