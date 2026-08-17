using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class CompanySettings
{
    [Key]
    public int Id { get; set; }
    [MaxLength(200)]
    public string CompanyName { get; set; } = "NHi General Store";
    public byte[]? Logo { get; set; }
    [MaxLength(20)]
    public string? Phone { get; set; }
    [MaxLength(20)]
    public string? WhatsApp { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }
    [MaxLength(100)]
    public string? City { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    [MaxLength(100)]
    public string? Website { get; set; }
    [MaxLength(50)]
    public string? TaxNumber { get; set; }
    [MaxLength(10)]
    public string Currency { get; set; } = "Rs.";
    [MaxLength(500)]
    public string? InvoiceFooter { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int? UpdatedBy { get; set; }
}
