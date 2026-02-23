using System.Collections.ObjectModel;
using Avalonia.Headless.NUnit;
using BotOrNot.Avalonia.Views;
using BotOrNot.Core.Models;
using BotOrNot.UITests.Helpers;

namespace BotOrNot.UITests.Tests;

[TestFixture]
public class DataGridSortingTests
{
    private static ObservableCollection<PlayerRow> CreateTestData() => new(new[]
    {
        new PlayerRow { Name = "Alice",   Kills = "10", Level = "50",      Placement = "1",  Bot = "false" },
        new PlayerRow { Name = "Bot1",    Kills = "2",  Level = "1",       Placement = "45", Bot = "true"  },
        new PlayerRow { Name = "Charlie", Kills = "9",  Level = "unknown", Placement = "3",  Bot = "unknown" },
        new PlayerRow { Name = "Diana",   Kills = "20", Level = "100",     Placement = null,  Bot = "false" },
        new PlayerRow { Name = "Eve",     Kills = null, Level = "25",      Placement = "10", Bot = "true"  },
    });

    private MainWindow CreateWindowWithTestData()
    {
        var data = CreateTestData();
        return DataGridTestHelper.CreateMainWindowWithData(data, data);
    }

    // ── Numeric column: Kills ──────────────────────────────────────
    // Mode cycle: click 1 = ascending (unknowns bottom),
    //             click 2 = unknowns first,
    //             click 3 = descending (unknowns bottom)

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Kills_Click1_SortsAscending(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Kills");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Kills);
        Assert.That(values, Is.EqualTo(new[] { "2", "9", "10", "20", null }));
    }

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Kills_Click2_UnknownsFirst(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Kills");
        DataGridTestHelper.ClickColumnHeader(window, grid, "Kills");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Kills);
        Assert.That(values, Is.EqualTo(new[] { null, "2", "9", "10", "20" }));
    }

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Kills_Click3_SortsDescending(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Kills");
        DataGridTestHelper.ClickColumnHeader(window, grid, "Kills");
        DataGridTestHelper.ClickColumnHeader(window, grid, "Kills");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Kills);
        Assert.That(values, Is.EqualTo(new[] { "20", "10", "9", "2", null }));
    }

    // ── Numeric column: Level ──────────────────────────────────────

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Level_Click1_SortsAscending(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Level");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Level);
        Assert.That(values, Is.EqualTo(new[] { "1", "25", "50", "100", "unknown" }));
    }

    // ── Numeric column: Placement ──────────────────────────────────

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Placement_Click1_SortsAscending(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Placement");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Placement);
        Assert.That(values, Is.EqualTo(new[] { "1", "3", "10", "45", null }));
    }

    // ── Non-numeric column: Bot (2-mode) ───────────────────────────
    // Mode cycle: click 1 = descending (false→true, unknown bottom),
    //             click 2 = ascending (true→false, unknown bottom)

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Bot_Click1_SortsFalseThenTrue(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Bot");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Bot);
        Assert.That(values, Is.EqualTo(new[] { "false", "false", "true", "true", "unknown" }));
    }

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Bot_Click2_SortsTrueThenFalse(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Bot");
        DataGridTestHelper.ClickColumnHeader(window, grid, "Bot");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Bot);
        Assert.That(values, Is.EqualTo(new[] { "true", "true", "false", "false", "unknown" }));
    }

    // ── Non-numeric column: Name (2-mode) ──────────────────────────
    // Mode cycle: click 1 = descending (Z→A), click 2 = ascending (A→Z)

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Name_Click1_SortsDescending(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Name");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Name);
        Assert.That(values, Is.EqualTo(new[] { "Eve", "Diana", "Charlie", "Bot1", "Alice" }));
    }

    [AvaloniaTest]
    [TestCase("OwnerEliminationsGrid")]
    [TestCase("PlayersGrid")]
    public void Name_Click2_SortsAscending(string gridName)
    {
        var window = CreateWindowWithTestData();
        var grid = DataGridTestHelper.GetDataGrid(window, gridName);

        DataGridTestHelper.ClickColumnHeader(window, grid, "Name");
        DataGridTestHelper.ClickColumnHeader(window, grid, "Name");

        var values = DataGridTestHelper.GetDisplayedValues(grid, p => p.Name);
        Assert.That(values, Is.EqualTo(new[] { "Alice", "Bot1", "Charlie", "Diana", "Eve" }));
    }
}
