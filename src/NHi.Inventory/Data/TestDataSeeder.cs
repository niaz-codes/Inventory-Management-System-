#if DEBUG
using System.Diagnostics;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Data;

/// <summary>
/// DEBUG-ONLY test/demo data seeder — this entire file is compiled out of Release builds
/// (see the #if DEBUG wrapping it), so it physically cannot run against a production
/// install regardless of any runtime flag. Safe to run on every Debug-build startup:
/// every master-data entity (customers/products/suppliers) is looked up by a stable key
/// (phone/SKU/name) before anything is inserted, and the transactional data (sales/
/// purchases/expenses) is skipped entirely once a "[DEMO]"-tagged record is found, so
/// re-running never creates duplicates. Everything goes through the same Services real
/// screens use (CustomerService, ProductService, SalesService, PurchaseService,
/// ExpenseService) — never a shortcut that bypasses validation, stock, or Khata ledger
/// logic. Every seeded record is tagged "[DEMO]" in its Notes/Description field.
/// </summary>
public static class TestDataSeeder
{
    private const string DemoTag = "[DEMO]";

    public static void SeedIfNeeded(AppDbContext db)
    {
        try
        {
            // Runs at startup before any login — scope this seeding session to Shop 1 so the
            // "already seeded?" .Any()/GetAll() lookups below see existing shop-scoped rows
            // through the query filter, instead of the still-null CurrentShopId hiding
            // everything and causing every demo record to be re-inserted on each Debug run.
            AuthService.SetShopContextForSeeding(1);

            TopUpShop(db);
            var customers = SeedCustomers();
            var products = SeedProducts();
            var suppliers = SeedSuppliers();
            SeedKhataAndSales(customers, products);
            SeedPurchases(suppliers, products);
            SeedExpenses();
        }
        catch (Exception ex)
        {
            // Demo-data seeding must never block the application from starting — but a
            // silent failure here is easy to miss, so at least surface it to the debug output.
            Debug.WriteLine($"[TestDataSeeder] Seeding failed: {ex}");
        }
    }

    private static void TopUpShop(AppDbContext db)
    {
        var shop = db.Shops.Find(1);
        if (shop == null) return;

        var changed = false;
        if (string.IsNullOrWhiteSpace(shop.Phone)) { shop.Phone = "0300-1234567"; changed = true; }
        if (string.IsNullOrWhiteSpace(shop.WhatsApp)) { shop.WhatsApp = "0300-1234567"; changed = true; }
        if (string.IsNullOrWhiteSpace(shop.Address)) { shop.Address = "Main Bazar, Alpurai, Shangla"; changed = true; }
        if (changed) db.SaveChanges();
    }

    private static List<Customer> SeedCustomers()
    {
        var service = new CustomerService();
        var existing = service.GetAll(false);
        var result = new List<Customer>();

        void Seed(string name, string father, string phone, string address, decimal opening, decimal limit)
        {
            var found = existing.FirstOrDefault(c => c.Phone == phone);
            if (found != null) { result.Add(found); return; }

            var customer = new Customer
            {
                Name = name,
                FatherName = father,
                Phone = phone,
                WhatsAppNumber = phone,
                Address = address,
                OpeningBalance = opening,
                CreditLimit = limit,
                Notes = $"{DemoTag} Sample customer for testing",
                IsActive = true
            };
            service.Save(customer);
            result.Add(customer);
        }

        // Ali Khan uses the developer's real test number (03450050667) so the WhatsApp
        // Click-to-Chat feature can be exercised end-to-end in DEBUG builds only — this is
        // dev/test data, never wired into any production code path.
        Seed("Ali Khan", "Muhammad Sharif", "03450050667", "Mohallah Kabal, Alpurai, Shangla", 2000, 20000);
        Seed("Ahmad Shah", "Gul Shah", "0301-0000002", "Bazar Road, Mingora, Swat", 0, 15000);
        Seed("Hamza Khan", "Rehmat Khan", "0301-0000003", "Near Jamia Masjid, Alpurai, Shangla", 2000, 10000);
        Seed("Usman Ahmad", "Ahmad Ali", "0301-0000004", "College Road, Shangla", 0, 15000);
        Seed("Bilal Khan", "Sher Khan", "0301-0000005", "Chowk Bazar, Alpurai, Shangla", 0, 15000);
        Seed("Salman Ali", "Ali Muhammad", "0301-0000006", "Kana Kandao, Shangla", 0, 15000);
        Seed("Imran Shah", "Sultan Shah", "0301-0000007", "Puran Alpurai, Shangla", 0, 15000);
        Seed("Faisal Khan", "Zaman Khan", "0301-0000008", "Martung Road, Shangla", 0, 5000);
        Seed("Waqas Ahmad", "Sarwar Ahmad", "0301-0000009", "Lilownai, Shangla", 0, 15000);
        Seed("Asad Khan", "Yaqoob Khan", "0301-0000010", "Besham Road, Shangla", 0, 15000);

        return result;
    }

