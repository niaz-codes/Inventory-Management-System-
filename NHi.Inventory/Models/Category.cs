using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Category
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
