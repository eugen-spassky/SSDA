using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SSDA.App.Converters;

/// <summary>
/// Returns <c>true</c> when <paramref name="value"/> equals the converter parameter
/// (parsed against the enum type of <paramref name="value"/>). Used to drive nav-button
/// "active" state from a single enum.
/// </summary>
public sealed class EnumEqualsToBooleanConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        var match = Enum.Parse(value.GetType(), parameter.ToString()!, ignoreCase: true);
        return value.Equals(match);
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