    private static List<Product> SeedProducts()
    {
        var service = new ProductService();
        var existing = service.GetAll(false);
        var categories = service.GetCategories().ToDictionary(c => c.Name, c => (int?)c.Id);
        var units = service.GetUnits().ToDictionary(u => u.Name, u => (int?)u.Id);
        var result = new List<Product>();

        void Seed(string nameEn, string nameUr, string sku, string barcode, string categoryName, string unitName,
            decimal purchase, decimal sale, decimal wholesale, decimal stock, decimal min, decimal max, DateTime? expiry = null)
        {
            var found = existing.FirstOrDefault(p => p.SKU == sku);
            if (found != null) { result.Add(found); return; }

            var product = new Product
            {
                NameEnglish = nameEn,
                NameUrdu = nameUr,
                SKU = sku,
                Barcode = barcode,
                CategoryId = categories.GetValueOrDefault(categoryName),
                UnitId = units.GetValueOrDefault(unitName),
                PurchasePrice = purchase,
                SalePrice = sale,
                WholesalePrice = wholesale,
                OpeningStock = stock,
                CurrentStock = stock,
                MinimumStock = min,
                MaximumStock = max,
                ExpiryDate = expiry,
                Description = $"{DemoTag} Sample product for testing",
                IsActive = true
            };
            var (success, _, _) = service.Save(product);
            if (success) result.Add(product);
        }

        // Beverages
        Seed("Pepsi 1.5L", "پیپسی 1.5 لیٹر", "DEMO-BEV-001", "9990000000011", "Beverages", "Bottle", 130, 150, 140, 100, 20, 200);
        Seed("Coca Cola 1.5L", "کوکا کولا 1.5 لیٹر", "DEMO-BEV-002", "9990000000012", "Beverages", "Bottle", 130, 150, 140, 90, 20, 200);
        Seed("7UP 1.5L", "سیون اپ 1.5 لیٹر", "DEMO-BEV-003", "9990000000013", "Beverages", "Bottle", 125, 145, 135, 80, 20, 200);
        Seed("Sprite 1.5L", "سپرائٹ 1.5 لیٹر", "DEMO-BEV-004", "9990000000014", "Beverages", "Bottle", 125, 145, 135, 70, 20, 200);
        Seed("Mineral Water 1.5L", "منرل واٹر 1.5 لیٹر", "DEMO-BEV-005", "9990000000015", "Beverages", "Bottle", 40, 60, 50, 150, 30, 300);
        Seed("Energy Drink", "انرجی ڈرنک", "DEMO-BEV-006", "9990000000016", "Beverages", "Bottle", 90, 120, 105, 60, 15, 150);
        Seed("Fruit Juice 1L", "جوس 1 لیٹر", "DEMO-BEV-007", "9990000000017", "Beverages", "Bottle", 140, 170, 155, 45, 15, 150);

        // Grocery
        Seed("Sugar 1kg", "چینی 1 کلو", "DEMO-GRO-001", "9990000000021", "Grocery", "Packet", 150, 170, 160, 200, 30, 400);
        Seed("Rice 5kg", "چاول 5 کلو", "DEMO-GRO-002", "9990000000022", "Grocery", "Packet", 900, 1000, 950, 60, 15, 150);
        Seed("Flour 10kg", "آٹا 10 کلو", "DEMO-GRO-003", "9990000000023", "Grocery", "Packet", 1400, 1550, 1480, 5, 10, 100);
        Seed("Cooking Oil 1L", "کوکنگ آئل 1 لیٹر", "DEMO-GRO-004", "9990000000024", "Grocery", "Bottle", 480, 540, 510, 45, 15, 150);
        Seed("Tea 500g", "چائے 500 گرام", "DEMO-GRO-005", "9990000000025", "Grocery", "Packet", 380, 430, 400, 55, 15, 150);
        Seed("Salt 1kg", "نمک 1 کلو", "DEMO-GRO-006", "9990000000026", "Grocery", "Packet", 40, 60, 50, 120, 20, 200);
        Seed("Daal 1kg", "دال 1 کلو", "DEMO-GRO-007", "9990000000027", "Grocery", "Packet", 280, 320, 300, 8, 15, 150);

        // Snacks
        Seed("Lays Chips", "لیز چپس", "DEMO-SNK-001", "9990000000031", "Snacks", "Packet", 40, 60, 50, 100, 20, 200);
        Seed("Kurkure", "کرکرے", "DEMO-SNK-002", "9990000000032", "Snacks", "Packet", 35, 50, 45, 90, 20, 200);
        Seed("Biscuits", "بسکٹ", "DEMO-SNK-003", "9990000000033", "Snacks", "Packet", 30, 45, 40, 110, 20, 200);
        Seed("Chocolate", "چاکلیٹ", "DEMO-SNK-004", "9990000000034", "Snacks", "Piece", 60, 90, 75, 70, 15, 150);
        Seed("Nimco", "نمکو", "DEMO-SNK-005", "9990000000035", "Snacks", "Packet", 90, 130, 110, 3, 10, 100);

        // Household
        Seed("Surf Excel 1kg", "سرف ایکسل 1 کلو", "DEMO-HH-001", "9990000000041", "Household", "Packet", 320, 380, 350, 40, 10, 100);
        Seed("Ariel 1kg", "ایریل 1 کلو", "DEMO-HH-002", "9990000000042", "Household", "Packet", 330, 390, 360, 35, 10, 100);
        Seed("Soap", "صابن", "DEMO-HH-003", "9990000000043", "Household", "Piece", 45, 65, 55, 150, 30, 300);
        Seed("Shampoo", "شیمپو", "DEMO-HH-004", "9990000000044", "Household", "Bottle", 350, 420, 390, 30, 10, 100);
        Seed("Toothpaste", "ٹوتھ پیسٹ", "DEMO-HH-005", "9990000000045", "Household", "Piece", 120, 160, 140, 60, 15, 150);
        Seed("Dishwashing Liquid", "برتن دھونے کا مائع", "DEMO-HH-006", "9990000000046", "Household", "Bottle", 150, 190, 170, 50, 15, 150);
        Seed("Hand Wash", "ہینڈ واش", "DEMO-HH-007", "9990000000047", "Household", "Bottle", 130, 170, 150, 40, 10, 100);
        Seed("Detergent Bar", "صابن کی ٹکیہ", "DEMO-HH-008", "9990000000048", "Household", "Piece", 35, 50, 45, 100, 20, 200);

        // Dairy (used for expiry-date testing)
        Seed("Milk 1L", "دودھ 1 لیٹر", "DEMO-DRY-001", "9990000000051", "Dairy", "Bottle", 180, 210, 195, 40, 10, 100, DateTime.Today.AddDays(10));
        Seed("Yogurt 1kg", "دہی 1 کلو", "DEMO-DRY-002", "9990000000052", "Dairy", "Packet", 150, 180, 165, 25, 10, 100, DateTime.Today.AddDays(5));
        Seed("Butter 200g", "مکھن 200 گرام", "DEMO-DRY-003", "9990000000053", "Dairy", "Packet", 320, 380, 350, 20, 10, 100);
        Seed("Cheese Slices", "پنیر سلائسز", "DEMO-DRY-004", "9990000000054", "Dairy", "Packet", 280, 330, 300, 15, 10, 100);

        return result;
    }

