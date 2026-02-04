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
            var teamIndex = ReflectionUtils.FirstString(pd, "TeamIndex");
            var death = ReflectionUtils.FirstString(pd, "DeathCause");
            var placement = ReflectionUtils.FirstString(pd, "Placement");

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
            row.TeamIndex = string.IsNullOrWhiteSpace(teamIndex) ? (row.TeamIndex ?? "unknown") : teamIndex;
            row.DeathCause = string.IsNullOrWhiteSpace(death)
                ? (row.DeathCause ?? "Unknown")
                : DeathCauseHelper.GetDisplayName(death);
            row.Placement = string.IsNullOrWhiteSpace(placement) ? null : placement;
            row.Pickaxe = pickaxe;
            row.Glider = glider;
        }

        // Build team data lookup for TeamKills and team placement
        var teamKillsByIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var teamPlacementByIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var team in result.TeamData ?? Enumerable.Empty<object>())
        {
            var teamIdx = ReflectionUtils.FirstString(team, "TeamIndex");
            var teamKills = ReflectionUtils.FirstString(team, "TeamKills");
            var teamPlacement = ReflectionUtils.FirstString(team, "Placement");

            if (!string.IsNullOrWhiteSpace(teamIdx))
            {
                if (!string.IsNullOrWhiteSpace(teamKills))
                    teamKillsByIndex[teamIdx] = teamKills;
                if (!string.IsNullOrWhiteSpace(teamPlacement))
                    teamPlacementByIndex[teamIdx] = teamPlacement;
            }
        }

        // Apply team kills and placement to players
        foreach (var row in playersById.Values)
        {
            if (!string.IsNullOrWhiteSpace(row.TeamIndex))
            {
                if (teamKillsByIndex.TryGetValue(row.TeamIndex, out var teamKills))
                    row.TeamKills = teamKills;
                // If player doesn't have placement but team does, use team placement
                if (string.IsNullOrWhiteSpace(row.Placement) && teamPlacementByIndex.TryGetValue(row.TeamIndex, out var teamPlacement))
                    row.Placement = teamPlacement;
            }
        }

        // Build a lookup by numeric Id for winning player resolution
        var playersByNumericId = new Dictionary<string, PlayerRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            var numericId = ReflectionUtils.FirstString(pd, "Id");
            var playerId = ReflectionUtils.FirstString(pd, "PlayerId", "UniqueId", "NetId");

            if (!string.IsNullOrWhiteSpace(numericId) && !string.IsNullOrWhiteSpace(playerId))
            {
                if (playersById.TryGetValue(playerId, out var row))
                    playersByNumericId[numericId] = row;
            }
        }

        // Extract winning team info from GameData
        int? winningTeam = null;
        var winningPlayerIds = new List<string>();
        var winningPlayerNames = new List<string>();

        if (result.GameData != null)
        {
            var winTeamObj = ReflectionUtils.GetObject(result.GameData, "WinningTeam");
            if (winTeamObj is int wt)
                winningTeam = wt;
            else if (int.TryParse(winTeamObj?.ToString(), out var parsed))
                winningTeam = parsed;

            var winIds = ReflectionUtils.GetObject(result.GameData, "WinningPlayerIds");
            if (winIds is System.Collections.IEnumerable enumerable)
            {
                foreach (var id in enumerable)
                {
                    var idStr = id?.ToString();
                    if (!string.IsNullOrWhiteSpace(idStr))
                    {
                        winningPlayerIds.Add(idStr);
                        // Look up player name by numeric Id
                        if (playersByNumericId.TryGetValue(idStr, out var player) && !string.IsNullOrWhiteSpace(player.Name))
                            winningPlayerNames.Add(player.Name);
                    }
                }
            }
        }

        // Mark winning team players with placement if they don't have it
        if (winningTeam.HasValue)
        {
            var winTeamStr = winningTeam.Value.ToString();
            foreach (var row in playersById.Values)
            {
                if (row.TeamIndex == winTeamStr && string.IsNullOrWhiteSpace(row.Placement))
                    row.Placement = "1";
            }
        }

        // Find replay owner from PlayerData by checking IsReplayOwner property
        string? ownerId = null;
        string? ownerName = null;
        int? ownerKills = null;

        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            if (ReflectionUtils.GetBool(pd, "IsReplayOwner"))
            {
                ownerId = ReflectionUtils.FirstString(pd, "PlayerId", "EpicId", "Id");
                ownerName = ReflectionUtils.FirstString(pd, "PlayerName", "DisplayName", "Name");
                var killsStr = ReflectionUtils.FirstString(pd, "Kills");
                if (int.TryParse(killsStr, out var kills))
                    ownerKills = kills;
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
                var killsStr = ReflectionUtils.FirstString(likelyOwner, "Kills");
                if (int.TryParse(killsStr, out var kills))
                    ownerKills = kills;
            }
        }

        var eliminations = new List<string>();
        var ownerEliminations = new List<PlayerRow>();

        // Track knock and finish states for proper elimination counting
        // An elimination counts when: you knock someone AND they get finished (by anyone)
        // Or: you directly eliminate a solo/last-standing player (who can't be knocked)
        var everKnocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knockedByOwner = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var finishedPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: identify all knocked players and track who the owner knocked
        foreach (var elim in result.Eliminations ?? Enumerable.Empty<object>())
        {
            var isKnock = ReflectionUtils.GetBool(elim, "Knocked");
            var eliminatedId = ReflectionUtils.FirstString(
                ReflectionUtils.GetObject(elim, "EliminatedInfo"), "Id")
                ?? ReflectionUtils.FirstString(elim, "Eliminated")
                ?? "unknown";
            var eliminatorId = ReflectionUtils.FirstString(
                ReflectionUtils.GetObject(elim, "EliminatorInfo"), "Id")
                ?? ReflectionUtils.FirstString(elim, "Eliminator")
                ?? "unknown";

            var isOwnerAction = !string.IsNullOrEmpty(ownerId) &&
                                eliminatorId.Equals(ownerId, StringComparison.OrdinalIgnoreCase);

            if (isKnock)
            {
                everKnocked.Add(eliminatedId);
                if (isOwnerAction)
                    knockedByOwner.Add(eliminatedId);
            }
            else
            {
                finishedPlayers.Add(eliminatedId);
            }
        }

        // Second pass: build elimination lists (only finishes, not knocks)
        foreach (var elim in result.Eliminations ?? Enumerable.Empty<object>())
        {
            var isKnock = ReflectionUtils.GetBool(elim, "Knocked");

            // Skip knockdowns - only show actual eliminations (finished players)
            if (isKnock)
                continue;

            var elimInfo = ReflectionUtils.GetObject(elim, "EliminatedInfo");
            var eliminatedId = ReflectionUtils.FirstString(elimInfo, "Id")
                               ?? ReflectionUtils.FirstString(elim, "Eliminated")
                               ?? "unknown";

            var display = playersById.TryGetValue(eliminatedId, out var row)
                ? $"{row.Name ?? row.Id} (bot={row.Bot}, kills={row.Kills})"
                : eliminatedId;

            eliminations.Add(display);
        }

        // Build owner eliminations: knocks by owner that resulted in finish + direct eliminations
        foreach (var victimId in knockedByOwner.Where(id => finishedPlayers.Contains(id)))
        {
            if (playersById.TryGetValue(victimId, out var victim))
            {
                ownerEliminations.Add(new PlayerRow
                {
                    Id = victim.Id,
                    Name = victim.Name,
                    Level = victim.Level,
                    Bot = victim.Bot,
                    Platform = victim.Platform,
                    Kills = victim.Kills,
                    TeamIndex = victim.TeamIndex,
                    DeathCause = victim.DeathCause,
                    Pickaxe = victim.Pickaxe,
                    Glider = victim.Glider
                });
            }
        }

        // Add direct eliminations by owner (solo/last-standing players who were never knocked)
        foreach (var elim in result.Eliminations ?? Enumerable.Empty<object>())
        {
            var isKnock = ReflectionUtils.GetBool(elim, "Knocked");
            if (isKnock) continue;

            var eliminatedId = ReflectionUtils.FirstString(
                ReflectionUtils.GetObject(elim, "EliminatedInfo"), "Id")
                ?? ReflectionUtils.FirstString(elim, "Eliminated")
                ?? "unknown";
            var eliminatorId = ReflectionUtils.FirstString(
                ReflectionUtils.GetObject(elim, "EliminatorInfo"), "Id")
                ?? ReflectionUtils.FirstString(elim, "Eliminator")
                ?? "unknown";

            var isOwnerKill = !string.IsNullOrEmpty(ownerId) &&
                              eliminatorId.Equals(ownerId, StringComparison.OrdinalIgnoreCase);

            // Direct elimination: owner finished someone who was never knocked
            if (isOwnerKill && !everKnocked.Contains(eliminatedId))
            {
                if (playersById.TryGetValue(eliminatedId, out var victim))
                {
                    ownerEliminations.Add(new PlayerRow
                    {
                        Id = victim.Id,
                        Name = victim.Name,
                        Level = victim.Level,
                        Bot = victim.Bot,
                        Platform = victim.Platform,
                        Kills = victim.Kills,
                        TeamIndex = victim.TeamIndex,
                        DeathCause = victim.DeathCause,
                        Pickaxe = victim.Pickaxe,
                        Glider = victim.Glider
                    });
                }
            }
        }

        var header = result.Header;

        // Get game mode display name from playlist mapping
        var playlist = result.GameData?.CurrentPlaylist ?? "";
        var gameMode = PlaylistHelper.GetDisplayNameWithFallback(playlist);
        var maxPlayers = result.GameData?.MaxPlayers;
        var matchDuration = (result.Info?.LengthInMs ?? 0) / 1000.0 / 60.0;

        var metadata = new ReplayMetadata
        {
            FileName = Path.GetFileName(path),
            Version = $"{header?.Major}.{header?.Minor}",
            GameNetProtocol = header?.GameNetworkProtocolVersion.ToString() ?? "",
            PlayerCount = playersById.Values.Count(p => !p.IsNpc),
            EliminationCount = eliminations.Count,
            GameMode = gameMode,
            Playlist = playlist,
            MaxPlayers = maxPlayers,
            MatchDurationMinutes = matchDuration,
            WinningTeam = winningTeam,
            WinningPlayerIds = winningPlayerIds,
            WinningPlayerNames = winningPlayerNames
        };

        return new ReplayData
        {
            Players = playersById.Values.OrderBy(v => v.Name ?? v.Id).ToList(),
            Eliminations = eliminations,
            OwnerEliminations = ownerEliminations,
            OwnerName = ownerName,
            OwnerKills = ownerKills,
            Metadata = metadata
        };
    }

}