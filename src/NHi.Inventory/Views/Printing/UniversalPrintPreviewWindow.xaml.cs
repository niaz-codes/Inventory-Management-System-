using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Drawing.Printing;
using NHi.Inventory.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using GdiImage = System.Drawing.Image;
using WinForms = System.Windows.Forms;

namespace NHi.Inventory.Views.Printing;

/// <summary>
/// The one preview + print window used by every printable document in the app.
/// Rasterises a QuestPDF document to page images, then offers numbered page
/// navigation, 25–500% zoom, a live info line, and Print Current / Range / All
/// (each opening the Windows print dialog). When a rebuild callback is supplied it
/// also exposes A4 / A5 / Letter / Custom paper selection (and, for barcode sheets,
/// label size + copies), re-flowing the whole document live on any change.
/// </summary>
public partial class UniversalPrintPreviewWindow : Window
{
    private const int RasterDpi = 200;

    private readonly Func<UniversalPrintPreviewWindow, IDocument> _build;
    private readonly Func<int>? _itemCount;
    private readonly bool _dynamic;

    private readonly List<GdiImage> _pagesGdi = new();
    private readonly List<BitmapSource> _pagesWpf = new();
    private int _current;
    private double _zoom = 1.0;
    private bool _loaded;

    /// <summary>Static document (receipts, statements, reports as they are built today).
    /// No paper selector — the document already defines its own page size.</summary>
    public UniversalPrintPreviewWindow(IDocument document, string title)
        : this(title, _ => document, dynamic: false, showBarcodeControls: false, itemCount: null) { }

    /// <summary>Re-flowable document: <paramref name="build"/> is re-invoked whenever the
    /// paper size / label size / copies change, so the layout stays correct for any paper.</summary>
    public UniversalPrintPreviewWindow(string title,
        Func<UniversalPrintPreviewWindow, IDocument> build,
        bool showBarcodeControls,
        Func<int>? itemCount)
        : this(title, build, dynamic: true, showBarcodeControls, itemCount) { }

