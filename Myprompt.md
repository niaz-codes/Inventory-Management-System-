# NHi Inventory Management System

Build a complete, professional, user-friendly **offline Inventory Management + POS + Khata/Ledger System** for shops.

## Software Name

**NHi Inventory Management System**

The software must be designed especially for small and medium-sized shops. It should be extremely simple for a shopkeeper to use, fast, reliable, and fully functional without an internet connection.

---

# 1. Core Requirements

Build a complete offline shop management system containing:

* Inventory Management
* Product Management
* POS Sale System
* Purchase Management
* Customer Management
* Supplier Management
* Khata / Credit Ledger
* Customer Payments
* Customer Credit Sales
* Discounts
* Stock Management
* Low Stock Alerts
* Sales Reports
* Purchase Reports
* Profit Reports
* Customer Ledger Reports
* Daily Closing
* Expense Management
* Cash Management
* Excel Export
* Printable Reports
* Crystal Reports integration
* Backup and Restore
* Admin Login
* Urdu + English language support
* Offline database

The system must work completely offline and must NOT require an internet connection for normal operation.

---

# 2. Login System

Create a secure login screen.

Default:

**Username:** admin
**Password:** admin

The administrator must be able to change the password.

Login screen should contain:

* NHi logo
* Username
* Password
* Show/Hide Password
* Login button
* Remember login option
* Urdu / English language selector

After successful login, open the main dashboard.

---

# 3. Fully Offline Database

Use a local offline database such as:

**SQLite**

All important data must be stored locally.

Database should contain tables such as:

* Users
* Products
* Categories
* Brands
* Units
* Customers
* Suppliers
* Purchases
* PurchaseItems
* Sales
* SaleItems
* CustomerLedger
* CustomerPayments
* SupplierLedger
* SupplierPayments
* Expenses
* StockTransactions
* CashTransactions
* Settings
* BackupHistory

The system must continue working even when Wi-Fi/mobile internet is completely unavailable.

---

# 4. Main Dashboard

Create a modern, clean and very simple dashboard.

Show:

* Today's Sales
* Today's Purchases
* Today's Profit
* Total Customers
* Total Suppliers
* Total Products
* Low Stock Products
* Customer Receivables
* Supplier Payables
* Today's Expenses
* Current Cash

Use attractive dashboard cards.

Dashboard should be responsive and easy to understand.

---

# 5. Bottom Navigation / Tabs

For the main application, use a simple bottom navigation system.

Tabs:

1. **Home**
2. **POS Sale**
3. **Inventory**
4. **Khata**
5. **More**

On desktop, these can also appear as a sidebar/menu.

The navigation must be extremely user-friendly.

---

# 6. Product / Inventory Management

Create a complete product management system.

When adding a product, fields should include:

* Product Name English
* Product Name Urdu
* Barcode
* SKU
* Category
* Brand
* Unit
* Purchase Price
* Sale Price
* Wholesale Price
* Opening Stock
* Minimum Stock
* Maximum Stock
* Product Image
* Expiry Date
* Batch Number
* Description
* Active/Inactive

Support units such as:

* Piece
* Packet
* Box
* Kg
* Gram
* Liter
* Dozen
* Meter
* Bottle
* Carton

---

# 7. Product Card System

This is very important.

When a user adds products, display them as simple product cards.

Each card should show:

* Product image/icon
* Product name
* Price
* Available stock
* Barcode
* Unit

Example:

**Coca Cola 1.5L**
Rs. 180
Stock: 25

When the shopkeeper clicks/selects the product card:

**Automatically add that product to the current POS sale/cart.**

No need to manually type the product name.

Allow:

* Search product
* Barcode scan
* Product card selection
* Category filtering
* Brand filtering
* Fast product selection

---

# 8. POS Sale System

Create a fast POS screen.

POS should contain:

### Left/Top Area

Product search and product cards.

### Right/Bottom Area

Current shopping cart.

Cart columns:

* Product
* Quantity
* Price
* Discount
* Total
* Remove

When the user selects a product card, automatically add it to the cart.

Quantity should be easily editable.

Example:

Product: Pepsi
Qty: 2
Price: Rs. 100
Total: Rs. 200

---

# 9. Discount System

