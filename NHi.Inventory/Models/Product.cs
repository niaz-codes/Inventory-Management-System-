using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Product
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    [Required, MaxLength(200)]
    public string NameEnglish { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? NameUrdu { get; set; }
    [MaxLength(50)]
    public string? Barcode { get; set; }
    [MaxLength(50)]
    public string? SKU { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public int? UnitId { get; set; }
    public Unit? Unit { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal OpeningStock { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal MaximumStock { get; set; }
    public DateTime? ExpiryDate { get; set; }
    [MaxLength(50)]
    public string? BatchNumber { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? ImageData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public decimal StockValue => CurrentStock * PurchasePrice;
}
