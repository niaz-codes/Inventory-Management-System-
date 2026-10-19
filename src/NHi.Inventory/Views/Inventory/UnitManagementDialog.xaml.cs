using System.Windows;
using System.Windows.Input;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

/// <summary>Add / rename / delete product units. The five built-in defaults (Kilogram, Foot,
/// Inch, Box, Packet) are protected: they can be renamed but not deleted. A custom unit that is
/// currently assigned to a product also can't be deleted, so no product ends up with a dangling
/// unit reference.</summary>
public partial class UnitManagementDialog : Window
{
    private readonly ProductService _service = new();
    private bool _changed;
    private int _editingId;   // 0 = add mode

    public UnitManagementDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        LoadItems();
        txtName.Focus();
    }

    /// <summary>Whether any add/edit/delete happened, so the caller can refresh its unit dropdown.</summary>
    public bool DataChanged => _changed;

    private void LoadItems()
    {
        icItems.ItemsSource = _service.GetUnits();
    }

    private void TxtName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnAdd_Click(sender, e);
        else if (e.Key == Key.Escape && _editingId != 0) BtnCancelEdit_Click(sender, e);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var name = txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        _service.SaveUnit(new Unit { Id = _editingId, Name = name });
        _changed = true;
        ResetToAddMode();
        LoadItems();
    }

    private void BtnEditItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: int id }) return;
        var unit = _service.GetUnits().FirstOrDefault(u => u.Id == id);
        if (unit == null) return;

        _editingId = id;
        txtName.Text = unit.Name;
        txtName.Focus();
        txtName.SelectAll();
        btnAdd.Content = LanguageManager.GetString("Save");
        btnCancelEdit.Visibility = Visibility.Visible;
    }

    private void BtnCancelEdit_Click(object sender, RoutedEventArgs e) => ResetToAddMode();

    private void ResetToAddMode()
    {
        _editingId = 0;
        txtName.Clear();
        btnAdd.Content = LanguageManager.GetString("Add");
        btnCancelEdit.Visibility = Visibility.Collapsed;
    }

    private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: int id }) return;

        if (MessageBox.Show(LanguageManager.GetString("ConfirmDeleteUnit"), "",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        switch (_service.DeleteUnit(id))
        {
            case UnitDeleteResult.Deleted:
                _changed = true;
                if (_editingId == id) ResetToAddMode();
                LoadItems();
                break;
            case UnitDeleteResult.Protected:
                MessageBox.Show(LanguageManager.GetString("UnitProtected"), LanguageManager.GetString("Warning"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
            case UnitDeleteResult.InUse:
                MessageBox.Show(LanguageManager.GetString("UnitInUse"), LanguageManager.GetString("Warning"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _changed;
        Close();
    }
}
