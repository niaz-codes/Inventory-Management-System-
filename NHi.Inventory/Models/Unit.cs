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

    /// <summary>True for the built-in defaults (Kilogram, Foot, Inch, Box, Packet) — these
    /// cannot be deleted from Unit Management. User-created units are always deletable
    /// (unless currently assigned to a product).</summary>
    public bool IsProtected { get; set; }
}
