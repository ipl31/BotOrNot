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
            var deathTagsObj = ReflectionUtils.GetObject(pd, "DeathTags");
            var deathTagStrings = (deathTagsObj as System.Collections.IEnumerable)?
                .Cast<object>()
                .Select(t => t.ToString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
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
            row.DeathCause = DeathCauseHelper.GetDisplayName(death, deathTagStrings) is var resolved && resolved != "Unknown"
                ? resolved
                : (row.DeathCause ?? "Unknown");
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

        // Winners never died — show "N/A Won Match" instead of "Unknown"
        foreach (var row in playersById.Values)
        {
            if (row.IsWinner && row.DeathCause == "Unknown")
                row.DeathCause = "N/A Won Match";
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

        // Track pending knocks (consumed on finish) for owner elimination credit with 60-second window
        // Stores knock time so stale knocks (player revived in Reload) can be detected
        var pendingKnocks = new Dictionary<string, TimeSpan?>(StringComparer.OrdinalIgnoreCase);
        var ownerKnockTimes = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

        string? ownerEliminatedBy = null;

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

            var eventTime = ParseElimTime(ReflectionUtils.GetObject(elim, "Time"));

            if (isKnock)
            {
                pendingKnocks[eliminatedId] = eventTime;
                if (isOwnerAction && eventTime.HasValue)
                    ownerKnockTimes[eliminatedId] = eventTime.Value;
                else if (!isOwnerAction)
                    ownerKnockTimes.Remove(eliminatedId);
            }
            else
            {
                // Finish event — add to general eliminations list
                var display = playersById.TryGetValue(eliminatedId, out var row)
                    ? $"{row.Name ?? row.Id} (bot={row.Bot}, kills={row.Kills})"
                    : eliminatedId;
                eliminations.Add(display);

                // Track who eliminated the replay owner
                if (!string.IsNullOrEmpty(ownerId) &&
                    eliminatedId.Equals(ownerId, StringComparison.OrdinalIgnoreCase) &&
                    !eliminatorId.Equals(ownerId, StringComparison.OrdinalIgnoreCase))
                {
                    ownerEliminatedBy = playersById.TryGetValue(eliminatorId, out var eliminatorRow)
                        ? eliminatorRow.Name ?? eliminatorRow.Id
                        : eliminatorId;
                }

                // Credit owner for this elimination?
                var creditOwner = false;

                // A pending knock is stale if >60s have passed (player was revived)
                var hasActiveKnock = pendingKnocks.TryGetValue(eliminatedId, out var knockedAt)
                    && (!knockedAt.HasValue || !eventTime.HasValue
                        || (eventTime.Value - knockedAt.Value).TotalSeconds <= 60);

                if (!hasActiveKnock && isOwnerAction)
                {
                    // Direct finish — no active knock for this victim
                    creditOwner = true;
                }
                else if (ownerKnockTimes.TryGetValue(eliminatedId, out var knockTime)
                         && eventTime.HasValue
                         && (eventTime.Value - knockTime).TotalSeconds <= 60)
                {
                    // Owner knocked them and finish arrived within 60 seconds
                    creditOwner = true;
                }

                if (creditOwner && playersById.TryGetValue(eliminatedId, out var victim))
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

                pendingKnocks.Remove(eliminatedId);
                ownerKnockTimes.Remove(eliminatedId);
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
            OwnerEliminatedBy = ownerEliminatedBy,
            Metadata = metadata
        };
    }

    static TimeSpan? ParseElimTime(object? timeObj)
    {
        if (timeObj is TimeSpan ts) return ts;
        var str = timeObj?.ToString();
        if (string.IsNullOrWhiteSpace(str)) return null;
        var parts = str.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var min) && int.TryParse(parts[1], out var sec))
            return TimeSpan.FromSeconds(min * 60 + sec);
        if (TimeSpan.TryParse(str, out var parsed)) return parsed;
        return null;
    }
}