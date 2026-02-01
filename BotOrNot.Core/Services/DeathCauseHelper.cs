namespace BotOrNot.Core.Services;

public static class DeathCauseHelper
{
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

        if (int.TryParse(deathCauseValue, out var code) && DeathCauses.TryGetValue(code, out var name))
            return $"{name} ({code})";

        // Return original value if not a recognized integer code
        return deathCauseValue;
    }
}