Allow discounts in two ways:

### Percentage Discount

Example:

Subtotal = Rs. 1,000
Discount = 10%
Discount Amount = Rs. 100
Final Total = Rs. 900

### Fixed Amount Discount

Example:

Subtotal = Rs. 1,000
Discount = Rs. 100
Final Total = Rs. 900

Allow discount:

* Per product
* On complete invoice

Display clearly:

Subtotal
Discount
Grand Total

---

# 10. Payment System

POS should support:

* Cash
* Credit / Khata
* Partial Payment

Example:

Total = Rs. 5,000

Customer pays:

Rs. 3,000 Cash

Remaining:

Rs. 2,000 Credit

Automatically add Rs. 2,000 to customer's Khata.

---

# 11. Customer Khata System

Create a complete traditional **Khata system**.

This should be one of the main features of the software.

Customer profile:

* Customer Name
* Father Name
* Phone
* Address
* Opening Balance
* Credit Limit
* Notes

Customer Khata should show:

### Customer Name

Ahmed Khan

### Transactions

| Date   | Description  | Debit | Credit | Balance |
| ------ | ------------ | ----: | -----: | ------: |
| 17-Aug | Grocery Sale | 2,000 |      - |   2,000 |
| 18-Aug | Payment      |     - |  1,000 |   1,000 |
| 20-Aug | Grocery Sale |   500 |      - |   1,500 |

Use simple terms for shopkeepers:

**Customer has to pay:** Rs. 1,500

---

# 12. Khata Payment

Customer payment screen should be very simple.

Select customer.

Show:

**Previous Balance: Rs. 5,000**

Enter payment:

**Rs. 2,000**

After saving:

**Remaining Balance: Rs. 3,000**

Automatically create a ledger transaction.

Allow printing of payment receipt.

---

# 13. Customer Ledger / Statement

For every customer, provide:

* Complete Khata
* Total Purchases
* Total Paid
* Remaining Balance
* Date-wise transactions
* Sale invoices
* Payment history

Allow:

* Print
* PDF
* Crystal Report
* Excel Export

---

# 14. Supplier Khata

Create the same system for suppliers.

Supplier ledger should track:

* Purchases
* Payments
* Remaining payable amount
* Date-wise transactions

Example:

Supplier Balance:

**You have to pay supplier: Rs. 25,000**

---

# 15. Stock Management

Automatically update stock.

When purchasing:

**Stock increases**

When selling:

**Stock decreases**

When returning:

**Stock adjusts**

Every stock change should create a Stock Transaction.

Show:

* Current Stock
* Opening Stock
* Purchased
* Sold
* Returned
* Damaged
* Adjusted

---

# 16. Low Stock Alert

Set minimum stock for every product.

Example:

Product stock = 3
Minimum stock = 5

Show:

**LOW STOCK**

Dashboard should display all low-stock products.

---

# 17. Purchase Management

Create Purchase module.

Fields:

* Supplier
* Invoice Number
* Date
* Product
* Quantity
* Purchase Price
* Discount
* Total
* Paid Amount
* Remaining Amount

Purchasing products must automatically increase inventory.

---

# 18. Sales Return

Create Sales Return system.

Allow user to:

* Search invoice
* Select product
* Enter returned quantity
* Calculate refund
* Update stock
* Update customer ledger

---

# 19. Purchase Return

Create Purchase Return system.

When purchase items are returned:

* Stock decreases
* Supplier ledger updates
* Amount adjusts automatically

---

# 20. Expense Management

Create Expense module.

Categories:

* Electricity
* Rent
* Salary
* Transport
* Maintenance
* Other

Fields:

* Date
* Expense Category
* Amount
* Description

Expenses should appear in reports and profit calculations.

---

# 21. Cash Management

Create cash management.

Track:

* Opening Cash
* Cash Sales
* Customer Payments
* Expenses
* Supplier Payments
* Cash Withdrawals
* Closing Cash

Calculate:

**Expected Closing Cash**

and show differences if any.

---

# 22. Daily Closing

Create a Daily Closing feature.

At the end of the day show:

* Total Sales
* Cash Sales
* Credit Sales
* Customer Payments
* Purchases
* Expenses
* Supplier Payments
* Opening Cash
* Closing Cash
* Net Profit

