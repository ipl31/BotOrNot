using System.Collections;
using System.Diagnostics;
using System.Reactive.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BotOrNot.Avalonia.ViewModels;
using BotOrNot.Core.Models;
using ReactiveUI;

namespace BotOrNot.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly MenuFlyout _columnsFlyout;
    // All columns use a 3-mode cycle:
    //   0 = desc (unknowns bottom), 1 = asc (unknowns bottom), 2 = unknowns-first
    private readonly Dictionary<DataGridColumn, int> _columnSortMode = new();
    private DataGrid? _playersGrid;
    private Button? _columnsButton;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        _columnsFlyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };

        // Get references to controls and set up the flyout
        _playersGrid = this.FindControl<DataGrid>("PlayersGrid");
        _columnsButton = this.FindControl<Button>("ColumnsButton");

        if (_columnsButton != null)
        {
            _columnsButton.Flyout = _columnsFlyout;
        }

        // Wire up custom sorting on both grids
        var ownerGrid = this.FindControl<DataGrid>("OwnerEliminationsGrid");
        if (ownerGrid != null) ownerGrid.Sorting += OnDataGridSorting;
        if (_playersGrid != null) _playersGrid.Sorting += OnDataGridSorting;

        // Build the columns menu when the window loads
        Loaded += (_, _) => BuildColumnsFlyout();
    }

    private void OnDataGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        var info = GetColumnSortInfo(e.Column);
        if (info.Selector == null) return;

        var items = grid.ItemsSource as IEnumerable<PlayerRow>;

        IComparer comparer;

        if (info.GroupCycle)
        {
            // Group-cycle columns: each click brings the next distinct value to the top
            comparer = (IComparer)BuildGroupCycleComparer(e.Column, info, items);
        }
        else
        {
            comparer = (IComparer)BuildStandardComparer(e.Column, info, items);
        }

        // Set on column so DataGrid knows sorting is active
        e.Column.CustomSortComparer = comparer;

        // The DataGrid's internal sort may apply the wrong direction (it toggles
        // its own asc/desc cycle independently of our mode cycle). Post a
        // callback to re-apply the correct sort after DataGrid finishes.
        Dispatcher.UIThread.Post(() =>
        {
            var cv = grid.CollectionView;
            if (cv != null)
            {
                cv.SortDescriptions.Clear();
                cv.SortDescriptions.Add(DataGridSortDescription.FromComparer(comparer));
            }
        });
    }

    private IComparer<PlayerRow> BuildStandardComparer(
        DataGridColumn column, ColumnSortInfo info, IEnumerable<PlayerRow>? items)
    {
        // Determine cycle length: text columns always 2-mode (asc/desc).
        // Numeric/bot columns get 3-mode only when the data has unknowns.
        var hasUnknowns = false;
        if (info.Numeric || info.Bot)
        {
            if (items != null)
                hasUnknowns = items.Any(p => PlayerRowSortComparer.IsUnknownOrEmpty(info.Selector!(p)));
        }
        var modeCount = (info.Numeric || info.Bot) && hasUnknowns ? 3 : 2;

        _columnSortMode.TryGetValue(column, out var currentMode);
        var nextMode = (currentMode + 1) % modeCount;
        _columnSortMode[column] = nextMode;

        // Modes (nextMode starts at 1 because currentMode defaults to 0):
        // 2-mode: 1=asc, 0=desc
        // 3-mode: 1=asc, 2=unknowns-first, 0=desc
        var descending = nextMode == 0;
        var unknownsFirst = modeCount == 3 && nextMode == 2;

        return new PlayerRowSortComparer(
            info.Selector!, descending: descending, numeric: info.Numeric,
            isBotField: info.Bot, unknownsFirst: unknownsFirst);
    }

    private IComparer<PlayerRow> BuildGroupCycleComparer(
        DataGridColumn column, ColumnSortInfo info, IEnumerable<PlayerRow>? items)
    {
        // Get distinct non-empty values sorted alphabetically
        var groups = (items ?? [])
            .Select(p => info.Selector!(p))
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var modeCount = Math.Max(groups.Count, 1);

        _columnSortMode.TryGetValue(column, out var currentMode);
        var nextMode = (currentMode + 1) % modeCount;
        _columnSortMode[column] = nextMode;

        var targetGroup = groups.Count > 0 ? groups[nextMode] : null;
        return new GroupCycleComparer(info.Selector!, targetGroup);
    }

    private record struct ColumnSortInfo(
        Func<PlayerRow, string?>? Selector, bool Numeric, bool Bot, bool GroupCycle);

    private static ColumnSortInfo GetColumnSortInfo(DataGridColumn column)
    {
        return column.Header?.ToString() switch
        {
            "Id"         => new(p => p.Id, false, false, false),
            "Name"       => new(p => p.Name, false, false, false),
            "Level"      => new(p => p.Level, true, false, false),
            "Bot"        => new(p => p.Bot, false, true, false),
            "Platform"   => new(p => p.Platform, false, false, true),
            "Kills"      => new(p => p.Kills, true, false, false),
            "Squad"      => new(p => p.TeamIndex, true, false, false),
            "Placement"  => new(p => p.Placement, true, false, false),
            "Death Cause" => new(p => p.DeathCause, false, false, true),
            "Elim Time"  => new(p => p.ElimTime, true, false, false),
            "Pickaxe"    => new(p => p.Pickaxe, false, false, false),
            "Glider"     => new(p => p.Glider, false, false, false),
            _ => default
        };
    }

    private async void OpenReplay_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Fortnite Replay File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Fortnite Replays")
                    {
                        Patterns = new[] { "*.replay" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    await _viewModel.LoadReplayCommand.Execute(path).FirstAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _viewModel.ErrorMessage = $"Failed to load replay: {ex.Message} (The file may still be locked by Fortnite.)";
            _viewModel.IsLoading = false;
        }
    }

    private void OpenFortniteTracker_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PlayerRow player } && !string.IsNullOrEmpty(player.Name))
        {
            var encodedName = Uri.EscapeDataString(player.Name);
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://fortnitetracker.com/profile/all/{encodedName}",
                UseShellExecute = true
            });
        }
    }

    private void BuildColumnsFlyout()
    {
        if (_playersGrid == null) return;

        _columnsFlyout.Items.Clear();

        foreach (var column in _playersGrid.Columns)
        {
            var menuItem = new MenuItem
            {
                Header = column.Header?.ToString() ?? "Column",
                Icon = column.IsVisible ? new CheckBox { IsChecked = true, IsHitTestVisible = false } : null,
                Tag = column
            };

            menuItem.Click += (sender, _) =>
            {
                if (sender is MenuItem item && item.Tag is DataGridColumn col)
                {
                    // Don't hide the last visible column
                    var visibleCount = _playersGrid.Columns.Count(c => c.IsVisible);
                    if (col.IsVisible && visibleCount <= 1)
                    {
                        return;
                    }

                    col.IsVisible = !col.IsVisible;
                    item.Icon = col.IsVisible ? new CheckBox { IsChecked = true, IsHitTestVisible = false } : null;
                }
            };

            _columnsFlyout.Items.Add(menuItem);
        }

        if (_playersGrid.Columns.Count > 0)
        {
            _columnsFlyout.Items.Add(new Separator());

            var showAllItem = new MenuItem { Header = "Show All" };
            showAllItem.Click += (_, _) =>
            {
                foreach (var col in _playersGrid.Columns)
                {
                    col.IsVisible = true;
                }
                BuildColumnsFlyout();
            };
            _columnsFlyout.Items.Add(showAllItem);
        }
    }
}
