using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BotOrNot.Avalonia.Converters;

public class BotColorConverter : IValueConverter
{
    public static readonly BotColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string bot && bot.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Colors.Green);
        }
        if (value is string notBot && notBot.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Colors.Red);
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BotBorderColorConverter : IValueConverter
{
    public static readonly BotBorderColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string bot && bot.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Colors.DarkGreen);
        }
        if (value is string notBot && notBot.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Colors.DarkRed);
        }
        return new SolidColorBrush(Colors.DarkGray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}