using System.Windows;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Inventory;

public partial class CategoryBrandDialog : Window
{
    private readonly string _type;

    public CategoryBrandDialog(string type)
    {
        InitializeComponent();
        _type = type;
        txtTitle.Text = type == "Category" ? LanguageManager.GetString("AddCategory") : LanguageManager.GetString("AddBrand");
        lblName.Text = type;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text)) return;

        var service = new ProductService();
        if (_type == "Category")
            service.SaveCategory(new Category { Name = txtName.Text.Trim() });
        else
            service.SaveBrand(new Brand { Name = txtName.Text.Trim() });

        this.DialogResult = true;
        this.Close();
    }
}
