using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NHi.Inventory.Data;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

/// <summary>
/// Generates unique product barcodes and renders them as Code 128-B images for
/// on-screen display and thermal label printing. Code 128-B is used because it
/// encodes any printable ASCII (so manually-typed alphanumeric barcodes work too),
/// is compact for the 12-digit auto-generated codes, and is read by every
/// consumer/2D-capable scanner.
/// </summary>
public static class BarcodeService
{
    // Canonical Code 128 pattern table (ISO/IEC 15417): index = code value,
    // string = alternating bar/space module widths (bar first). All 11 modules
    // except STOP (index 106) which is 13 modules / 7 elements.
    private static readonly string[] Patterns =
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    };

    private const int StartB = 104;
    private const int Stop = 106;

    /// <summary>Returns a 12-digit numeric barcode that no other product currently uses.</summary>
    public static string GenerateUnique()
    {
        using var db = new AppDbContext();
        return GenerateUnique(db);
    }

    public static string GenerateUnique(AppDbContext db)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            // "20" prefix keeps auto-generated codes visually distinct from any real
            // retail EAN/UPC a shopkeeper might also type in.
            var candidate = "20" + Random.Shared.NextInt64(0, 10_000_000_000L).ToString("D10");
            var exists = db.Products.IgnoreQueryFilters().Any(p => p.Barcode == candidate);
            if (!exists) return candidate;
        }
        // Astronomically unlikely fallback: timestamp-based, still 12 digits.
        return "20" + (DateTime.UtcNow.Ticks % 10_000_000_000L).ToString("D10");
    }

    /// <summary>
    /// One-time backfill: every active product without a barcode gets a unique one.
    /// Safe to run on every startup — it no-ops once all products have a code.
    /// </summary>
    public static void BackfillMissing()
    {
        using var db = new AppDbContext();
        var missing = db.Products.IgnoreQueryFilters()
            .Where(p => p.Barcode == null || p.Barcode == "")
            .ToList();
        if (missing.Count == 0) return;

        foreach (var p in missing)
            p.Barcode = GenerateUnique(db);

        db.SaveChanges();
        AuditLogService.Log("BARCODE_BACKFILL", "Product", null, $"Auto-generated barcodes for {missing.Count} product(s)");
    }

    /// <summary>
    /// Renders <paramref name="data"/> as a Code 128-B barcode PNG.
    /// <paramref name="moduleWidth"/> is the pixel width of one narrow bar,
    /// <paramref name="barHeight"/> the bar height in pixels.
    /// Returns null if the string contains a character Code 128-B can't encode.
    /// </summary>
    public static byte[]? RenderPng(string data, int moduleWidth = 2, int barHeight = 70, bool withText = true)
    {
        if (string.IsNullOrEmpty(data)) return null;
        foreach (var ch in data)
            if (ch < 32 || ch > 126) return null; // outside Code 128-B

        try
        {
            return RenderCore(data, moduleWidth, barHeight, withText);
        }
        catch
        {
            // A GDI+/font failure must never take down a whole print run — the caller
            // falls back to printing the barcode digits as plain text.
            return null;
        }
    }

    private static byte[] RenderCore(string data, int moduleWidth, int barHeight, bool withText)
    {
        // --- build the code-value sequence ---
        var values = new List<int> { StartB };
        long sum = StartB;
        var pos = 1;
        foreach (var ch in data)
        {
            var v = ch - 32;
            values.Add(v);
            sum += (long)v * pos;
            pos++;
        }
        values.Add((int)(sum % 103)); // checksum
        values.Add(Stop);

        // --- expand to module widths ---
        var modules = new List<int>();
        foreach (var v in values)
            foreach (var c in Patterns[v])
                modules.Add(c - '0');

        const int quiet = 10; // quiet zone in modules, each side
        var totalModules = quiet * 2 + modules.Sum();
        var width = totalModules * moduleWidth;
        var textHeight = withText ? Math.Max(14, moduleWidth * 7) : 0;
        var height = barHeight + textHeight + 6;

        using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            var x = quiet * moduleWidth;
            var isBar = true; // patterns always start with a bar
            foreach (var m in modules)
            {
                var w = m * moduleWidth;
                if (isBar) g.FillRectangle(Brushes.Black, x, 0, w, barHeight);
                x += w;
                isBar = !isBar;
            }

            if (withText)
            {
                using var font = new Font("Consolas", Math.Max(7f, moduleWidth * 4f), FontStyle.Regular, GraphicsUnit.Pixel);
                var spaced = string.Join(" ", data.ToCharArray());
                var size = g.MeasureString(spaced, font);
                g.DrawString(spaced, font, Brushes.Black, (width - size.Width) / 2f, barHeight + 3);
            }
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
