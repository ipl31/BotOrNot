using System.ComponentModel;
using System.Reflection;
using FortniteReplayReader;
using Unreal.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace BotOrNot;

public sealed class MainForm : Form
{
    private readonly Button _openBtn;
    private readonly DataGridView _grid;
    private readonly ListBox _elimList;
    private readonly TextBox _metaBox;
    private readonly ContextMenuStrip _colMenu = new();
    private readonly Button _columnsBtn;
    
    private readonly Bitmap _imgTrue = Dots.Green();
    private readonly Bitmap _imgFalse = Dots.Red();


    private BindingList<PlayerRow> _players = new();

    public MainForm()
        {
    Text = "Bot or Not?";
    Width = 1100;
    Height = 700;

    var layout = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 4
    };
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // controls row
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 10));

    // --- Top bar ---
    _openBtn = new Button { Text = "Open Replay...", AutoSize = true };
    _openBtn.Click += OpenReplay_Click;

    _columnsBtn = new Button { Text = "Columns ▾", AutoSize = true };
    _columnsBtn.Click += (s, e) => { BuildColumnMenu(); _colMenu.Show(_columnsBtn, 0, _columnsBtn.Height); };

    var topbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
    topbar.Controls.AddRange(new Control[] { _openBtn, _columnsBtn });
    layout.Controls.Add(topbar, 0, 0);

    // --- Grid (create BEFORE wiring events!) ---
    _grid = new DataGridView
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        DataSource = _players
    };
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Id", DataPropertyName = "Id", Width = 280, Visible = false});
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 180 });
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Level", DataPropertyName = "Level", Width = 180 });
    _grid.Columns.Add(new DataGridViewImageColumn { HeaderText = "Bot", Name = "BotIcon", Width = 28, ImageLayout = DataGridViewImageCellLayout.Zoom});
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Platform", DataPropertyName = "Platform", Width = 100 });
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kills", DataPropertyName = "Kills", Width = 60 });
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DeathCause", DataPropertyName = "DeathCause", Width = 140 });
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pickaxe", DataPropertyName = "Pickaxe", Width = 150, Visible = false });
    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Glider", DataPropertyName = "Glider", Width = 150, Visible = false });
    
    _grid.RowTemplate.Height = Math.Max(_grid.RowTemplate.Height, 22);
   
    // Add fromatting hooks for bitmaps
    _grid.CellFormatting += (s, e) =>
    {
        if (_grid.Columns[e.ColumnIndex].Name == "BotIcon" && e.RowIndex >= 0)
        {
            var row = (PlayerRow)_grid.Rows[e.RowIndex].DataBoundItem;
            var v = row.Bot;

            if (!string.IsNullOrEmpty(v) && v.Equals("true", StringComparison.OrdinalIgnoreCase))
                e.Value = _imgTrue;
            else if (!string.IsNullOrEmpty(v) && v.Equals("false", StringComparison.OrdinalIgnoreCase))
                e.Value = _imgFalse;
            e.FormattingApplied = true; // prevent default text formatting
        }
    };

    // NOW safe to wire the header click menu
    _grid.ColumnHeaderMouseClick += (s, e) =>
    {
        if (Control.MouseButtons == MouseButtons.Right)
        {
            BuildColumnMenu();
            _colMenu.Show(_grid, _grid.PointToClient(Cursor.Position));
        }
    };

    layout.Controls.Add(_grid, 0, 1);

    // --- Eliminations list ---
    _elimList = new ListBox { Dock = DockStyle.Fill };
    layout.Controls.Add(_elimList, 0, 2);

    // --- Meta box ---
    _metaBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
    layout.Controls.Add(_metaBox, 0, 3);

    Controls.Add(layout);
}
    
    private void BuildColumnMenu()
    {
        _colMenu.Items.Clear();

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            var item = new ToolStripMenuItem(col.HeaderText)
            {
                Checked = col.Visible,
                CheckOnClick = false, // we toggle manually to enforce "keep at least one visible"
                Tag = col
            };

            item.Click += (sender, _) =>
            {
                var it = (ToolStripMenuItem)sender!;
                var c  = (DataGridViewColumn)it.Tag!;

                int visibleCount = _grid.Columns.Cast<DataGridViewColumn>().Count(x => x.Visible);
                bool willShow = !it.Checked; // clicking flips the state

                if (!willShow && visibleCount <= 1)
                {
                    System.Media.SystemSounds.Beep.Play(); // don’t hide the last visible column
                    return;
                }

                c.Visible = willShow;
                it.Checked = willShow;
            };

            _colMenu.Items.Add(item);
        }

        if (_grid.Columns.Count > 0)
        {
            _colMenu.Items.Add(new ToolStripSeparator());
            _colMenu.Items.Add(new ToolStripMenuItem("Show All", null, (_, __) =>
            {
                foreach (DataGridViewColumn c in _grid.Columns) c.Visible = true;
                foreach (ToolStripItem tsi in _colMenu.Items)
                    if (tsi is ToolStripMenuItem t && t.Tag is DataGridViewColumn) t.Checked = true;
            }));
        }
    }


    private async void OpenReplay_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Fortnite Replay File",
            Filter = "Fortnite replays (*.replay)|*.replay|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _openBtn.Enabled = false;
            try
            {
                await LoadReplayAsync(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to read replay:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _openBtn.Enabled = true;
            }
        }
    }

    private async Task LoadReplayAsync(string path)
    {
        // set up logger
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Error));
        var logger = loggerFactory.CreateLogger<MainForm>();
        var reader = new ReplayReader(logger, ParseMode.Full);

        _players.Clear();
        _elimList.Items.Clear();
        _metaBox.Clear();

        var result = await Task.Run(() => reader.ReadReplay(path));

        // Build player map
        var playersById = new Dictionary<string, PlayerRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var pd in result.PlayerData ?? Enumerable.Empty<object>())
        {
            var id = ReflectionUtils.FirstString(pd, "PlayerId", "UniqueId", "NetId");
            var name = ReflectionUtils.FirstString(pd, "PlayerName", "DisplayName", "Name");
            var bot = ReflectionUtils.FirstString(pd, "IsBot");
            var platform = ReflectionUtils.FirstString(pd, "Platform");
            var kills = ReflectionUtils.FirstString(pd, "Kills");
            var death = ReflectionUtils.FirstString(pd, "DeathCause");

            var cosmetics = ReflectionUtils.GetObject(pd, "Cosmetics");
            var pickaxe = ReflectionUtils.FirstString(cosmetics, "Pickaxe") ?? "unknown";
            var glider = ReflectionUtils.FirstString(cosmetics, "Glider") ?? "unknown";

            var key = !string.IsNullOrWhiteSpace(id) ? id
                : !string.IsNullOrWhiteSpace(name) ? name
                : Guid.NewGuid().ToString("N");

            if (!playersById.TryGetValue(key, out var row))
            {
                row = new PlayerRow { Id = key };
                playersById[key] = row;
            }

            row.Name = string.IsNullOrWhiteSpace(name) ? (row.Name ?? "unknown") : name;
            row.Bot = string.IsNullOrWhiteSpace(bot) ? (row.Bot ?? "unknown") : bot;
            row.Platform = platform ?? row.Platform;
            row.Kills = string.IsNullOrWhiteSpace(kills) ? (row.Kills ?? "unknown") : kills;
            row.DeathCause = string.IsNullOrWhiteSpace(death) ? (row.DeathCause ?? "unknown") : death;
            row.Pickaxe = pickaxe;
            row.Glider = glider;
        }

        // Bind players
        foreach (var p in playersById.Values.OrderBy(v => v.Name ?? v.Id))
            _players.Add(p);

        // Meta
        var header = result.Header;
        _metaBox.Text =
            $"File: {Path.GetFileName(path)}{Environment.NewLine}" +
            $"Version: {header.Major}.{header.Minor}{Environment.NewLine}" +
            $"GameNet Protocol: {header.GameNetworkProtocolVersion}{Environment.NewLine}" +
            $"Players: {playersById.Count}{Environment.NewLine}" +
            $"Eliminations: {result.Eliminations?.Count ?? 0}";

        // Eliminations list (best-effort via reflection)
        foreach (var elim in result.Eliminations ?? Enumerable.Empty<object>())
        {
            // Try "EliminatedInfo.Id" first
            var elimInfo = ReflectionUtils.GetObject(elim, "EliminatedInfo");
            var eliminatedId = ReflectionUtils.FirstString(elimInfo, "Id")
                               ?? ReflectionUtils.FirstString(elim, "Eliminated")
                               ?? "unknown";

            var display = playersById.TryGetValue(eliminatedId, out var row)
                ? $"{row.Name ?? row.Id} (bot={row.Bot}, kills={row.Kills})"
                : eliminatedId;

            _elimList.Items.Add(display);
        }
    }

    private sealed class PlayerRow
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public string? Bot { get; set; }
        public string? Platform { get; set; }
        public string? Kills { get; set; }
        public string? DeathCause { get; set; }
        public string? Pickaxe { get; set; }
        public string? Glider { get; set; }
    }
}