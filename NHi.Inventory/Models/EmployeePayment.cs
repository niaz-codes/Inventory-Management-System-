using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class EmployeePayment
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    [MaxLength(300)]
    public string? Notes { get; set; }
    public int? CreatedBy { get; set; }
}
