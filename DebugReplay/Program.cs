using System.Reflection;
using System.Collections;
using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Unreal.Core.Models.Enums;

string? replayPath = args.Length > 0 ? args[0] : null;

if (string.IsNullOrEmpty(replayPath) || !File.Exists(replayPath))
{
    Console.WriteLine("Please provide a path to a .replay file as an argument");
    return;
}

Console.WriteLine($"=== ANALYZING: {Path.GetFileName(replayPath)} ===");
Console.WriteLine($"Path: {replayPath}\n");

using var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
var logger = loggerFactory.CreateLogger<ReplayReader>();
var reader = new ReplayReader(logger, ParseMode.Full);
var result = reader.ReadReplay(replayPath);

// Key indicators
Console.WriteLine("KEY MODE INDICATORS:");
Console.WriteLine("--------------------");

// Header.GameSpecificData
Console.WriteLine($"GameSpecificData: {string.Join(", ", result.Header?.GameSpecificData ?? Array.Empty<string>())}");

// Match duration
Console.WriteLine($"Match Duration: {result.Info?.LengthInMs / 1000.0 / 60.0:F1} minutes ({result.Info?.LengthInMs}ms)");

// Player counts
var playerCount = result.PlayerData?.Count() ?? 0;
Console.WriteLine($"Total Players in Replay: {playerCount}");

// Team data
var teamCount = 0;
if (result.TeamData != null)
{
    foreach (var _ in result.TeamData) teamCount++;
}
Console.WriteLine($"Teams in Replay: {teamCount}");

// Eliminations
var elimCount = result.Eliminations?.Count ?? 0;
Console.WriteLine($"Total Eliminations: {elimCount}");

// GameData fields
Console.WriteLine($"\nGAMEDATA:");
Console.WriteLine($"  CurrentPlaylist: {result.GameData?.CurrentPlaylist ?? "(null)"}");
Console.WriteLine($"  MaxPlayers: {result.GameData?.MaxPlayers?.ToString() ?? "(null)"}");
Console.WriteLine($"  TeamSize: {result.GameData?.TeamSize?.ToString() ?? "(null)"}");
Console.WriteLine($"  TotalTeams: {result.GameData?.TotalTeams?.ToString() ?? "(null)"}");
Console.WriteLine($"  TotalBots: {result.GameData?.TotalBots?.ToString() ?? "(null)"}");
Console.WriteLine($"  IsLargeTeamGame: {result.GameData?.IsLargeTeamGame?.ToString() ?? "(null)"}");
Console.WriteLine($"  MapInfo: {result.GameData?.MapInfo ?? "(null)"}");
Console.WriteLine($"  GameSessionId: {result.GameData?.GameSessionId ?? "(null)"}");

// Check MapData
Console.WriteLine($"\nMAPDATA:");
if (result.MapData != null)
{
    DumpAllProperties(result.MapData, "  ");
}
else
{
    Console.WriteLine("  (null)");
}

// Header info
Console.WriteLine($"\nHEADER:");
Console.WriteLine($"  Branch: {result.Header?.Branch}");
Console.WriteLine($"  LevelNamesAndTimes: {string.Join(", ", result.Header?.LevelNamesAndTimes?.Select(x => x.ToString()) ?? Array.Empty<string>())}");

// Look for unique team indices in player data
Console.WriteLine($"\nPLAYER TEAM ANALYSIS:");
var teamIndices = new HashSet<int>();
if (result.PlayerData != null)
{
    foreach (var player in result.PlayerData)
    {
        var teamIndex = GetPropValue<int?>(player, "TeamIndex");
        if (teamIndex.HasValue)
        {
            teamIndices.Add(teamIndex.Value);
        }
    }
}
Console.WriteLine($"  Unique TeamIndex values: {teamIndices.Count} ({string.Join(", ", teamIndices.OrderBy(x => x).Take(10))}{(teamIndices.Count > 10 ? "..." : "")})");

// Check for respawn indicators (Reload has respawns)
Console.WriteLine($"\nRESPAWN/REBOOT INDICATORS:");
var playersWithReboot = 0;
if (result.PlayerData != null)
{
    foreach (var player in result.PlayerData)
    {
        var rebootCounter = GetPropValue<int?>(player, "RebootCounter");
        if (rebootCounter.HasValue && rebootCounter.Value > 0)
        {
            playersWithReboot++;
        }
    }
}
Console.WriteLine($"  Players with RebootCounter > 0: {playersWithReboot}");

// Kill feed analysis
Console.WriteLine($"\nKILLFEED:");
Console.WriteLine($"  Total kills in feed: {result.KillFeed?.Count ?? 0}");

Console.WriteLine("\n" + new string('=', 50) + "\n");

void DumpAllProperties(object? obj, string indent)
{
    if (obj == null) return;
    var type = obj.GetType();
    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        try
        {
            var val = prop.GetValue(obj);
            if (val != null)
            {
                if (val is IEnumerable enumerable && !(val is string))
                {
                    var count = 0;
                    foreach (var _ in enumerable) count++;
                    Console.WriteLine($"{indent}{prop.Name}: [{count} items]");
                }
                else
                {
                    Console.WriteLine($"{indent}{prop.Name}: {val}");
                }
            }
            else
            {
                Console.WriteLine($"{indent}{prop.Name}: (null)");
            }
        }
        catch { }
    }
}

T? GetPropValue<T>(object? obj, string name)
{
    if (obj == null) return default;
    var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    if (prop == null) return default;
    var val = prop.GetValue(obj);
    if (val is T t) return t;
    return default;
}