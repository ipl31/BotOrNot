using System.Reflection;
using BotOrNot.Core.Services;
using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Unreal.Core.Models.Enums;

public static class DumpUnknownDeaths
{
    public static void Run(string replayPath)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddFilter(_ => false));
        var logger = loggerFactory.CreateLogger<ReplayReader>();
        var reader = new ReplayReader(logger, ParseMode.Full);
        var result = reader.ReadReplay(replayPath);

        Console.WriteLine($"=== Players with null/unknown DeathCause ===");
        Console.WriteLine($"Replay: {Path.GetFileName(replayPath)}");
        Console.WriteLine();

        int count = 0;

        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            var id = ReflectionUtils.FirstString(pd, "PlayerId", "UniqueId", "NetId");
            var name = ReflectionUtils.FirstString(pd, "PlayerName", "DisplayName", "Name");
            var death = ReflectionUtils.FirstString(pd, "DeathCause");
            var isBot = ReflectionUtils.FirstString(pd, "IsBot");
            var placement = ReflectionUtils.FirstString(pd, "Placement");

            // Get death tags
            var deathTagsObj = ReflectionUtils.GetObject(pd, "DeathTags");
            var deathTagStrings = (deathTagsObj as System.Collections.IEnumerable)?
                .Cast<object>()
                .Select(t => t.ToString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList() ?? new List<string>();

            // Check if death cause is null/empty or resolves to "Unknown"
            var resolved = DeathCauseHelper.GetDisplayName(death, deathTagStrings);
            bool isNullOrUnknown = string.IsNullOrWhiteSpace(death) ||
                                   resolved.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                                   resolved.Contains("Unknown");

            if (!isNullOrUnknown) continue;

            count++;
            Console.WriteLine($"--- Player: {name ?? "(no name)"} ---");
            Console.WriteLine($"  ID:         {id ?? "(null)"}");
            Console.WriteLine($"  IsBot:      {isBot ?? "(null)"}");
            Console.WriteLine($"  Placement:  {placement ?? "(null)"}");
            Console.WriteLine($"  DeathCause: {(string.IsNullOrWhiteSpace(death) ? "(null)" : death)}");
            Console.WriteLine($"  Resolved:   {resolved}");
            Console.WriteLine($"  DeathTags ({deathTagStrings.Count}):");
            if (deathTagStrings.Count == 0)
            {
                Console.WriteLine($"    (none)");
            }
            else
            {
                foreach (var tag in deathTagStrings)
                    Console.WriteLine($"    - {tag}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Total players with null/unknown death cause: {count}");
    }
}
