namespace NHi.Inventory.Models;

/// <summary>Snapshot of a customer's Khata (credit ledger) totals for display and WhatsApp
/// reminders — there's no persisted "Khata" table; a customer's Khata is always a live view
/// computed on demand from their CustomerLedger rows plus CurrentBalance.</summary>
public class KhataSummary
{
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public string? LastTransactionDescription { get; set; }
    public DateTime? LastTransactionDate { get; set; }
}
