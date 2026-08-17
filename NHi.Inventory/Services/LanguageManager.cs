using System.Windows;
using System.Globalization;
using System.Threading;

namespace NHi.Inventory.Services;

public static class LanguageManager
{
    private static string _currentLanguage = "en";

    public static string CurrentLanguage => _currentLanguage;
    public static bool IsRTL => _currentLanguage == "ur";
    public static FlowDirection CurrentFlowDirection => IsRTL ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

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
            foreach (Window window in app.Windows)
            {
                window.FlowDirection = flowDirection;
            }
        }

        Thread.CurrentThread.CurrentCulture = new CultureInfo(language == "ur" ? "ur-PK" : "en-US");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(language == "ur" ? "ur-PK" : "en-US");

        LanguageChanged?.Invoke();
    }

    public static string GetString(string key)
    {
        if (Application.Current.TryFindResource(key) is string value)
            return value;
        return key;
    }
}
