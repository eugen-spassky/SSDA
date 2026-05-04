using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SSDA.App.Converters;

/// <summary>
/// <c>Visible</c> when the bound string is non-null and non-empty; <c>Collapsed</c> otherwise.
/// Used to hide error banners while there is no error to show.
/// </summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
