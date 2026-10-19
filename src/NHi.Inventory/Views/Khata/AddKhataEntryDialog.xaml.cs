using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

public partial class AddKhataEntryDialog : Window
{
    private readonly int _customerId;

    public AddKhataEntryDialog(int customerId, string? defaultType = null)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _customerId = customerId;

        var customer = new CustomerService().GetById(customerId);
        txtCustomerName.Text = customer?.Name ?? "";

        rbAdjustment.Visibility = AuthService.HasPermission("KhataAdjustment") ? Visibility.Visible : Visibility.Collapsed;
        if (defaultType == "Payment") rbPayment.IsChecked = true;
        else if (defaultType == "Adjustment" && rbAdjustment.Visibility == Visibility.Visible) rbAdjustment.IsChecked = true;

        txtCreatedBy.Text = $"{LanguageManager.GetString("CreatedByLabel")}: {AuthService.CurrentUser?.FullName ?? AuthService.CurrentUser?.Username}";
        txtCreatedAt.Text = $"{LanguageManager.GetString("CreatedAtLabel")}: {DateTime.Now:dd-MMM-yyyy hh:mm tt}";

        EntryType_Changed(this, new RoutedEventArgs());
    }

    private void EntryType_Changed(object sender, RoutedEventArgs e)
    {
        if (pnlDueDate == null || pnlAdjustmentDirection == null) return;
        pnlDueDate.Visibility = rbCredit.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        pnlAdjustmentDirection.Visibility = rbAdjustment.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtDescription.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string type;
        var signedAmount = amount;
        if (rbCredit.IsChecked == true)
        {
            type = "Credit";
        }
        else if (rbPayment.IsChecked == true)
        {
            type = "Payment";
        }
        else
        {
            if (!AuthService.HasPermission("KhataAdjustment"))
            {
                MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            type = "Adjustment";
            if (rbDecrease.IsChecked == true) signedAmount = -amount;
        }

        var dueDate = (type == "Credit") ? dpDueDate.SelectedDate : null;
        new CustomerService().AddManualEntry(_customerId, type, signedAmount, txtDescription.Text.Trim(), dueDate, AuthService.CurrentUser?.Id);

        MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"), LanguageManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
        this.DialogResult = true;
        this.Close();
    }
}
