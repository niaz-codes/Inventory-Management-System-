namespace NHi.Inventory.Models;

/// <summary>
/// POS receipt printing preferences for a shop. Persisted as JSON on <see cref="Shop"/>.PosPrintJson
/// so adding an option never needs a schema change.
/// </summary>
public class PosPrintSettings
{
    /// <summary>"Thermal" (continuous roll) or "Standard" (A4/A5/Letter page invoice).</summary>
    public string PrinterType { get; set; } = "Thermal";

    /// <summary>Preferred Windows printer name; null = system default.</summary>
    public string? PrinterName { get; set; }

    // --- Thermal ---
    /// <summary>"58mm", "80mm" or "Custom".</summary>
    public string ThermalWidth { get; set; } = "80mm";
    public double CustomThermalWidthMm { get; set; } = 80;

    // --- Standard ---
    /// <summary>"A4", "A5", "Letter" or "Custom".</summary>
    public string StandardPaper { get; set; } = "A4";
    public double CustomWidthMm { get; set; } = 210;
    public double CustomHeightMm { get; set; } = 297;

    /// <summary>Inner content margin in millimetres (all sides).</summary>
    public double MarginMm { get; set; } = 3;

    public int Copies { get; set; } = 1;

    /// <summary>"Customer", "Shop" or "Both". Defaults to Both so a sale prints the
    /// customer's receipt and the shop's own copy together.</summary>
    public string CopyMode { get; set; } = "Both";

    /// <summary>Print automatically as soon as a sale completes (no button press).</summary>
    public bool AutoPrint { get; set; }

    /// <summary>Append a cut marker / rely on the driver's end-of-job auto-cut.</summary>
    public bool AutoCut { get; set; } = true;

    /// <summary>Show the live preview window; when false and AutoPrint is on, prints silently.</summary>
    public bool LivePreview { get; set; } = true;
}
