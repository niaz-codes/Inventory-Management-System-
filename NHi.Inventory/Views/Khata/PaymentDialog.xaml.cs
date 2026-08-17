using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class PaymentDialog : Window
{
    private readonly int _entityId;
    private readonly string _type;

    public PaymentDialog(int entityId, string type)
    {
        InitializeComponent();
        _entityId = entityId;
        _type = type;

        if (type == "customer")
        {
            txtTitle.Text = LanguageManager.GetString("ReceivePayment");
            var customer = new CustomerService().GetById(entityId);
            txtBalance.Text = $"Current Balance: Rs. {customer?.CurrentBalance:N0}";
        }
        else
        {
            txtTitle.Text = LanguageManager.GetString("MakePayment");
            var supplier = new SupplierService().GetById(entityId);
            txtBalance.Text = $"Current Balance: Rs. {supplier?.CurrentBalance:N0}";
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("Please enter a valid amount", "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_type == "customer")
            new CustomerService().ReceivePayment(_entityId, amount, txtNotes.Text.Trim());
        else
            new SupplierService().MakePayment(_entityId, amount, txtNotes.Text.Trim());

        MessageBox.Show(LanguageManager.GetString("PaymentReceived"), "", MessageBoxButton.OK, MessageBoxImage.Information);
        this.DialogResult = true;
        this.Close();
    }
}
