using System.Reflection;
using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Unreal.Core.Models.Enums;

public static class DumpPlayerElims
{
    public static void Run(string replayPath, string searchName)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddFilter(_ => false));
        var logger = loggerFactory.CreateLogger<ReplayReader>();
        var reader = new ReplayReader(logger, ParseMode.Full);
        var result = reader.ReadReplay(replayPath);

        // Build player name lookup
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            var id = GetStr(pd, "PlayerId", "UniqueId", "NetId");
            var name = GetStr(pd, "PlayerName", "DisplayName", "Name");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                names[id] = name;
        }

        // Find the target player's ID(s)
        var targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in names)
        {
            if (kvp.Value.Contains(searchName, StringComparison.OrdinalIgnoreCase))
                targetIds.Add(kvp.Key);
        }

        Console.WriteLine($"=== Events involving '{searchName}' ===");
        Console.WriteLine($"Matching player IDs: {string.Join(", ", targetIds.Select(id => $"{id} ({names.GetValueOrDefault(id, "?")})"))}");
        Console.WriteLine();

        int idx = 0;
        int matchCount = 0;
        foreach (var elim in result.Eliminations ?? Enumerable.Empty<object>())
        {
            var eInfo = GetObj(elim, "EliminatedInfo");
            var rInfo = GetObj(elim, "EliminatorInfo");
            var eliminatedId = GetStr(eInfo, "Id") ?? GetStr(elim, "Eliminated") ?? "unknown";
            var eliminatorId = GetStr(rInfo, "Id") ?? GetStr(elim, "Eliminator") ?? "unknown";

            var involves = targetIds.Contains(eliminatedId) || targetIds.Contains(eliminatorId);
            if (!involves)
            {
                idx++;
                continue;
            }

            matchCount++;
            var eliminatedName = names.GetValueOrDefault(eliminatedId, "?");
            var eliminatorName = names.GetValueOrDefault(eliminatorId, "?");
            var knocked = GetObj(elim, "Knocked");
            var time = GetStr(elim, "Time");
            var gunType = GetObj(elim, "GunType");
            var isSelf = GetObj(elim, "IsSelfElimination");
            var distance = GetObj(elim, "Distance");

            Console.WriteLine($"--- Event [{idx}] ---");
            Console.WriteLine($"  Time: {time}");
            Console.WriteLine($"  Knocked: {knocked}");
            Console.WriteLine($"  GunType: {gunType}");
            Console.WriteLine($"  IsSelfElimination: {isSelf}");
            Console.WriteLine($"  Distance: {distance}");

            Console.WriteLine($"  EliminatedInfo:");
            DumpObj(eInfo, "    ");
            Console.WriteLine($"    PlayerName: {eliminatedName}");

            Console.WriteLine($"  EliminatorInfo:");
            DumpObj(rInfo, "    ");
            Console.WriteLine($"    PlayerName: {eliminatorName}");

            Console.WriteLine();
            idx++;
        }

        Console.WriteLine($"Total events involving '{searchName}': {matchCount}");
    }

    static void DumpObj(object? obj, string indent)
    {
        if (obj == null) { Console.WriteLine($"{indent}(null)"); return; }
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = prop.GetValue(obj);
            Console.WriteLine($"{indent}{prop.Name}: {val}");
        }
    }

    static string? GetStr(object? obj, params string[] propNames)
    {
        if (obj is null) return null;
        var t = obj.GetType();
        foreach (var n in propNames)
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
}