    private static List<Supplier> SeedSuppliers()
    {
        var service = new SupplierService();
        var existing = service.GetAll(false);
        var result = new List<Supplier>();

        void Seed(string name, string phone, string address)
        {
            var found = existing.FirstOrDefault(s => s.Name == name);
            if (found != null) { result.Add(found); return; }

            var supplier = new Supplier
            {
                Name = name,
                Phone = phone,
                Address = address,
                Notes = $"{DemoTag} Sample supplier for testing",
                IsActive = true
            };
            service.Save(supplier);
            result.Add(supplier);
        }

        Seed("Shangla Wholesale Mart", "0932-410001", "Main Bazar, Alpurai, Shangla");
        Seed("Mingora Traders", "0946-720002", "Green Chowk, Mingora, Swat");
        Seed("Swat General Distributors", "0946-720003", "Makan Bagh Road, Mingora, Swat");
        Seed("KPK Beverage Suppliers", "091-5820004", "Ring Road, Peshawar");
        Seed("Alpurai Wholesale Center", "0932-410005", "Chowk Bazar, Alpurai, Shangla");

        return result;
    }

    /// <summary>Recreates the Khata worked-example from the spec (Ali Khan / Ahmad Shah / Hamza Khan) via real
    /// SalesService/CustomerService calls, plus several more varied cash/credit/partial/discount sales — all
    /// skipped as one block if a "[DEMO]"-tagged sale already exists, so it never duplicates on re-run.</summary>
    private static void SeedKhataAndSales(List<Customer> customers, List<Product> products)
    {
        var salesService = new SalesService();
        var customerService = new CustomerService();

        var existingSales = salesService.GetSales();
        if (existingSales.Any(s => s.Notes != null && s.Notes.Contains(DemoTag))) return;

        Customer? Find(string phone) => customers.FirstOrDefault(c => c.Phone == phone);
        Product? FindP(string sku) => products.FirstOrDefault(p => p.SKU == sku);

        var aliKhan = Find("0301-0000001");
        var ahmadShah = Find("0301-0000002");
        var hamzaKhan = Find("0301-0000003");
        var usmanAhmad = Find("0301-0000004");
        var bilalKhan = Find("0301-0000005");
        var salmanAli = Find("0301-0000006");
        var imranShah = Find("0301-0000007");
        var faisalKhan = Find("0301-0000008");
        var waqasAhmad = Find("0301-0000009");
        var asadKhan = Find("0301-0000010");

        var pepsi = FindP("DEMO-BEV-001");
        var cola = FindP("DEMO-BEV-002");
        var sevenUp = FindP("DEMO-BEV-003");
        var water = FindP("DEMO-BEV-005");
        var lays = FindP("DEMO-SNK-001");
        var kurkure = FindP("DEMO-SNK-002");
        var biscuits = FindP("DEMO-SNK-003");
        var chocolate = FindP("DEMO-SNK-004");
        var surfExcel = FindP("DEMO-HH-001");
        var ariel = FindP("DEMO-HH-002");
        var soap = FindP("DEMO-HH-003");
        var shampoo = FindP("DEMO-HH-004");
        var toothpaste = FindP("DEMO-HH-005");
        var dishwash = FindP("DEMO-HH-006");
        var handwash = FindP("DEMO-HH-007");
        var rice = FindP("DEMO-GRO-002");

        // Ali Khan: Opening 2,000 -> Credit Sale 3,000 -> Payment 1,500 -> Balance 3,500
        if (aliKhan != null && pepsi != null)
        {
            CreateDemoSale(salesService, aliKhan, new[] { (pepsi, 20m) }, paid: 0, daysAgo: 12);
            customerService.ReceivePayment(aliKhan.Id, 1500, $"{DemoTag} Sample payment", "Cash");
        }

        // Ahmad Shah: Credit Sale 5,000 -> Payment 5,000 -> Balance 0
        if (ahmadShah != null && rice != null)
        {
            CreateDemoSale(salesService, ahmadShah, new[] { (rice, 5m) }, paid: 0, daysAgo: 10);
            customerService.ReceivePayment(ahmadShah.Id, 5000, $"{DemoTag} Sample payment", "Cash");
        }

        // Hamza Khan: Opening 2,000 -> Overpayment 3,000 -> Rs. 1,000 Advance
        if (hamzaKhan != null)
        {
            customerService.ReceivePayment(hamzaKhan.Id, 3000, $"{DemoTag} Sample overpayment", "Cash");
        }

        if (bilalKhan != null && cola != null && lays != null && kurkure != null)
            CreateDemoSale(salesService, bilalKhan, new[] { (cola, 2m), (lays, 1m), (kurkure, 1m) }, paid: null, daysAgo: 8);

        if (salmanAli != null && surfExcel != null && ariel != null)
            CreateDemoSale(salesService, salmanAli, new[] { (surfExcel, 1m), (ariel, 1m) }, paid: 400, discountPercent: 10, daysAgo: 7);

        if (water != null && biscuits != null)
            CreateDemoSale(salesService, null, new[] { (water, 3m), (biscuits, 2m) }, paid: null, daysAgo: 6);

        if (imranShah != null && shampoo != null && toothpaste != null)
            CreateDemoSale(salesService, imranShah, new[] { (shampoo, 1m), (toothpaste, 1m) }, paid: 0, daysAgo: 5);

        if (waqasAhmad != null && chocolate != null)
            CreateDemoSale(salesService, waqasAhmad, new[] { (chocolate, 5m) }, paid: null, fixedDiscount: 50, daysAgo: 4);

        if (asadKhan != null && dishwash != null && handwash != null)
            CreateDemoSale(salesService, asadKhan, new[] { (dishwash, 2m), (handwash, 1m) }, paid: 300, daysAgo: 3);

        if (usmanAhmad != null && soap != null)
            CreateDemoSale(salesService, usmanAhmad, new[] { (soap, 10m) }, paid: null, daysAgo: 2);

        // Faisal Khan has a deliberately low Rs. 5,000 credit limit — push him close to it (not over) for dashboard testing.
        if (faisalKhan != null && sevenUp != null)
            CreateDemoSale(salesService, faisalKhan, new[] { (sevenUp, 33m) }, paid: 0, daysAgo: 1);
    }

