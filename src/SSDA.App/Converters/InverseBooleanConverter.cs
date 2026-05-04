using System.Globalization;
using System.Windows.Data;

namespace SSDA.App.Converters;

/// <summary>Negates a <see cref="bool"/>; useful for binding <c>IsEnabled</c> against <c>IsLoading</c>.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : false;
}
