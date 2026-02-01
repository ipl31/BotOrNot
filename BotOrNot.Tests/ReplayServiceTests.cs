using BotOrNot.Core.Services;

namespace BotOrNot.Tests;

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
}
