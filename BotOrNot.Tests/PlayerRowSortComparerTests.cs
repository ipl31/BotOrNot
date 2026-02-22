using BotOrNot.Core.Models;

namespace BotOrNot.Tests;

[TestFixture]
public class PlayerRowSortComparerTests
{
    private static PlayerRow Row(string? kills = null, string? bot = null, string? placement = null, string? name = null)
        => new() { Kills = kills, Bot = bot, Placement = placement, Name = name };

    // --- Numeric sorting (Kills, Level, Placement, etc.) ---

    [Test]
    public void Numeric_Ascending_SortsAsIntegers()
    {
        var comparer = new PlayerRowSortComparer(p => p.Kills, descending: false, numeric: true);
        var rows = new[]
        {
            Row(kills: "10"), Row(kills: "2"), Row(kills: "9"), Row(kills: "1"), Row(kills: "20")
        };

        var sorted = rows.OrderBy(r => r, comparer).Select(r => r.Kills).ToList();

        Assert.That(sorted, Is.EqualTo(new[] { "1", "2", "9", "10", "20" }));
    }

    [Test]
    public void Numeric_Descending_SortsAsIntegers()
    {
        // Simulate what the DataGrid does: use comparer then negate for descending
        var comparer = new PlayerRowSortComparer(p => p.Kills, descending: true, numeric: true);
        var rows = new[]
        {
            Row(kills: "10"), Row(kills: "2"), Row(kills: "9"), Row(kills: "1"), Row(kills: "20")
        };

        // DataGrid negates the result for descending, so we simulate that here
        var sorted = rows.OrderBy(r => r, new NegatingComparer(comparer)).Select(r => r.Kills).ToList();

        Assert.That(sorted, Is.EqualTo(new[] { "20", "10", "9", "2", "1" }));
    }

    // --- Unknown always at bottom ---

    [Test]
    public void Unknown_Ascending_SortsToBottom()
    {
        var comparer = new PlayerRowSortComparer(p => p.Kills, descending: false, numeric: true);
        var rows = new[]
        {
            Row(kills: "unknown"), Row(kills: "5"), Row(kills: "1"), Row(kills: null), Row(kills: "10")
        };

        var sorted = rows.OrderBy(r => r, comparer).Select(r => r.Kills).ToList();

        Assert.That(sorted, Is.EqualTo(new[] { "1", "5", "10", "unknown", null }));
    }

    [Test]
    public void Unknown_Descending_StillSortsToBottom()
    {
        var comparer = new PlayerRowSortComparer(p => p.Kills, descending: true, numeric: true);
        var rows = new[]
        {
            Row(kills: "unknown"), Row(kills: "5"), Row(kills: "1"), Row(kills: null), Row(kills: "10")
        };

        // Simulate DataGrid's descending negation
        var sorted = rows.OrderBy(r => r, new NegatingComparer(comparer)).Select(r => r.Kills).ToList();

        // Known values descending, then Unknown/null at bottom
        Assert.That(sorted[0], Is.EqualTo("10"));
        Assert.That(sorted[1], Is.EqualTo("5"));
        Assert.That(sorted[2], Is.EqualTo("1"));
        // Last two are unknown/null in any order
        Assert.That(sorted[3], Is.AnyOf("unknown", null));
        Assert.That(sorted[4], Is.AnyOf("unknown", null));
    }

    [Test]
    public void Unknown_CaseInsensitive()
    {
        var comparer = new PlayerRowSortComparer(p => p.Kills, descending: false, numeric: true);

        var unknown = Row(kills: "Unknown");
        var known = Row(kills: "5");

        Assert.That(comparer.Compare(unknown, known), Is.GreaterThan(0));
    }

    // --- Bot field sorting ---

    [Test]
    public void Bot_Ascending_SortsTrueThenFalse()
    {
        var comparer = new PlayerRowSortComparer(p => p.Bot, descending: false, isBotField: true);
        var rows = new[]
        {
            Row(bot: "false"), Row(bot: "true"), Row(bot: "false"), Row(bot: "true")
        };

        var sorted = rows.OrderBy(r => r, comparer).Select(r => r.Bot).ToList();

        Assert.That(sorted, Is.EqualTo(new[] { "true", "true", "false", "false" }));
    }

