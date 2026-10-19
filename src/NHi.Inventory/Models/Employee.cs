using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class Employee
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? Position { get; set; }
    [MaxLength(20)]
    public string? Phone { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }

    /// <summary>"Daily" (per-day wage, entered per working day) or "Monthly" (fixed salary).</summary>
    [MaxLength(20)]
    public string SalaryType { get; set; } = "Daily";
    public decimal DailyWage { get; set; }
    public decimal MonthlySalary { get; set; }

    public DateTime JoiningDate { get; set; } = DateTime.Now;
    [MaxLength(500)]
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Total unpaid dues currently owed to this employee.</summary>
    public decimal CurrentBalance { get; set; }
}
