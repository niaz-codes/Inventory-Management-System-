using NHi.Inventory.Data;
using NHi.Inventory.Models;

namespace NHi.Inventory.Services;

public static class AuditLogService
{
    public static void Log(string action, string? entityType = null, int? entityId = null, string? details = null)
    {
        try
        {
            using var db = new AppDbContext();
            db.AuditLogs.Add(new AuditLog
            {
                UserId = AuthService.CurrentUser?.Id,
                Username = AuthService.CurrentUser?.Username,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details
            });
            db.SaveChanges();
        }
        catch
        {
            // Audit logging must never block or crash the primary business operation.
        }
    }
}
