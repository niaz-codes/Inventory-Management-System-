using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Admin;

public partial class ShopDialog : Window
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    private const long MaxImageBytes = 2 * 1024 * 1024; // 2 MB

    private readonly ShopService _shopService = new();
    private readonly Shop? _existingShop;
    private byte[]? _logoData;

    public ShopDialog(Shop? existingShop = null)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _existingShop = existingShop;
        if (existingShop != null) LoadShop(existingShop);
    }

    private void LoadShop(Shop shop)
    {
        txtTitle.Text = LanguageManager.GetString("EditShop");
        txtName.Text = shop.Name;
        txtOwnerName.Text = shop.OwnerName ?? "";
        txtBusinessType.Text = shop.BusinessType ?? "";
        txtPhone.Text = shop.Phone ?? "";
        txtCity.Text = shop.City ?? "";
        txtAddress.Text = shop.Address ?? "";
        txtEmail.Text = shop.Email ?? "";
        txtCurrency.Text = shop.Currency;
        _logoData = shop.Logo;
        ShowLogoPreview(_logoData);
        // An existing shop's login account(s) are managed from ShopUsersWindow, not here.
        panelLoginAccount.Visibility = Visibility.Collapsed;
    }

    private void ShowLogoPreview(byte[]? data)
    {
        if (data == null || data.Length == 0) { imgLogoPreview.Source = null; return; }
        try
        {
            using var ms = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            imgLogoPreview.Source = bitmap;
        }
        catch
        {
            imgLogoPreview.Source = null;
        }
    }

    private void BtnChooseLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif" };
        if (dialog.ShowDialog() != true) return;

        var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            MessageBox.Show(LanguageManager.GetString("InvalidImageFile"), LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > MaxImageBytes)
            {
                MessageBox.Show(LanguageManager.GetString("ImageTooLarge"), LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _logoData = File.ReadAllBytes(dialog.FileName);
            ShowLogoPreview(_logoData);
        }
        catch
        {
            MessageBox.Show(LanguageManager.GetString("InvalidImageFile"), LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isNewShop = _existingShop == null;
        string? loginUsername = null;
        string? loginStatus = null;
        bool loginIsActive = true;

        if (isNewShop)
        {
            loginUsername = txtLoginUsername.Text.Trim();
            if (string.IsNullOrWhiteSpace(loginUsername) || string.IsNullOrWhiteSpace(txtLoginPassword.Password))
            {
                MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (txtLoginPassword.Password != txtLoginPasswordConfirm.Password)
            {
                MessageBox.Show(LanguageManager.GetString("PasswordsDoNotMatch"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using (var checkDb = new AppDbContext())
            {
                if (checkDb.Users.IgnoreQueryFilters().Any(u => u.Username == loginUsername))
                {
                    MessageBox.Show(LanguageManager.GetString("DuplicateUsername"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var statusChoice = (cmbAccountStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Active";
            loginStatus = statusChoice == "Pending" ? "Pending" : "Approved";
            loginIsActive = statusChoice != "Inactive";
        }

        var shop = _existingShop ?? new Shop();
        shop.Name = txtName.Text.Trim();
        shop.OwnerName = txtOwnerName.Text.Trim();
        shop.BusinessType = txtBusinessType.Text.Trim();
        shop.Phone = txtPhone.Text.Trim();
        shop.City = txtCity.Text.Trim();
        shop.Address = txtAddress.Text.Trim();
        shop.Email = txtEmail.Text.Trim();
        shop.Currency = string.IsNullOrWhiteSpace(txtCurrency.Text) ? "Rs." : txtCurrency.Text.Trim();
        shop.Logo = _logoData;

        _shopService.Save(shop);

        if (isNewShop)
        {
            using var db = new AppDbContext();
            db.Users.Add(new User
            {
                ShopId = shop.Id,
                Username = loginUsername!,
                FullName = shop.OwnerName ?? shop.Name,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(txtLoginPassword.Password),
                Role = "USER",
                Status = loginStatus!,
                IsActive = loginIsActive
            });
            db.SaveChanges();
            AuditLogService.Log("SHOP_LOGIN_CREATE", "User", null, $"Created login '{loginUsername}' for shop '{shop.Name}'");
        }

        this.DialogResult = true;
        this.Close();
    }
}
