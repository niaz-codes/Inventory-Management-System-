using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.POS;

public partial class CustomerDialog : Window
{
    private readonly Customer? _editing;

    /// <summary>The customer that was just created or updated, populated right before the dialog
    /// closes successfully — lets a caller (e.g. POS's Customer Search "+ Add New Customer") pick
    /// up and auto-select the exact saved record without a second DB lookup.</summary>
    public Customer? SavedCustomer { get; private set; }

    public CustomerDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;

        // Wired up here rather than in XAML: XAML's IsChecked="True" on chkWhatsAppSame fires
        // the Checked event during InitializeComponent itself, before later-declared controls
        // (txtWhatsApp, lblWhatsApp) are connected — crashing with a NullReferenceException the
        // instant the dialog is constructed. Wiring after InitializeComponent avoids that, and
        // the manual call syncs the initial UI state that IsChecked="True" would otherwise have driven.
        chkWhatsAppSame.Checked += ChkWhatsAppSame_Changed;
        chkWhatsAppSame.Unchecked += ChkWhatsAppSame_Changed;
        ChkWhatsAppSame_Changed(chkWhatsAppSame, new RoutedEventArgs());
    }

    public CustomerDialog(Customer customer) : this()
    {
        _editing = customer;
        txtTitle.Text = LanguageManager.GetString("EditCustomer");
        txtName.Text = customer.Name;
        txtFatherName.Text = customer.FatherName;
        txtPhone.Text = customer.Phone;
        txtAddress.Text = customer.Address;
        txtEmail.Text = customer.Email;
        txtOpeningBalance.Text = customer.OpeningBalance.ToString("0.##");
        txtCreditLimit.Text = customer.CreditLimit.ToString("0.##");
        txtNotes.Text = customer.Notes;
        chkActive.IsChecked = customer.IsActive;
        chkWhatsAppSame.IsChecked = customer.WhatsAppSameAsPhone;
        // Set unconditionally: when WhatsAppSameAsPhone is already true (the default from the
        // base constructor), the IsChecked assignment above is a no-op and never re-fires
        // ChkWhatsAppSame_Changed, so the mirrored text wouldn't otherwise get synced here.
        txtWhatsApp.Text = customer.WhatsAppSameAsPhone ? customer.Phone : customer.WhatsAppNumber;

        // Opening balance is only ever applied once, at creation — editing it later would
        // silently diverge from the ledger's "Opening Balance" row, so it's locked here.
        txtOpeningBalance.IsEnabled = false;
    }

    /// <summary>Keeps the WhatsApp field mirrored to Phone while "same as phone" is checked, so
    /// the two numbers can never silently drift apart without the user explicitly unchecking it.</summary>
    private void ChkWhatsAppSame_Changed(object sender, RoutedEventArgs e)
    {
        var same = chkWhatsAppSame.IsChecked == true;
        txtWhatsApp.IsEnabled = !same;
        lblWhatsApp.Text = LanguageManager.GetString("WhatsAppNumber");
        if (same) txtWhatsApp.Text = txtPhone.Text;
    }

    private void TxtPhone_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (chkWhatsAppSame.IsChecked == true) txtWhatsApp.Text = txtPhone.Text;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var customer = _editing ?? new Customer();
        customer.Name = txtName.Text.Trim();
        customer.FatherName = txtFatherName.Text.Trim();
        customer.Phone = txtPhone.Text.Trim();
        customer.WhatsAppSameAsPhone = chkWhatsAppSame.IsChecked == true;
        customer.WhatsAppNumber = customer.WhatsAppSameAsPhone ? customer.Phone : txtWhatsApp.Text.Trim();
        customer.Address = txtAddress.Text.Trim();
        customer.Email = txtEmail.Text.Trim();
        customer.CreditLimit = decimal.TryParse(txtCreditLimit.Text, out var limit) ? limit : 0;
        customer.Notes = txtNotes.Text.Trim();
        customer.IsActive = chkActive.IsChecked == true;

        if (_editing == null)
            customer.OpeningBalance = decimal.TryParse(txtOpeningBalance.Text, out var opening) ? opening : 0;

        new CustomerService().Save(customer);
        SavedCustomer = customer;
        this.DialogResult = true;
        this.Close();
    }
}
