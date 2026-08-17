using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Unit
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;
}
