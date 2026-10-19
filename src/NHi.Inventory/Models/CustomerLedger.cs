using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class CustomerLedger
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public int? ReferenceId { get; set; }
    [MaxLength(50)]
    public string? ReferenceType { get; set; }
    /// <summary>Only meaningful on Debit/CreditSale rows; drives overdue tracking.</summary>
    public DateTime? DueDate { get; set; }
    public int? CreatedBy { get; set; }
    /// <summary>True once a Reversal entry has been posted to cancel this row. The row itself is never deleted.</summary>
    public bool IsReversed { get; set; }
    public int? ReversedByLedgerId { get; set; }
    /// <summary>OpeningBalance | CreditSale | Payment | SaleReturn | Adjustment | Reversal</summary>
    [MaxLength(30)]
    public string TransactionType { get; set; } = string.Empty;
}
