using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BotOrNot.Avalonia.ViewModels;
using ReactiveUI;

namespace BotOrNot.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly MenuFlyout _columnsFlyout;
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

        // Build the columns menu when the window loads
        Loaded += (_, _) => BuildColumnsFlyout();
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