using NHi.Inventory.Views.Printing;
using QuestPDF.Infrastructure;

namespace NHi.Inventory.Services;

/// <summary>
/// Thin entry point kept for every existing caller: hands the QuestPDF document to the
/// app-wide <see cref="UniversalPrintPreviewWindow"/>, which rasterises it, shows a
/// professional multi-page preview (numbered navigation, 25–500% zoom, live info line)
/// and prints selected pages through the Windows print dialog.
/// </summary>
public static class PrintingService
{
    public static void ShowPrintPreview(IDocument document, string documentTitle)
    {
        var window = new UniversalPrintPreviewWindow(document, documentTitle);
        if (System.Windows.Application.Current?.Windows.Count > 0)
        {
            foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
            {
                if (w.IsActive) { window.Owner = w; break; }
            }
        }
        window.ShowDialog();
    }
}
