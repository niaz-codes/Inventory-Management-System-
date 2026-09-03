using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.POS;

public enum BarcodeNotFoundChoice { Cancel, TryAgain, AddNew }

public partial class BarcodeNotFoundDialog : Window
{
    public BarcodeNotFoundChoice Choice { get; private set; } = BarcodeNotFoundChoice.Cancel;

    public BarcodeNotFoundDialog(string barcode)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        txtBarcode.Text = barcode;
    }

    private void BtnAddNew_Click(object sender, RoutedEventArgs e) { Choice = BarcodeNotFoundChoice.AddNew; Close(); }
    private void BtnTryAgain_Click(object sender, RoutedEventArgs e) { Choice = BarcodeNotFoundChoice.TryAgain; Close(); }
    private void BtnCancel_Click(object sender, RoutedEventArgs e) { Choice = BarcodeNotFoundChoice.Cancel; Close(); }
}
