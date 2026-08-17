using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = "USER";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLogin { get; set; }
    public string? Language { get; set; } = "en";
}
