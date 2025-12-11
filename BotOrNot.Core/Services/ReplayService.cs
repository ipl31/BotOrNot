using BotOrNot.Core.Models;
using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Unreal.Core.Models.Enums;

namespace BotOrNot.Core.Services;

public interface IReplayService
{
    Task<ReplayData> LoadReplayAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class ReplayService : IReplayService
{
    private readonly ILogger<ReplayService> _logger;

    public ReplayService(ILogger<ReplayService>? logger = null)
    {
        _logger = logger ?? NullLogger<ReplayService>.Instance;
    }

    public async Task<ReplayData> LoadReplayAsync(string path, CancellationToken cancellationToken = default)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddFilter(_ => false)); // Suppress replay reader logs
        var readerLogger = loggerFactory.CreateLogger<ReplayReader>();
        var reader = new ReplayReader(readerLogger, ParseMode.Full);

        var result = await Task.Run(() => reader.ReadReplay(path), cancellationToken);

        var playersById = new Dictionary<string, PlayerRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            var id = ReflectionUtils.FirstString(pd, "PlayerId", "UniqueId", "NetId");
            var name = ReflectionUtils.FirstString(pd, "PlayerName", "DisplayName", "Name");
            var level = ReflectionUtils.FirstString(pd, "Level");
            var bot = ReflectionUtils.FirstString(pd, "IsBot");
            var platform = ReflectionUtils.FirstString(pd, "Platform");
            var kills = ReflectionUtils.FirstString(pd, "Kills");
            var death = ReflectionUtils.FirstString(pd, "DeathCause");

            var cosmetics = ReflectionUtils.GetObject(pd, "Cosmetics");
            var pickaxe = ReflectionUtils.FirstString(cosmetics, "Pickaxe") ?? "unknown";
            var glider = ReflectionUtils.FirstString(cosmetics, "Glider") ?? "unknown";

            var key = !string.IsNullOrWhiteSpace(id) ? id
                : !string.IsNullOrWhiteSpace(name) ? name
                : Guid.NewGuid().ToString("N");

            if (!playersById.TryGetValue(key, out var row))
            {
                row = new PlayerRow { Id = key };
                playersById[key] = row;
            }

