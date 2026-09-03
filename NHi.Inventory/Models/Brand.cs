using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Brand
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
