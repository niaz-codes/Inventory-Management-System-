using System.Security.Cryptography;
using NHi.Inventory.Models;
using NHi.Inventory.Services;
using BCrypt.Net;

namespace NHi.Inventory.Data;

public static class SeedData
{
    /// <summary>
    /// When the initial administrator account is created on first run and no password was
    /// supplied through the NHI_ADMIN_PASSWORD environment variable, a strong random password
    /// is generated. It is exposed here so the UI can show it exactly once. The password is
    /// never hard-coded and is never written to source control.
    /// </summary>
    public static string? FirstRunAdminPassword { get; private set; }

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
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(ResolveInitialAdminPassword()),
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
    /// Resolves the password for the initial administrator account. The password is never
    /// hard-coded: it comes from the NHI_ADMIN_PASSWORD environment variable when set,
    /// otherwise a strong random password is generated and surfaced once through
    /// <see cref="FirstRunAdminPassword"/> so the UI can show it to the operator.
    /// </summary>
    private static string ResolveInitialAdminPassword()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("NHI_ADMIN_PASSWORD");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment!;

        var generated = GenerateStrongPassword(12);
        FirstRunAdminPassword = generated;
        return generated;
    }

    private static string GenerateStrongPassword(int length)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        const string all = upper + lower + digits + symbols;

        var chars = new char[length];
        // Guarantee at least one character from each class.
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        for (var i = 4; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Fisher-Yates shuffle so the guaranteed characters are not always in front.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
