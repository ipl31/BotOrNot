using BotOrNot.Core.Services;

namespace BotOrNot.Tests;

[TestFixture]
public class PlaylistHelperTests
{
    [TestCase("Playlist_NoBuildBR_Solo", "BR Zero Build - Solo")]
    [TestCase("Playlist_NoBuildBR_Duo", "BR Zero Build - Duos")]
    [TestCase("Playlist_NoBuildBR_Trio", "BR Zero Build - Trios")]
    [TestCase("Playlist_NoBuildBR_Squad", "BR Zero Build - Squads")]
    [TestCase("Playlist_DefaultSolo", "BR - Solo")]
    [TestCase("Playlist_DefaultDuo", "BR - Duos")]
    [TestCase("playlist_trios", "BR - Trios")]
    [TestCase("Playlist_DefaultSquad", "BR - Squads")]
    [TestCase("Playlist_Respawn_24_Alt", "Team Rumble")]
    public void GetDisplayName_ReturnsCorrectMapping(string playlist, string expectedDisplay)
    {
        var result = PlaylistHelper.GetDisplayName(playlist);
        Assert.That(result, Is.EqualTo(expectedDisplay));
    }

    [Test]
    public void GetDisplayName_IsCaseInsensitive()
    {
        var lower = PlaylistHelper.GetDisplayName("playlist_nobuildBR_solo");
        var upper = PlaylistHelper.GetDisplayName("PLAYLIST_NOBUILDGBR_SOLO");
        var mixed = PlaylistHelper.GetDisplayName("Playlist_NoBuildBR_Solo");

        Assert.That(lower, Is.EqualTo("BR Zero Build - Solo"));
        Assert.That(mixed, Is.EqualTo("BR Zero Build - Solo"));
    }

    [Test]
    public void GetDisplayName_ReturnsNullForUnknown()
    {
        var result = PlaylistHelper.GetDisplayName("Unknown_Playlist_Name");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetDisplayNameWithFallback_UsesPatternForUnknown()
    {
        // Unknown playlist with recognizable pattern
        var result = PlaylistHelper.GetDisplayNameWithFallback("Playlist_NewMode_NoBuild_Squad");
        Assert.That(result, Does.Contain("Zero Build"));
        Assert.That(result, Does.Contain("Squads"));
    }
}

[TestFixture]
public class ReplayServiceTests
{
    private static string TestReplayPath => Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "TestData",
        "UnsavedReplay-2026.01.31-15.34.27.replay");

    /// <summary>
    /// Validates elimination counting logic for the recording player.
    /// An elimination counts when:
    /// 1. You knock someone and they get finished (by anyone) - they weren't revived
    /// 2. You directly eliminate a solo/last-standing player (who can't be knocked)
    /// </summary>
    [Test]
    public async Task RecordingPlayer_ShouldHave8Eliminations()
    {
        // Arrange
        var service = new ReplayService();

        // Act
        var result = await service.LoadReplayAsync(TestReplayPath);

        // Assert
        Assert.That(result.OwnerName, Is.Not.Null.And.Not.Empty,
            "Could not find replay owner");

        Assert.That(result.OwnerEliminations.Count, Is.EqualTo(8),
            $"Expected recording player ({result.OwnerName}) to have 8 eliminations, " +
            $"but found {result.OwnerEliminations.Count}");
    }

    [Test]
    public async Task EliminationsList_ShouldNotIncludeKnocks()
    {
        // Arrange
        var service = new ReplayService();

        // Act
        var result = await service.LoadReplayAsync(TestReplayPath);

        // Assert - elimination count should match the filtered list
        Assert.That(result.Metadata.EliminationCount, Is.EqualTo(result.Eliminations.Count),
            "Elimination count in metadata should match the eliminations list");

        // The eliminations list should only contain finishes, not knocks
        // In this replay, there are fewer eliminations than total events because knocks are filtered
        Assert.That(result.Eliminations.Count, Is.GreaterThan(0),
            "Should have some eliminations");
    }

    [Test]
    public async Task FancyPumpkin87_ShouldHaveDeathCauseOfSMG()
    {
        // Arrange
        var service = new ReplayService();

        // Act
        var result = await service.LoadReplayAsync(TestReplayPath);

        // Assert - find FancyPumpkin87 in owner eliminations and verify death cause
        var victim = result.OwnerEliminations.FirstOrDefault(p =>
            p.Name?.Equals("FancyPumpkin87", StringComparison.OrdinalIgnoreCase) == true);

        Assert.That(victim, Is.Not.Null,
            "FancyPumpkin87 should be in the owner's eliminations");

        Assert.That(victim!.DeathCause, Does.StartWith("SMG"),
            $"FancyPumpkin87's death cause should be SMG, but was {victim.DeathCause}");
    }

    [Test]
    public async Task WinningTeam_ShouldBeExtracted()
    {
        // Arrange
        var service = new ReplayService();

        // Act
        var result = await service.LoadReplayAsync(TestReplayPath);

        // Assert - winning team should be team 10
        Assert.That(result.Metadata.WinningTeam, Is.EqualTo(10),
            "Winning team should be team 10");

        Assert.That(result.Metadata.WinningPlayerNames, Has.Count.EqualTo(3),
            "Winning team should have 3 players");

        Assert.That(result.Metadata.WinningPlayerNames, Does.Contain("ModPackDad"),
            "ModPackDad should be one of the winning players");
    }

    [Test]
    public async Task WinningPlayers_ShouldHavePlacement1()
    {
        // Arrange
        var service = new ReplayService();

        // Act
        var result = await service.LoadReplayAsync(TestReplayPath);

        // Assert - find winning team players and verify placement
        var modPackDad = result.Players.FirstOrDefault(p =>
            p.Name?.Equals("ModPackDad", StringComparison.OrdinalIgnoreCase) == true);

        Assert.That(modPackDad, Is.Not.Null,
            "ModPackDad should be in the players list");

        Assert.That(modPackDad!.Placement, Is.EqualTo("1"),
            $"ModPackDad's placement should be 1, but was {modPackDad.Placement}");

        Assert.That(modPackDad.IsWinner, Is.True,
            "ModPackDad should be marked as winner");

        // All team 10 players should have placement 1
        var team10Players = result.Players.Where(p => p.TeamIndex == "10").ToList();
        Assert.That(team10Players.All(p => p.Placement == "1"), Is.True,
            "All team 10 players should have placement 1");
    }

    [Test]
    public async Task TeamKills_ShouldBeExtracted()
    {
        // Arrange
        var service = new ReplayService();

        // Act
        var result = await service.LoadReplayAsync(TestReplayPath);

        // Assert - team 10 should have 20 team kills
        var team10Player = result.Players.FirstOrDefault(p => p.TeamIndex == "10");

        Assert.That(team10Player, Is.Not.Null,
            "Should have players from team 10");

        Assert.That(team10Player!.TeamKills, Is.EqualTo("20"),
            $"Team 10 should have 20 team kills, but had {team10Player.TeamKills}");
    }
}
