using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class AuditLog
{
    [Key]
    public int Id { get; set; }
    public int? ShopId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int? UserId { get; set; }
    [MaxLength(100)]
    public string? Username { get; set; }
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    [MaxLength(1000)]
    public string? Details { get; set; }
}
