namespace BotOrNot.Core.Models;

public sealed class PlayerRow
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Level { get; set; }
    public string? Bot { get; set; }
    public string? Platform { get; set; }
    public string? Kills { get; set; }
    public string? DeathCause { get; set; }
    public string? Pickaxe { get; set; }
    public string? Glider { get; set; }

    public bool IsBot => !string.IsNullOrEmpty(Bot) && Bot.Equals("true", StringComparison.OrdinalIgnoreCase);
}