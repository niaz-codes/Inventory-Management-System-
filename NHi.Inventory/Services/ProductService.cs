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

    public int Save(Product product)
    {
        using var db = new AppDbContext();
        if (product.Id == 0)
        {
            product.CreatedAt = DateTime.Now;
            db.Products.Add(product);
        }
        else
        {
            product.UpdatedAt = DateTime.Now;
            db.Products.Update(product);
        }
        db.SaveChanges();
        return product.Id;
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var product = db.Products.Find(id);
        if (product != null)
        {
            product.IsActive = false;
            db.SaveChanges();
            return true;
        }
        return false;
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
        return db.Units.OrderBy(u => u.Name).ToList();
    }

    public int SaveCategory(Category category)
    {
        using var db = new AppDbContext();
        if (category.Id == 0) db.Categories.Add(category);
        else db.Categories.Update(category);
        db.SaveChanges();
        return category.Id;
    }

    public int SaveBrand(Brand brand)
    {
        using var db = new AppDbContext();
        if (brand.Id == 0) db.Brands.Add(brand);
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
