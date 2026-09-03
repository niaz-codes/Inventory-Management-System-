using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace NHi.Inventory.Converters;

/// <summary>
/// Projects a ComboBox's SelectedItem into its closed-box display text directly, instead of
/// relying on WPF's built-in SelectionBoxItem/SelectionBoxItemTemplate auto-generation - that
/// mechanism doesn't reliably kick in for a fully custom ComboBox ControlTemplate, so a
/// DisplayMemberPath-bound item falls back to its raw ToString() (the object's full type name)
/// instead of the intended property. Handles both ItemsSource+DisplayMemberPath combos and
/// combos built from literal &lt;ComboBoxItem&gt; children (shows their own Content).
/// </summary>
public class DisplayMemberConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not { } item) return string.Empty;

        if (item is ComboBoxItem cbi) return cbi.Content?.ToString() ?? string.Empty;

        var path = values[1] as string;
        if (string.IsNullOrEmpty(path)) return item.ToString() ?? string.Empty;

        var value = item.GetType().GetProperty(path)?.GetValue(item);
        return value?.ToString() ?? string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
