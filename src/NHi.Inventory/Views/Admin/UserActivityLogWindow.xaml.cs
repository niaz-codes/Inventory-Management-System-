using System.Windows;
using NHi.Inventory.Data;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Admin;

public partial class UserActivityLogWindow : Window
{
    public UserActivityLogWindow(int userId, string username)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        txtTitle.Text = $"{LanguageManager.GetString("ActivityLog")} — {username}";

        using var db = new AppDbContext();
        dg.ItemsSource = db.AuditLogs.Where(a => a.UserId == userId).OrderByDescending(a => a.Timestamp).Take(300).ToList();
    }
}
