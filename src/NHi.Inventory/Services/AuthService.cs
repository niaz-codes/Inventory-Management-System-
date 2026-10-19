using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public static class AuthService
{
    public static User? CurrentUser { get; private set; }

    /// <summary>The shop the current session is scoped to. Null while logged out, and
    /// null for a logged-in Main Admin (who isn't tied to any single shop). EF Core's
    /// global query filters in AppDbContext read this directly, so every query against a
    /// shop-scoped entity is automatically restricted to this shop with no manual .Where() needed.</summary>
    public static int? CurrentShopId { get; private set; }

    public static bool Login(string username, string password)
    {
        using var db = new AppDbContext();
        var user = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Username == username && u.IsActive && u.Status == "Approved");
        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.LastLogin = DateTime.Now;
            db.SaveChanges();
            CurrentUser = user;
            CurrentShopId = user.ShopId;
            AuditLogService.Log("LOGIN_SUCCESS", "User", user.Id, $"User '{username}' logged in");
            return true;
        }
        AuditLogService.Log("LOGIN_FAILED", "User", null, $"Failed login attempt for username '{username}'");
        return false;
    }

    public static void Logout()
    {
        AuditLogService.Log("LOGOUT", "User", CurrentUser?.Id, $"User '{CurrentUser?.Username}' logged out");
        CurrentUser = null;
        CurrentShopId = null;
    }

    public static bool IsAdmin() => CurrentUser?.Role is "ADMIN" or "MAIN_ADMIN";
    public static bool IsMainAdmin() => CurrentUser?.Role == "MAIN_ADMIN";

    /// <summary>
    /// For startup-time seeding only (SeedData/TestDataSeeder run before any login), so their
    /// writes stamp a real ShopId and their "already seeded?" .Any() checks see existing rows
    /// through the query filter instead of being hidden behind a still-null CurrentShopId.
    /// A real Login() call always overwrites this afterwards, so nothing is left stuck.
    /// </summary>
    public static void SetShopContextForSeeding(int shopId) => CurrentShopId = shopId;

    public static bool HasPermission(string action)
    {
        if (CurrentUser == null) return false;
        if (CurrentUser.Role == "ADMIN") return true;
        var adminOnly = new[] { "ManageCompany", "ManageUsers", "Backup", "Restore", "DeleteSale", "VoidSale", "SystemSettings", "ManagePermissions", "OverrideCreditLimit", "KhataAdjustment" };
        return !adminOnly.Contains(action);
    }

    /// <summary>Minimum acceptable password length for a self-service change.</summary>
    public const int MinPasswordLength = 6;

    /// <summary>Rough 0-4 strength score for the live meter in the UI.
    /// 0 empty · 1 very weak · 2 weak · 3 fair · 4 strong.</summary>
    public static int EvaluatePasswordStrength(string? pw)
    {
        if (string.IsNullOrEmpty(pw)) return 0;
        int score = 0;
        if (pw.Length >= 6) score++;
        if (pw.Length >= 10) score++;
        if (pw.Any(char.IsLower) && pw.Any(char.IsUpper)) score++;
        if (pw.Any(char.IsDigit)) score++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) score++;
        return Math.Min(score, 4);
    }

    /// <summary>Changes the logged-in user's own password. Verifies the current password,
    /// rejects a too-short or unchanged new password, and re-hashes with BCrypt. Returns an
    /// error key (localised by the caller) on failure.</summary>
    public static (bool Ok, string? ErrorKey) ChangePassword(string currentPassword, string newPassword)
    {
        if (CurrentUser == null) return (false, "SessionExpired");
        if ((newPassword?.Length ?? 0) < MinPasswordLength) return (false, "PasswordTooShort");

        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == CurrentUser.Id);
        if (user == null) return (false, "SessionExpired");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return (false, "CurrentPasswordWrong");
        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash)) return (false, "PasswordSameAsOld");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        db.SaveChanges();
        AuditLogService.Log("CHANGE_PASSWORD", "User", user.Id, "User changed their own password");
        return (true, null);
    }

    /// <summary>Changes the logged-in user's own username. Requires the current password,
    /// enforces a minimum length, and guarantees uniqueness across ALL users (case-insensitive,
    /// query filters ignored so a name taken in another shop still blocks). Keeps the live
    /// session's username in sync on success.</summary>
    public static (bool Ok, string? ErrorKey) ChangeUsername(string currentPassword, string newUsername)
    {
        newUsername = (newUsername ?? "").Trim();
        if (CurrentUser == null) return (false, "SessionExpired");
        if (newUsername.Length < 3) return (false, "UsernameTooShort");
        if (newUsername.Any(char.IsWhiteSpace)) return (false, "UsernameHasSpaces");

        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == CurrentUser.Id);
        if (user == null) return (false, "SessionExpired");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return (false, "CurrentPasswordWrong");
        if (string.Equals(user.Username, newUsername, StringComparison.OrdinalIgnoreCase)) return (false, "UsernameSame");

        var taken = db.Users.IgnoreQueryFilters().AsEnumerable()
            .Any(u => u.Id != user.Id && string.Equals(u.Username, newUsername, StringComparison.OrdinalIgnoreCase));
        if (taken) return (false, "UsernameTaken");

        var old = user.Username;
        user.Username = newUsername;
        db.SaveChanges();
        CurrentUser.Username = newUsername;
        AuditLogService.Log("CHANGE_USERNAME", "User", user.Id, $"Username changed from '{old}' to '{newUsername}'");
        return (true, null);
    }

    public static bool ChangeUserPassword(int userId, string newPassword)
    {
        if (!IsAdmin()) return false;
        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        if (user != null)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            db.SaveChanges();
            AuditLogService.Log("RESET_USER_PASSWORD", "User", user.Id, $"Admin reset password for '{user.Username}'");
            return true;
        }
        return false;
    }
}
