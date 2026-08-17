using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public static class AuthService
{
    public static User? CurrentUser { get; private set; }

    public static bool Login(string username, string password)
    {
        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Username == username && u.IsActive);
        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.LastLogin = DateTime.Now;
            db.SaveChanges();
            CurrentUser = user;
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
    }

    public static bool IsAdmin() => CurrentUser?.Role == "ADMIN";

    public static bool HasPermission(string action)
    {
        if (CurrentUser == null) return false;
        if (CurrentUser.Role == "ADMIN") return true;
        var adminOnly = new[] { "ManageCompany", "ManageUsers", "Backup", "Restore", "DeleteSale", "VoidSale", "SystemSettings", "ManagePermissions" };
        return !adminOnly.Contains(action);
    }

    public static bool ChangePassword(string currentPassword, string newPassword)
    {
        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Id == CurrentUser!.Id);
        if (user != null && BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            db.SaveChanges();
            AuditLogService.Log("CHANGE_PASSWORD", "User", user.Id, "User changed their own password");
            return true;
        }
        return false;
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