    private static void CreateDemoSale(SalesService salesService, Customer? customer, (Product product, decimal qty)[] items,
        decimal? paid, decimal discountPercent = 0, decimal fixedDiscount = 0, int daysAgo = 0)
    {
        var subtotal = items.Sum(i => i.product.SalePrice * i.qty);
        var discountAmount = fixedDiscount > 0 ? Math.Min(fixedDiscount, subtotal) : subtotal * discountPercent / 100;
        var grandTotal = subtotal - discountAmount;
        var paidAmount = paid ?? grandTotal;
        if (paidAmount > grandTotal) paidAmount = grandTotal;
        if (paidAmount < 0) paidAmount = 0;
        var remaining = grandTotal - paidAmount;

        var sale = new Sale
        {
            SaleDate = DateTime.Now.AddDays(-daysAgo),
            CustomerId = customer?.Id,
            Subtotal = subtotal,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            GrandTotal = grandTotal,
            PaidAmount = paidAmount,
            RemainingAmount = remaining,
            PaymentMethod = remaining > 0 ? (paidAmount > 0 ? "Partial" : "Credit") : "Cash",
            IsCreditSale = remaining > 0,
            Notes = $"{DemoTag} Sample sale for testing"
        };

        var saleItems = items.Select(i => new SaleItem
        {
            ProductId = i.product.Id,
            Quantity = i.qty,
            UnitPrice = i.product.SalePrice,
            PurchasePriceAtSale = i.product.PurchasePrice
        }).ToList();

        var (success, errorKey, _) = salesService.CompleteSale(sale, saleItems);
        if (!success) Debug.WriteLine($"[TestDataSeeder] Demo sale for '{customer?.Name ?? "Walk-in"}' failed: {errorKey}");
    }

