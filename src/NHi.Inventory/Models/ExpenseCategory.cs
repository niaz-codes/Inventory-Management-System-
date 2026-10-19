using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class ExpenseCategory
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
