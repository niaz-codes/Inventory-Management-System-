using System.IO;

namespace NHi.Inventory.Services;

/// <summary>
/// App-level preferences that must be readable BEFORE any shop/login context exists — the
/// database and its per-shop settings aren't the right home for those. Currently just the UI
/// language, persisted as a tiny plain-text file next to the database so the choice survives a
/// restart and is already in effect on the login screen. Every read/write is best-effort: a
/// missing, unreadable, or unwritable file simply falls back to the English default.
/// </summary>
public static class AppPreferences
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NHiInventory");
    private static readonly string LanguageFile = Path.Combine(Dir, "language.txt");
    private static readonly string ScanModeFile = Path.Combine(Dir, "scanmode.txt");

    private static string Read(string file, string fallback, params string[] allowed)
    {
        try
        {
            if (File.Exists(file))
            {
                var v = File.ReadAllText(file).Trim().ToLowerInvariant();
                if (Array.IndexOf(allowed, v) >= 0) return v;
            }
        }
        catch { /* fall through */ }
        return fallback;
    }

    private static void Write(string file, string value)
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(file, value); }
        catch { /* best-effort */ }
    }

    /// <summary>POS default barcode-scan method: "scanner" (USB/Bluetooth keyboard-wedge) or
    /// "camera". The POS screen lets the cashier switch anytime; the last choice is restored
    /// on the next launch.</summary>
    public static string ScanMode
    {
        get => Read(ScanModeFile, "scanner", "scanner", "camera");
        set => Write(ScanModeFile, value == "camera" ? "camera" : "scanner");
    }

    private static readonly string PosCartWidthFile = Path.Combine(Dir, "poscartwidth.txt");

    /// <summary>User-chosen width (px) of the POS cart panel, clamped to a sane range.</summary>
    public static double PosCartWidth
    {
        get
        {
            try
            {
                if (File.Exists(PosCartWidthFile) &&
                    double.TryParse(File.ReadAllText(PosCartWidthFile).Trim(),
                        System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return Math.Clamp(v, 360, 640);
            }
            catch { /* fall through */ }
            return 420;
        }
        set => Write(PosCartWidthFile, Math.Clamp(value, 360, 640)
            .ToString("0", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>"en" or "ur". Defaults to "en" when nothing valid is saved.</summary>
    public static string Language
    {
        get
        {
            try
            {
                if (File.Exists(LanguageFile))
                {
                    var v = File.ReadAllText(LanguageFile).Trim().ToLowerInvariant();
                    if (v is "ur" or "en") return v;
                }
            }
            catch { /* fall through to default */ }
            return "en";
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(LanguageFile, value == "ur" ? "ur" : "en");
            }
            catch { /* saving a preference is never worth crashing over */ }
        }
    }
}
