using System.Collections;

namespace BotOrNot.Core.Models;

/// <summary>
/// Comparer that sorts players matching a target group value to the top,
/// with remaining players sorted alphabetically by the same field.
/// </summary>
public sealed class GroupCycleComparer : IComparer<PlayerRow>, IComparer
{
    private readonly Func<PlayerRow, string?> _selector;
    private readonly string? _targetGroup;

    public GroupCycleComparer(Func<PlayerRow, string?> selector, string? targetGroup)
    {
        _selector = selector;
        _targetGroup = targetGroup;
    }

    public int Compare(PlayerRow? x, PlayerRow? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        var vx = _selector(x);
        var vy = _selector(y);

        var xMatch = IsMatch(vx);
        var yMatch = IsMatch(vy);

        // Target group sorts to top
        if (xMatch && !yMatch) return -1;
        if (!xMatch && yMatch) return 1;

        // Within same group (both match or both don't), sort alphabetically
        return string.Compare(vx, vy, StringComparison.OrdinalIgnoreCase);
    }

    int IComparer.Compare(object? x, object? y) => Compare(x as PlayerRow, y as PlayerRow);

    private bool IsMatch(string? value) =>
        string.Equals(value, _targetGroup, StringComparison.OrdinalIgnoreCase);
}
