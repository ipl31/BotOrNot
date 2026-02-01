using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using BotOrNot.Core.Models;
using BotOrNot.Core.Services;
using ReactiveUI;

namespace BotOrNot.Avalonia.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly IReplayService _replayService;

    private ObservableCollection<PlayerRow> _players = new();
    private ObservableCollection<PlayerRow> _ownerEliminations = new();
    private string _metadataText = "";
    private string _ownerKillsHeader = "Your Eliminations";
    private string _playersSeenHeader = "Players Seen";
    private bool _isLoading;
    private string? _errorMessage;

    public MainWindowViewModel()
    {
        _replayService = new ReplayService();

        LoadReplayCommand = ReactiveCommand.CreateFromTask<string>(LoadReplayAsync);
        LoadReplayCommand.ThrownExceptions.Subscribe(ex =>
        {
            ErrorMessage = $"Failed to load replay: {ex.Message}";
            IsLoading = false;
        });
    }

    public ObservableCollection<PlayerRow> Players
    {
        get => _players;
        set => this.RaiseAndSetIfChanged(ref _players, value);
    }

    public ObservableCollection<PlayerRow> OwnerEliminations
    {
        get => _ownerEliminations;
        set => this.RaiseAndSetIfChanged(ref _ownerEliminations, value);
    }

    public string OwnerKillsHeader
    {
        get => _ownerKillsHeader;
        set => this.RaiseAndSetIfChanged(ref _ownerKillsHeader, value);
    }

    public string PlayersSeenHeader
    {
        get => _playersSeenHeader;
        set => this.RaiseAndSetIfChanged(ref _playersSeenHeader, value);
    }

    public string MetadataText
    {
        get => _metadataText;
        set => this.RaiseAndSetIfChanged(ref _metadataText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public ReactiveCommand<string, Unit> LoadReplayCommand { get; }

    private async Task LoadReplayAsync(string path)
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            var data = await _replayService.LoadReplayAsync(path);

            Players = new ObservableCollection<PlayerRow>(data.Players);
            OwnerEliminations = new ObservableCollection<PlayerRow>(data.OwnerEliminations);

            var ownerDisplay = !string.IsNullOrEmpty(data.OwnerName) ? data.OwnerName : "Your";
            // Exclude NPCs from elimination counts
            var nonNpcEliminations = data.OwnerEliminations.Where(p => !p.IsNpc).ToList();
            var botKills = nonNpcEliminations.Count(p => p.IsBot);
            var playerKills = nonNpcEliminations.Count - botKills;
            OwnerKillsHeader = $"{ownerDisplay}'s Eliminations ({nonNpcEliminations.Count}) - {playerKills} Players, {botKills} Bots";

            // Build Players Seen header with breakdown (excluding NPCs)
            var npcCount = data.Players.Count(p => p.IsNpc);
            var nonNpcPlayers = data.Players.Where(p => !p.IsNpc).ToList();
            var totalPlayers = nonNpcPlayers.Count;
            var botPlayers = nonNpcPlayers.Count(p => p.IsBot);
            var humanPlayers = totalPlayers - botPlayers;

            // Group by platform (using friendly names) and build platform breakdown string (excluding NPCs)
            var platformGroups = nonNpcPlayers
                .Where(p => !string.IsNullOrWhiteSpace(p.Platform))
                .GroupBy(p => PlatformHelper.GetFriendlyName(p.Platform))
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Count()} {g.Key}")
                .ToList();

            var platformBreakdown = platformGroups.Count > 0 ? " | " + string.Join(", ", platformGroups) : "";
            var npcPrefix = npcCount > 0 ? $"NPCs Seen ({npcCount}) | " : "";
            PlayersSeenHeader = $"{npcPrefix}Players Seen ({totalPlayers}) - {humanPlayers} Players, {botPlayers} Bots{platformBreakdown}";

            // Find owner's placement
            var ownerPlayer = data.Players.FirstOrDefault(p =>
                !string.IsNullOrEmpty(data.OwnerName) &&
                p.Name?.Equals(data.OwnerName, StringComparison.OrdinalIgnoreCase) == true);
            var ownerPlacement = ownerPlayer?.Placement;
            var placementText = !string.IsNullOrEmpty(ownerPlacement)
                ? $"Placement: #{ownerPlacement}"
                : "Placement: Unknown";

            MetadataText = $"File: {data.Metadata.FileName}\n" +
                          $"Mode: {data.Metadata.GameMode}\n" +
                          $"Duration: {data.Metadata.MatchDurationMinutes:F1} minutes | {placementText}\n" +
                          $"Players: {data.Metadata.PlayerCount}" + (data.Metadata.MaxPlayers.HasValue ? $" (Max: {data.Metadata.MaxPlayers})" : "") + "\n" +
                          $"Eliminations (in replay): {data.Metadata.EliminationCount}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
