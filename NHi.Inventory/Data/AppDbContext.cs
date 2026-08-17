using Microsoft.EntityFrameworkCore;
using NHi.Inventory.Models;

namespace NHi.Inventory.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<CustomerLedger> CustomerLedgers => Set<CustomerLedger>();
    public DbSet<SupplierLedger> SupplierLedgers => Set<SupplierLedger>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<DailyClosing> DailyClosings => Set<DailyClosing>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeLedger> EmployeeLedgers => Set<EmployeeLedger>();
    public DbSet<EmployeePayment> EmployeePayments => Set<EmployeePayment>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NHiInventory", "inventory.db");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SaleItem>().Ignore(s => s.Total);
        modelBuilder.Entity<PurchaseItem>().Ignore(p => p.Total);

        modelBuilder.Entity<Product>().HasIndex(p => p.Barcode);
        modelBuilder.Entity<Product>().HasIndex(p => p.SKU);
        modelBuilder.Entity<Product>().HasIndex(p => p.NameEnglish);
        modelBuilder.Entity<Customer>().HasIndex(c => c.Phone);
        modelBuilder.Entity<Supplier>().HasIndex(s => s.Phone);
        modelBuilder.Entity<Sale>().HasIndex(s => s.InvoiceNumber);
        modelBuilder.Entity<Sale>().HasIndex(s => s.SaleDate);
        modelBuilder.Entity<Purchase>().HasIndex(p => p.InvoiceNumber);
        modelBuilder.Entity<Employee>().HasIndex(e => e.Phone);
    }
}
