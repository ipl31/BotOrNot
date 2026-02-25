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
    private string _filterText = "";

    private readonly List<PlayerRow> _allPlayers = new();
    private readonly List<PlayerRow> _allOwnerEliminations = new();

    private ObservableCollection<PlayerRow> _filteredPlayers = new();
    private ObservableCollection<PlayerRow> _filteredOwnerEliminations = new();

    private string? _eliminatorName;

    public MainWindowViewModel()
    {
        _replayService = new ReplayService();

        LoadReplayCommand = ReactiveCommand.CreateFromTask<string>(LoadReplayAsync);
        LoadReplayCommand.ThrownExceptions.Subscribe(ex =>
        {
            ErrorMessage = $"Failed to load replay: {ex.Message}";
            IsLoading = false;
        });

        FilterByPlayerCommand = ReactiveCommand.Create<string>(FilterByPlayer);

        // Filter logic - react to filter text changes
        this.WhenAnyValue(x => x.FilterText)
            .Subscribe(_ => ApplyFilter());
    }

    public ObservableCollection<PlayerRow> Players => _filteredPlayers;

    public ObservableCollection<PlayerRow> OwnerEliminations => _filteredOwnerEliminations;

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

    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    public ReactiveCommand<string, Unit> LoadReplayCommand { get; }

    public string? EliminatorName
    {
        get => _eliminatorName;
        set => this.RaiseAndSetIfChanged(ref _eliminatorName, value);
    }

    public ReactiveCommand<string, Unit> FilterByPlayerCommand { get; }

    private void FilterByPlayer(string playerName)
    {
        FilterText = playerName;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            _filteredPlayers.Clear();
            _filteredOwnerEliminations.Clear();
            foreach (var p in _allPlayers) _filteredPlayers.Add(p);
            foreach (var p in _allOwnerEliminations) _filteredOwnerEliminations.Add(p);
        }
        else
        {
            var searchTerm = FilterText.ToLowerInvariant();
            var filteredPlayersList = _allPlayers.Where(p => MatchesFilter(p, searchTerm)).ToList();
            var filteredOwnerElimList = _allOwnerEliminations.Where(p => MatchesFilter(p, searchTerm)).ToList();

            _filteredPlayers.Clear();
            _filteredOwnerEliminations.Clear();
            foreach (var p in filteredPlayersList) _filteredPlayers.Add(p);
            foreach (var p in filteredOwnerElimList) _filteredOwnerEliminations.Add(p);
        }
    }

    private static bool MatchesFilter(PlayerRow player, string searchTerm)
    {
        return (player.Name?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (player.Level?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (player.Platform?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (player.Kills?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (player.TeamIndex?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (player.Placement?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (player.DeathCause?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private async Task LoadReplayAsync(string path)
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            var data = await _replayService.LoadReplayAsync(path);

            _allPlayers.Clear();
            _allOwnerEliminations.Clear();
            foreach (var p in data.Players) _allPlayers.Add(p);
            foreach (var p in data.OwnerEliminations) _allOwnerEliminations.Add(p);

            ApplyFilter();

            var ownerDisplay = !string.IsNullOrEmpty(data.OwnerName) ? data.OwnerName : "Your";
            // Use authoritative kill count from PlayerData, fall back to event-based count
            var nonNpcEliminations = data.OwnerEliminations.Where(p => !p.IsNpc).ToList();
            var totalKills = data.OwnerKills ?? nonNpcEliminations.Count;
            var botKills = nonNpcEliminations.Count(p => p.IsBot);
            var playerKills = totalKills - botKills;
            OwnerKillsHeader = $"{ownerDisplay}'s Eliminations ({totalKills}) - {playerKills} Players, {botKills} Bots";

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

            // Extract eliminator name for clickable link (only when we know they didn't win)
            // Note: Only shows eliminator when owner placement is known (not null).
            // Original behavior showed eliminator even when placement was unknown; this change
            // is intentional to avoid showing misleading information when owner wasn't found.
            EliminatorName = ownerPlacement is not null and not "1" && data.OwnerEliminatedBy != null
                ? data.OwnerEliminatedBy
                : null;

            MetadataText = $"File: {data.Metadata.FileName}\n" +
                          $"Mode: {data.Metadata.GameMode}\n" +
                          $"Playlist Name: {data.Metadata.Playlist}\n" +
                          $"Duration: {data.Metadata.MatchDurationMinutes:F1} minutes | {placementText}\n" +
                          $"Players: {data.Metadata.PlayerCount}" + (data.Metadata.MaxPlayers.HasValue ? $" (Max: {data.Metadata.MaxPlayers})" : "") + "\n" +
                          $"Eliminations (in replay): {data.Metadata.EliminationCount}";
        }
        catch (IOException ex)
        {
            ErrorMessage = $"Could not read replay file: {ex.Message} (The file may still be locked by Fortnite.)";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
