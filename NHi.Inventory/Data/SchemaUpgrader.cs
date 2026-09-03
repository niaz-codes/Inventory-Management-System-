using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Data;

/// <summary>
/// EnsureCreated() only builds the schema for a brand-new database file — it
/// never adds tables/columns to a database that already exists from a prior
/// version of the app. This runs once at startup to bring an existing
/// database up to date with new tables/columns added after initial release,
/// without touching any existing data.
/// </summary>
public static class SchemaUpgrader
{
    public static void Run(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        connection.Open();

        EnsureEmployeeTables(connection);
        EnsureEmployeeDailyWageSchema(connection);
        EnsureEmployeeSalarySchema(connection);
        EnsureCompanySettingsWhatsAppColumns(connection);
        EnsureCustomerKhataColumns(connection);
        EnsureCustomerContactColumns(connection);
        EnsureMessageLogTable(connection);
        EnsureInventoryColumns(connection);
        EnsureThemeColumn(connection);
        EnsureSupplierKhataColumns(connection);
        // Must run last: it copies from CompanySettings into the new Shops table, so every
        // column the copy reads (Theme, WhatsApp*, ExpiryWarningDays, BlockExpiredSale) needs
        // to already exist on CompanySettings by the time it runs.
        EnsureShopSchema(connection);
        EnsureShopPosPrintColumn(connection);
        EnsureUnitSchema(connection);

        connection.Close();
    }

