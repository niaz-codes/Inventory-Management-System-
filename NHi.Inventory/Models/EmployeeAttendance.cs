using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

/// <summary>One row per employee per calendar day worked. Amount is a snapshot of the
/// employee's daily wage at the time the day was marked, so later wage changes don't
/// rewrite already-recorded history.</summary>
public class EmployeeAttendance
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime WorkDate { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(200)]
    public string? Notes { get; set; }
    /// <summary>The EmployeeLedger row this day's wage was posted as.</summary>
    public int? LedgerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
