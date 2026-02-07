using System.Reflection;
using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Unreal.Core.Models.Enums;

public static class DumpElimFields
{
    public static void Run(string replayPath)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddFilter(_ => false));
        var logger = loggerFactory.CreateLogger<ReplayReader>();
        var reader = new ReplayReader(logger, ParseMode.Full);
        var result = reader.ReadReplay(replayPath);

        var elims = result.Eliminations;
        if (elims == null || elims.Count == 0)
        {
            Console.WriteLine("No eliminations found.");
            return;
        }

        // Dump all fields on the first elimination event
        var first = elims[0];
        Console.WriteLine("=== ELIMINATION EVENT FIELDS ===");
        Console.WriteLine($"Type: {first.GetType().FullName}\n");
        foreach (var prop in first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = prop.GetValue(first);
            Console.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {val}");
        }

        // Dump EliminatedInfo fields
        var elimInfo = first.GetType().GetProperty("EliminatedInfo")?.GetValue(first);
        if (elimInfo != null)
        {
            Console.WriteLine($"\n=== EliminatedInfo FIELDS ===");
            Console.WriteLine($"Type: {elimInfo.GetType().FullName}\n");
            foreach (var prop in elimInfo.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = prop.GetValue(elimInfo);
                Console.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {val}");
            }
        }

        // Dump EliminatorInfo fields
        var eliminatorInfo = first.GetType().GetProperty("EliminatorInfo")?.GetValue(first);
        if (eliminatorInfo != null)
        {
            Console.WriteLine($"\n=== EliminatorInfo FIELDS ===");
            Console.WriteLine($"Type: {eliminatorInfo.GetType().FullName}\n");
            foreach (var prop in eliminatorInfo.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = prop.GetValue(eliminatorInfo);
                Console.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {val}");
            }
        }

        // Show a few more events to see variety in values
        Console.WriteLine($"\n=== SAMPLE EVENTS (first 10) ===\n");
        for (int i = 0; i < Math.Min(10, elims.Count); i++)
        {
            var e = elims[i];
            var knocked = e.GetType().GetProperty("Knocked")?.GetValue(e);
            var eInfo = e.GetType().GetProperty("EliminatedInfo")?.GetValue(e);
            var rInfo = e.GetType().GetProperty("EliminatorInfo")?.GetValue(e);
            var eId = eInfo?.GetType().GetProperty("Id")?.GetValue(eInfo);
            var rId = rInfo?.GetType().GetProperty("Id")?.GetValue(rInfo);

            // Dump all non-null fields for each event
            Console.WriteLine($"[{i}]");
            foreach (var prop in e.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = prop.GetValue(e);
                if (val != null && prop.Name != "EliminatedInfo" && prop.Name != "EliminatorInfo")
                    Console.WriteLine($"  {prop.Name}: {val}");
            }
            Console.WriteLine($"  EliminatedInfo.Id: {eId}");
            Console.WriteLine($"  EliminatorInfo.Id: {rId}");
            Console.WriteLine();
        }
    }
}
