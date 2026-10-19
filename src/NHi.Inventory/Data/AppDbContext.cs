using Microsoft.EntityFrameworkCore;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Data;

public class AppDbContext : DbContext
{
    /// <summary>Snapshot of AuthService.CurrentShopId taken when this context is constructed.
    /// The query filters below deliberately read THIS instance property (via `this`) rather
    /// than the static AuthService property directly: EF Core only re-parameterizes a query
    /// filter value per execution when it depends on the DbContext instance. A filter that
    /// closes over a static member instead gets evaluated once and baked into the cached
    /// query plan as a constant — so the very first shop to query after the process starts
    /// would silently stick for every context/user for the rest of that run. Since every
    /// caller already does `using var db = new AppDbContext()` fresh per operation, this
    /// instance property naturally captures a correct, current value every time.</summary>
    public int? CurrentShopId { get; }

    public AppDbContext()
    {
        CurrentShopId = AuthService.CurrentShopId;
    }

    public DbSet<Shop> Shops => Set<Shop>();
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
    public DbSet<EmployeeAttendance> EmployeeAttendances => Set<EmployeeAttendance>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();

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
        modelBuilder.Entity<Product>().Ignore(p => p.StockValue);

        // --- Multi-shop scoping ---
        // Strict filter: every operational entity is only ever visible within its own shop.
        // Shop itself carries no filter (it's the tenant root). User/AuditLog use a permissive
        // filter below so Main Admin (CurrentShopId == null) can see across every shop.
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Category>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Brand>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<ExpenseCategory>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<CustomerLedger>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<CustomerPayment>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<SupplierLedger>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<SupplierPayment>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Sale>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<SaleItem>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<SaleReturn>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Purchase>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<PurchaseItem>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<PurchaseReturn>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<StockTransaction>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<CashTransaction>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<DailyClosing>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<Employee>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<EmployeeLedger>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<EmployeePayment>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<EmployeeAttendance>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<MessageLog>().HasQueryFilter(e => e.ShopId == CurrentShopId);
        modelBuilder.Entity<User>().HasQueryFilter(e => e.ShopId == CurrentShopId || CurrentShopId == null);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.ShopId == CurrentShopId || CurrentShopId == null);

        modelBuilder.Entity<Product>().HasIndex(p => p.Barcode);
        modelBuilder.Entity<Product>().HasIndex(p => p.SKU);
        modelBuilder.Entity<Product>().HasIndex(p => p.NameEnglish);
        modelBuilder.Entity<Customer>().HasIndex(c => c.Phone);
        modelBuilder.Entity<CustomerLedger>().HasIndex(l => l.CustomerId);
        modelBuilder.Entity<Supplier>().HasIndex(s => s.Phone);
        modelBuilder.Entity<Sale>().HasIndex(s => s.InvoiceNumber);
        modelBuilder.Entity<Sale>().HasIndex(s => s.SaleDate);
        modelBuilder.Entity<Purchase>().HasIndex(p => p.InvoiceNumber);
        modelBuilder.Entity<Employee>().HasIndex(e => e.Phone);
        modelBuilder.Entity<EmployeeAttendance>().HasIndex(a => a.EmployeeId);
        modelBuilder.Entity<EmployeeAttendance>().HasIndex(a => new { a.EmployeeId, a.WorkDate }).IsUnique();
        modelBuilder.Entity<StockTransaction>().HasIndex(s => s.ProductId);
        modelBuilder.Entity<StockTransaction>().HasIndex(s => s.TransactionDate);
        modelBuilder.Entity<MessageLog>().HasIndex(m => m.CustomerId);
        modelBuilder.Entity<MessageLog>().HasIndex(m => m.CreatedAt);
    }
}
