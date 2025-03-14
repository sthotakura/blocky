using System.Globalization;
using System.Windows.Data;

namespace Blocky.Converters;

public class BoolToStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value == null || !(bool)value ? "Stopped" : "Running";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}