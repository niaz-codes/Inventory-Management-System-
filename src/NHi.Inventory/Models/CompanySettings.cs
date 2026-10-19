using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

public class CompanySettings
{
    [Key]
    public int Id { get; set; }
    [MaxLength(200)]
    public string CompanyName { get; set; } = "NHI General Store";
    public byte[]? Logo { get; set; }
    [MaxLength(20)]
    public string? Phone { get; set; }
    [MaxLength(20)]
    public string? WhatsApp { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }
    [MaxLength(100)]
    public string? City { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    [MaxLength(100)]
    public string? Website { get; set; }
    [MaxLength(50)]
    public string? TaxNumber { get; set; }
    [MaxLength(10)]
    public string Currency { get; set; } = "Rs.";
    [MaxLength(500)]
    public string? InvoiceFooter { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int? UpdatedBy { get; set; }

    // --- WhatsApp notification integration (Twilio WhatsApp API) ---
    // These are read at send-time by WhatsAppService. Left blank, notifications
    // are simply skipped (no crash, no blocking) until an admin fills them in
    // under Settings -> WhatsApp Notifications.
    public bool WhatsAppNotificationsEnabled { get; set; }
    [MaxLength(100)]
    public string? WhatsAppAccountSid { get; set; }
    [MaxLength(200)]
    public string? WhatsAppAuthToken { get; set; }
    [MaxLength(30)]
    public string? WhatsAppFromNumber { get; set; }

    // --- Inventory / expiry policy ---
    public int ExpiryWarningDays { get; set; } = 30;
    public bool BlockExpiredSale { get; set; }

    /// <summary>"light" or "dark" — the persisted UI theme choice.</summary>
    [MaxLength(10)]
    public string Theme { get; set; } = "light";
}