Allow printing/exporting the daily closing report.

---

# 23. Reports

Create a complete Reports section.

Reports should include:

### Sales Reports

* Daily Sales
* Weekly Sales
* Monthly Sales
* Date Range Sales
* Product-wise Sales
* Customer-wise Sales
* Cash Sales
* Credit Sales

### Purchase Reports

* Daily Purchases
* Monthly Purchases
* Supplier-wise Purchases

### Inventory Reports

* Current Stock
* Low Stock
* Stock Valuation
* Stock Movement
* Product-wise Stock

### Profit Reports

* Gross Profit
* Net Profit
* Product-wise Profit
* Daily Profit
* Monthly Profit

### Khata Reports

* Customer Ledger
* Supplier Ledger
* Receivables
* Payables
* Payment History

### Expense Reports

* Daily Expenses
* Monthly Expenses
* Category-wise Expenses

---

# 24. Crystal Reports

Integrate **Crystal Reports** for professional printable reports.

Create Crystal Reports for:

* Sales Invoice
* POS Receipt
* Customer Khata
* Supplier Ledger
* Daily Sales
* Monthly Sales
* Purchase Report
* Stock Report
* Profit Report
* Daily Closing
* Customer Payment Receipt

Reports should contain:

**NHi Inventory Management System**

Shop Name
Shop Address
Phone
Date
Invoice Number

---

# 25. Excel Export

Every major report should have:

**Export to Excel**

button.

Export:

* Sales
* Purchases
* Products
* Stock
* Customers
* Suppliers
* Customer Khata
* Supplier Ledger
* Expenses
* Profit
* Daily Closing

Excel files should have clean headers and proper formatting.

---

# 26. Invoice / Receipt

After POS sale, show:

**Sale Successful**

Invoice number should be automatically generated.

Example:

NHI-2026-000001

Receipt should show:

* Shop Name
* Invoice Number
* Date
* Customer
* Products
* Quantity
* Price
* Discount
* Total
* Paid
* Remaining
* Payment Method

Allow:

* Print
* Reprint
* Save PDF

---

# 27. Urdu + English Full Support

The complete application must support both:

**English 🇬🇧**

and

**اردو 🇵🇰**

This is not just a language selector on the login page.

ALL UI text must support translation:

* Buttons
* Menus
* Dashboard
* Products
* POS
* Customers
* Suppliers
* Khata
* Reports
* Settings
* Validation messages
* Invoice
* Receipts
* Error messages
* Notifications
* Dialog boxes

Example:

English:

**Add Product**

Urdu:

**پروڈکٹ شامل کریں**

English:

**Customer Khata**

Urdu:

**کسٹمر کھاتہ**

English:

**Remaining Balance**

Urdu:

**باقی رقم**

---

# 28. Urdu RTL Support

When Urdu is selected:

* Enable RTL layout
* Align text appropriately
* Reverse navigation where appropriate
* Support Urdu fonts
* Ensure tables work correctly
* Ensure invoices print correctly
* Ensure Crystal Reports support Urdu
* Ensure Excel exports support Urdu

When English is selected:

Use normal LTR layout.

Language can be switched without restarting the application if possible.

---

# 29. Very User-Friendly UI

The target user may not be technically skilled.

Therefore:

DO NOT create a complicated enterprise UI.

Use:

* Large buttons
* Clear icons
* Simple labels
* Large product cards
* Easy search
* Minimal typing
* Keyboard shortcuts
* Confirmation dialogs
* Clear success/error messages

POS should be usable by a shopkeeper after only a few minutes of training.

---

# 30. Keyboard Shortcuts

Add useful POS shortcuts:

F1 = New Sale
F2 = Search Product
F3 = Customer
F4 = Payment
F5 = Hold Sale
F6 = Discount
F7 = Sales History
F8 = Khata
F9 = Print
F10 = Complete Sale
ESC = Cancel

---

# 31. Sales History

Create Sales History.

Show:

* Invoice Number
* Date
* Customer
* Total
* Paid
* Remaining
* Payment Method

Actions:

* View
* Print
* Reprint
* Return
* Delete/Void with admin confirmation

---

# 32. Search

