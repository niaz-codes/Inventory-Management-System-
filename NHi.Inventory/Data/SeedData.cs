using NHi.Inventory.Models;
using NHi.Inventory.Services;
using BCrypt.Net;

namespace NHi.Inventory.Data;

public static class SeedData
{
    public static void Initialize()
    {
        // This runs once at first-ever startup, before any login — SchemaUpgrader has
        // already created Shop 1 by this point, so scope the seeding session to it. This
        // also makes the "already seeded?" .Any() checks below see existing shop-scoped
        // rows correctly instead of the (still-null) CurrentShopId hiding everything.
        AuthService.SetShopContextForSeeding(1);

        // A fresh context is required here rather than reusing callerDb: AppDbContext
        // snapshots CurrentShopId once, in its constructor, and callerDb was constructed
        // (in App.xaml.cs) before the shop context above was set — so its query filters
        // would stay stuck filtering on ShopId == null for its entire lifetime.
        using var db = new AppDbContext();

        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("REDACTED"),
                    FullName = "Administrator",
                    Role = "MAIN_ADMIN",
                    ShopId = null,
                    IsActive = true
                },
                new User
                {
                    Username = "user",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("user"),
                    FullName = "Shop User",
                    Role = "USER",
                    ShopId = 1,
                    IsActive = true
                }
            );
        }

        if (!db.Units.Any())
        {
            db.Units.AddRange(
                // Protected defaults — cannot be deleted from Unit Management.
                new Unit { Name = "Kilogram", Symbol = "KG", IsProtected = true },
                new Unit { Name = "Foot", Symbol = "ft", IsProtected = true },
                new Unit { Name = "Inch", Symbol = "in", IsProtected = true },
                new Unit { Name = "Box", Symbol = "box", IsProtected = true },
                new Unit { Name = "Packet", Symbol = "pkt", IsProtected = true },
                // Pre-made everyday units — editable / deletable like any custom unit.
                new Unit { Name = "Piece", Symbol = "pc" },
                new Unit { Name = "Gram", Symbol = "g" },
                new Unit { Name = "Liter", Symbol = "L" },
                new Unit { Name = "Dozen", Symbol = "dz" },
                new Unit { Name = "Meter", Symbol = "m" },
                new Unit { Name = "Bottle", Symbol = "btl" },
                new Unit { Name = "Carton", Symbol = "ctn" }
            );
        }

        if (!db.ExpenseCategories.Any())
        {
            db.ExpenseCategories.AddRange(
                new ExpenseCategory { ShopId = 1, Name = "Electricity" },
                new ExpenseCategory { ShopId = 1, Name = "Rent" },
                new ExpenseCategory { ShopId = 1, Name = "Salary" },
                new ExpenseCategory { ShopId = 1, Name = "Transport" },
                new ExpenseCategory { ShopId = 1, Name = "Maintenance" },
                new ExpenseCategory { ShopId = 1, Name = "Other" }
            );
        }

        if (!db.CompanySettings.Any())
        {
            db.CompanySettings.Add(new CompanySettings
            {
                CompanyName = "NHI General Store",
                Phone = "",
                Address = "",
                Currency = "Rs."
            });
        }

        if (!db.Categories.Any())
        {
            db.Categories.AddRange(
                new Category { ShopId = 1, Name = "General" },
                new Category { ShopId = 1, Name = "Grocery" },
                new Category { ShopId = 1, Name = "Beverages" },
                new Category { ShopId = 1, Name = "Snacks" },
                new Category { ShopId = 1, Name = "Dairy" },
                new Category { ShopId = 1, Name = "Personal Care" },
                new Category { ShopId = 1, Name = "Household" }
            );
        }

        if (!db.Brands.Any())
        {
            db.Brands.AddRange(
                new Brand { ShopId = 1, Name = "Generic" },
                new Brand { ShopId = 1, Name = "Nestle" },
                new Brand { ShopId = 1, Name = "Unilever" },
                new Brand { ShopId = 1, Name = "Coca-Cola" },
                new Brand { ShopId = 1, Name = "PepsiCo" }
            );
        }

        db.SaveChanges();
    }

    /// <summary>
    /// One-time migration for databases created before the default admin
    /// password was hardened: if the admin account still has the old
    /// "admin" password, upgrade it to the new default without touching
    /// any other data.
    /// </summary>
    public static void UpgradeDefaultAdminPassword(AppDbContext db)
    {
        var admin = db.Users.FirstOrDefault(u => u.Username == "admin");
        if (admin != null && BCrypt.Net.BCrypt.Verify("admin", admin.PasswordHash))
        {
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("REDACTED");
            db.SaveChanges();
        }
    }
}
