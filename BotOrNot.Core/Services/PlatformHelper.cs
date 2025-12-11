namespace BotOrNot.Core.Services;

public static class PlatformHelper
{
    private static readonly Dictionary<string, string> PlatformNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows/PC
        { "WIN", "PC" },
        { "Windows", "PC" },
        { "PC", "PC" },

        // PlayStation
        { "PS4", "PlayStation" },
        { "PS5", "PlayStation" },
        { "PSN", "PlayStation" },
        { "PlayStation", "PlayStation" },

        // Xbox
        { "XBL", "Xbox" },
        { "XB1", "Xbox" },
        { "XSX", "Xbox" },
        { "Xbox", "Xbox" },
        { "XboxOne", "Xbox" },
        { "XboxSeriesX", "Xbox" },

        // Nintendo Switch
        { "SWT", "Switch" },
        { "Switch", "Switch" },
        { "Nintendo", "Switch" },

        // Mobile
        { "AND", "Mobile" },
        { "IOS", "Mobile" },
        { "Android", "Mobile" },
        { "Mobile", "Mobile" },

        // Mac
        { "MAC", "Mac" },
    };

    public static string GetFriendlyName(string? platformCode)
    {
        if (string.IsNullOrWhiteSpace(platformCode))
            return "Unknown";

        return PlatformNames.TryGetValue(platformCode, out var friendly)
            ? friendly
            : platformCode;
    }
}