Global search should support:

* Product name
* Barcode
* SKU
* Customer name
* Customer phone
* Invoice number
* Supplier name

Search should be fast.

---

# 33. Backup and Restore

Because the system is offline, create a strong backup system.

Features:

* Backup Database
* Restore Database
* Automatic Backup
* Backup Location
* Backup History

Allow backup to:

* USB
* Local folder
* External drive

Never delete existing data accidentally during restore.

Ask for confirmation before restore.

---

# 34. Security

Admin authentication must be secure.

Do not store plain-text passwords in the database.

Use password hashing.

Add:

* Login protection
* Session management
* Admin-only sensitive actions
* Password change
* Logout
* Delete confirmation
* Audit log

Sensitive operations such as deleting sales, changing stock, deleting customers, or restoring backup should require admin permission.

---

# 35. Settings

Create Settings module.

Settings:

* Shop Name
* Shop Address
* Phone
* Logo
* Currency
* Language
* Urdu/English
* Invoice Format
* Tax Settings
* Discount Settings
* Backup Settings
* Printer Settings
* Database Settings
* Admin Password

Default currency:

**PKR / Rs.**

---

# 36. Database Safety

Use database transactions for important operations.

For example, completing a POS sale must update:

1. Sale
2. Sale Items
3. Product Stock
4. Customer Ledger
5. Cash Transaction

as one transaction.

If something fails, rollback the entire operation.

Never create a sale where stock or ledger becomes inconsistent.

---

# 37. Product Card UX Example

Product cards should look like:

**[Product Image]**

### Pepsi 1.5L

Rs. 180

Stock: 25

[Add]

Clicking the card or Add button should immediately add it to the POS cart.

If the same product is selected again:

**Quantity automatically increases.**

Example:

First click:

Pepsi × 1

Second click:

Pepsi × 2

Third click:

Pepsi × 3

---

# 38. Customer Selection in POS

At the top of POS:

**Customer: Walk-in Customer**

Allow changing customer.

Search customer by:

* Name
* Phone

If the customer has an existing Khata balance, show:

**Previous Balance: Rs. 4,500**

When a credit sale is completed, automatically add the new amount to the customer's ledger.

---

# 39. Dashboard Example

Dashboard:

**Good Evening 👋**

Today's Sales
Rs. 35,500

Today's Profit
Rs. 8,250

Receivable
Rs. 125,000

Payable
Rs. 75,000

Low Stock
12 Products

Today's Expenses
Rs. 3,500

Current Cash
Rs. 52,000

---

# 40. Architecture

Use a clean and maintainable architecture.

Separate:

* UI
* Business Logic
* Database
* Reports
* Authentication
* Localization
* Printing
* Excel Export
* Backup/Restore

Use proper models, services and repositories.

Do not put all application logic inside UI forms.

---

# 41. Error Handling

The application must never crash because of normal user mistakes.

Show friendly messages such as:

**English:**

"Please enter product quantity."

**Urdu:**

"براہ کرم پروڈکٹ کی مقدار درج کریں۔"

For database errors, show a user-friendly message and log the technical error separately.

---

# 42. Performance

The software must remain fast with:

* 10,000+ products
* 100,000+ sales
* Large customer ledgers
* Large stock history

Use database indexes for:

* Barcode
* Product Name
* SKU
* Customer Phone
* Invoice Number
* Sale Date

Do not load thousands of records unnecessarily into the UI.

---

# 43. Important Business Logic

Implement all calculations correctly.

### Sale

Subtotal = Sum(Product Price × Quantity)

Discount Percentage:

Discount = Subtotal × Discount% / 100

Final Total:

Final Total = Subtotal - Discount

Remaining:

Remaining = Final Total - Paid Amount

### Customer Balance

New Balance = Previous Balance + Credit Sale - Payment

### Stock

New Stock = Previous Stock + Purchase - Sale + Sale Return - Purchase Return ± Adjustment

### Profit

Gross Profit = Sale Revenue - Cost of Sold Products

Net Profit = Gross Profit - Expenses

All calculations must be tested carefully.

---

# 44. User Experience Rule

The most important rule:

**The shopkeeper should be able to complete a normal sale in only a few clicks.**

Ideal flow:

1. Open POS
2. Select customer
3. Click product cards
4. Change quantity if needed
5. Add discount if required
6. Enter payment
7. Complete Sale
8. Print receipt

No unnecessary forms.

---

# 45. Final Main Navigation

Use:

**🏠 Home**

**🛒 POS**

**📦 Inventory**

**📒 Khata**

**☰ More**

Inside More:

* Customers
* Suppliers
* Purchases
* Sales History
* Expenses
* Reports
* Daily Closing
* Backup
* Settings
* Users
* Logout

---

# 46. Development Quality

Do not create a demo-only application.

Build this as a real production-ready shop management system.

Requirements:

* No fake buttons
* No placeholder functionality
* No broken navigation
* No dummy reports
* No hard-coded business calculations
* No internet dependency
* No Firebase dependency unless explicitly required
* Proper database persistence
* Proper validation
* Proper error handling
* Proper backup
* Proper reporting
* Proper Urdu RTL support

Every button must perform its actual function.

---

# 47. Testing

Create test cases for:

* Login
* Add Product
* Edit Product
* Delete Product
* Stock
* Purchase
* POS Sale
* Discount Percentage
* Discount Amount
* Cash Sale
* Credit Sale
* Partial Payment
* Customer Khata
* Customer Payment
* Supplier Ledger
* Sales Return
* Purchase Return
* Expenses
* Profit
* Daily Closing
* Excel Export
* Crystal Reports
* Backup
* Restore
* Urdu Mode
* English Mode
* RTL
* Large Database

Test all calculations and database transactions before considering the system complete.

---

# 48. Final Goal

The final application should feel like a professional Pakistani shop POS + inventory + traditional **Khata system**, but with a modern digital interface.

The most important features are:

**Inventory + POS + Product Cards + Discounts + Customer Khata + Supplier Ledger + Stock + Reports + Crystal Reports + Excel Export + Urdu RTL + English + Offline SQLite Database + Backup/Restore.**

Application branding everywhere should use:

# NHi Inventory Management System

Make the interface modern, clean, fast, simple and highly user-friendly.
# NHi Inventory Management System — Company Information & Login System

Add the following features to the existing **NHi Inventory Management System**.

## 1. Company / Shop Information

Create a centralized **Company Profile / Shop Information** module.

The following information must be stored in the offline database:

* Company / Shop Name
* Company Logo
* Phone Number
* WhatsApp Number
* Address
* City
* Email
* Website (optional)
* NTN / Tax Number (optional)
* Currency
* Invoice Footer
* Additional Notes

Example:

**NHi General Store**
Phone: 03XX-XXXXXXX
Address: Main Bazar, Alpurai, Shangla

---

## 2. Use Company Information Everywhere

The saved company information must automatically appear wherever appropriate.

For example:

### POS Sale

At the top of the POS invoice/receipt:

**Company Logo**
**Company Name**
Phone Number
Address

Then:

* Invoice Number
* Date
* Customer
* Products
* Quantity
* Price
* Discount
* Total
* Paid
* Remaining

### Sales Invoice

Company information must automatically appear on every invoice.

### POS Receipt

Company information must automatically appear on every printed receipt.

### Customer Khata

Customer ledger/Khata printout should contain:

**Company Name**
**Phone**
**Address**

### Supplier Ledger

Supplier reports should also contain the company information.

### Crystal Reports

All Crystal Reports must automatically retrieve the current company information from the database.

Use it in:

* POS Receipt
* Sales Invoice
* Customer Khata
* Supplier Ledger
* Sales Reports
* Purchase Reports
* Stock Reports
* Profit Reports
* Daily Closing
* Payment Receipts

### Excel Export

Where appropriate, exported Excel reports should include company name, phone and address at the top of the report.

---

# 3. Admin-Only Company Information Editing

Only the **Admin** can change company information.

Normal users must NOT be able to:

* Change company name
* Change logo
* Change phone
* Change address
* Change email
* Change tax information
* Change invoice footer
* Change other company settings

When a normal user opens Company Profile:

Show the information as **Read Only**.

Only Admin should see:

**Edit Company Information**

button.

---

# 4. Admin Login

The application already has an Admin login system.

Keep the default Admin account:

