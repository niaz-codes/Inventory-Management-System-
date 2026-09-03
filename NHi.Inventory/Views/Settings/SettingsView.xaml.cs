using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;
using NHi.Inventory.Views.Inventory;

namespace NHi.Inventory.Views.Settings;

public partial class SettingsView : UserControl
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    private const long MaxImageBytes = 2 * 1024 * 1024; // 2 MB

    private byte[]? _logoData;
    private bool _logoChanged;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            LoadProfileInfo();
            LoadCompanySettings();
            ApplyCompanyProfilePermissions();
            LoadInventorySettings();
            ApplyInventorySettingsPermissions();
            LoadPrintSettings();
            ApplyPrintSettingsPermissions();
            rbDark.IsChecked = ThemeManager.IsDark;
            rbLight.IsChecked = !ThemeManager.IsDark;
            SyncLanguageCards();
        };
    }

    private void LoadProfileInfo()
    {
        var u = AuthService.CurrentUser;
        txtProfileName.Text = u?.FullName ?? "";
        txtProfileUsername.Text = u?.Username ?? "";
        txtProfileRole.Text = u?.Role ?? "";

        txtCurrentUsername.Text = u?.Username ?? "";
        // Username changes are an admin-only account action.
        cardChangeUsername.Visibility = AuthService.IsAdmin() ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Settings launcher <-> detail panel navigation ---

    private void OpenPanel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tag) return;

        FrameworkElement? target = tag switch
        {
            "Profile" => panelProfile,
            "Password" => panelPassword,
            "Inventory" => panelInventory,
            "Receipt" => panelReceipt,
            "Layout" => panelLayout,
            "Language" => panelLanguage,
            "Theme" => panelTheme,
            _ => null
        };
        if (target == null) return;

        launcherPane.Visibility = Visibility.Collapsed;
        detailHost.Visibility = Visibility.Visible;
        foreach (var p in new FrameworkElement[] { panelProfile, panelPassword, panelInventory, panelReceipt, panelLayout, panelLanguage, panelTheme })
            p.Visibility = ReferenceEquals(p, target) ? Visibility.Visible : Visibility.Collapsed;

        AnimateIn(target);
    }

    private void BackToLauncher_Click(object sender, RoutedEventArgs e)
    {
        detailHost.Visibility = Visibility.Collapsed;
        launcherPane.Visibility = Visibility.Visible;
        AnimateIn(launcherPane);
    }

    private void OpenPosPrint_Click(object sender, RoutedEventArgs e)
    {
        new PosPrintSettingsDialog { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void OpenBarcodePrint_Click(object sender, RoutedEventArgs e)
    {
        var products = new ProductService().GetAll(activeOnly: true);
        if (products.Count == 0)
        {
            MessageBox.Show(LanguageManager.GetString("NoProductsToPrint"),
                LanguageManager.GetString("PrintBarcodes"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var items = products.Select(p => (p, 1)).ToList();
        var win = BarcodeSheetService.BuildPreviewWindow(items);
        win.Owner = Window.GetWindow(this);
        win.ShowDialog();
    }

    /// <summary>Short fade + slide-up so switching between the launcher and a panel feels animated.</summary>
    private static void AnimateIn(UIElement el)
    {
        var slide = new TranslateTransform(0, 12);
        el.RenderTransform = slide;
        el.Opacity = 0;
        el.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void LoadCompanySettings()
    {
        using var db = new AppDbContext();
        var settings = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (settings != null)
        {
            txtCompanyName.Text = settings.Name;
            txtPhone.Text = settings.Phone ?? "";
            txtAddress.Text = settings.Address ?? "";
            txtCity.Text = settings.City ?? "";
            txtEmail.Text = settings.Email ?? "";
            txtCurrency.Text = settings.Currency;
            _logoData = settings.Logo;
            _logoChanged = false;
            ShowLogoPreview(_logoData);
        }
    }

    /// <summary>
    /// Shop identity (name/owner/logo/contact info) can only be changed by the Main Admin —
    /// not even a shop-level Admin can edit their own shop's profile here, only view it.
    /// Main Admin makes these changes through ShopDialog instead (and never reaches this
    /// screen at all, since they have no shop context of their own).
    /// </summary>
    private void ApplyCompanyProfilePermissions()
    {
        foreach (var box in new[] { txtCompanyName, txtPhone, txtAddress, txtCity, txtEmail, txtCurrency })
            box.IsReadOnly = true;
        btnSaveCompany.Visibility = Visibility.Collapsed;
        btnUploadLogo.Visibility = Visibility.Collapsed;
        btnRemoveLogo.Visibility = Visibility.Collapsed;
        txtReadOnlyNotice.Visibility = Visibility.Visible;
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
            // A corrupted/unsupported logo image must never block the rest of Settings from working.
            imgLogoPreview.Source = null;
        }
    }

    private void BtnUploadLogo_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsAdmin()) { MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired")); return; }

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
            _logoChanged = true;
            ShowLogoPreview(_logoData);
        }
        catch
        {
            MessageBox.Show(LanguageManager.GetString("InvalidImageFile"), LanguageManager.GetString("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnRemoveLogo_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsAdmin()) { MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired")); return; }
        _logoData = null;
        _logoChanged = true;
        ShowLogoPreview(null);
    }

    private void BtnSaveCompany(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsAdmin()) { MessageBox.Show(LanguageManager.GetString("AdminPermissionRequired")); return; }
        using var db = new AppDbContext();
        var settings = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (settings == null) return;
        settings.Name = txtCompanyName.Text.Trim();
        settings.Phone = txtPhone.Text.Trim();
        settings.Address = txtAddress.Text.Trim();
        settings.City = txtCity.Text.Trim();
        settings.Email = txtEmail.Text.Trim();
        settings.Currency = txtCurrency.Text.Trim();
        if (_logoChanged) settings.Logo = _logoData;
        settings.UpdatedAt = DateTime.Now;
        settings.UpdatedBy = AuthService.CurrentUser?.Id;
        db.SaveChanges();
        _logoChanged = false;
        AuditLogService.Log("UPDATE_COMPANY_SETTINGS", "Shop", settings.Id, $"Company profile updated to '{settings.Name}'");
        MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
    }

    private void LoadInventorySettings()
    {
        using var db = new AppDbContext();
        var settings = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (settings != null)
        {
            txtExpiryWarningDays.Text = settings.ExpiryWarningDays.ToString();
            chkBlockExpiredSale.IsChecked = settings.BlockExpiredSale;
            chkAllowNegativeStock.IsChecked = settings.AllowNegativeStock;
        }
    }

    private void ApplyInventorySettingsPermissions()
    {
        borderInventorySettings.Visibility = Visibility.Visible;
    }

    private void BtnSaveInventorySettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtExpiryWarningDays.Text, out var days) || days < 0)
        {
            MessageBox.Show(LanguageManager.GetString("InvalidStock"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = new AppDbContext();
        var settings = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (settings == null) return;
        settings.ExpiryWarningDays = days;
        settings.BlockExpiredSale = chkBlockExpiredSale.IsChecked == true;
        settings.AllowNegativeStock = chkAllowNegativeStock.IsChecked == true;
        settings.UpdatedAt = DateTime.Now;
        settings.UpdatedBy = AuthService.CurrentUser?.Id;
        db.SaveChanges();
        AuditLogService.Log("UPDATE_INVENTORY_SETTINGS", "Shop", settings.Id, "Inventory expiry settings updated");
        MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
    }

    private void LoadPrintSettings()
    {
        using var db = new AppDbContext();
        var settings = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (settings != null)
        {
            txtReceiptWidth.Text = (settings.ReceiptWidthMm ?? 80).ToString(System.Globalization.CultureInfo.InvariantCulture);
            txtReceiptHeight.Text = settings.ReceiptHeightMm?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        }
    }

    private void ApplyPrintSettingsPermissions()
    {
        borderPrintSettings.Visibility = Visibility.Visible;
    }

    private void BtnSavePrintSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(txtReceiptWidth.Text, out var width) || width <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("InvalidStock"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        double? height = null;
        if (!string.IsNullOrWhiteSpace(txtReceiptHeight.Text))
        {
            if (!double.TryParse(txtReceiptHeight.Text, out var parsedHeight) || parsedHeight <= 0)
            {
                MessageBox.Show(LanguageManager.GetString("InvalidStock"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            height = parsedHeight;
        }

        using var db = new AppDbContext();
        var settings = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (settings == null) return;
        settings.ReceiptWidthMm = width;
        settings.ReceiptHeightMm = height;
        settings.UpdatedAt = DateTime.Now;
        settings.UpdatedBy = AuthService.CurrentUser?.Id;
        db.SaveChanges();
        AuditLogService.Log("UPDATE_PRINT_SETTINGS", "Shop", settings.Id, $"Receipt print size updated to {width}x{(height?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "auto")}mm");
        MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
    }

    private void BtnOpenPrintLayout_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintLayoutDialog();
        dialog.ShowDialog();
    }

    // ===================== Admin account management =====================

    /// <summary>Reads whichever field variant is visible (masked box vs. revealed text box).</summary>
    private static string PwOf(PasswordBox box, TextBox visible) =>
        visible.Visibility == Visibility.Visible ? visible.Text : box.Password;

    private void SetStatus(TextBlock target, string message, bool ok)
    {
        target.Text = message;
        target.Foreground = (Brush)Application.Current.Resources[ok ? "SuccessTextBrush" : "DangerTextBrush"];
        target.Visibility = Visibility.Visible;
    }

    private void ShowPasswords_Toggled(object sender, RoutedEventArgs e)
    {
        bool show = chkShowPasswords.IsChecked == true;
        foreach (var (pb, tb) in new[]
        {
            (txtCurrentPass, txtCurrentPassVisible),
            (txtNewPass, txtNewPassVisible),
            (txtConfirmPass, txtConfirmPassVisible),
        })
        {
            if (show) { tb.Text = pb.Password; }
            else { pb.Password = tb.Text; }
            pb.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            tb.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void NewPassword_Changed(object sender, RoutedEventArgs e)
    {
        var pw = PwOf(txtNewPass, txtNewPassVisible);
        var score = AuthService.EvaluatePasswordStrength(pw);
        pbStrength.Value = score;

        string key; string colour;
        if (score <= 1) { key = "StrengthWeak"; colour = "DangerTextBrush"; }
        else if (score <= 2) { key = "StrengthFair"; colour = "WarningTextBrush"; }
        else { key = "StrengthStrong"; colour = "SuccessTextBrush"; }

        txtStrengthLabel.Text = pw.Length == 0 ? "" : LanguageManager.GetString(key);
        var brush = (Brush)Application.Current.Resources[colour];
        txtStrengthLabel.Foreground = brush;
        pbStrength.Foreground = brush;
    }

    private void BtnChangeUsername_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.IsAdmin()) return;

        var newName = txtNewUsername.Text.Trim();
        var currentPw = txtUsernameCurrentPass.Password;

        if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrEmpty(currentPw))
        {
            SetStatus(txtUsernameStatus, LanguageManager.GetString("FillRequired"), ok: false);
            return;
        }

        var (ok, errorKey) = AuthService.ChangeUsername(currentPw, newName);
        if (ok)
        {
            SetStatus(txtUsernameStatus, LanguageManager.GetString("UsernameChanged"), ok: true);
            txtNewUsername.Clear();
            txtUsernameCurrentPass.Clear();
            LoadProfileInfo();   // refresh the displayed username everywhere on this panel
        }
        else
        {
            SetStatus(txtUsernameStatus, LanguageManager.GetString(errorKey ?? "ErrorOccurred"), ok: false);
        }
    }

    private void BtnChangePassword(object sender, RoutedEventArgs e)
    {
        var current = PwOf(txtCurrentPass, txtCurrentPassVisible);
        var next = PwOf(txtNewPass, txtNewPassVisible);
        var confirm = PwOf(txtConfirmPass, txtConfirmPassVisible);

        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(next) || string.IsNullOrEmpty(confirm))
        {
            SetStatus(txtPasswordStatus, LanguageManager.GetString("FillRequired"), ok: false);
            return;
        }
        if (next != confirm)
        {
            SetStatus(txtPasswordStatus, LanguageManager.GetString("PasswordsDoNotMatch"), ok: false);
            return;
        }
        if (AuthService.EvaluatePasswordStrength(next) < 2)
        {
            SetStatus(txtPasswordStatus, LanguageManager.GetString("PasswordTooWeak"), ok: false);
            return;
        }

        var (ok, errorKey) = AuthService.ChangePassword(current, next);
        if (!ok)
        {
            SetStatus(txtPasswordStatus, LanguageManager.GetString(errorKey ?? "ErrorOccurred"), ok: false);
            return;
        }

        SetStatus(txtPasswordStatus, LanguageManager.GetString("PasswordChanged"), ok: true);
        txtCurrentPass.Clear(); txtNewPass.Clear(); txtConfirmPass.Clear();
        txtCurrentPassVisible.Clear(); txtNewPassVisible.Clear(); txtConfirmPassVisible.Clear();

        // Handle the session securely: force a fresh sign-in with the new credentials.
        MessageBox.Show(LanguageManager.GetString("PasswordChanged"),
            LanguageManager.GetString("ChangePassword"), MessageBoxButton.OK, MessageBoxImage.Information);
        AuthService.Logout();
        var login = new NHi.Inventory.Views.LoginWindow();
        login.Show();
        Window.GetWindow(this)?.Close();
    }

    private bool _syncingLanguageCards;

    /// <summary>Reflects the live language onto the two cards without re-triggering a switch.</summary>
    private void SyncLanguageCards()
    {
        _syncingLanguageCards = true;
        var isUrdu = LanguageManager.CurrentLanguage == "ur";
        cardEnglish.IsChecked = !isUrdu;
        cardUrdu.IsChecked = isUrdu;
        _syncingLanguageCards = false;
    }

    private void LanguageCard_Click(object sender, RoutedEventArgs e)
    {
        if (_syncingLanguageCards || !IsLoaded) return;
        if (sender is not System.Windows.Controls.Primitives.ToggleButton tb || tb.Tag is not string lang) return;

        // Enforce single-select: the clicked card wins, the other clears. (Clicking the already
        // active card would otherwise un-toggle it and leave no language selected.)
        _syncingLanguageCards = true;
        cardEnglish.IsChecked = lang == "en";
        cardUrdu.IsChecked = lang == "ur";
        _syncingLanguageCards = false;

        if (LanguageManager.CurrentLanguage != lang)
            LanguageManager.SetLanguage(lang);   // applies + persists app-wide, immediately
    }

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var theme = rbDark.IsChecked == true ? "dark" : "light";
        ThemeManager.SetTheme(theme);

        using var db = new AppDbContext();
        var settings = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (settings == null) return;
        settings.Theme = theme;
        db.SaveChanges();
    }
}
