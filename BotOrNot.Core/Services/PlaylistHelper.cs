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
    /// Gets the display name for a playlist, or returns the raw playlist string if no mapping exists.
    /// </summary>
    public static string GetDisplayNameWithFallback(string? playlistName)
    {
        // Try exact match first
        var displayName = GetDisplayName(playlistName);
        if (displayName != null)
            return displayName;

        // Return raw playlist name if no mapping found
        return playlistName ?? "Unknown";
    }
}
