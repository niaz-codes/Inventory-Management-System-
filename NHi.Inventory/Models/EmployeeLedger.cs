using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class EmployeeLedger
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;
    /// <summary>Due added (e.g. salary accrued, bonus).</summary>
    public decimal Debit { get; set; }
    /// <summary>Amount paid to the employee.</summary>
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public int? ReferenceId { get; set; }
    [MaxLength(50)]
    public string? ReferenceType { get; set; }
}
