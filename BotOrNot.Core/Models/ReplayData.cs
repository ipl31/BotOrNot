namespace BotOrNot.Core.Models;

public sealed class ReplayData
{
    public List<PlayerRow> Players { get; set; } = new();
    public List<string> Eliminations { get; set; } = new();
    public List<PlayerRow> OwnerEliminations { get; set; } = new();
    public string? OwnerName { get; set; }
    /// <summary>
    /// Authoritative kill count from the owner's PlayerData.Kills property.
    /// </summary>
    public int? OwnerKills { get; set; }
    public string? OwnerEliminatedBy { get; set; }
    public ReplayMetadata Metadata { get; set; } = new();
}

public sealed class ReplayMetadata
{
    public string FileName { get; set; } = "";
    public string Version { get; set; } = "";
    public string GameNetProtocol { get; set; } = "";
    public int PlayerCount { get; set; }
    public int EliminationCount { get; set; }
    public string GameMode { get; set; } = "";
    public string Playlist { get; set; } = "";
    public int? MaxPlayers { get; set; }
    public double MatchDurationMinutes { get; set; }
    public int? WinningTeam { get; set; }
    public List<string> WinningPlayerIds { get; set; } = new();
    public List<string> WinningPlayerNames { get; set; } = new();
}