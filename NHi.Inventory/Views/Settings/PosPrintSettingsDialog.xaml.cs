using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Settings;

public partial class PosPrintSettingsDialog : Window
{
    private const string DefaultPrinterTag = "__default__";

    public PosPrintSettingsDialog()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;

        cmbPrinter.Items.Add(new ComboBoxItem { Content = LanguageManager.GetString("SystemDefaultPrinter"), Tag = DefaultPrinterTag });
        foreach (var name in PosPrintService.InstalledPrinters())
            cmbPrinter.Items.Add(new ComboBoxItem { Content = name, Tag = name });

        Load(PosPrintService.Load());
    }

    private void Load(PosPrintSettings s)
    {
        cmbPrinterType.SelectedIndex = s.PrinterType == "Standard" ? 1 : 0;

        var printerIdx = 0;
        if (!string.IsNullOrWhiteSpace(s.PrinterName))
            for (var i = 0; i < cmbPrinter.Items.Count; i++)
                if (((ComboBoxItem)cmbPrinter.Items[i]).Tag as string == s.PrinterName) { printerIdx = i; break; }
        cmbPrinter.SelectedIndex = printerIdx;

        cmbThermalWidth.SelectedIndex = s.ThermalWidth switch { "58mm" => 0, "Custom" => 2, _ => 1 };
        txtThermalCustom.Text = s.CustomThermalWidthMm.ToString("0.##");

        cmbStandardPaper.SelectedIndex = s.StandardPaper switch { "A5" => 1, "Letter" => 2, "Custom" => 3, _ => 0 };
        txtStdW.Text = s.CustomWidthMm.ToString("0.##");
        txtStdH.Text = s.CustomHeightMm.ToString("0.##");

        txtMargin.Text = s.MarginMm.ToString("0.##");
        txtCopies.Text = s.Copies.ToString();
        cmbCopyMode.SelectedIndex = s.CopyMode switch { "Shop" => 1, "Both" => 2, _ => 0 };

        chkAutoPrint.IsChecked = s.AutoPrint;
        chkAutoCut.IsChecked = s.AutoCut;
        chkLivePreview.IsChecked = s.LivePreview;

        UpdateGroups();
    }

    private PosPrintSettings Collect()
    {
        double D(string t, double d) => double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : d;
        int I(string t, int d) => int.TryParse(t, out var v) ? v : d;

        return new PosPrintSettings
        {
            PrinterType = cmbPrinterType.SelectedIndex == 1 ? "Standard" : "Thermal",
            PrinterName = (cmbPrinter.SelectedItem as ComboBoxItem)?.Tag as string is { } tag && tag != DefaultPrinterTag ? tag : null,
            ThermalWidth = cmbThermalWidth.SelectedIndex switch { 0 => "58mm", 2 => "Custom", _ => "80mm" },
            CustomThermalWidthMm = Math.Clamp(D(txtThermalCustom.Text, 80), 40, 120),
            StandardPaper = cmbStandardPaper.SelectedIndex switch { 1 => "A5", 2 => "Letter", 3 => "Custom", _ => "A4" },
            CustomWidthMm = Math.Clamp(D(txtStdW.Text, 210), 60, 1000),
            CustomHeightMm = Math.Clamp(D(txtStdH.Text, 297), 60, 2000),
            MarginMm = Math.Clamp(D(txtMargin.Text, 3), 0, 40),
            Copies = Math.Clamp(I(txtCopies.Text, 1), 1, 20),
            CopyMode = cmbCopyMode.SelectedIndex switch { 1 => "Shop", 2 => "Both", _ => "Customer" },
            AutoPrint = chkAutoPrint.IsChecked == true,
            AutoCut = chkAutoCut.IsChecked == true,
            LivePreview = chkLivePreview.IsChecked == true,
        };
    }

    private void UpdateGroups()
    {
        if (grpThermal == null) return;
        var standard = cmbPrinterType.SelectedIndex == 1;
        grpThermal.Visibility = standard ? Visibility.Collapsed : Visibility.Visible;
        grpStandard.Visibility = standard ? Visibility.Visible : Visibility.Collapsed;
        pnlThermalCustom.Visibility = cmbThermalWidth.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        pnlStdCustom.Visibility = cmbStandardPaper.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PrinterType_Changed(object sender, SelectionChangedEventArgs e) => UpdateGroups();
    private void ThermalWidth_Changed(object sender, SelectionChangedEventArgs e) => UpdateGroups();
    private void StandardPaper_Changed(object sender, SelectionChangedEventArgs e) => UpdateGroups();

    private void BtnTest_Click(object sender, RoutedEventArgs e) => PosPrintService.TestPrint(Collect());

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        PosPrintService.Save(Collect());
        MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
        DialogResult = true;
        Close();
    }
}
