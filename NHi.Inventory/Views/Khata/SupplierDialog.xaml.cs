using System.Windows;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class SupplierDialog : Window
{
    private readonly Supplier? _existingSupplier;

    public SupplierDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
    }

    public SupplierDialog(Supplier supplier) : this()
    {
        _existingSupplier = supplier;
        txtTitle.Text = LanguageManager.GetString("EditSupplier");
        txtName.Text = supplier.Name;
        txtCompanyName.Text = supplier.CompanyName;
        txtContact.Text = supplier.ContactPerson;
        txtPhone.Text = supplier.Phone;
        txtWhatsApp.Text = supplier.WhatsAppNumber;
        txtAddress.Text = supplier.Address;
        txtOpeningBalance.Text = supplier.OpeningBalance.ToString("0.##");
        chkActive.IsChecked = supplier.IsActive;

        // Opening balance is only ever applied once, at creation — editing it later would
        // silently diverge from the ledger's "Opening Balance" row, so it's locked here
        // (same reasoning as CustomerDialog).
        txtOpeningBalance.IsEnabled = false;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var supplier = _existingSupplier ?? new Supplier();
        supplier.Name = txtName.Text.Trim();
        supplier.CompanyName = txtCompanyName.Text.Trim();
        supplier.ContactPerson = txtContact.Text.Trim();
        supplier.Phone = txtPhone.Text.Trim();
        supplier.WhatsAppNumber = txtWhatsApp.Text.Trim();
        supplier.Address = txtAddress.Text.Trim();
        supplier.IsActive = chkActive.IsChecked == true;

        if (_existingSupplier == null)
            supplier.OpeningBalance = decimal.TryParse(txtOpeningBalance.Text, out var opening) ? opening : 0;

        new SupplierService().Save(supplier);
        this.DialogResult = true;
        this.Close();
    }
}
