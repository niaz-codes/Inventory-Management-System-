using System.IO;

namespace NHi.Inventory.Services;

public static class BackupService
{
    public static string GetDatabasePath()
    {
        return System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NHiInventory", "inventory.db");
    }

    public static bool BackupDatabase(string backupPath)
    {
        try
        {
            var dbPath = GetDatabasePath();
            var backupDir = System.IO.Path.GetDirectoryName(backupPath);
            if (backupDir != null) System.IO.Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fullBackupPath = System.IO.Path.Combine(backupDir ?? backupPath,
                $"NHiBackup_{timestamp}.db");
            System.IO.File.Copy(dbPath, fullBackupPath, true);
            return true;
        }
        catch { return false; }
    }

    public static bool RestoreDatabase(string backupFilePath)
    {
        try
        {
            var dbPath = GetDatabasePath();
            var tempPath = dbPath + ".backup";
            if (System.IO.File.Exists(dbPath))
                System.IO.File.Copy(dbPath, tempPath, true);

            try
            {
                System.IO.File.Copy(backupFilePath, dbPath, true);
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
                return true;
            }
            catch
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Copy(tempPath, dbPath, true);
                return false;
            }
        }
        catch { return false; }
    }

    public static List<string> GetBackupFiles(string backupDir)
    {
        if (!System.IO.Directory.Exists(backupDir)) return new List<string>();
        return System.IO.Directory.GetFiles(backupDir, "NHiBackup_*.db").OrderByDescending(f => f).ToList();
    }
}
