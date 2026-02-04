using System.Reflection;
using System.Text.Json;

namespace BotOrNot.Core.Services;

/// <summary>
/// Provides playlist name to display name mapping using data from Fortnite's content API.
/// Data source: https://fortnitecontent-website-prod07.ol.epicgames.com/content/api/pages/fortnite-game/
/// </summary>
public static class PlaylistHelper
{
    private static readonly Dictionary<string, string> PlaylistMappings;

    static PlaylistHelper()
    {
        PlaylistMappings = LoadPlaylistMappings();
    }

    private static Dictionary<string, string> LoadPlaylistMappings()
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "BotOrNot.Core.Data.PlaylistMappings.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return mappings;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("playlists", out var playlists))
            {
                foreach (var playlist in playlists.EnumerateArray())
                {
                    if (playlist.TryGetProperty("playlist_name", out var nameElement) &&
                        playlist.TryGetProperty("display_name", out var displayElement))
                    {
                        var name = nameElement.GetString();
                        var display = displayElement.GetString();

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(display))
                        {
                            mappings[name] = display;
                        }
                    }
                }
            }
        }
        catch
        {
            // If loading fails, return empty mappings - fallback logic will handle it
        }

        return mappings;
    }

    /// <summary>
    /// Gets the display name for a playlist. Returns null if no mapping exists.
    /// </summary>
    public static string? GetDisplayName(string? playlistName)
    {
        if (string.IsNullOrEmpty(playlistName))
            return null;

        return PlaylistMappings.TryGetValue(playlistName, out var displayName) ? displayName : null;
    }

    /// <summary>
    /// Gets the display name for a playlist, or falls back to pattern-based detection.
    /// </summary>
    public static string GetDisplayNameWithFallback(string? playlistName)
    {
        // Try exact match first
        var displayName = GetDisplayName(playlistName);
        if (displayName != null)
            return displayName;

        // Fallback to pattern-based detection
        return ParsePlaylistFallback(playlistName);
    }

    private static string ParsePlaylistFallback(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist))
            return "Unknown";

        var parts = new List<string>();

        // Detect base mode
        if (playlist.Contains("Sunflower", StringComparison.OrdinalIgnoreCase))
            parts.Add("Reload");
        else if (playlist.Contains("ForbiddenFruit", StringComparison.OrdinalIgnoreCase) ||
                 playlist.Contains("Blitz", StringComparison.OrdinalIgnoreCase))
            parts.Add("Blitz");
        else if (playlist.Contains("Ranked", StringComparison.OrdinalIgnoreCase) ||
                 playlist.Contains("Comp_", StringComparison.OrdinalIgnoreCase))
            parts.Add("Ranked");
        else if (playlist.Contains("Creative", StringComparison.OrdinalIgnoreCase))
            parts.Add("Creative");

        // Check for Zero Build
        if (playlist.Contains("NoBuild", StringComparison.OrdinalIgnoreCase))
            parts.Add("Zero Build");

        // Check for team size
        if (playlist.Contains("Solo", StringComparison.OrdinalIgnoreCase))
            parts.Add("Solo");
        else if (playlist.Contains("Duo", StringComparison.OrdinalIgnoreCase))
            parts.Add("Duos");
        else if (playlist.Contains("Trio", StringComparison.OrdinalIgnoreCase))
            parts.Add("Trios");
        else if (playlist.Contains("Squad", StringComparison.OrdinalIgnoreCase))
            parts.Add("Squads");

        if (parts.Count == 0)
            return playlist; // Return raw name if nothing detected

        return string.Join(" - ", parts);
    }
}
