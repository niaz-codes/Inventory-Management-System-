using NHi.Inventory.Models;
using BCrypt.Net;

namespace NHi.Inventory.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        if (!db.Users.Any())
        {
            db.Users.AddRange(
                new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                    FullName = "Administrator",
                    Role = "ADMIN",
                    IsActive = true
                },
                new User
                {
                    Username = "user",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("user"),
                    FullName = "Shop User",
                    Role = "USER",
                    IsActive = true
                }
            );
        }

        if (!db.Units.Any())
        {
            db.Units.AddRange(
                new Unit { Name = "Piece", Symbol = "pc" },
                new Unit { Name = "Packet", Symbol = "pkt" },
                new Unit { Name = "Box", Symbol = "box" },
                new Unit { Name = "Kilogram", Symbol = "kg" },
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
                new ExpenseCategory { Name = "Electricity" },
                new ExpenseCategory { Name = "Rent" },
                new ExpenseCategory { Name = "Salary" },
                new ExpenseCategory { Name = "Transport" },
                new ExpenseCategory { Name = "Maintenance" },
                new ExpenseCategory { Name = "Other" }
            );
        }

        if (!db.CompanySettings.Any())
        {
            db.CompanySettings.Add(new CompanySettings
            {
                CompanyName = "NHi General Store",
                Phone = "",
                Address = "",
                Currency = "Rs."
            });
        }

        if (!db.Categories.Any())
        {
            db.Categories.AddRange(
                new Category { Name = "General" },
                new Category { Name = "Grocery" },
                new Category { Name = "Beverages" },
                new Category { Name = "Snacks" },
                new Category { Name = "Dairy" },
                new Category { Name = "Personal Care" },
                new Category { Name = "Household" }
            );
        }

        if (!db.Brands.Any())
        {
            db.Brands.AddRange(
                new Brand { Name = "Generic" },
                new Brand { Name = "Nestle" },
                new Brand { Name = "Unilever" },
                new Brand { Name = "Coca-Cola" },
                new Brand { Name = "PepsiCo" }
            );
        }

        db.SaveChanges();
    }
}
