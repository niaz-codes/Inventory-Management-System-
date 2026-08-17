using System.Windows;
using System.Threading;
using System.Globalization;
using NHi.Inventory.Data;
using NHi.Inventory.Services;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "NHiInventoryManagementSystem", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("Application is already running.", "NHi Inventory", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

        LanguageManager.SetLanguage("en");

        using var db = new AppDbContext();
        db.Database.EnsureCreated();
        SeedData.Initialize(db);

        base.OnStartup(e);
    }
}