            row.Name = string.IsNullOrWhiteSpace(name) ? (row.Name ?? "unknown") : name;
            row.Level = string.IsNullOrWhiteSpace(level) ? (row.Level ?? "unknown") : level;
            row.Bot = string.IsNullOrWhiteSpace(bot) ? (row.Bot ?? "unknown") : bot;
            row.Platform = platform ?? row.Platform;
            row.Kills = string.IsNullOrWhiteSpace(kills) ? (row.Kills ?? "unknown") : kills;
            row.DeathCause = string.IsNullOrWhiteSpace(death) ? (row.DeathCause ?? "unknown") : death;
            row.Pickaxe = pickaxe;
            row.Glider = glider;
        }

        // Find replay owner from PlayerData by checking IsReplayOwner property
        string? ownerId = null;
        string? ownerName = null;

        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            if (ReflectionUtils.GetBool(pd, "IsReplayOwner"))
            {
                ownerId = ReflectionUtils.FirstString(pd, "PlayerId", "EpicId", "Id");
                ownerName = ReflectionUtils.FirstString(pd, "PlayerName", "DisplayName", "Name");
                break;
            }
        }

        // Fallback: If no owner found, use the player with the most location data
        // (the replay owner's position is tracked much more frequently)
        if (string.IsNullOrEmpty(ownerId))
        {
            object? likelyOwner = null;
            int maxLocations = 0;

            foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
            {
                var locations = ReflectionUtils.GetObject(pd, "Locations");
                if (locations is System.Collections.IEnumerable enumerable)
                {
                    int count = 0;
                    foreach (var _ in enumerable) count++;

                    if (count > maxLocations)
                    {
                        maxLocations = count;
                        likelyOwner = pd;
                    }
                }
            }

            if (likelyOwner != null)
            {
                ownerId = ReflectionUtils.FirstString(likelyOwner, "PlayerId", "EpicId", "Id");
                ownerName = ReflectionUtils.FirstString(likelyOwner, "PlayerName", "DisplayName", "Name");
            }
        }

        var eliminations = new List<string>();
        var ownerEliminations = new List<PlayerRow>();

        foreach (var elim in result.Eliminations ?? Enumerable.Empty<object>())
        {
            // Get eliminated player info - check both nested object and direct property
            var elimInfo = ReflectionUtils.GetObject(elim, "EliminatedInfo");
            var eliminatedId = ReflectionUtils.FirstString(elimInfo, "Id")
                               ?? ReflectionUtils.FirstString(elim, "Eliminated")
                               ?? "unknown";

            // Get eliminator (killer) info - check both nested object and direct property
            var eliminatorInfo = ReflectionUtils.GetObject(elim, "EliminatorInfo");
            var eliminatorId = ReflectionUtils.FirstString(eliminatorInfo, "Id")
                               ?? ReflectionUtils.FirstString(elim, "Eliminator")
                               ?? "unknown";

            var display = playersById.TryGetValue(eliminatedId, out var row)
                ? $"{row.Name ?? row.Id} (bot={row.Bot}, kills={row.Kills})"
                : eliminatedId;

            eliminations.Add(display);

            // Check if this elimination was done by the replay owner
            var isOwnerKill = !string.IsNullOrEmpty(ownerId) &&
                              eliminatorId.Equals(ownerId, StringComparison.OrdinalIgnoreCase);

            if (isOwnerKill && playersById.TryGetValue(eliminatedId, out var victim))
            {
                // Add a copy of the victim's data to owner eliminations
                ownerEliminations.Add(new PlayerRow
                {
                    Id = victim.Id,
                    Name = victim.Name,
                    Level = victim.Level,
                    Bot = victim.Bot,
                    Platform = victim.Platform,
                    Kills = victim.Kills,
                    DeathCause = victim.DeathCause,
                    Pickaxe = victim.Pickaxe,
                    Glider = victim.Glider
                });
            }
        }

        var header = result.Header;

        // Parse game mode from GameSpecificData and CurrentPlaylist
        var gameMode = ParseGameMode(header?.GameSpecificData, result.GameData?.CurrentPlaylist);
        var playlist = result.GameData?.CurrentPlaylist ?? "";
        var maxPlayers = result.GameData?.MaxPlayers;
        var matchDuration = (result.Info?.LengthInMs ?? 0) / 1000.0 / 60.0;

        var metadata = new ReplayMetadata
        {
            FileName = Path.GetFileName(path),
            Version = $"{header?.Major}.{header?.Minor}",
            GameNetProtocol = header?.GameNetworkProtocolVersion.ToString() ?? "",
            PlayerCount = playersById.Count,
            EliminationCount = result.Eliminations?.Count ?? 0,
            GameMode = gameMode,
            Playlist = playlist,
            MaxPlayers = maxPlayers,
            MatchDurationMinutes = matchDuration
        };

        return new ReplayData
        {
            Players = playersById.Values.OrderBy(v => v.Name ?? v.Id).ToList(),
            Eliminations = eliminations,
            OwnerEliminations = ownerEliminations,
            OwnerName = ownerName,
            Metadata = metadata
        };
    }

    private static string ParseGameMode(IEnumerable<string>? gameSpecificData, string? playlist)
    {
        var parts = new List<string>();

        // Parse VerseURI from GameSpecificData for base mode
        string? verseUri = null;
        if (gameSpecificData != null)
        {
            foreach (var item in gameSpecificData)
            {
                if (item.StartsWith("VerseURI="))
                {
                    verseUri = item.Substring(9);
                    break;
                }
            }
        }

        // Determine base game mode from VerseURI first
        bool baseModeDetected = false;
        if (!string.IsNullOrEmpty(verseUri))
        {
            if (verseUri.Contains("BlitzRoot", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Blitz");
                baseModeDetected = true;
            }
            else if (verseUri.Contains("ReloadRoot", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Reload");
                baseModeDetected = true;
            }
            else if (verseUri.Contains("BRRoot", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Battle Royale");
                baseModeDetected = true;
            }
            else if (verseUri.Contains("Ranked", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Ranked");
                baseModeDetected = true;
            }
        }

        // Parse playlist for additional details and fallback base mode detection
        if (!string.IsNullOrEmpty(playlist))
        {
            // Fallback: detect base mode from playlist codenames if not found in VerseURI
            if (!baseModeDetected)
            {
                // Reload uses codename "Sunflower"
                if (playlist.Contains("Sunflower", StringComparison.OrdinalIgnoreCase))
                    parts.Add("Reload");
                // Blitz uses codenames like "ForbiddenFruit"
                else if (playlist.Contains("ForbiddenFruit", StringComparison.OrdinalIgnoreCase))
                    parts.Add("Blitz");
            }

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
        }

        // Fallback if nothing detected
        if (parts.Count == 0)
            return "Unknown";

        return string.Join(" ", parts);
    }
}