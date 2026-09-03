using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Admin;

/// <summary>Main-Admin-only host window for BackupRestoreView. The database file is shared
/// across every shop, so backup/restore is a whole-system action reserved for the Main
/// Admin — never reachable from a shop's own dashboard/More menu.</summary>
public partial class BackupRestoreWindow : Window
{
    public BackupRestoreWindow()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
    }
}
