using System.Reflection;
using FortniteReplayReader;
using Microsoft.Extensions.Logging;
using Unreal.Core.Models.Enums;

public static class DumpAllElims
{
    public static void Run(string replayPath, string? outputPath = null)
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

        var elims = result.Eliminations;
        if (elims == null || elims.Count == 0)
        {
            Console.WriteLine("No eliminations found.");
            return;
        }

        TextWriter writer = Console.Out;
        StreamWriter? fileWriter = null;
        if (!string.IsNullOrEmpty(outputPath))
        {
            fileWriter = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);
            writer = fileWriter;
        }

        try
        {
            writer.WriteLine($"=== ALL ELIMINATION EVENTS ({elims.Count} total) ===");
            writer.WriteLine($"Replay: {Path.GetFileName(replayPath)}");
            writer.WriteLine();

            for (int i = 0; i < elims.Count; i++)
            {
                var e = elims[i];
                var eInfo = GetObj(e, "EliminatedInfo");
                var rInfo = GetObj(e, "EliminatorInfo");
                var eliminatedId = GetStr(eInfo, "Id") ?? GetStr(e, "Eliminated") ?? "unknown";
                var eliminatorId = GetStr(rInfo, "Id") ?? GetStr(e, "Eliminator") ?? "unknown";
                var eliminatedName = names.GetValueOrDefault(eliminatedId, "?");
                var eliminatorName = names.GetValueOrDefault(eliminatorId, "?");
                var knocked = GetObj(e, "Knocked");

                writer.WriteLine($"Event [{i}] — {eliminatorName} {(knocked is true ? "knocks" : "finishes")} {eliminatedName}");

                // Dump all top-level properties via reflection
                DumpObject(writer, e, "  ", names, skipNested: true);

                // Dump EliminatedInfo with all nested fields
                writer.WriteLine($"  EliminatedInfo:");
                DumpPlayerInfo(writer, eInfo, "    ", names);

                // Dump EliminatorInfo with all nested fields
                writer.WriteLine($"  EliminatorInfo:");
                DumpPlayerInfo(writer, rInfo, "    ", names);

                writer.WriteLine();
            }

            if (!string.IsNullOrEmpty(outputPath))
                Console.WriteLine($"Wrote {elims.Count} elimination events to: {outputPath}");
        }
        finally
        {
            fileWriter?.Dispose();
        }
    }

    static void DumpObject(TextWriter w, object? obj, string indent, Dictionary<string, string> names, bool skipNested)
    {
        if (obj == null) { w.WriteLine($"{indent}(null)"); return; }
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (skipNested && (prop.Name == "EliminatedInfo" || prop.Name == "EliminatorInfo"))
                continue;

            var val = prop.GetValue(obj);
            w.WriteLine($"{indent}{prop.Name}: {FormatValue(val)}");
        }
    }

    static void DumpPlayerInfo(TextWriter w, object? obj, string indent, Dictionary<string, string> names)
    {
        if (obj == null) { w.WriteLine($"{indent}(null)"); return; }
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = prop.GetValue(obj);
            if (val != null && IsNestedObject(prop.PropertyType))
            {
                w.WriteLine($"{indent}{prop.Name}:");
                DumpNestedObject(w, val, indent + "  ");
            }
            else
            {
                var display = FormatValue(val);
                // If this is Id, also show the resolved player name
                if (prop.Name == "Id" && val is string id && names.TryGetValue(id, out var name))
                    display += $" ({name})";
                w.WriteLine($"{indent}{prop.Name}: {display}");
            }
        }
    }

    static void DumpNestedObject(TextWriter w, object? obj, string indent)
    {
        if (obj == null) { w.WriteLine($"{indent}(null)"); return; }
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = prop.GetValue(obj);
            if (val != null && IsNestedObject(prop.PropertyType))
            {
                w.WriteLine($"{indent}{prop.Name}:");
                DumpNestedObject(w, val, indent + "  ");
            }
            else
            {
                w.WriteLine($"{indent}{prop.Name}: {FormatValue(val)}");
            }
        }
    }

    static bool IsNestedObject(Type t)
    {
        // Treat as nested if it's a class/struct (not string, not primitive, not enum)
        if (t == typeof(string)) return false;
        if (t.IsPrimitive) return false;
        if (t.IsEnum) return false;
        if (t == typeof(decimal)) return false;
        if (Nullable.GetUnderlyingType(t) is { } u)
            return IsNestedObject(u);
        return t.IsClass || t.IsValueType;
    }

    static string FormatValue(object? val)
    {
        if (val == null) return "(null)";
        if (val is float f) return f.ToString("G6");
        if (val is double d) return d.ToString("G6");
        return val.ToString() ?? "(null)";
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