    /// <summary>Adds Unit.IsProtected and guarantees the five protected default units
    /// (Kilogram, Foot, Inch, Box, Packet) exist and are flagged — on databases created before
    /// Unit Management shipped.</summary>
    private static void EnsureUnitSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "Units")) return;

        if (!GetColumns(connection, "Units").Contains("IsProtected"))
            Exec(connection, "ALTER TABLE \"Units\" ADD COLUMN \"IsProtected\" INTEGER NOT NULL DEFAULT 0;");

        foreach (var (name, symbol) in new[] { ("Kilogram", "KG"), ("Foot", "ft"), ("Inch", "in"), ("Box", "box"), ("Packet", "pkt") })
            Exec(connection,
                $"INSERT INTO \"Units\" (\"Name\",\"Symbol\",\"IsProtected\") " +
                $"SELECT '{name}','{symbol}',1 WHERE NOT EXISTS (SELECT 1 FROM \"Units\" WHERE lower(\"Name\")='{name.ToLower()}');");

        Exec(connection, "UPDATE \"Units\" SET \"IsProtected\"=1 WHERE lower(\"Name\") IN ('kilogram','foot','inch','box','packet');");
    }

    private static bool TableExists(System.Data.Common.DbConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name";
        var param = cmd.CreateParameter();
        param.ParameterName = "$name";
        param.Value = tableName;
        cmd.Parameters.Add(param);
        return cmd.ExecuteScalar() != null;
    }

    private static List<string> GetColumns(System.Data.Common.DbConnection connection, string tableName)
    {
        var columns = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(reader.GetOrdinal("name")));
        return columns;
    }

    private static void Exec(System.Data.Common.DbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void EnsureEmployeeTables(System.Data.Common.DbConnection connection)
    {
        if (TableExists(connection, "Employees")) return;

        Exec(connection, """
            CREATE TABLE "Employees" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Employees" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Position" TEXT NULL,
                "Phone" TEXT NULL,
                "Address" TEXT NULL,
                "DailyWage" TEXT NOT NULL,
                "JoiningDate" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "CurrentBalance" TEXT NOT NULL
            );
            """);

        Exec(connection, """
            CREATE TABLE "EmployeeLedgers" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_EmployeeLedgers" PRIMARY KEY AUTOINCREMENT,
                "EmployeeId" INTEGER NOT NULL,
                "TransactionDate" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Debit" TEXT NOT NULL,
                "Credit" TEXT NOT NULL,
                "Balance" TEXT NOT NULL,
                "ReferenceId" INTEGER NULL,
                "ReferenceType" TEXT NULL,
                CONSTRAINT "FK_EmployeeLedgers_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
            );
            """);

        Exec(connection, """
            CREATE TABLE "EmployeePayments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_EmployeePayments" PRIMARY KEY AUTOINCREMENT,
                "EmployeeId" INTEGER NOT NULL,
                "Amount" TEXT NOT NULL,
                "PaymentDate" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "CreatedBy" INTEGER NULL,
                CONSTRAINT "FK_EmployeePayments_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
            );
            """);

        Exec(connection, "CREATE INDEX \"IX_EmployeeLedgers_EmployeeId\" ON \"EmployeeLedgers\" (\"EmployeeId\");");
        Exec(connection, "CREATE INDEX \"IX_EmployeePayments_EmployeeId\" ON \"EmployeePayments\" (\"EmployeeId\");");
        Exec(connection, "CREATE INDEX \"IX_Employees_Phone\" ON \"Employees\" (\"Phone\");");
    }

    /// <summary>
    /// Upgrades an Employees table created before the daily-wage feature (which still has
    /// MonthlySalary) and ensures EmployeeAttendances exists — independent of whether
    /// EnsureEmployeeTables just created a brand-new schema above.
    /// </summary>
    private static void EnsureEmployeeDailyWageSchema(System.Data.Common.DbConnection connection)
    {
        var employeeColumns = GetColumns(connection, "Employees");
        if (employeeColumns.Contains("MonthlySalary") && !employeeColumns.Contains("DailyWage"))
            Exec(connection, "ALTER TABLE \"Employees\" RENAME COLUMN \"MonthlySalary\" TO \"DailyWage\";");

        if (!TableExists(connection, "EmployeeAttendances"))
        {
            Exec(connection, """
                CREATE TABLE "EmployeeAttendances" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_EmployeeAttendances" PRIMARY KEY AUTOINCREMENT,
                    "EmployeeId" INTEGER NOT NULL,
                    "WorkDate" TEXT NOT NULL,
                    "Amount" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "LedgerId" INTEGER NULL,
                    "CreatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_EmployeeAttendances_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
                );
                """);
            Exec(connection, "CREATE UNIQUE INDEX \"IX_EmployeeAttendances_EmployeeId_WorkDate\" ON \"EmployeeAttendances\" (\"EmployeeId\", \"WorkDate\");");
            Exec(connection, "CREATE INDEX \"IX_EmployeeAttendances_EmployeeId\" ON \"EmployeeAttendances\" (\"EmployeeId\");");
        }
    }

    /// <summary>
    /// Adds the monthly-salary option alongside the existing per-day wage: a SalaryType
    /// discriminator ("Daily"/"Monthly") and a MonthlySalary amount. Existing employees
    /// keep working as daily-wage (the column default).
    /// </summary>
    private static void EnsureEmployeeSalarySchema(System.Data.Common.DbConnection connection)
    {
        var cols = GetColumns(connection, "Employees");
        if (cols.Count == 0) return;
        if (!cols.Contains("SalaryType"))
            Exec(connection, "ALTER TABLE \"Employees\" ADD COLUMN \"SalaryType\" TEXT NOT NULL DEFAULT 'Daily';");
        if (!cols.Contains("MonthlySalary"))
            Exec(connection, "ALTER TABLE \"Employees\" ADD COLUMN \"MonthlySalary\" TEXT NOT NULL DEFAULT '0';");
    }

    private static void EnsureShopPosPrintColumn(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "Shops")) return;
        var cols = GetColumns(connection, "Shops");
        if (!cols.Contains("PosPrintJson"))
            Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PosPrintJson\" TEXT NULL;");
        if (!cols.Contains("AllowNegativeStock"))
            Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"AllowNegativeStock\" INTEGER NOT NULL DEFAULT 0;");
    }

    private static void EnsureThemeColumn(System.Data.Common.DbConnection connection)
    {
        var columns = GetColumns(connection, "CompanySettings");
        if (!columns.Contains("Theme"))
            Exec(connection, "ALTER TABLE \"CompanySettings\" ADD COLUMN \"Theme\" TEXT NOT NULL DEFAULT 'light';");
    }

    private static void EnsureCompanySettingsWhatsAppColumns(System.Data.Common.DbConnection connection)
    {
        var columns = GetColumns(connection, "CompanySettings");
        if (!columns.Contains("WhatsAppNotificationsEnabled"))
            Exec(connection, "ALTER TABLE \"CompanySettings\" ADD COLUMN \"WhatsAppNotificationsEnabled\" INTEGER NOT NULL DEFAULT 0;");
        if (!columns.Contains("WhatsAppAccountSid"))
            Exec(connection, "ALTER TABLE \"CompanySettings\" ADD COLUMN \"WhatsAppAccountSid\" TEXT NULL;");
        if (!columns.Contains("WhatsAppAuthToken"))
            Exec(connection, "ALTER TABLE \"CompanySettings\" ADD COLUMN \"WhatsAppAuthToken\" TEXT NULL;");
        if (!columns.Contains("WhatsAppFromNumber"))
            Exec(connection, "ALTER TABLE \"CompanySettings\" ADD COLUMN \"WhatsAppFromNumber\" TEXT NULL;");
    }

    private static void EnsureCustomerKhataColumns(System.Data.Common.DbConnection connection)
    {
        var customerColumns = GetColumns(connection, "Customers");
        if (!customerColumns.Contains("WhatsAppNumber"))
            Exec(connection, "ALTER TABLE \"Customers\" ADD COLUMN \"WhatsAppNumber\" TEXT NULL;");

        var ledgerColumns = GetColumns(connection, "CustomerLedgers");
        if (!ledgerColumns.Contains("DueDate"))
            Exec(connection, "ALTER TABLE \"CustomerLedgers\" ADD COLUMN \"DueDate\" TEXT NULL;");
        if (!ledgerColumns.Contains("CreatedBy"))
            Exec(connection, "ALTER TABLE \"CustomerLedgers\" ADD COLUMN \"CreatedBy\" INTEGER NULL;");
        if (!ledgerColumns.Contains("IsReversed"))
            Exec(connection, "ALTER TABLE \"CustomerLedgers\" ADD COLUMN \"IsReversed\" INTEGER NOT NULL DEFAULT 0;");
        if (!ledgerColumns.Contains("ReversedByLedgerId"))
            Exec(connection, "ALTER TABLE \"CustomerLedgers\" ADD COLUMN \"ReversedByLedgerId\" INTEGER NULL;");
        if (!ledgerColumns.Contains("TransactionType"))
            Exec(connection, "ALTER TABLE \"CustomerLedgers\" ADD COLUMN \"TransactionType\" TEXT NOT NULL DEFAULT '';");

        Exec(connection, "CREATE INDEX IF NOT EXISTS \"IX_CustomerLedgers_CustomerId\" ON \"CustomerLedgers\" (\"CustomerId\");");

        var paymentColumns = GetColumns(connection, "CustomerPayments");
        if (!paymentColumns.Contains("ReceiptNumber"))
            Exec(connection, "ALTER TABLE \"CustomerPayments\" ADD COLUMN \"ReceiptNumber\" TEXT NULL;");
    }

    private static void EnsureCustomerContactColumns(System.Data.Common.DbConnection connection)
    {
        var customerColumns = GetColumns(connection, "Customers");
        if (!customerColumns.Contains("Email"))
            Exec(connection, "ALTER TABLE \"Customers\" ADD COLUMN \"Email\" TEXT NULL;");
        if (!customerColumns.Contains("WhatsAppSameAsPhone"))
            Exec(connection, "ALTER TABLE \"Customers\" ADD COLUMN \"WhatsAppSameAsPhone\" INTEGER NOT NULL DEFAULT 1;");
    }

    /// <summary>Brings the Supplier module up to Customer Khata parity — see Models/Supplier.cs,
    /// SupplierLedger.cs, SupplierPayment.cs, MessageLog.cs. ShopId on these tables is already
    /// added by the generic shopScopedTables loop in EnsureShopSchema below.</summary>
    private static void EnsureSupplierKhataColumns(System.Data.Common.DbConnection connection)
    {
        var supplierColumns = GetColumns(connection, "Suppliers");
        if (!supplierColumns.Contains("CompanyName"))
            Exec(connection, "ALTER TABLE \"Suppliers\" ADD COLUMN \"CompanyName\" TEXT NULL;");
        if (!supplierColumns.Contains("WhatsAppNumber"))
            Exec(connection, "ALTER TABLE \"Suppliers\" ADD COLUMN \"WhatsAppNumber\" TEXT NULL;");

        var ledgerColumns = GetColumns(connection, "SupplierLedgers");
        if (!ledgerColumns.Contains("TransactionType"))
            Exec(connection, "ALTER TABLE \"SupplierLedgers\" ADD COLUMN \"TransactionType\" TEXT NOT NULL DEFAULT '';");
        if (!ledgerColumns.Contains("CreatedBy"))
            Exec(connection, "ALTER TABLE \"SupplierLedgers\" ADD COLUMN \"CreatedBy\" INTEGER NULL;");

        var paymentColumns = GetColumns(connection, "SupplierPayments");
        if (!paymentColumns.Contains("ReceiptNumber"))
            Exec(connection, "ALTER TABLE \"SupplierPayments\" ADD COLUMN \"ReceiptNumber\" TEXT NULL;");
        if (!paymentColumns.Contains("ReferenceNumber"))
            Exec(connection, "ALTER TABLE \"SupplierPayments\" ADD COLUMN \"ReferenceNumber\" TEXT NULL;");

        if (TableExists(connection, "MessageLogs"))
        {
            var messageLogColumns = GetColumns(connection, "MessageLogs");
            if (!messageLogColumns.Contains("SupplierId"))
                Exec(connection, "ALTER TABLE \"MessageLogs\" ADD COLUMN \"SupplierId\" INTEGER NULL;");
        }
    }

    /// <summary>One row per individual reminder/receipt send attempt (one language, one
    /// channel) — see Models/MessageLog.cs. ShopId is added by the generic shopScopedTables
    /// loop in EnsureShopSchema below, same as every other new shop-owned table.</summary>
    private static void EnsureMessageLogTable(System.Data.Common.DbConnection connection)
    {
        if (TableExists(connection, "MessageLogs")) return;

        Exec(connection, """
            CREATE TABLE "MessageLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MessageLogs" PRIMARY KEY AUTOINCREMENT,
                "CustomerId" INTEGER NULL,
                "MessageType" TEXT NOT NULL,
                "Language" TEXT NOT NULL,
                "Channel" TEXT NOT NULL,
                "ReferenceType" TEXT NULL,
                "ReferenceId" INTEGER NULL,
                "Status" TEXT NOT NULL,
                "ErrorDetail" TEXT NULL,
                "MessageBody" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """);
        Exec(connection, "CREATE INDEX \"IX_MessageLogs_CustomerId\" ON \"MessageLogs\" (\"CustomerId\");");
        Exec(connection, "CREATE INDEX \"IX_MessageLogs_CreatedAt\" ON \"MessageLogs\" (\"CreatedAt\");");
    }

    private static void EnsureInventoryColumns(System.Data.Common.DbConnection connection)
    {
        var settingsColumns = GetColumns(connection, "CompanySettings");
        if (!settingsColumns.Contains("ExpiryWarningDays"))
            Exec(connection, "ALTER TABLE \"CompanySettings\" ADD COLUMN \"ExpiryWarningDays\" INTEGER NOT NULL DEFAULT 30;");
        if (!settingsColumns.Contains("BlockExpiredSale"))
            Exec(connection, "ALTER TABLE \"CompanySettings\" ADD COLUMN \"BlockExpiredSale\" INTEGER NOT NULL DEFAULT 0;");

        Exec(connection, "CREATE INDEX IF NOT EXISTS \"IX_StockTransactions_ProductId\" ON \"StockTransactions\" (\"ProductId\");");
        Exec(connection, "CREATE INDEX IF NOT EXISTS \"IX_StockTransactions_TransactionDate\" ON \"StockTransactions\" (\"TransactionDate\");");
    }

    /// <summary>
    /// Multi-shop migration: creates the Shops table, seeds "Shop 1" from the existing
    /// single CompanySettings row (or a bare default if none exists), adds a ShopId column
    /// to every shop-scoped table (existing rows backfill to Shop 1 automatically via the
    /// column DEFAULT), and promotes the original 'admin' account to Main Admin. Every other
    /// existing user keeps working exactly as before, now scoped to Shop 1.
    /// </summary>
    private static void EnsureShopSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "Shops"))
        {
            Exec(connection, """
                CREATE TABLE "Shops" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Shops" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "OwnerName" TEXT NULL,
                    "BusinessType" TEXT NULL,
                    "Logo" BLOB NULL,
                    "Phone" TEXT NULL,
                    "WhatsApp" TEXT NULL,
                    "Address" TEXT NULL,
                    "City" TEXT NULL,
                    "Email" TEXT NULL,
                    "Website" TEXT NULL,
                    "TaxNumber" TEXT NULL,
                    "Currency" TEXT NOT NULL,
                    "InvoiceFooter" TEXT NULL,
                    "Theme" TEXT NOT NULL,
                    "WhatsAppNotificationsEnabled" INTEGER NOT NULL,
                    "WhatsAppAccountSid" TEXT NULL,
                    "WhatsAppAuthToken" TEXT NULL,
                    "WhatsAppFromNumber" TEXT NULL,
                    "ExpiryWarningDays" INTEGER NOT NULL,
                    "BlockExpiredSale" INTEGER NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "UpdatedBy" INTEGER NULL
                );
                """);
        }

        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM \"Shops\"";
            var shopCount = Convert.ToInt32(checkCmd.ExecuteScalar());
            if (shopCount == 0)
            {
                if (TableExists(connection, "CompanySettings"))
                {
                    using var countCmd = connection.CreateCommand();
                    countCmd.CommandText = "SELECT COUNT(*) FROM \"CompanySettings\"";
                    var settingsCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    if (settingsCount > 0)
                    {
                        Exec(connection, """
                            INSERT INTO "Shops"
                                ("Name","OwnerName","Logo","Phone","WhatsApp","Address","City","Email","Website",
                                 "TaxNumber","Currency","InvoiceFooter","Theme","WhatsAppNotificationsEnabled",
                                 "WhatsAppAccountSid","WhatsAppAuthToken","WhatsAppFromNumber","ExpiryWarningDays",
                                 "BlockExpiredSale","IsActive","CreatedAt","UpdatedAt","UpdatedBy")
                            SELECT "CompanyName", NULL, "Logo", "Phone", "WhatsApp", "Address", "City", "Email", "Website",
                                   "TaxNumber", "Currency", "InvoiceFooter", "Theme", "WhatsAppNotificationsEnabled",
                                   "WhatsAppAccountSid", "WhatsAppAuthToken", "WhatsAppFromNumber", "ExpiryWarningDays",
                                   "BlockExpiredSale", 1, datetime('now'), datetime('now'), "UpdatedBy"
                            FROM "CompanySettings" LIMIT 1;
                            """);
                    }
                }

                using var recheckCmd = connection.CreateCommand();
                recheckCmd.CommandText = "SELECT COUNT(*) FROM \"Shops\"";
                if (Convert.ToInt32(recheckCmd.ExecuteScalar()) == 0)
                {
                    Exec(connection, """
                        INSERT INTO "Shops" ("Name","Currency","Theme","ExpiryWarningDays","IsActive","CreatedAt","UpdatedAt")
                        VALUES ('NHI General Store', 'Rs.', 'light', 30, 1, datetime('now'), datetime('now'));
                        """);
                }
            }
        }

        if (TableExists(connection, "Shops"))
        {
            var shopColumns = GetColumns(connection, "Shops");
            if (!shopColumns.Contains("BusinessType"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"BusinessType\" TEXT NULL;");
            if (!shopColumns.Contains("ReceiptWidthMm"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"ReceiptWidthMm\" REAL NULL;");
            if (!shopColumns.Contains("ReceiptHeightMm"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"ReceiptHeightMm\" REAL NULL;");

            // --- Print Layout (page size preset, custom size, margins, orientation) ---
            // Defaults reproduce the page's pre-existing hardcoded 10pt (~3.53mm) margin and
            // Receipt/Portrait layout exactly, so shops that never open the new Print Layout
            // dialog keep printing exactly as before.
            if (!shopColumns.Contains("PrintPageSize"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintPageSize\" TEXT NOT NULL DEFAULT 'Receipt';");
            if (!shopColumns.Contains("PrintCustomWidth"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintCustomWidth\" REAL NULL;");
            if (!shopColumns.Contains("PrintCustomHeight"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintCustomHeight\" REAL NULL;");
            if (!shopColumns.Contains("PrintUnit"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintUnit\" TEXT NOT NULL DEFAULT 'mm';");
            if (!shopColumns.Contains("PrintMarginTop"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintMarginTop\" REAL NOT NULL DEFAULT 3.53;");
            if (!shopColumns.Contains("PrintMarginBottom"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintMarginBottom\" REAL NOT NULL DEFAULT 3.53;");
            if (!shopColumns.Contains("PrintMarginLeft"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintMarginLeft\" REAL NOT NULL DEFAULT 3.53;");
            if (!shopColumns.Contains("PrintMarginRight"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintMarginRight\" REAL NOT NULL DEFAULT 3.53;");
            if (!shopColumns.Contains("PrintOrientation"))
                Exec(connection, "ALTER TABLE \"Shops\" ADD COLUMN \"PrintOrientation\" TEXT NOT NULL DEFAULT 'Portrait';");
        }

        var shopScopedTables = new[]
        {
            "Products", "Categories", "Brands", "ExpenseCategories",
            "Customers", "CustomerLedgers", "CustomerPayments",
            "Suppliers", "SupplierLedgers", "SupplierPayments",
            "Sales", "SaleItems", "SaleReturns",
            "Purchases", "PurchaseItems", "PurchaseReturns",
            "Expenses", "StockTransactions", "CashTransactions", "DailyClosings",
            "Employees", "EmployeeLedgers", "EmployeePayments", "EmployeeAttendances",
            "MessageLogs"
        };
        foreach (var table in shopScopedTables)
        {
            if (!TableExists(connection, table)) continue;
            var columns = GetColumns(connection, table);
            if (!columns.Contains("ShopId"))
                Exec(connection, $"ALTER TABLE \"{table}\" ADD COLUMN \"ShopId\" INTEGER NOT NULL DEFAULT 1;");
        }

        var usersJustGainedShopId = false;
        foreach (var table in new[] { "Users", "AuditLogs" })
        {
            if (!TableExists(connection, table)) continue;
            var columns = GetColumns(connection, table);
            if (!columns.Contains("ShopId"))
            {
                Exec(connection, $"ALTER TABLE \"{table}\" ADD COLUMN \"ShopId\" INTEGER NULL;");
                if (table == "Users") usersJustGainedShopId = true;
            }
        }

        if (TableExists(connection, "Users"))
        {
            var userColumns = GetColumns(connection, "Users");
            if (!userColumns.Contains("Status"))
                // Every pre-existing account (admin included) is already in active use, so it
                // must not suddenly get locked out by the new Pending-approval gate — only
                // brand-new accounts created going forward start Pending.
                Exec(connection, "ALTER TABLE \"Users\" ADD COLUMN \"Status\" TEXT NOT NULL DEFAULT 'Approved';");
        }

        // One-time only: this must NOT re-run on every startup, or it would keep pulling the
        // already-promoted Main Admin's ShopId back to 1 (their ShopId is legitimately NULL
        // going forward, which would otherwise look identical to "column just added").
        if (usersJustGainedShopId && TableExists(connection, "Users"))
        {
            // The ShopId column has no default (it's legitimately nullable going forward), so
            // every pre-existing user starts out NULL right after the ALTER TABLE — backfill
            // all of them to Shop 1 first, then carve out just the seeded 'admin' account as
            // the shop-less Main Admin. Order matters: promoting admin first and backfilling
            // everyone to 1 after would wrongly pull admin back into Shop 1 too.
            Exec(connection, "UPDATE \"Users\" SET \"ShopId\" = 1 WHERE \"ShopId\" IS NULL;");
            Exec(connection, "UPDATE \"Users\" SET \"Role\" = 'MAIN_ADMIN', \"ShopId\" = NULL WHERE \"Username\" = 'admin' AND \"Role\" = 'ADMIN';");
        }
    }
}
