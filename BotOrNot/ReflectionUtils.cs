using System.Reflection;

namespace BotOrNot;

public static class ReflectionUtils
{
    
    private static readonly Dictionary<(Type, string), PropertyInfo?> _cache = new();

    private static PropertyInfo? FindProp(Type t, string name)
    {
        var key = (t, name);
        if (_cache.TryGetValue(key, out var pi)) return pi;
        pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        _cache[key] = pi;
        return pi;
    }

    public static object? GetObject(object? obj, string prop)
    {
        if (obj is null) return null;
        var pi = FindProp(obj.GetType(), prop);
        return pi?.GetValue(obj);
    }

    public static string? FirstString(object? obj, params string[] names)
    {
        if (obj is null) return null;
        var t = obj.GetType();
        foreach (var n in names)
        {
            var pi = FindProp(t, n);
            if (pi == null) continue;
            var v = pi.GetValue(obj);
            if (v == null) continue;
            var s = v.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        return null;
    }
}
