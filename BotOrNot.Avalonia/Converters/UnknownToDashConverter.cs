using System.Globalization;
using Avalonia.Data.Converters;

namespace BotOrNot.Avalonia.Converters;

public class UnknownToDashConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return "\u2014"; // em dash
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
