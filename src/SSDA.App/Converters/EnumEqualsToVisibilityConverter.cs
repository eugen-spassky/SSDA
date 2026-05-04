using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SSDA.App.Converters;

/// <summary>
/// Returns <see cref="Visibility.Visible"/> when <paramref name="value"/> equals the
/// converter parameter (parsed against the enum type of <paramref name="value"/>) and
/// <see cref="Visibility.Collapsed"/> otherwise. Use this when binding a step-state
/// enum directly onto a <c>Visibility</c> property — binding the bool-returning
/// <see cref="EnumEqualsToBooleanConverter"/> there silently coerces <c>true</c> to
/// <c>Hidden</c> via the underlying integer value, inverting the intended behavior.
/// </summary>
public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return Visibility.Collapsed;
        var match = Enum.Parse(value.GetType(), parameter.ToString()!, ignoreCase: true);
        return value.Equals(match) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
