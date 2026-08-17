using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.POS;

public partial class HeldSalesDialog : Window
{
    private readonly List<HeldSale> _heldSales;

    public HeldSale? SelectedHeldSale { get; private set; }
    public bool DeletedAny { get; private set; }

    public HeldSalesDialog(List<HeldSale> heldSales)
    {
        InitializeComponent();
        _heldSales = heldSales;
        lv.ItemsSource = _heldSales;
    }

    private void BtnResume_Click(object sender, RoutedEventArgs e)
    {
        if (lv.SelectedItem is HeldSale held)
        {
            SelectedHeldSale = held;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lv.SelectedItem is HeldSale held)
        {
            _heldSales.Remove(held);
            lv.ItemsSource = null;
            lv.ItemsSource = _heldSales;
            DeletedAny = true;
        }
    }
}