    private static void SeedPurchases(List<Supplier> suppliers, List<Product> products)
    {
        var purchaseService = new PurchaseService();
        var existingPurchases = purchaseService.GetPurchases();
        if (existingPurchases.Any(p => p.Notes != null && p.Notes.Contains(DemoTag))) return;

        Supplier? FindS(string name) => suppliers.FirstOrDefault(s => s.Name == name);
        Product? FindP(string sku) => products.FirstOrDefault(p => p.SKU == sku);

        var shangla = FindS("Shangla Wholesale Mart");
        var mingora = FindS("Mingora Traders");
        var swatGeneral = FindS("Swat General Distributors");
        var kpkBev = FindS("KPK Beverage Suppliers");
        var alpurai = FindS("Alpurai Wholesale Center");

        var pepsi = FindP("DEMO-BEV-001");
        var cola = FindP("DEMO-BEV-002");
        var sevenUp = FindP("DEMO-BEV-003");
        var sprite = FindP("DEMO-BEV-004");
        var water = FindP("DEMO-BEV-005");
        var sugar = FindP("DEMO-GRO-001");
        var rice = FindP("DEMO-GRO-002");
        var lays = FindP("DEMO-SNK-001");
        var kurkure = FindP("DEMO-SNK-002");
        var biscuits = FindP("DEMO-SNK-003");
        var surfExcel = FindP("DEMO-HH-001");
        var ariel = FindP("DEMO-HH-002");
        var soap = FindP("DEMO-HH-003");

        if (shangla != null && pepsi != null && cola != null)
            CreateDemoPurchase(purchaseService, shangla, new[] { (pepsi, 100m, 130m), (cola, 100m, 130m) }, discount: 500, paid: null, daysAgo: 20);

        if (mingora != null && sugar != null && rice != null)
            CreateDemoPurchase(purchaseService, mingora, new[] { (sugar, 50m, 150m), (rice, 20m, 900m) }, discount: 0, paid: 15000, daysAgo: 18);

        if (swatGeneral != null && lays != null && kurkure != null && biscuits != null)
            CreateDemoPurchase(purchaseService, swatGeneral, new[] { (lays, 100m, 40m), (kurkure, 100m, 35m), (biscuits, 100m, 30m) }, discount: 0, paid: null, daysAgo: 15);

        if (kpkBev != null && sevenUp != null && sprite != null && water != null)
            CreateDemoPurchase(purchaseService, kpkBev, new[] { (sevenUp, 80m, 125m), (sprite, 80m, 125m), (water, 100m, 40m) }, discount: 400, paid: 15000, daysAgo: 12);

        if (alpurai != null && surfExcel != null && ariel != null && soap != null)
            CreateDemoPurchase(purchaseService, alpurai, new[] { (surfExcel, 40m, 320m), (ariel, 40m, 330m), (soap, 100m, 45m) }, discount: 0, paid: null, daysAgo: 9);
    }

