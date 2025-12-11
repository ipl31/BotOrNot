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
    private ObservableCollection<string> _eliminations = new();
    private string _metadataText = "";
    private string _ownerKillsHeader = "Your Eliminations";
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

    public ObservableCollection<string> Eliminations
    {
        get => _eliminations;
        set => this.RaiseAndSetIfChanged(ref _eliminations, value);
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
            Eliminations = new ObservableCollection<string>(data.Eliminations);
            OwnerEliminations = new ObservableCollection<PlayerRow>(data.OwnerEliminations);

            var ownerDisplay = !string.IsNullOrEmpty(data.OwnerName) ? data.OwnerName : "Your";
            OwnerKillsHeader = $"{ownerDisplay}'s Eliminations ({data.OwnerEliminations.Count})";

            MetadataText = $"File: {data.Metadata.FileName}\n" +
                          $"Mode: {data.Metadata.GameMode}\n" +
                          $"Duration: {data.Metadata.MatchDurationMinutes:F1} minutes\n" +
                          $"Players: {data.Metadata.PlayerCount}" + (data.Metadata.MaxPlayers.HasValue ? $" (Max: {data.Metadata.MaxPlayers})" : "") + "\n" +
                          $"Eliminations: {data.Metadata.EliminationCount}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
