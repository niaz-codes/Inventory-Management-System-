using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Globalization;
using NHi.Inventory.Data;
using NHi.Inventory.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace NHi.Inventory;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        QuestPDF.Settings.License = LicenseType.Community;
        RegisterUrduFont();

        _mutex = new Mutex(true, "NHiInventoryManagementSystem", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("Application is already running.", "NHI Inventory", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

        // Restore the language chosen on a previous run (persisted app-wide, not per-shop, so it
        // also applies to the login screen before any shop context exists).
        LanguageManager.SetLanguage(AppPreferences.Language);

        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        SchemaUpgrader.Run(db);
        SeedData.Initialize();
        if (SeedData.FirstRunAdminPassword is { } firstRunAdminPassword)
        {
            MessageBox.Show(
                "Your administrator account was created.\n\n"
                + "Username:  admin\n"
                + "Password:  " + firstRunAdminPassword + "\n\n"
                + "Write this password down now, then change it after signing in.\n"
                + "It will not be shown again.",
                "NHI Inventory - Administrator Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
#if DEBUG
        TestDataSeeder.SeedIfNeeded(db);
#endif
        // Ensure every existing product has a unique barcode (one-time backfill; no-ops afterwards).
        BarcodeService.BackfillMissing();
        BackupService.RunAutomaticBackupIfDue();

        // Theme is now a per-Shop setting (Shop.Theme) — there's no shop context yet before
        // login, so start on the default and let LoginWindow re-apply the actual shop's
        // theme immediately after a successful login.
        ThemeManager.SetTheme("light");

        base.OnStartup(e);
    }

    /// <summary>
    /// Registers the bundled Urdu Nastaleeq font (Resources/Fonts/*.ttf, WPF "Resource" build
    /// action) with QuestPDF so print output can shape Urdu text properly. Registering a font
    /// doesn't take an explicit name - QuestPDF reads whatever family name is embedded in the
    /// .ttf's own name table, which for this file is "Jameel Noori Nastaleeq" (verified via
    /// System.Drawing.Text.PrivateFontCollection), matching ReceiptService.UrduFontFamilyName.
    /// The file on disk is "Jameel Noori Nastaleeq Kasheeda.ttf" (a Kasheeda/justified variant of
    /// that same family) - the pack URI below must match that exact filename, since a mismatch
    /// here fails silently (caught below) and every print quietly falls back to the default font
    /// with no visible error, which is exactly what happened before this fix.
    /// </summary>
    private static void RegisterUrduFont()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/Fonts/Jameel Noori Nastaleeq Kasheeda.ttf");
            var info = GetResourceStream(uri);
            if (info == null) return;
            using var stream = info.Stream;
            QuestPDF.Drawing.FontManager.RegisterFont(stream);
        }
        catch
        {
            // A missing or invalid font file must never block startup.
        }
    }

    /// <summary>
    /// Last-resort safety net: an unhandled exception on the UI thread would otherwise
    /// terminate the whole application (WPF has no default recovery). This logs the
    /// technical details to a file (never the DB, so logging itself can't be the next
    /// point of failure) and shows one friendly message, then marks the exception
    /// handled so the app keeps running instead of crashing on a normal user mistake.
    /// </summary>
    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(
            "Something went wrong, but the application will keep running.\nTechnical details have been logged.\n\n" + e.Exception.Message,
            "NHI Inventory", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NHiInventory");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // Logging must never be the reason the crash handler itself fails.
        }
    }
}
