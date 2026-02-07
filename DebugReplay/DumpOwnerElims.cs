using System.Collections;
using System.Reflection;
using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Unreal.Core.Models.Enums;
using BotOrNot.Core.Services;

public static class DumpOwnerElims
{
    public static async Task RunAsync(string replayPath)
    {
        // First, use BotOrNot service to get processed data
        var service = new ReplayService();
        var data = await service.LoadReplayAsync(replayPath);

        Console.WriteLine($"=== {Path.GetFileName(replayPath)} ===");
        Console.WriteLine($"Owner: {data.OwnerName}");
        Console.WriteLine($"OwnerKills (from PlayerData): {data.OwnerKills}");
        Console.WriteLine($"OwnerEliminations count (non-NPC): {data.OwnerEliminations.Count(p => !p.IsNpc)}");
        Console.WriteLine($"OwnerEliminations listed:");
        foreach (var e in data.OwnerEliminations.Where(p => !p.IsNpc))
            Console.WriteLine($"  - {e.Name} (bot={e.Bot}, kills={e.Kills})");

        // Now raw dump of all elimination events from the replay
        Console.WriteLine($"\n=== RAW ELIMINATION EVENTS ===");
        using var loggerFactory = LoggerFactory.Create(b => b.AddFilter(_ => false));
        var logger = loggerFactory.CreateLogger<ReplayReader>();
        var reader = new ReplayReader(logger, ParseMode.Full);
        var result = reader.ReadReplay(replayPath);

        // Find owner ID
        string? ownerId = null;
        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            if (GetBool(pd, "IsReplayOwner"))
            {
                ownerId = GetStr(pd, "PlayerId", "UniqueId", "NetId");
                break;
            }
        }
        Console.WriteLine($"Owner ID: {ownerId}\n");

        // Build player name lookup
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            var id = GetStr(pd, "PlayerId", "UniqueId", "NetId");
            var name = GetStr(pd, "PlayerName", "DisplayName", "Name");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                names[id] = name;
        }

        // Track who knocked each victim
        var knockedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int idx = 0;
        foreach (var elim in result.Eliminations ?? Enumerable.Empty<object>())
        {
            var isKnock = GetBool(elim, "Knocked");
            var eliminatedId = GetStr(GetObj(elim, "EliminatedInfo"), "Id")
                ?? GetStr(elim, "Eliminated") ?? "unknown";
            var eliminatorId = GetStr(GetObj(elim, "EliminatorInfo"), "Id")
                ?? GetStr(elim, "Eliminator") ?? "unknown";

            if (isKnock)
                knockedBy[eliminatedId] = eliminatorId;

            var involvesOwner = (!string.IsNullOrEmpty(ownerId)) &&
                (eliminatedId.Equals(ownerId, StringComparison.OrdinalIgnoreCase) ||
                 eliminatorId.Equals(ownerId, StringComparison.OrdinalIgnoreCase));

            if (involvesOwner)
            {
                var eliminatedName = names.GetValueOrDefault(eliminatedId, "?");
                var eliminatorName = names.GetValueOrDefault(eliminatorId, "?");
                var tag = isKnock ? "KNOCK" : "FINISH";
                var role = eliminatorId.Equals(ownerId, StringComparison.OrdinalIgnoreCase) ? "OWNER_KILLS" : "OWNER_DIES";

                // For finishes, show who originally knocked the victim
                var knockInfo = "";
                if (!isKnock && knockedBy.TryGetValue(eliminatedId, out var knockerId))
                {
                    var knockerName = names.GetValueOrDefault(knockerId, "?");
                    var knockerIsOwner = knockerId.Equals(ownerId, StringComparison.OrdinalIgnoreCase);
                    knockInfo = $" [knocked by: {knockerName} ({(knockerIsOwner ? "OWNER" : "OTHER")})]";
                }

                Console.WriteLine($"[{idx,3}] {tag,-6} {role,-12} | {eliminatorName} ({eliminatorId}) -> {eliminatedName} ({eliminatedId}){knockInfo}");
            }
            idx++;
        }
    }

    static string? GetStr(object? obj, params string[] names)
    {
        if (obj is null) return null;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var pi = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null) continue;
            var v = pi.GetValue(obj)?.ToString();
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }

    static object? GetObj(object? obj, string name)
    {
        if (obj is null) return null;
        var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return pi?.GetValue(obj);
    }

    static bool GetBool(object? obj, string name)
    {
        if (obj is null) return false;
        var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (pi == null) return false;
        var v = pi.GetValue(obj);
        if (v is bool b) return b;
        return false;
    }
}