    private UniversalPrintPreviewWindow(string title,
        Func<UniversalPrintPreviewWindow, IDocument> build,
        bool dynamic, bool showBarcodeControls, Func<int>? itemCount)
    {
        InitializeComponent();
        Title = title;
        _build = build;
        _dynamic = dynamic;
        _itemCount = itemCount;

        pnlPaper.Visibility = dynamic ? Visibility.Visible : Visibility.Collapsed;
        pnlBarcode.Visibility = (dynamic && showBarcodeControls) ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) => { _loaded = true; Rebuild(); };
        Closed += (_, _) => DisposePages();
    }

    // -------- values the build callback reads --------

    public double PaperWidthMm { get; private set; } = 210;
    public double PaperHeightMm { get; private set; } = 297;
    public double LabelWidthMm => Num(txtLabelW.Text, 55, 15, 400);
    public double LabelHeightMm => Num(txtLabelH.Text, 32, 12, 300);
    public int Copies => (int)Num(txtCopies.Text, 1, 1, 50);

    private static double Num(string text, double fallback, double min, double max)
        => double.TryParse(text, out var v) ? Math.Clamp(v, min, max) : fallback;

    // -------- (re)build --------

    private void Opt_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        pnlCustom.Visibility = (cmbPaper.SelectedIndex == 3) ? Visibility.Visible : Visibility.Collapsed;
        Rebuild();
    }

    private void ResolvePaper()
    {
        switch (cmbPaper.SelectedIndex)
        {
            case 1: PaperWidthMm = 148.0; PaperHeightMm = 210.0; break;               // A5
            case 2: PaperWidthMm = 215.9; PaperHeightMm = 279.4; break;               // Letter
            case 3:
                PaperWidthMm = Num(txtCustomW.Text, 100, 20, 2000);
                PaperHeightMm = Num(txtCustomH.Text, 150, 20, 3000);
                break;
            default: PaperWidthMm = 210.0; PaperHeightMm = 297.0; break;              // A4
        }
    }

    private void Rebuild()
    {
        ResolvePaper();

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            var doc = _build(this);
            var pngs = doc.GenerateImages(new ImageGenerationSettings { RasterDpi = RasterDpi }).ToList();

            DisposePages();
            foreach (var bytes in pngs)
            {
                _pagesGdi.Add(GdiImage.FromStream(new MemoryStream(bytes)));

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                _pagesWpf.Add(bmp);
            }

            _current = Math.Clamp(_current, 0, Math.Max(0, _pagesWpf.Count - 1));
            txtFrom.Text = "1";
            txtTo.Text = _pagesWpf.Count.ToString();

            BuildPageButtons();
            ShowPage(_current);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{LanguageManager.GetString("ErrorOccurred")}: {ex.Message}",
                LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }
    }

    private void BuildPageButtons()
    {
        pageButtons.Children.Clear();
        for (var k = 1; k <= _pagesWpf.Count; k++)
        {
            var pageIndex = k - 1;
            var btn = new Button
            {
                Content = k.ToString(),
                Style = (Style)FindResource("PagerBtn"),
                Tag = pageIndex == _current ? "active" : null
            };
            btn.Click += (_, _) => ShowPage(pageIndex);
            pageButtons.Children.Add(btn);
        }
    }

    private void ShowPage(int index)
    {
        if (_pagesWpf.Count == 0) return;
        _current = Math.Clamp(index, 0, _pagesWpf.Count - 1);

        var img = _pagesWpf[_current];
        var factor = 96.0 / RasterDpi * _zoom;
        imgPreview.Source = img;
        imgPreview.Width = img.PixelWidth * factor;
        imgPreview.Height = img.PixelHeight * factor;

        var total = _pagesWpf.Count;
        if (_itemCount != null && total > 0)
        {
            var items = _itemCount();
            var perPage = (int)Math.Ceiling((double)items / total);
            txtPageInfo.Text = string.Format(LanguageManager.GetString("BarcodePageInfo"),
                _current + 1, total, perPage, items);
        }
        else
        {
            txtPageInfo.Text = string.Format(LanguageManager.GetString("PageXofN"), _current + 1, total);
        }

        txtZoom.Text = $"{_zoom * 100:0}%";
        btnPrev.IsEnabled = _current > 0;
        btnNext.IsEnabled = _current < total - 1;

        for (var i = 0; i < pageButtons.Children.Count; i++)
            if (pageButtons.Children[i] is Button b)
                b.Tag = (i == _current) ? "active" : null;
    }

    // -------- navigation / zoom --------

    private void BtnPrev_Click(object sender, RoutedEventArgs e) => ShowPage(_current - 1);
    private void BtnNext_Click(object sender, RoutedEventArgs e) => ShowPage(_current + 1);

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e) { _zoom = Math.Min(5.0, _zoom * 1.25); ShowPage(_current); }
    private void BtnZoomOut_Click(object sender, RoutedEventArgs e) { _zoom = Math.Max(0.25, _zoom / 1.25); ShowPage(_current); }

    private void BtnFit_Click(object sender, RoutedEventArgs e)
    {
        if (_pagesWpf.Count == 0) return;
        var img = _pagesWpf[_current];
        var availH = Math.Max(200, previewScroller.ActualHeight - 60);
        var pageH = img.PixelHeight * (96.0 / RasterDpi);
        _zoom = Math.Clamp(availH / pageH, 0.25, 5.0);
        ShowPage(_current);
    }

    // -------- printing --------

    private void BtnPrintCurrent_Click(object sender, RoutedEventArgs e) => Print(new List<int> { _current });
    private void BtnPrintAll_Click(object sender, RoutedEventArgs e) => Print(Enumerable.Range(0, _pagesWpf.Count).ToList());

    private void BtnPrintRange_Click(object sender, RoutedEventArgs e)
    {
        var total = _pagesWpf.Count;
        var from = (int)Num(txtFrom.Text, 1, 1, total);
        var to = (int)Num(txtTo.Text, total, 1, total);
        if (from > to) (from, to) = (to, from);
        Print(Enumerable.Range(from - 1, to - from + 1).ToList());
    }

    private void Print(List<int> pageIndices)
    {
        pageIndices = pageIndices.Where(i => i >= 0 && i < _pagesGdi.Count).Distinct().ToList();
        if (pageIndices.Count == 0) return;

        var pd = new PrintDocument { DocumentName = Title };
        try
        {
            pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            var first = _pagesGdi[pageIndices[0]];
            var w = (int)Math.Round(first.Width / (double)RasterDpi * 100);
            var h = (int)Math.Round(first.Height / (double)RasterDpi * 100);
            if (w > 0 && h > 0)
                pd.DefaultPageSettings.PaperSize = new PaperSize("Custom", w, h);
        }
        catch { /* fall back to the printer's own default paper */ }

        var k = 0;
        pd.BeginPrint += (_, _) => k = 0;
        pd.PrintPage += (_, e) =>
        {
            var image = _pagesGdi[pageIndices[k]];
            var bounds = e.MarginBounds;
            var scale = Math.Min((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
            var w = image.Width * scale;
            var h = image.Height * scale;
            var x = bounds.Left + (bounds.Width - w) / 2f;
            var y = bounds.Top + (bounds.Height - h) / 2f;
            e.Graphics!.DrawImage(image, x, y, w, h);
            k++;
            e.HasMorePages = k < pageIndices.Count;
        };

        using var dlg = new WinForms.PrintDialog { Document = pd, UseEXDialog = true, AllowSomePages = true };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) { pd.Dispose(); return; }

        try
        {
            pd.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{LanguageManager.GetString("ErrorOccurred")}: {ex.Message}",
                LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            pd.Dispose();
        }
    }

    private void DisposePages()
    {
        foreach (var img in _pagesGdi) img.Dispose();
        _pagesGdi.Clear();
        _pagesWpf.Clear();
    }
}
