using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    /// <summary>Null = Main Admin (not tied to any shop). Non-null = a shop-level Admin/User.</summary>
    public int? ShopId { get; set; }
    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = "USER";
    public bool IsActive { get; set; } = true;
    /// <summary>Pending | Approved | Rejected. A brand-new account created by a Main Admin
    /// starts Pending and cannot log in until explicitly Approved; Rejected is a permanent
    /// "never let this account in" mark distinct from Deactivate (which is for an
    /// already-approved account being temporarily disabled).</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Approved";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLogin { get; set; }
    public string? Language { get; set; } = "en";
}
