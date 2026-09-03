using System.Windows;
using Microsoft.Win32;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

public partial class BackupRestoreView : System.Windows.Controls.UserControl
{
    public BackupRestoreView() { InitializeComponent(); }

    private void BtnBackup(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsMainAdmin())
        {
            MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog { Filter = "Database Files|*.db", FileName = $"NHiBackup_{DateTime.Now:yyyyMMdd_HHmmss}.db" };
        if (dialog.ShowDialog() == true)
        {
            if (BackupService.BackupDatabase(dialog.FileName))
            {
                AuditLogService.Log("BACKUP", "Database", null, $"Backup created at {dialog.FileName}");
                MessageBox.Show(LanguageManager.GetString("BackupSuccess"));
            }
            else MessageBox.Show(LanguageManager.GetString("ErrorOccurred"));
        }
    }

    private void BtnRestore(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsMainAdmin())
        {
            MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(LanguageManager.GetString("RestoreConfirm"), "", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        var dialog = new OpenFileDialog { Filter = "Database Files|*.db" };
        if (dialog.ShowDialog() == true)
        {
            if (BackupService.RestoreDatabase(dialog.FileName))
            {
                AuditLogService.Log("RESTORE", "Database", null, $"Database restored from {dialog.FileName}");
                MessageBox.Show(LanguageManager.GetString("RestoreSuccess"));
                Application.Current.Shutdown();
            }
            else MessageBox.Show(LanguageManager.GetString("ErrorOccurred"));
        }
    }
}
