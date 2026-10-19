using System.Windows;
using System.Windows.Input;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class CategoryBrandDialog : Window
{
    private readonly string _type;
    private readonly ProductService _service = new();
    private bool _changed;

    public CategoryBrandDialog(string type)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _type = type;
        txtTitle.Text = type == "Category" ? LanguageManager.GetString("AddCategory") : LanguageManager.GetString("AddBrand");
        lblName.Text = type;
        LoadItems();
    }

    private void LoadItems()
    {
        if (_type == "Category")
            icItems.ItemsSource = _service.GetCategories();
        else
            icItems.ItemsSource = _service.GetBrands();

        var hasItems = icItems.Items.Count > 0;
        icItems.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        txtEmpty.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TxtName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnAdd_Click(sender, e);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text)) return;

        if (_type == "Category")
            _service.SaveCategory(new Category { Name = txtName.Text.Trim() });
        else
            _service.SaveBrand(new Brand { Name = txtName.Text.Trim() });

        _changed = true;
        txtName.Clear();
        LoadItems();
    }

    private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: int id }) return;

        var result = MessageBox.Show(LanguageManager.GetString("ConfirmDelete"), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        if (_type == "Category") _service.DeleteCategory(id);
        else _service.DeleteBrand(id);

        _changed = true;
        LoadItems();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = _changed;
        this.Close();
    }
}
