using Microsoft.EntityFrameworkCore;
using NHi.Inventory.Data;
using NHi.Inventory.Models;

namespace NHi.Inventory.Services;

/// <summary>Main-Admin-only shop management: list/create/edit/deactivate shops, plus seeding
/// a new shop's starter Categories/Brands/ExpenseCategories so it isn't empty on day one.</summary>
public class ShopService
{
    public List<Shop> GetAll()
    {
        using var db = new AppDbContext();
        return db.Shops.OrderBy(s => s.Name).ToList();
    }

    public Shop? GetById(int id)
    {
        using var db = new AppDbContext();
        return db.Shops.Find(id);
    }

    public int GetUserCount(int shopId)
    {
        using var db = new AppDbContext();
        return db.Users.IgnoreQueryFilters().Count(u => u.ShopId == shopId);
    }

    public int Save(Shop shop)
    {
        using var db = new AppDbContext();
        if (shop.Id == 0)
        {
            shop.CreatedAt = DateTime.Now;
            shop.UpdatedAt = DateTime.Now;
            db.Shops.Add(shop);
            db.SaveChanges();
            SeedShopDefaults(db, shop.Id);
            AuditLogService.Log("SHOP_CREATE", "Shop", shop.Id, $"Created shop '{shop.Name}'");
        }
        else
        {
            var existing = db.Shops.Find(shop.Id);
            if (existing == null) return 0;

            existing.Name = shop.Name;
            existing.OwnerName = shop.OwnerName;
            existing.Phone = shop.Phone;
            existing.WhatsApp = shop.WhatsApp;
            existing.Address = shop.Address;
            existing.City = shop.City;
            existing.Email = shop.Email;
            existing.Website = shop.Website;
            existing.TaxNumber = shop.TaxNumber;
            existing.Currency = shop.Currency;
            existing.InvoiceFooter = shop.InvoiceFooter;
            if (shop.Logo != null) existing.Logo = shop.Logo;
            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = AuthService.CurrentUser?.Id;
            db.SaveChanges();
            AuditLogService.Log("SHOP_UPDATE", "Shop", existing.Id, $"Updated shop '{existing.Name}'");
        }
        return shop.Id;
    }

    public bool SetActive(int shopId, bool isActive)
    {
        using var db = new AppDbContext();
        var shop = db.Shops.Find(shopId);
        if (shop == null) return false;
        shop.IsActive = isActive;
        shop.UpdatedAt = DateTime.Now;
        db.SaveChanges();
        AuditLogService.Log(isActive ? "SHOP_ACTIVATE" : "SHOP_DEACTIVATE", "Shop", shopId, shop.Name);
        return true;
    }

    /// <summary>Same starter Categories/Brands/ExpenseCategories every fresh install gets via
    /// SeedData, so a newly created shop isn't empty on day one. Shop 1 doesn't need this — it
    /// already inherited the pre-existing global set when its ShopId columns were backfilled.</summary>
    private static void SeedShopDefaults(AppDbContext db, int shopId)
    {
        db.Categories.AddRange(
            new Category { ShopId = shopId, Name = "General" },
            new Category { ShopId = shopId, Name = "Grocery" },
            new Category { ShopId = shopId, Name = "Beverages" },
            new Category { ShopId = shopId, Name = "Snacks" },
            new Category { ShopId = shopId, Name = "Dairy" },
            new Category { ShopId = shopId, Name = "Personal Care" },
            new Category { ShopId = shopId, Name = "Household" }
        );
        db.Brands.AddRange(
            new Brand { ShopId = shopId, Name = "Generic" },
            new Brand { ShopId = shopId, Name = "Nestle" },
            new Brand { ShopId = shopId, Name = "Unilever" },
            new Brand { ShopId = shopId, Name = "Coca-Cola" },
            new Brand { ShopId = shopId, Name = "PepsiCo" }
        );
        db.ExpenseCategories.AddRange(
            new ExpenseCategory { ShopId = shopId, Name = "Electricity" },
            new ExpenseCategory { ShopId = shopId, Name = "Rent" },
            new ExpenseCategory { ShopId = shopId, Name = "Salary" },
            new ExpenseCategory { ShopId = shopId, Name = "Transport" },
            new ExpenseCategory { ShopId = shopId, Name = "Maintenance" },
            new ExpenseCategory { ShopId = shopId, Name = "Other" }
        );
        db.SaveChanges();
    }
}
