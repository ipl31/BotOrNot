using System.Globalization;
using Avalonia.Data.Converters;

namespace BotOrNot.Avalonia.Converters;

public class SquadDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s) && !s.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return $"Squad # {s}";
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
