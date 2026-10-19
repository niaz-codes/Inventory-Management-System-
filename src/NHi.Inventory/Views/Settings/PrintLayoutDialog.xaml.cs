using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Data;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

/// <summary>
/// Print Layout settings: page size (standard presets or a fully custom width/height/unit),
/// margins, and orientation for receipts and payment slips, with a live schematic preview.
/// "Save as Default" persists to the current shop; ReceiptService reads it fresh on every print,
/// so changes apply immediately without needing a restart.
/// </summary>
public partial class PrintLayoutDialog : Window
{
    private sealed record PresetItem(string Key, string Display);

    private Shop? _shop;
    private bool _loaded;

    public PrintLayoutDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        Loaded += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        using (var db = new AppDbContext())
            _shop = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);

        cmbPageSize.DisplayMemberPath = nameof(PresetItem.Display);
        cmbPageSize.SelectedValuePath = nameof(PresetItem.Key);
        cmbPageSize.ItemsSource = PrintLayoutService.Presets.Select(p => new PresetItem(p, PresetDisplayName(p))).ToList();

        cmbUnit.ItemsSource = PrintLayoutService.Units;

        ApplyToForm(_shop);
        _loaded = true;
        OnSettingChanged();
    }

    private static string PresetDisplayName(string preset) => preset switch
    {
        PrintLayoutService.PresetReceipt => LanguageManager.GetString("PageSizeReceipt"),
        PrintLayoutService.PresetCustom => LanguageManager.GetString("PageSizeCustom"),
        _ => preset,
    };

    private void ApplyToForm(Shop? shop)
    {
        cmbPageSize.SelectedValue = string.IsNullOrWhiteSpace(shop?.PrintPageSize) ? PrintLayoutService.PresetReceipt : shop!.PrintPageSize;
        cmbUnit.SelectedItem = string.IsNullOrWhiteSpace(shop?.PrintUnit) ? "mm" : shop!.PrintUnit;
        txtCustomWidth.Text = shop?.PrintCustomWidth?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        txtCustomHeight.Text = shop?.PrintCustomHeight?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        txtMarginTop.Text = (shop?.PrintMarginTop ?? 3.53).ToString(System.Globalization.CultureInfo.InvariantCulture);
        txtMarginBottom.Text = (shop?.PrintMarginBottom ?? 3.53).ToString(System.Globalization.CultureInfo.InvariantCulture);
        txtMarginLeft.Text = (shop?.PrintMarginLeft ?? 3.53).ToString(System.Globalization.CultureInfo.InvariantCulture);
        txtMarginRight.Text = (shop?.PrintMarginRight ?? 3.53).ToString(System.Globalization.CultureInfo.InvariantCulture);
        rbLandscape.IsChecked = shop?.PrintOrientation == "Landscape";
        rbPortrait.IsChecked = shop?.PrintOrientation != "Landscape";
    }

    private void Setting_Changed(object sender, RoutedEventArgs e) => OnSettingChanged();
    private void Setting_TextChanged(object sender, TextChangedEventArgs e) => OnSettingChanged();
    private void Setting_SelectionChanged(object sender, SelectionChangedEventArgs e) => OnSettingChanged();

    private void OnSettingChanged()
    {
        if (!_loaded) return;
        var preset = cmbPageSize.SelectedValue as string ?? PrintLayoutService.PresetReceipt;
        panelCustomFields.Visibility = preset == PrintLayoutService.PresetCustom ? Visibility.Visible : Visibility.Collapsed;
        UpdateFixedSizeInfo(preset);
        UpdatePreview();
    }

    private void UpdateFixedSizeInfo(string preset)
    {
        if (preset == PrintLayoutService.PresetCustom)
        {
            txtFixedSizeInfo.Text = "";
            return;
        }

        if (preset == PrintLayoutService.PresetReceipt)
        {
            var w = _shop?.ReceiptWidthMm ?? 80;
            var h = _shop?.ReceiptHeightMm;
            var heightText = h.HasValue ? $"{h.Value:0.#} mm" : LanguageManager.GetString("AutoFitHeight");
            txtFixedSizeInfo.Text = $"{w:0.#} mm × {heightText}  —  {LanguageManager.GetString("ReceiptSizeManagedHint")}";
            return;
        }

        var geometry = PrintLayoutService.Resolve(preset, null, null, "mm", 0, 0, 0, 0, "Portrait");
        txtFixedSizeInfo.Text = geometry.HeightMm.HasValue ? $"{geometry.WidthMm:0.#} mm × {geometry.HeightMm:0.#} mm" : "";
    }

    private PrintLayoutService.Geometry BuildGeometryFromForm()
    {
        var preset = cmbPageSize.SelectedValue as string ?? PrintLayoutService.PresetReceipt;
        var unit = cmbUnit.SelectedItem as string ?? "mm";
        double? customWidth = double.TryParse(txtCustomWidth.Text, out var cw) ? cw : null;
        double? customHeight = double.TryParse(txtCustomHeight.Text, out var ch) ? ch : null;
        var orientation = rbLandscape.IsChecked == true ? "Landscape" : "Portrait";

        return PrintLayoutService.Resolve(preset, customWidth, customHeight, unit,
            ParseOr(txtMarginTop.Text), ParseOr(txtMarginBottom.Text), ParseOr(txtMarginLeft.Text), ParseOr(txtMarginRight.Text),
            orientation, receiptWidthMm: _shop?.ReceiptWidthMm ?? 80, receiptHeightMm: _shop?.ReceiptHeightMm);
    }

    private static double ParseOr(string text, double fallback = 3.53) =>
        double.TryParse(text, out var v) && v >= 0 ? v : fallback;

    /// <summary>Redraws the schematic page preview: a page-shaped rectangle sized proportionally
    /// to the resolved geometry, with the printable area inset by the configured margins. Not a
    /// pixel-accurate render of the real receipt — a fast, always-in-sync geometry guide.</summary>
    private void UpdatePreview()
    {
        var geometry = BuildGeometryFromForm();

        const double maxW = 250, maxH = 360;
        var assumedHeightMm = geometry.HeightMm ?? geometry.WidthMm * 3.4;
        if (assumedHeightMm <= 0) assumedHeightMm = 1;
        var widthMm = geometry.WidthMm <= 0 ? 1 : geometry.WidthMm;

        var scale = Math.Min(maxW / widthMm, maxH / assumedHeightMm);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) scale = 1;
        scale = Math.Min(scale, 3.2);

        previewPage.Width = Math.Max(widthMm * scale, 36);
        previewPage.Height = Math.Max(assumedHeightMm * scale, 36);
        previewMargin.Margin = new Thickness(
            Math.Max(geometry.MarginLeftMm * scale, 0),
            Math.Max(geometry.MarginTopMm * scale, 0),
            Math.Max(geometry.MarginRightMm * scale, 0),
            Math.Max(geometry.MarginBottomMm * scale, 0));

        var heightLabel = geometry.HeightMm.HasValue ? $"{geometry.HeightMm.Value:0.#} mm" : LanguageManager.GetString("AutoFitHeight");
        var orientationLabel = LanguageManager.GetString(geometry.IsLandscape ? "Landscape" : "Portrait");
        txtPreviewDims.Text = $"{geometry.WidthMm:0.#} mm × {heightLabel}  •  {orientationLabel}";
    }

    private void BtnSaveDefault_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateAndSave("Print layout updated"))
            MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
    }

    private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
    {
        var def = PrintLayoutService.Default(_shop?.ReceiptWidthMm ?? 80);
        cmbPageSize.SelectedValue = PrintLayoutService.PresetReceipt;
        cmbUnit.SelectedItem = "mm";
        txtCustomWidth.Text = "";
        txtCustomHeight.Text = "";
        txtMarginTop.Text = def.MarginTopMm.ToString(System.Globalization.CultureInfo.InvariantCulture);
        txtMarginBottom.Text = def.MarginBottomMm.ToString(System.Globalization.CultureInfo.InvariantCulture);
        txtMarginLeft.Text = def.MarginLeftMm.ToString(System.Globalization.CultureInfo.InvariantCulture);
        txtMarginRight.Text = def.MarginRightMm.ToString(System.Globalization.CultureInfo.InvariantCulture);
        rbPortrait.IsChecked = true;
        OnSettingChanged();

        if (ValidateAndSave("Print layout reset to default"))
            MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
    }

    private bool ValidateAndSave(string auditNote)
    {
        var preset = cmbPageSize.SelectedValue as string ?? PrintLayoutService.PresetReceipt;
        var unit = cmbUnit.SelectedItem as string ?? "mm";

        double? customWidth = null, customHeight = null;
        if (preset == PrintLayoutService.PresetCustom)
        {
            if (!double.TryParse(txtCustomWidth.Text, out var w) || w <= 0)
            {
                MessageBox.Show(LanguageManager.GetString("InvalidStock"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            customWidth = w;

            if (!string.IsNullOrWhiteSpace(txtCustomHeight.Text))
            {
                if (!double.TryParse(txtCustomHeight.Text, out var h) || h <= 0)
                {
                    MessageBox.Show(LanguageManager.GetString("InvalidStock"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                customHeight = h;
            }
        }

        if (!TryParseMargin(txtMarginTop.Text, out var mTop) || !TryParseMargin(txtMarginBottom.Text, out var mBottom) ||
            !TryParseMargin(txtMarginLeft.Text, out var mLeft) || !TryParseMargin(txtMarginRight.Text, out var mRight))
        {
            MessageBox.Show(LanguageManager.GetString("InvalidStock"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        using var db = new AppDbContext();
        var shop = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId);
        if (shop == null) return false;

        shop.PrintPageSize = preset;
        shop.PrintUnit = unit;
        shop.PrintCustomWidth = customWidth;
        shop.PrintCustomHeight = customHeight;
        shop.PrintMarginTop = mTop;
        shop.PrintMarginBottom = mBottom;
        shop.PrintMarginLeft = mLeft;
        shop.PrintMarginRight = mRight;
        shop.PrintOrientation = rbLandscape.IsChecked == true ? "Landscape" : "Portrait";
        shop.UpdatedAt = DateTime.Now;
        shop.UpdatedBy = AuthService.CurrentUser?.Id;
        db.SaveChanges();

        AuditLogService.Log("UPDATE_PRINT_LAYOUT", "Shop", shop.Id,
            $"{auditNote}: {preset}/{shop.PrintOrientation}, margins {mTop:0.##}/{mBottom:0.##}/{mLeft:0.##}/{mRight:0.##}mm");

        _shop = shop;
        OnSettingChanged();
        return true;
    }

    private static bool TryParseMargin(string text, out double value) =>
        double.TryParse(text, out value) && value >= 0;

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
