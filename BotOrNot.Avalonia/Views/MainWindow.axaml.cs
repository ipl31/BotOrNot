using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BotOrNot.Avalonia.ViewModels;
using BotOrNot.Core.Models;
using ReactiveUI;

namespace BotOrNot.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly MenuFlyout _columnsFlyout;
    private readonly Dictionary<DataGridColumn, bool> _columnSortDescending = new();
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
        var (selector, numeric, bot) = GetColumnSortInfo(e.Column);
        if (selector == null) return;

        // Track direction ourselves (Avalonia doesn't expose SortDirection on DataGridColumn)
        _columnSortDescending.TryGetValue(e.Column, out var wasDescending);
        var willBeDescending = !wasDescending;
        _columnSortDescending[e.Column] = willBeDescending;

        e.Column.CustomSortComparer = new PlayerRowSortComparer(
            selector, descending: willBeDescending, numeric: numeric, isBotField: bot);
    }

    private static (Func<PlayerRow, string?>? selector, bool numeric, bool bot) GetColumnSortInfo(
        DataGridColumn column)
    {
        return column.Header?.ToString() switch
        {
            "Id" => (p => p.Id, false, false),
            "Name" => (p => p.Name, false, false),
            "Level" => (p => p.Level, true, false),
            "Bot" => (p => p.Bot, false, true),
            "Platform" => (p => p.Platform, false, false),
            "Kills" => (p => p.Kills, true, false),
            "Squad" => (p => p.TeamIndex, true, false),
            "Placement" => (p => p.Placement, true, false),
            "Death Cause" => (p => p.DeathCause, false, false),
            "Pickaxe" => (p => p.Pickaxe, false, false),
            "Glider" => (p => p.Glider, false, false),
            _ => (null, false, false)
        };
    }

    private async void OpenReplay_Click(object? sender, RoutedEventArgs e)
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