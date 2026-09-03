using NHi.Inventory.Models;

namespace NHi.Inventory.Services;

/// <summary>
/// Resolves a shop's Print Layout settings (page size preset, custom size + unit, margins,
/// orientation) into concrete millimetre geometry. Shared by the Print Layout settings dialog's
/// live preview and by <see cref="ReceiptService"/>'s actual PDF page setup, so what the user
/// previews is exactly what gets printed.
/// </summary>
public static class PrintLayoutService
{
    public const string PresetA4 = "A4";
    public const string PresetA5 = "A5";
    public const string PresetA6 = "A6";
    public const string PresetLetter = "Letter";
    public const string PresetLegal = "Legal";
    public const string PresetReceipt = "Receipt";
    public const string PresetCustom = "Custom";

    /// <summary>Display order for the Page Size dropdown.</summary>
    public static readonly string[] Presets = { PresetReceipt, PresetA4, PresetA5, PresetA6, PresetLetter, PresetLegal, PresetCustom };

    public static readonly string[] Units = { "mm", "cm", "in" };

    /// <summary>Standard sizes in millimetres, portrait orientation (width x height).</summary>
    private static readonly Dictionary<string, (double Width, double Height)> StandardSizesMm = new()
    {
        [PresetA4] = (210, 297),
        [PresetA5] = (148, 210),
        [PresetA6] = (105, 148),
        [PresetLetter] = (215.9, 279.4),
        [PresetLegal] = (215.9, 355.6),
    };

    public static double ToMillimetres(double value, string unit) => unit switch
    {
        "cm" => value * 10,
        "in" => value * 25.4,
        _ => value,
    };

    public static double FromMillimetres(double mm, string unit) => unit switch
    {
        "cm" => mm / 10,
        "in" => mm / 25.4,
        _ => mm,
    };

    /// <summary>Resolved page geometry in millimetres, orientation already applied.
    /// A null <see cref="HeightMm"/> means "auto-fit to content" — only possible for the
    /// Receipt preset when no fixed ReceiptHeightMm is configured.</summary>
    public record Geometry(
        double WidthMm,
        double? HeightMm,
        double MarginTopMm,
        double MarginBottomMm,
        double MarginLeftMm,
        double MarginRightMm,
        bool IsLandscape);

    /// <summary>The application's built-in factory-default layout: an 80mm auto-height receipt
    /// with the original ~10pt (3.53mm) margin the app always used, in Portrait. Used both as the
    /// column defaults for shops that never open the dialog and as the dialog's "Reset to Default".</summary>
    public static Geometry Default(double receiptWidthFallback = 80) =>
        new(receiptWidthFallback, null, 3.53, 3.53, 3.53, 3.53, false);

    public static Geometry Resolve(Shop? shop) => Resolve(
        preset: string.IsNullOrWhiteSpace(shop?.PrintPageSize) ? PresetReceipt : shop!.PrintPageSize,
        customWidth: shop?.PrintCustomWidth,
        customHeight: shop?.PrintCustomHeight,
        unit: string.IsNullOrWhiteSpace(shop?.PrintUnit) ? "mm" : shop!.PrintUnit,
        marginTopMm: shop?.PrintMarginTop ?? 3.53,
        marginBottomMm: shop?.PrintMarginBottom ?? 3.53,
        marginLeftMm: shop?.PrintMarginLeft ?? 3.53,
        marginRightMm: shop?.PrintMarginRight ?? 3.53,
        orientation: shop?.PrintOrientation ?? "Portrait",
        receiptWidthMm: shop?.ReceiptWidthMm ?? 80,
        receiptHeightMm: shop?.ReceiptHeightMm);

    /// <summary>Core resolver taking raw (possibly not-yet-saved) values — used both by
    /// <see cref="Resolve(Shop?)"/> and directly by the Print Layout dialog's live preview, so the
    /// preview reflects exactly what would be saved without needing a round trip through the DB.</summary>
    public static Geometry Resolve(string preset, double? customWidth, double? customHeight, string unit,
        double marginTopMm, double marginBottomMm, double marginLeftMm, double marginRightMm,
        string orientation, double receiptWidthMm = 80, double? receiptHeightMm = null)
    {
        double width;
        double? height;

        if (preset == PresetCustom)
        {
            width = ToMillimetres(customWidth is > 0 ? customWidth.Value : 80, unit);
            height = customHeight is > 0 ? ToMillimetres(customHeight.Value, unit) : (double?)null;
        }
        else if (preset == PresetReceipt)
        {
            width = receiptWidthMm;
            height = receiptHeightMm;
        }
        else if (StandardSizesMm.TryGetValue(preset, out var size))
        {
            width = size.Width;
            height = size.Height;
        }
        else
        {
            width = 80;
            height = null;
        }

        // Orientation only makes sense for a fixed height — an auto-fit receipt has no second
        // dimension to swap with, so Landscape is simply a no-op for that case.
        var landscape = orientation == "Landscape";
        if (landscape && height.HasValue)
            (width, height) = (height.Value, width);

        return new Geometry(width, height, marginTopMm, marginBottomMm, marginLeftMm, marginRightMm, landscape);
    }
}
