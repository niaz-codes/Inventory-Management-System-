using System.Windows;

namespace NHi.Inventory.Services;

/// <summary>Swaps the app's light/dark color dictionary at runtime, mirroring how
/// LanguageManager swaps the Strings.*.xaml dictionary — every {DynamicResource}
/// reference to a themed brush (LightBgBrush, CardBrush, TextPrimaryBrush,
/// TextSecondaryBrush, BorderBrush, HoverBrush) repaints automatically.</summary>
public static class ThemeManager
{
    private static string _currentTheme = "light";

    public static string CurrentTheme => _currentTheme;
    public static bool IsDark => _currentTheme == "dark";

    public static event Action? ThemeChanged;

    public static void SetTheme(string theme)
    {
        _currentTheme = theme == "dark" ? "dark" : "light";
        var dictName = _currentTheme == "dark" ? "Themes/Colors.Dark.xaml" : "Themes/Colors.Light.xaml";
        var newDict = new ResourceDictionary { Source = new Uri(dictName, UriKind.Relative) };

        var app = Application.Current;
        if (app != null)
        {
            var oldDict = app.Resources.MergedDictionaries
                .OfType<ResourceDictionary>()
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Colors."));

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
        }

        ThemeChanged?.Invoke();
    }

    public static void ToggleTheme() => SetTheme(IsDark ? "light" : "dark");
}
