using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class ProductService
{
    public List<Product> GetAll(bool activeOnly = true)
    {
        using var db = new AppDbContext();
        var query = db.Products.Include(p => p.Category).Include(p => p.Brand).Include(p => p.Unit).AsQueryable();
        if (activeOnly) query = query.Where(p => p.IsActive);
        return query.OrderBy(p => p.NameEnglish).ToList();
    }

    public List<Product> Search(string searchText)
    {
        using var db = new AppDbContext();
        var text = searchText.ToLower();
        return db.Products
            .Include(p => p.Category).Include(p => p.Brand).Include(p => p.Unit)
            .Where(p => p.IsActive && (p.NameEnglish.ToLower().Contains(text) ||
                (p.NameUrdu != null && p.NameUrdu.Contains(searchText)) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(text)) ||
                (p.SKU != null && p.SKU.ToLower().Contains(text))))
            .OrderBy(p => p.NameEnglish).ToList();
    }

    /// <summary>
    /// Server-side paged + filtered product query backing InventoryView, so the whole
    /// table is never loaded into memory just to show one page or apply a filter.
    /// </summary>
    public (List<Product> Items, int TotalCount) GetAllPaged(int page, int pageSize, bool activeOnly,
        string? searchText = null, int? categoryId = null, bool lowStockOnly = false)
    {
        using var db = new AppDbContext();
        var query = db.Products.Include(p => p.Category).Include(p => p.Brand).Include(p => p.Unit).AsQueryable();
        if (activeOnly) query = query.Where(p => p.IsActive);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (lowStockOnly) query = query.Where(p => p.CurrentStock <= p.MinimumStock);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var text = searchText.ToLower();
            query = query.Where(p => p.NameEnglish.ToLower().Contains(text) ||
                (p.NameUrdu != null && p.NameUrdu.Contains(searchText)) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(text)) ||
                (p.SKU != null && p.SKU.ToLower().Contains(text)));
        }

        var total = query.Count();
        var items = query.OrderBy(p => p.NameEnglish)
            .Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public Product? GetById(int id)
    {
        using var db = new AppDbContext();
        return db.Products.Include(p => p.Category).Include(p => p.Brand).Include(p => p.Unit)
            .FirstOrDefault(p => p.Id == id);
    }

    public Product? GetByBarcode(string barcode)
    {
        using var db = new AppDbContext();
        return db.Products.Include(p => p.Category).Include(p => p.Brand).Include(p => p.Unit)
            .FirstOrDefault(p => p.Barcode == barcode && p.IsActive);
    }

    public List<Product> GetLowStock()
    {
        using var db = new AppDbContext();
        return db.Products.Include(p => p.Category)
            .Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.NameEnglish).ToList();
    }

    public List<Product> GetExpiredProducts()
    {
        using var db = new AppDbContext();
        var today = DateTime.Today;
        return db.Products.Include(p => p.Category)
            .Where(p => p.IsActive && p.ExpiryDate != null && p.ExpiryDate.Value.Date < today)
            .OrderBy(p => p.ExpiryDate).ToList();
    }

    public List<Product> GetExpiringProducts(int withinDays)
    {
        using var db = new AppDbContext();
        var today = DateTime.Today;
        var cutoff = today.AddDays(withinDays);
        return db.Products.Include(p => p.Category)
            .Where(p => p.IsActive && p.ExpiryDate != null && p.ExpiryDate.Value.Date >= today && p.ExpiryDate.Value.Date <= cutoff)
            .OrderBy(p => p.ExpiryDate).ToList();
    }

    public List<CategoryStockValue> GetStockValueByCategory()
    {
        using var db = new AppDbContext();
        var products = db.Products.Include(p => p.Category).Where(p => p.IsActive).ToList();
        return products.GroupBy(p => p.Category?.Name ?? "Uncategorized")
            .Select(g => new CategoryStockValue
            {
                CategoryName = g.Key,
                ProductCount = g.Count(),
                StockValue = g.Sum(p => p.CurrentStock * p.PurchasePrice)
            })
            .OrderByDescending(c => c.StockValue)
            .ToList();
    }

    /// <summary>
    /// Required-field, price/stock-sanity, and duplicate-barcode/SKU checks — the single
    /// source of truth for product validity, run before every Save so the UI can show a
    /// friendly, specific message instead of silently saving bad data.
    /// </summary>
    private static (bool ok, string? errorKey) Validate(Product product, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(product.NameEnglish)) return (false, "FillRequired");
        if (product.SalePrice < 0 || product.PurchasePrice < 0 || product.WholesalePrice < 0) return (false, "InvalidPrice");
        if (product.OpeningStock < 0 || product.MinimumStock < 0 || product.MaximumStock < 0) return (false, "InvalidStock");
        if (product.MaximumStock > 0 && product.MaximumStock < product.MinimumStock) return (false, "InvalidStockRange");

        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            var dupBarcode = db.Products.Any(p => p.IsActive && p.Id != product.Id && p.Barcode == product.Barcode);
            if (dupBarcode) return (false, "DuplicateBarcode");
        }
        if (!string.IsNullOrWhiteSpace(product.SKU))
        {
            var dupSku = db.Products.Any(p => p.IsActive && p.Id != product.Id && p.SKU == product.SKU);
            if (dupSku) return (false, "DuplicateSKU");
        }
        return (true, null);
    }

    /// <summary>
    /// Creates or updates a product. On create with a non-zero OpeningStock, also writes a
    /// matching "OPENING_STOCK" StockTransaction so every unit of stock in the system is
    /// always explained by a ledger row, same as Purchases/Sales/Returns.
    /// </summary>
    public (bool success, string? errorKey, int id) Save(Product product)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            // Every product must be scannable — if no barcode was entered, mint a unique one.
            if (string.IsNullOrWhiteSpace(product.Barcode))
                product.Barcode = BarcodeService.GenerateUnique(db);

            var (ok, errorKey) = Validate(product, db);
            if (!ok) { transaction.Rollback(); return (false, errorKey, 0); }

            if (product.Id == 0)
            {
                product.ShopId = AuthService.CurrentShopId ?? 1;
                product.CreatedAt = DateTime.Now;
                product.UpdatedAt = DateTime.Now;
                db.Products.Add(product);
                db.SaveChanges();

                if (product.OpeningStock != 0)
                {
                    db.StockTransactions.Add(new StockTransaction
                    {
                        ShopId = product.ShopId,
                        ProductId = product.Id,
                        TransactionType = "OPENING_STOCK",
                        Quantity = product.OpeningStock,
                        PreviousStock = 0,
                        NewStock = product.CurrentStock,
                        Description = "Opening stock"
                    });
                    db.SaveChanges();
                }

                transaction.Commit();
                AuditLogService.Log("PRODUCT_CREATE", "Product", product.Id, $"Created product '{product.NameEnglish}'");
            }
            else
            {
                product.UpdatedAt = DateTime.Now;
                db.Products.Update(product);
                db.SaveChanges();

                transaction.Commit();
                AuditLogService.Log("PRODUCT_UPDATE", "Product", product.Id, $"Updated product '{product.NameEnglish}'");
            }

            return (true, null, product.Id);
        }
        catch { transaction.Rollback(); throw; }
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var product = db.Products.Find(id);
        if (product == null) return false;

        product.IsActive = false;
        db.SaveChanges();
        AuditLogService.Log("PRODUCT_DELETE", "Product", id, $"Deactivated product '{product.NameEnglish}'");
        return true;
    }

    public bool UpdateStock(int productId, decimal quantityChange, string transactionType, int? referenceId = null, string? referenceType = null, string? description = null)
    {
        using var db = new AppDbContext();
        var product = db.Products.Find(productId);
        if (product == null) return false;

        var previousStock = product.CurrentStock;
        product.CurrentStock += quantityChange;

        db.StockTransactions.Add(new StockTransaction
        {
            ShopId = product.ShopId,
            ProductId = productId,
            TransactionType = transactionType,
            Quantity = quantityChange,
            PreviousStock = previousStock,
            NewStock = product.CurrentStock,
            Description = description,
            ReferenceId = referenceId,
            ReferenceType = referenceType
        });

        db.SaveChanges();
        return true;
    }

    /// <summary>
    /// Admin/authorized-user manual stock correction (damage, loss, count correction).
    /// Floors at 0 — an adjustment can never take stock negative. Callers must already have
    /// verified the acting user is authorized (AuthService.HasPermission("StockAdjustment")),
    /// same pattern as the Khata module's manual Adjustment entries.
    /// </summary>
    public bool AdjustStock(int productId, decimal delta, string reason, string? notes, int? userId)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var product = db.Products.Find(productId);
            if (product == null) { transaction.Rollback(); return false; }

            var previousStock = product.CurrentStock;
            var newStock = Math.Max(0, previousStock + delta);
            var actualDelta = newStock - previousStock;
            product.CurrentStock = newStock;

            db.StockTransactions.Add(new StockTransaction
            {
                ShopId = product.ShopId,
                ProductId = productId,
                TransactionType = "ADJUSTMENT",
                Quantity = actualDelta,
                PreviousStock = previousStock,
                NewStock = newStock,
                Description = string.IsNullOrWhiteSpace(notes) ? reason : $"{reason} - {notes}"
            });
            db.SaveChanges();
            transaction.Commit();

            AuditLogService.Log("STOCK_ADJUSTMENT", "Product", productId,
                $"{reason}: {previousStock:0.##} -> {newStock:0.##} ({(actualDelta >= 0 ? "+" : "")}{actualDelta:0.##})" +
                (string.IsNullOrWhiteSpace(notes) ? "" : $" - {notes}"));
            return true;
        }
        catch { transaction.Rollback(); throw; }
    }

    public List<Category> GetCategories()
    {
        using var db = new AppDbContext();
        return db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
    }

    public List<Brand> GetBrands()
    {
        using var db = new AppDbContext();
        return db.Brands.Where(b => b.IsActive).OrderBy(b => b.Name).ToList();
    }

    public List<Unit> GetUnits()
    {
        using var db = new AppDbContext();
        // Protected defaults first, then everything else alphabetically.
        return db.Units.OrderByDescending(u => u.IsProtected).ThenBy(u => u.Name).ToList();
    }

    /// <summary>Adds a new unit (Id == 0) or renames an existing one. Symbol tracks the name for
    /// custom units. Protected defaults can be renamed but never lose their protected flag.</summary>
    public int SaveUnit(Unit unit)
    {
        using var db = new AppDbContext();
        unit.Name = unit.Name.Trim();
        if (unit.Id == 0)
        {
            unit.Symbol = unit.Name.Length <= 20 ? unit.Name : unit.Name[..20];
            db.Units.Add(unit);
        }
        else
        {
            var existing = db.Units.Find(unit.Id);
            if (existing == null) return 0;
            existing.Name = unit.Name;
            if (!existing.IsProtected) existing.Symbol = unit.Name.Length <= 20 ? unit.Name : unit.Name[..20];
        }
        db.SaveChanges();
        return unit.Id;
    }

    public bool IsUnitInUse(int unitId)
    {
        using var db = new AppDbContext();
        return db.Products.Any(p => p.UnitId == unitId);
    }

    public UnitDeleteResult DeleteUnit(int id)
    {
        using var db = new AppDbContext();
        var unit = db.Units.Find(id);
        if (unit == null) return UnitDeleteResult.NotFound;
        if (unit.IsProtected) return UnitDeleteResult.Protected;
        if (db.Products.Any(p => p.UnitId == id)) return UnitDeleteResult.InUse;

        db.Units.Remove(unit);
        db.SaveChanges();
        return UnitDeleteResult.Deleted;
    }

    /// <summary>Returns the Id of the unit named <paramref name="name"/> (case-insensitive),
    /// creating it if it doesn't exist. Backs the "Other / دیگر" custom-unit flow in the product
    /// dialog: a typed unit (English or Urdu) becomes a first-class Unit row, so it then shows up
    /// exactly like a built-in unit everywhere product units are displayed — and is reusable for
    /// the next product. Returns null for blank input.</summary>
    public int? GetOrCreateUnit(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();

        using var db = new AppDbContext();
        var existing = db.Units.AsEnumerable().FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;

        var unit = new Unit { Name = name, Symbol = name.Length <= 20 ? name : name[..20] };
        db.Units.Add(unit);
        db.SaveChanges();
        return unit.Id;
    }

    public int SaveCategory(Category category)
    {
        using var db = new AppDbContext();
        if (category.Id == 0) { category.ShopId = AuthService.CurrentShopId ?? 1; db.Categories.Add(category); }
        else db.Categories.Update(category);
        db.SaveChanges();
        return category.Id;
    }

    public int SaveBrand(Brand brand)
    {
        using var db = new AppDbContext();
        if (brand.Id == 0) { brand.ShopId = AuthService.CurrentShopId ?? 1; db.Brands.Add(brand); }
        else db.Brands.Update(brand);
        db.SaveChanges();
        return brand.Id;
    }

    public bool DeleteCategory(int id)
    {
        using var db = new AppDbContext();
        var cat = db.Categories.Find(id);
        if (cat != null) { cat.IsActive = false; db.SaveChanges(); return true; }
        return false;
    }

    public bool DeleteBrand(int id)
    {
        using var db = new AppDbContext();
        var brand = db.Brands.Find(id);
        if (brand != null) { brand.IsActive = false; db.SaveChanges(); return true; }
        return false;
    }
}

public class CategoryStockValue
{
    public string CategoryName { get; set; } = "";
    public int ProductCount { get; set; }
    public decimal StockValue { get; set; }
}

public enum UnitDeleteResult
{
    Deleted,
    Protected,   // one of the five built-in defaults
    InUse,       // still assigned to at least one product
    NotFound
}
