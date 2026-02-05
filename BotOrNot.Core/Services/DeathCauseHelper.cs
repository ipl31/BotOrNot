namespace BotOrNot.Core.Services;

public static class DeathCauseHelper
{
    private static readonly Dictionary<string, string> WeaponTagMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Area51Gun", "Arc Gun" }
    };

    private static readonly Dictionary<int, string> DeathCauses = new()
    {
        { 0, "Storm" },
        { 1, "Fall Damage" },
        { 2, "Pistol" },
        { 3, "Shotgun" },
        { 4, "Rifle" },
        { 5, "SMG" },
        { 6, "Sniper" },
        { 7, "Sniper No Scope" },
        { 8, "Melee" },
        { 9, "Infinity Blade" },
        { 10, "Grenade" },
        { 11, "C4" },
        { 12, "Grenade Launcher" },
        { 13, "Rocket Launcher" },
        { 14, "Minigun" },
        { 15, "Bow" },
        { 16, "Trap" },
        { 17, "Bled Out" },
        { 18, "Banhammer" },
        { 19, "Removed From Game" },
        { 20, "Boss Melee" },
        { 21, "Boss Dive Attack" },
        { 22, "Boss Ranged" },
        { 23, "Vehicle" },
        { 24, "Shopping Cart" },
        { 25, "ATK" },
        { 26, "Quad Crasher" },
        { 27, "Biplane" },
        { 28, "Biplane Gun" },
        { 29, "LMG" },
        { 30, "Stink Bomb" },
        { 31, "Environmental" },
        { 32, "Fell Out Of World" },
        { 33, "Under Landscape" },
        { 34, "Turret" },
        { 35, "Ship Cannon" },
        { 36, "Cube" },
        { 37, "Balloon" },
        { 38, "Storm Surge" },
        { 39, "Lava" },
        { 40, "Zombie" },
        { 41, "Elite Zombie" },
        { 42, "Ranged Zombie" },
        { 43, "Brute" },
        { 44, "Elite Brute" },
        { 45, "Mega Brute" },
        { 46, "Switched To Spectate" },
        { 47, "Logged Out" },
        { 48, "Team Switch" },
        { 49, "Won Match" },
        { 50, "Unspecified" },
        { 51, "MAX" }
    };

    /// <summary>
    /// Converts a death cause code to a human-readable string with the code in parentheses.
    /// </summary>
    /// <param name="deathCauseValue">The raw death cause value (integer as string)</param>
    /// <returns>Human-readable death cause like "Storm (0)" or the original value if not recognized</returns>
    public static string GetDisplayName(string? deathCauseValue)
    {
        if (string.IsNullOrWhiteSpace(deathCauseValue))
            return "Unknown";

        if (int.TryParse(deathCauseValue, out var code))
        {
            if (DeathCauses.TryGetValue(code, out var name))
                return $"{name} ({code})";

            // Known integer but not in our mapping
            return $"Unknown ({code})";
        }

        // Return original value if not a recognized integer code
        return deathCauseValue;
    }

    /// <summary>
    /// Converts a death cause code to a human-readable string, falling back to death tags
    /// when the numeric code is null or missing.
    /// </summary>
    public static string GetDisplayName(string? deathCauseValue, IEnumerable<string>? deathTags)
    {
        // Try numeric code resolution first
        if (!string.IsNullOrWhiteSpace(deathCauseValue))
            return GetDisplayName(deathCauseValue);

        // Fall back to death tag resolution
        if (deathTags != null)
        {
            foreach (var tag in deathTags)
            {
                if (tag.StartsWith("Item.Weapon.", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var (key, displayName) in WeaponTagMappings)
                    {
                        if (tag.Contains(key, StringComparison.OrdinalIgnoreCase))
                            return displayName;
                    }
                }
            }
        }

        return "Unknown";
    }
}
