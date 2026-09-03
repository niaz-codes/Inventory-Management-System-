using System.ComponentModel.DataAnnotations;

namespace NHi.Inventory.Models;

/// <summary>
/// One row per shop. Replaces the old single global CompanySettings row — every shop
/// carries its own complete identity, branding, and policy, and everything a shop-level
/// user sees is scoped to their one Shop via AuthService.CurrentShopId.
/// </summary>
public class Shop
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? OwnerName { get; set; }
    [MaxLength(100)]
    public string? BusinessType { get; set; }
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

    /// <summary>"light" or "dark" — this shop's persisted UI theme choice.</summary>
    [MaxLength(10)]
    public string Theme { get; set; } = "light";

    // --- Inventory / expiry policy ---
    public int ExpiryWarningDays { get; set; } = 30;
    public bool BlockExpiredSale { get; set; }
    /// <summary>When true, POS lets a product be sold past its available stock (stock can go
    /// negative). Off by default so the cashier is warned before overselling.</summary>
    public bool AllowNegativeStock { get; set; }

    /// <summary>Custom thermal receipt paper size in millimetres. Null width falls back to
    /// the standard 80mm roll; null height keeps the receipt's normal auto-height layout
    /// (which grows with item count) instead of clipping content to a fixed size.</summary>
    public double? ReceiptWidthMm { get; set; }
    public double? ReceiptHeightMm { get; set; }

    // --- Print Layout: page size/margins/orientation for receipts and payment slips ---
    /// <summary>One of "A4","A5","A6","Letter","Legal","Receipt","Custom". "Receipt" keeps using
    /// ReceiptWidthMm/ReceiptHeightMm above; "Custom" uses PrintCustomWidth/Height + PrintUnit.</summary>
    [MaxLength(20)]
    public string PrintPageSize { get; set; } = "Receipt";
    public double? PrintCustomWidth { get; set; }
    public double? PrintCustomHeight { get; set; }
    /// <summary>Unit for PrintCustomWidth/Height: "mm", "cm", or "in".</summary>
    [MaxLength(5)]
    public string PrintUnit { get; set; } = "mm";
    /// <summary>Margins in millimetres, applied inside the printed page content itself
    /// (matches how every existing document already bakes its margin into the PDF).</summary>
    public double PrintMarginTop { get; set; } = 3.53;
    public double PrintMarginBottom { get; set; } = 3.53;
    public double PrintMarginLeft { get; set; } = 3.53;
    public double PrintMarginRight { get; set; } = 3.53;
    /// <summary>"Portrait" or "Landscape".</summary>
    [MaxLength(10)]
    public string PrintOrientation { get; set; } = "Portrait";

    /// <summary>POS receipt printing preferences (printer type, copies, customer/shop copy,
    /// auto-print, etc.) serialised as JSON — see <see cref="PosPrintSettings"/>.</summary>
    [MaxLength(4000)]
    public string? PosPrintJson { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int? UpdatedBy { get; set; }
}
