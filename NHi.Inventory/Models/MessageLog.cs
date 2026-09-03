using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

/// <summary>One row per individual send attempt (one language, one channel) — a single
/// Reminder or Receipt event produces up to 4 of these (English/Urdu x WhatsApp/SMS).
/// This is the record shown in a customer's Message History and the unit a Retry acts on.</summary>
public class MessageLog
{
    [Key]
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>"Reminder" | "Receipt"</summary>
    [MaxLength(20)]
    public string MessageType { get; set; } = string.Empty;
    /// <summary>"English" | "Urdu"</summary>
    [MaxLength(10)]
    public string Language { get; set; } = string.Empty;
    /// <summary>"WhatsApp" | "SMS"</summary>
    [MaxLength(10)]
    public string Channel { get; set; } = string.Empty;

    /// <summary>"Sale" | "Khata" — what this message was about, for cross-referencing.</summary>
    [MaxLength(20)]
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    /// <summary>"Sent" | "Failed" | "NotAvailable" (no phone/WhatsApp number on file for this channel).</summary>
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? ErrorDetail { get; set; }

    [MaxLength(1000)]
    public string MessageBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
