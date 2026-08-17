using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class CustomerListView : UserControl
{
    private readonly CustomerService _service = new();
    public CustomerListView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData() { dg.ItemsSource = string.IsNullOrWhiteSpace(txtSearch?.Text) ? _service.GetAll() : _service.Search(txtSearch.Text); }
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => LoadData();
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.POS.CustomerDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
            if (MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _service.Delete(id); LoadData(); }
    }
}
