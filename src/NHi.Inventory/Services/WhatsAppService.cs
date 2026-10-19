using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Views;

namespace NHi.Inventory.Services;

public interface IWhatsAppService
{
    /// <summary>Converts a locally-stored Pakistani number (e.g. "03450050667") into the
    /// wa.me international format (e.g. "923450050667"). Returns null if the number isn't a
    /// valid Pakistani mobile number.</summary>
    string? NormalizePakistanNumber(string phoneNumber);
    string GenerateKhataReminderMessage(Customer customer, KhataSummary khata);
    string GenerateInvoiceMessage(Customer customer, Sale invoice);
    string GenerateSupplierKhataReminderMessage(Supplier supplier, SupplierKhataSummary khata);
    void OpenWhatsApp(string phoneNumber, string message);
}

/// <summary>
/// Opens a prefilled WhatsApp chat in an embedded WebView2 browser window inside the POS
/// itself, using the official wa.me Click-to-Chat link — https://wa.me/{number}?text={message}.
/// There is no WhatsApp Cloud/Business API involved, no credentials, and no network call made
/// directly by this app: WhatsApp Web (loaded inside the app's own WebView2 control, never an
/// external Chrome/Edge window) does all the work, and the employee still has to press Send
/// inside that page themselves. Every attempt is therefore logged as "Opened", never "Sent" —
/// this app has no way to know whether Send was pressed.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    // Pakistani mobile numbers, once normalized, are always 92 + 3 + 9 digits = 12 digits total.
    private static readonly Regex PakMobileRegex = new(@"^923\d{9}$", RegexOptions.Compiled);

    // Used only to render month names in Urdu (e.g. "اگست") for dates inside messages —
    // never assigned to Thread.CurrentCulture, so it can't affect number/date formatting
    // anywhere else in the app.
    private static readonly CultureInfo UrduCulture = new("ur-PK");

    // Guards against a double-click firing two overlapping WhatsApp opens for the same
    // customer/reference — the UI also disables the triggering button while processing, but
    // this is the authoritative, un-bypassable guard since it lives at the service layer.
    private static readonly HashSet<string> _inFlight = new();

    // One embedded WhatsApp window per normalized phone number at a time — a repeated click
    // (Reminder or Invoice, doesn't matter which) while a window for that number is still open
    // just refocuses it instead of opening a second overlapping window.
    private static readonly Dictionary<string, WhatsAppWebViewWindow> _openWindows = new();

    public string? NormalizePakistanNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;

        if (digits.StartsWith("0092")) digits = digits[2..];                          // 0092... -> 92...
        else if (digits.StartsWith("0")) digits = "92" + digits[1..];                 // 03XXXXXXXXX -> 923XXXXXXXXX
        else if (!digits.StartsWith("92") && digits.Length == 10 && digits[0] == '3') digits = "92" + digits; // 3XXXXXXXXX -> 923XXXXXXXXX

        return PakMobileRegex.IsMatch(digits) ? digits : null;
    }

    public string GenerateKhataReminderMessage(Customer customer, KhataSummary khata)
    {
        var shopName = GetShopName();
        var lastTransaction = khata.LastTransactionDate.HasValue
            ? khata.LastTransactionDate.Value.ToString("d MMMM yyyy", UrduCulture)
            : "دستیاب نہیں";

        return
            $"السلام علیکم {customer.Name} صاحب،\n\n" +
            "آپ کے کھاتے کی موجودہ تفصیل درج ذیل ہے۔\n\n" +
            "*کھاتہ کی تفصیل:*\n" +
            $"• کل رقم: {khata.TotalAmount:N0} روپے\n" +
            $"• جمع شدہ رقم: {khata.PaidAmount:N0} روپے\n" +
            $"• بقایا رقم: {khata.RemainingBalance:N0} روپے\n" +
            $"• آخری لین دین: {lastTransaction}\n\n" +
            "براہِ کرم بقایا رقم اپنی سہولت کے مطابق ادا کر دیں۔\n\n" +
            "شکریہ۔\n" +
            $"*{shopName}*";
    }

    public string GenerateSupplierKhataReminderMessage(Supplier supplier, SupplierKhataSummary khata)
    {
        var shopName = GetShopName();
        var lastTransaction = khata.LastTransactionDate.HasValue
            ? khata.LastTransactionDate.Value.ToString("d MMMM yyyy", UrduCulture)
            : "دستیاب نہیں";

        return
            $"السلام علیکم {supplier.Name} صاحب،\n\n" +
            "آپ کے کھاتے کی موجودہ تفصیل درج ذیل ہے۔\n\n" +
            "*کھاتہ کی تفصیل:*\n" +
            $"• کل خریداری: {khata.TotalPurchases:N0} روپے\n" +
            $"• ادا شدہ رقم: {khata.TotalPaid:N0} روپے\n" +
            $"• باقی ادائیگی: {khata.RemainingPayable:N0} روپے\n" +
            $"• آخری لین دین: {lastTransaction}\n\n" +
            "براہِ کرم باقی رقم جلد از جلد ادا کر دیں۔\n\n" +
            "شکریہ۔\n" +
            $"*{shopName}*";
    }

    public string GenerateInvoiceMessage(Customer customer, Sale invoice)
    {
        var shopName = GetShopName();

        return
            $"السلام علیکم {customer.Name} صاحب،\n\n" +
            "آپ کی خریداری کامیابی سے مکمل ہو گئی ہے۔\n\n" +
            "*انوائس کی تفصیل:*\n" +
            $"• انوائس نمبر: {invoice.InvoiceNumber}\n" +
            $"• کل خریداری: {invoice.GrandTotal:N0} روپے\n" +
            $"• جمع شدہ رقم: {invoice.PaidAmount:N0} روپے\n" +
            $"• بقایا رقم: {invoice.RemainingAmount:N0} روپے\n\n" +
            "آپ کا شکریہ۔\n" +
            $"*{shopName}*";
    }

    /// <summary>Opens the wa.me link inside an embedded WebView2 window instead of the
    /// system's default browser — no Process.Start, no UseShellExecute, no external Chrome/Edge.
    /// If a window for this number is already open, it's refocused instead of opening a
    /// duplicate.</summary>
    public void OpenWhatsApp(string phoneNumber, string message)
    {
        var url = $"https://wa.me/{phoneNumber}?text={Uri.EscapeDataString(message)}";

        if (_openWindows.TryGetValue(phoneNumber, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? Application.Current.MainWindow;

        var window = new WhatsAppWebViewWindow(url) { Owner = owner };
        _openWindows[phoneNumber] = window;
        window.Closed += (_, _) => _openWindows.Remove(phoneNumber);
        window.Show();
    }

    private static string GetShopName()
    {
        using var db = new AppDbContext();
        return db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId)?.Name ?? "NHI Inventory Management";
    }

    // --- Orchestration: validate -> normalize -> generate -> open -> log, producing the exact
    // user-facing error strings the POS/Khata screens show without crashing the app. ---

    public WhatsAppActionResult SendKhataReminder(Customer customer)
    {
        var khata = new CustomerService().GetKhataSummary(customer.Id);
        return Execute(customer, () => GenerateKhataReminderMessage(customer, khata), "Reminder", "Khata", customer.Id);
    }

    public WhatsAppActionResult SendInvoiceMessage(Customer customer, Sale sale)
    {
        return Execute(customer, () => GenerateInvoiceMessage(customer, sale), "Invoice", "Sale", sale.Id);
    }

    public WhatsAppActionResult SendSupplierKhataReminder(Supplier supplier)
    {
        var khata = new SupplierService().GetKhataSummary(supplier.Id);
        return ExecuteSupplier(supplier, () => GenerateSupplierKhataReminderMessage(supplier, khata), "Reminder", "SupplierKhata", supplier.Id);
    }

    private WhatsAppActionResult Execute(Customer customer, Func<string> buildMessage, string messageType, string referenceType, int referenceId)
    {
        var key = $"{messageType}:{customer.Id}:{referenceId}";
        lock (_inFlight) { if (!_inFlight.Add(key)) return WhatsAppActionResult.Fail("Still processing the previous request."); }

        try
        {
            var rawNumber = customer.WhatsAppNumber ?? customer.Phone;
            if (string.IsNullOrWhiteSpace(rawNumber))
                return WhatsAppActionResult.Fail("This customer does not have a mobile number. Please add a WhatsApp number first.");

            var normalized = NormalizePakistanNumber(rawNumber);
            if (normalized == null)
                return WhatsAppActionResult.Fail("Invalid Pakistani mobile number.");

            string message;
            try { message = buildMessage(); }
            catch { return WhatsAppActionResult.Fail("Unable to prepare WhatsApp message. Please try again."); }

            try
            {
                OpenWhatsApp(normalized, message);
            }
            catch
            {
                LogMessage(customer.Id, messageType, referenceType, referenceId, message, "Failed");
                return WhatsAppActionResult.Fail("Unable to prepare WhatsApp message. Please try again.");
            }

            LogMessage(customer.Id, messageType, referenceType, referenceId, message, "Opened");
            return WhatsAppActionResult.Ok();
        }
        finally { lock (_inFlight) { _inFlight.Remove(key); } }
    }

    /// <summary>Same orchestration as Execute above, for a Supplier instead of a Customer — kept
    /// as a parallel method (rather than a generic one) to match this file's existing style, and
    /// prefixed in the in-flight/lock key so a customer and a supplier that happen to share the
    /// same numeric Id can never collide.</summary>
    private WhatsAppActionResult ExecuteSupplier(Supplier supplier, Func<string> buildMessage, string messageType, string referenceType, int referenceId)
    {
        var key = $"Supplier:{messageType}:{supplier.Id}:{referenceId}";
        lock (_inFlight) { if (!_inFlight.Add(key)) return WhatsAppActionResult.Fail("Still processing the previous request."); }

        try
        {
            var rawNumber = supplier.WhatsAppNumber ?? supplier.Phone;
            if (string.IsNullOrWhiteSpace(rawNumber))
                return WhatsAppActionResult.Fail("This supplier does not have a mobile number. Please add a WhatsApp number first.");

            var normalized = NormalizePakistanNumber(rawNumber);
            if (normalized == null)
                return WhatsAppActionResult.Fail("Invalid Pakistani mobile number.");

            string message;
            try { message = buildMessage(); }
            catch { return WhatsAppActionResult.Fail("Unable to prepare WhatsApp message. Please try again."); }

            try
            {
                OpenWhatsApp(normalized, message);
            }
            catch
            {
                LogSupplierMessage(supplier.Id, messageType, referenceType, referenceId, message, "Failed");
                return WhatsAppActionResult.Fail("Unable to prepare WhatsApp message. Please try again.");
            }

            LogSupplierMessage(supplier.Id, messageType, referenceType, referenceId, message, "Opened");
            return WhatsAppActionResult.Ok();
        }
        finally { lock (_inFlight) { _inFlight.Remove(key); } }
    }

    private static void LogMessage(int customerId, string messageType, string referenceType, int referenceId, string message, string status)
    {
        using var db = new AppDbContext();
        db.MessageLogs.Add(new MessageLog
        {
            ShopId = AuthService.CurrentShopId ?? 1,
            CustomerId = customerId,
            MessageType = messageType,
            Language = "Urdu",
            Channel = "WhatsApp",
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Status = status,
            MessageBody = message
        });
        db.SaveChanges();
    }

    private static void LogSupplierMessage(int supplierId, string messageType, string referenceType, int referenceId, string message, string status)
    {
        using var db = new AppDbContext();
        db.MessageLogs.Add(new MessageLog
        {
            ShopId = AuthService.CurrentShopId ?? 1,
            SupplierId = supplierId,
            MessageType = messageType,
            Language = "Urdu",
            Channel = "WhatsApp",
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Status = status,
            MessageBody = message
        });
        db.SaveChanges();
    }

    public List<MessageLog> GetHistory(int customerId) =>
        new AppDbContext().MessageLogs.Where(l => l.CustomerId == customerId).OrderByDescending(l => l.CreatedAt).ToList();

    /// <summary>Re-opens WhatsApp with the exact message body of a previously logged attempt —
    /// used by the Retry action in Message History for a row that previously failed to open.</summary>
    public WhatsAppActionResult Retry(int logId)
    {
        using var db = new AppDbContext();
        var log = db.MessageLogs.Include(l => l.Customer).FirstOrDefault(l => l.Id == logId);
        if (log?.Customer == null) return WhatsAppActionResult.Fail("Unable to prepare WhatsApp message. Please try again.");

        var rawNumber = log.Customer.WhatsAppNumber ?? log.Customer.Phone;
        var normalized = rawNumber != null ? NormalizePakistanNumber(rawNumber) : null;
        if (normalized == null)
        {
            log.Status = "Failed";
            db.SaveChanges();
            return WhatsAppActionResult.Fail("Invalid Pakistani mobile number.");
        }

        try
        {
            OpenWhatsApp(normalized, log.MessageBody);
            log.Status = "Opened";
        }
        catch
        {
            log.Status = "Failed";
        }
        log.CreatedAt = DateTime.Now;
        db.SaveChanges();

        return log.Status == "Opened" ? WhatsAppActionResult.Ok() : WhatsAppActionResult.Fail("Unable to prepare WhatsApp message. Please try again.");
    }
}

public class WhatsAppActionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public static WhatsAppActionResult Ok() => new() { Success = true };
    public static WhatsAppActionResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}
