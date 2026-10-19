using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using System.Threading;

namespace NHi.Inventory.Services;

public static class LanguageManager
{
    private static string _currentLanguage = "en";

    public static string CurrentLanguage => _currentLanguage;
    public static bool IsRTL => _currentLanguage == "ur";
    public static FlowDirection CurrentFlowDirection => IsRTL ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public static FontFamily? UrduFontFamily => Application.Current?.TryFindResource("UrduFontFamily") as FontFamily;

    public static event Action? LanguageChanged;

    public static void SetLanguage(string language)
    {
        _currentLanguage = language;
        var dictName = language == "ur" ? "Resources/Strings.ur.xaml" : "Resources/Strings.en.xaml";
        var newDict = new ResourceDictionary { Source = new Uri(dictName, UriKind.Relative) };

        var app = Application.Current;
        if (app != null)
        {
            var oldDict = app.Resources.MergedDictionaries
                .OfType<ResourceDictionary>()
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));

            if (oldDict != null)
            {
                var index = app.Resources.MergedDictionaries.IndexOf(oldDict);
                app.Resources.MergedDictionaries.RemoveAt(index);
                app.Resources.MergedDictionaries.Insert(index, newDict);
            }
            else
            {
                app.Resources.MergedDictionaries.Add(newDict);
            }

            var flowDirection = language == "ur" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            var urduFont = UrduFontFamily;
            foreach (Window window in app.Windows)
            {
                // The print preview window hosts a WinForms PrintPreviewControl showing the
                // actual page image — mirroring it would flip that image backwards, so it
                // always stays left-to-right regardless of the app's language.
                if (window is Views.Printing.UniversalPrintPreviewWindow) continue;
                window.FlowDirection = flowDirection;
                if (language == "ur" && urduFont != null) window.FontFamily = urduFont;
                else window.ClearValue(Control.FontFamilyProperty);
            }
        }

        Thread.CurrentThread.CurrentCulture = new CultureInfo(language == "ur" ? "ur-PK" : "en-US");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(language == "ur" ? "ur-PK" : "en-US");

        // Remember the choice so it's already in effect next launch (including on the login screen).
        AppPreferences.Language = _currentLanguage;

        LanguageChanged?.Invoke();
    }

    public static string GetString(string key)
    {
        if (Application.Current.TryFindResource(key) is string value)
            return value;
        return key;
    }
}