    private static void CreateDemoPurchase(PurchaseService purchaseService, Supplier supplier, (Product product, decimal qty, decimal unitPrice)[] items,
        decimal discount, decimal? paid, int daysAgo)
    {
        var subtotal = items.Sum(i => i.qty * i.unitPrice);
        var grandTotal = subtotal - discount;
        var paidAmount = paid ?? grandTotal;
        if (paidAmount > grandTotal) paidAmount = grandTotal;
        var remaining = grandTotal - paidAmount;

        var purchase = new Purchase
        {
            InvoiceNumber = purchaseService.GenerateInvoiceNumber(),
            PurchaseDate = DateTime.Now.AddDays(-daysAgo),
            SupplierId = supplier.Id,
            Subtotal = subtotal,
            DiscountAmount = discount,
            GrandTotal = grandTotal,
            PaidAmount = paidAmount,
            RemainingAmount = remaining,
            Notes = $"{DemoTag} Sample purchase for testing"
        };

        var purchaseItems = items.Select(i => new PurchaseItem
        {
            ProductId = i.product.Id,
            Quantity = i.qty,
            UnitPrice = i.unitPrice
        }).ToList();

        purchaseService.CompletePurchase(purchase, purchaseItems);
    }

    private static void SeedExpenses()
    {
        var expenseService = new ExpenseService();
        var existing = expenseService.GetAll();

        void Seed(string category, decimal amount, string description, int daysAgo)
        {
            var desc = $"{DemoTag} {description}";
            if (existing.Any(e => e.Description == desc)) return;
            expenseService.Save(new Expense
            {
                ExpenseDate = DateTime.Now.AddDays(-daysAgo),
                Category = category,
                Amount = amount,
                Description = desc,
                PaymentMethod = "Cash"
            });
        }

        Seed("Electricity", 8500, "Monthly electricity bill", 5);
        Seed("Rent", 25000, "Shop monthly rent", 10);
        Seed("Transport", 3000, "Goods transport charges", 3);
        Seed("Maintenance", 4500, "Shop repair and maintenance", 7);
        Seed("Other", 1200, "Miscellaneous shop expenses", 2);
    }
}
#endif