    [Test]
    public void Bot_UnknownAlwaysLast()
    {
        var comparer = new PlayerRowSortComparer(p => p.Bot, descending: false, isBotField: true);
        var rows = new[]
        {
            Row(bot: "unknown"), Row(bot: "false"), Row(bot: "true"), Row(bot: null)
        };

        var sorted = rows.OrderBy(r => r, comparer).Select(r => r.Bot).ToList();

        Assert.That(sorted[0], Is.EqualTo("true"));
        Assert.That(sorted[1], Is.EqualTo("false"));
        // Last two are unknown/null
        Assert.That(sorted[2], Is.AnyOf("unknown", null));
        Assert.That(sorted[3], Is.AnyOf("unknown", null));
    }

    [Test]
    public void Bot_Descending_UnknownStillLast()
    {
        var comparer = new PlayerRowSortComparer(p => p.Bot, descending: true, isBotField: true);
        var rows = new[]
        {
            Row(bot: "unknown"), Row(bot: "false"), Row(bot: "true"), Row(bot: null)
        };

        var sorted = rows.OrderBy(r => r, new NegatingComparer(comparer)).Select(r => r.Bot).ToList();

        Assert.That(sorted[0], Is.EqualTo("false"));
        Assert.That(sorted[1], Is.EqualTo("true"));
        // Last two are unknown/null
        Assert.That(sorted[2], Is.AnyOf("unknown", null));
        Assert.That(sorted[3], Is.AnyOf("unknown", null));
    }

    // --- String sorting ---

    [Test]
    public void String_Ascending_SortsAlphabetically()
    {
        var comparer = new PlayerRowSortComparer(p => p.Name, descending: false);
        var rows = new[]
        {
            Row(name: "Charlie"), Row(name: "Alice"), Row(name: "Bob")
        };

        var sorted = rows.OrderBy(r => r, comparer).Select(r => r.Name).ToList();

        Assert.That(sorted, Is.EqualTo(new[] { "Alice", "Bob", "Charlie" }));
    }

    // --- Placement (numeric with nulls) ---

    [Test]
    public void Placement_NullPushedToBottom()
    {
        var comparer = new PlayerRowSortComparer(p => p.Placement, descending: false, numeric: true);
        var rows = new[]
        {
            Row(placement: null), Row(placement: "1"), Row(placement: "50"), Row(placement: "3")
        };

        var sorted = rows.OrderBy(r => r, comparer).Select(r => r.Placement).ToList();

        Assert.That(sorted, Is.EqualTo(new[] { "1", "3", "50", null }));
    }

    // --- Null PlayerRow handling ---

    [Test]
    public void NullRows_SortToBottom()
    {
        var comparer = new PlayerRowSortComparer(p => p.Kills, descending: false, numeric: true);

        Assert.That(comparer.Compare(null, Row(kills: "5")), Is.GreaterThan(0));
        Assert.That(comparer.Compare(Row(kills: "5"), null), Is.LessThan(0));
        Assert.That(comparer.Compare((PlayerRow?)null, null), Is.EqualTo(0));
    }

    // --- IComparer (non-generic) interface ---

    [Test]
    public void NonGeneric_IComparer_Works()
    {
        var comparer = (System.Collections.IComparer)new PlayerRowSortComparer(
            p => p.Kills, descending: false, numeric: true);

        var a = Row(kills: "2");
        var b = Row(kills: "10");

        Assert.That(comparer.Compare(a, b), Is.LessThan(0));
    }

    /// <summary>
    /// Simulates the DataGrid's descending behavior: it negates
    /// the comparer's result to reverse the sort order.
    /// </summary>
    private sealed class NegatingComparer(IComparer<PlayerRow> inner) : IComparer<PlayerRow>
    {
        public int Compare(PlayerRow? x, PlayerRow? y) => -inner.Compare(x, y);
    }
}