**Username:** admin
**Password:** admin

Do NOT create another duplicate admin account.

The existing Admin account must have full system permissions.

Admin can:

* Manage company information
* Manage products
* Manage inventory
* Manage customers
* Manage suppliers
* Manage purchases
* Manage sales
* Manage Khata
* Manage reports
* Manage users
* Change passwords
* Backup database
* Restore database
* Change application settings
* Edit/delete sensitive records
* Manage permissions

---

# 5. Default User Login

Create/keep a normal User account:

**Username:** user
**Password:** user

This account must have normal shop-operation permissions.

The User should be able to:

* Open Dashboard
* Use POS
* Add products to POS
* Create sales
* Apply allowed discounts
* Select customers
* Create credit/Khata sales
* Receive customer payments
* View customer Khata
* View inventory
* Add/manage sales according to permissions
* Print receipts
* View permitted reports

But the User must NOT be able to change important system/company settings.

---

# 6. User Restrictions

Normal User must NOT be allowed to:

* Change Company Name
* Change Company Logo
* Change Company Phone
* Change Company Address
* Change tax information
* Change database settings
* Restore database
* Delete the Admin
* Change Admin credentials
* Change critical system settings

For sensitive actions, display:

**English:**
"Admin permission required."

**Urdu:**
"اس کارروائی کے لیے ایڈمن کی اجازت ضروری ہے۔"

---

# 7. Role-Based Access

Use two roles:

### ADMIN

Username:
`admin`

Password:
`admin`

Role:
`ADMIN`

Full access.

### USER

Username:
`user`

Password:
`user`

Role:
`USER`

Limited operational access.

Do not rely only on hiding buttons.

The backend/business logic/database layer must also verify permissions.

---

# 8. Company Information Flow

Company information should have a single source of truth.

Example database record:

CompanySettings:

* companyName
* logo
* phone
* whatsapp
* address
* city
* email
* website
* taxNumber
* currency
* invoiceFooter
* updatedAt
* updatedBy

Whenever an invoice/report/receipt is generated, retrieve the latest CompanySettings record.

Do NOT hard-code the company name, phone or address inside individual screens or reports.

---

# 9. Automatic Updates

If Admin changes:

**Company Name**

from:

`NHi General Store`

to:

`NHi Super Store`

then all NEW:

* POS receipts
* Sales invoices
* Crystal Reports
* Customer Khata reports
* Supplier reports
* Daily reports
* Excel exports

must automatically use:

`NHi Super Store`

The developer must not need to manually change every report.

---

# 10. Company Logo

Admin can upload/change the company logo.

The logo should automatically appear on:

* Login screen if enabled
* Dashboard
* POS receipt
* Sales invoice
* Crystal Reports
* Printable Khata
* Reports

Validate the image before saving.

---

# 11. Company Settings UI

Create:

**Settings → Company Profile**

Admin sees:

* Company Name
* Logo
* Phone
* WhatsApp
* Address
* City
* Email
* Website
* Tax Number
* Currency
* Invoice Footer

Buttons:

**Save Changes**

**Cancel**

Normal User sees:

**Company Information**

with all fields read-only.

No Edit button should be available to the User.

---

# 12. Login Demo Accounts

For initial installation/testing, the application must contain:

### Admin

Username: `admin`
Password: `admin`

### User

Username: `user`
Password: `user`

Passwords must be securely hashed in the database.

These are default development credentials and should be changeable from the Admin settings.

---

# 13. Important Security Requirement

Even if the User manually tries to access an Admin-only URL/page or API action, the application must reject the request.

Example:

User attempts:

`CompanySettings/Edit`

System response:

**English:**
"Access Denied. Admin permission required."

**Urdu:**
"رسائی مسترد۔ صرف ایڈمن یہ کارروائی کر سکتا ہے۔"

Use proper role-based authorization at the application/business-logic level.

---

# 14. Final Requirement

The complete system must have:

**ADMIN**

* `admin / admin`
* Full access

**USER**

* `user / user`
* Normal shop operation access

And a centralized:

**Company Profile**

where only Admin can modify company information.

All POS sales, invoices, receipts, Khata, ledgers, Crystal Reports and other relevant records/reports must automatically use the latest saved company information.
