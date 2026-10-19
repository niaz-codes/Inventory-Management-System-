using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class ExpenseView : UserControl
{
    private readonly ExpenseService _service = new();
    public ExpenseView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData() { dg.ItemsSource = _service.GetAll(); }
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Expenses.xlsx" };
        if (dialog.ShowDialog() == true)
        { ExcelExportService.ExportExpenses(dialog.FileName); MessageBox.Show(LanguageManager.GetString("SavedSuccessfully")); }
    }
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    { var dialog = new ExpenseDialog(); if (dialog.ShowDialog() == true) LoadData(); }
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
            if (MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _service.Delete(id); LoadData(); }
    }
}
