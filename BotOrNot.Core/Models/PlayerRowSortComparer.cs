using System.Collections;

namespace BotOrNot.Core.Models;

/// <summary>
/// Compares PlayerRow objects for DataGrid column sorting.
/// Numeric fields sort as integers, bot status sorts logically (true → false),
/// and Unknown/null/empty values sort to bottom or top depending on mode.
///
/// The <paramref name="descending"/> constructor flag must match the DataGrid's
/// sort direction so the "always-at-bottom" logic survives the DataGrid's
/// automatic result negation for descending sorts.
///
/// When <paramref name="unknownsFirst"/> is true, Unknown/null/empty values
/// are pushed to the top instead (mode 3 of the 3-mode sort cycle).
/// </summary>
public sealed class PlayerRowSortComparer : IComparer<PlayerRow>, IComparer
{
    private readonly Func<PlayerRow, string?> _selector;
    private readonly bool _numeric;
    private readonly bool _isBotField;
    private readonly bool _descending;
    private readonly bool _unknownsFirst;

    public PlayerRowSortComparer(
        Func<PlayerRow, string?> selector,
        bool descending = false,
        bool numeric = false,
        bool isBotField = false,
        bool unknownsFirst = false)
    {
        _selector = selector;
        _descending = descending;
        _numeric = numeric;
        _isBotField = isBotField;
        _unknownsFirst = unknownsFirst;
    }

    public int Compare(PlayerRow? x, PlayerRow? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return _descending ? -1 : 1;
        if (y is null) return _descending ? 1 : -1;

        var vx = _selector(x);
        var vy = _selector(y);

        var xUnknown = IsUnknownOrEmpty(vx);
        var yUnknown = IsUnknownOrEmpty(vy);

        if (_unknownsFirst)
        {
            // Push Unknown/null/empty to TOP.
            // The DataGrid negates for descending, so pre-invert accordingly.
            if (xUnknown && yUnknown) return 0;
            if (xUnknown) return _descending ? 1 : -1;
            if (yUnknown) return _descending ? -1 : 1;
        }
        else
        {
            // Push Unknown/null/empty to BOTTOM regardless of direction.
            // The DataGrid negates the result for descending, so we
            // pre-invert so the final result still pushes Unknown last.
            if (xUnknown && yUnknown) return 0;
            if (xUnknown) return _descending ? -1 : 1;
            if (yUnknown) return _descending ? 1 : -1;
        }

        int result;

        if (_isBotField)
            result = BotRank(vx!).CompareTo(BotRank(vy!));
        else if (_numeric)
            result = CompareNumeric(vx!, vy!);
        else
            result = string.Compare(vx, vy, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    int IComparer.Compare(object? x, object? y) => Compare(x as PlayerRow, y as PlayerRow);

    private static int CompareNumeric(string vx, string vy)
    {
        var xParsed = int.TryParse(vx, out var xNum);
        var yParsed = int.TryParse(vy, out var yNum);

        if (xParsed && yParsed) return xNum.CompareTo(yNum);
        if (xParsed) return -1;
        if (yParsed) return 1;

        return string.Compare(vx, vy, StringComparison.OrdinalIgnoreCase);
    }

    private static int BotRank(string value) => value.ToLowerInvariant() switch
    {
        "true" => 0,
        "false" => 1,
        _ => 2
    };

    internal static bool IsUnknownOrEmpty(string? value) =>
        string.IsNullOrEmpty(value) ||
        value.Equals("unknown", StringComparison.OrdinalIgnoreCase);
}
