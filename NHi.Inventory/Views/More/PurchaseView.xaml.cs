using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class PurchaseView : UserControl
{
    private readonly PurchaseService _service = new();
    public PurchaseView() { InitializeComponent(); Loaded += (s, e) => LoadData(); }
    private void LoadData() { dg.ItemsSource = _service.GetPurchases(); }
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PurchaseDialog();
        if (dialog.ShowDialog() == true) LoadData();
    }
}
