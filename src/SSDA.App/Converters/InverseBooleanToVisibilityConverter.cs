using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SSDA.App.Converters;

/// <summary>Maps <c>false</c> → <c>Visible</c> and <c>true</c> → <c>Collapsed</c>.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Collapsed;
}
