using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace TearsOfMetal.Trainer;

internal sealed class TrainerForm : Form
{
    private static readonly (string Label, string Key, Color Accent)[] Currencies =
    [
        ("Coins", "Wallet_COIN", Color.FromArgb(240, 193, 75)),
        ("Crosses", "Wallet_CROSS", Color.FromArgb(226, 226, 221)),
        ("Statues", "Wallet_STATUE", Color.FromArgb(191, 145, 93)),
        ("Gems", "Wallet_GEM", Color.FromArgb(72, 198, 211)),
        ("Scrolls", "Wallet_SCROLL", Color.FromArgb(211, 151, 220)),
        ("Meteors", "Wallet_METEOR", Color.FromArgb(238, 100, 74))
    ];

    private readonly Dictionary<string, NumericUpDown> _currencyInputs = [];
    private readonly Label _saveStatus = new();
    private readonly Label _gameStatus = new();
    private readonly Label _message = new();
    private readonly Button _applyButton;
    private readonly Button _damageBlockButton;
    private readonly Label _damageBlockStatus = new();
    private readonly CheckBox _automaticBackup = new();
    private readonly System.Windows.Forms.Timer _processTimer = new() { Interval = 1000 };
    private readonly DamageBlockPatch _damageBlockPatch = new();

    private SaveGame? _save;
    private bool _gameRunning;

    public TrainerForm()
    {
        Text = "Tears of Metal Trainer";
        ClientSize = new Size(780, 720);
        MinimumSize = new Size(796, 759);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(16, 18, 22);
        ForeColor = Color.FromArgb(236, 233, 225);
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(28, 22, 28, 22)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildStatusPanel(), 0, 1);
        _damageBlockButton = CreateButton("Enable no damage", Color.FromArgb(55, 93, 124), Color.White);
        _damageBlockButton.Click += (_, _) => ToggleDamageBlock();
        root.Controls.Add(BuildLivePanel(), 0, 2);
        root.Controls.Add(BuildCurrencyPanel(), 0, 3);

        _applyButton = CreateButton("Apply changes", Color.FromArgb(178, 52, 44), Color.White);
        _applyButton.Click += (_, _) => ApplyChanges();
        root.Controls.Add(BuildActions(), 0, 4);

        _message.Dock = DockStyle.Fill;
        _message.TextAlign = ContentAlignment.MiddleLeft;
        _message.ForeColor = Color.FromArgb(163, 168, 176);
        _message.Text = "Edits are local and intended for offline play.";
        root.Controls.Add(_message, 0, 5);

        _processTimer.Tick += (_, _) => RefreshGameStatus();
        _processTimer.Start();
        FormClosing += (_, _) => RestoreDamageMethodOnExit();

        Shown += (_, _) =>
        {
            RefreshGameStatus();
            LoadSave();
        };
    }

    private Control BuildLivePanel()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0)
        };
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 34, 42),
            Padding = new Padding(18, 10, 18, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 46));

        var title = new Label
        {
            Text = "◆  No player damage",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(112, 188, 230),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var description = new Label
        {
            Text = "Prevents resolved hit damage from being subtracted from HP",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(151, 160, 171),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _damageBlockStatus.Text = "Launch the game to enable";
        _damageBlockStatus.Dock = DockStyle.Fill;
        _damageBlockStatus.TextAlign = ContentAlignment.MiddleLeft;
        _damageBlockStatus.ForeColor = Color.FromArgb(151, 160, 171);

        _damageBlockButton.Dock = DockStyle.Fill;
        _damageBlockButton.Margin = new Padding(8, 3, 0, 3);
        _damageBlockButton.Enabled = false;

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(description, 0, 1);
        layout.Controls.Add(_damageBlockStatus, 1, 0);
        layout.SetRowSpan(_damageBlockStatus, 2);
        layout.Controls.Add(_damageBlockButton, 2, 0);
        layout.SetRowSpan(_damageBlockButton, 2);
        panel.Controls.Add(layout);
        outer.Controls.Add(panel);
        return outer;
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        var title = new Label
        {
            AutoSize = true,
            Text = "TEARS OF METAL",
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(238, 226, 201),
            Location = new Point(0, 0)
        };
        var subtitle = new Label
        {
            AutoSize = true,
            Text = "LOCAL SAVE TRAINER  •  BUILD 56935",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(178, 52, 44),
            Location = new Point(3, 45)
        };
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        return panel;
    }

    private Control BuildStatusPanel()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(25, 28, 34),
            Padding = new Padding(18, 10, 18, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        _saveStatus.Text = "Save: locating…";
        _saveStatus.AutoEllipsis = true;
        _saveStatus.Dock = DockStyle.Fill;
        _saveStatus.TextAlign = ContentAlignment.MiddleLeft;

        _gameStatus.Text = "Game: checking…";
        _gameStatus.Dock = DockStyle.Fill;
        _gameStatus.TextAlign = ContentAlignment.MiddleRight;

        var path = new Label
        {
            Text = SaveGame.DefaultSavePath,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(124, 130, 140),
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var openFolder = new LinkLabel
        {
            Text = "Open save folder",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            LinkColor = Color.FromArgb(203, 166, 98),
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Color.FromArgb(203, 166, 98)
        };
        openFolder.LinkClicked += (_, _) => OpenSaveFolder();

        layout.Controls.Add(_saveStatus, 0, 0);
        layout.Controls.Add(_gameStatus, 1, 0);
        layout.Controls.Add(path, 0, 1);
        layout.Controls.Add(openFolder, 1, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildCurrencyPanel()
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 16, 0, 8)
        };
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Text = "Persistent currencies",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        section.Controls.Add(heading, 0, 0);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var row = 0; row < 3; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        }

        for (var index = 0; index < Currencies.Length; index++)
        {
            var currency = Currencies[index];
            var card = BuildCurrencyCard(currency.Label, currency.Key, currency.Accent);
            card.Margin = index % 2 == 0
                ? new Padding(0, 0, 8, 8)
                : new Padding(8, 0, 0, 8);
            grid.Controls.Add(card, index % 2, index / 2);
        }

        section.Controls.Add(grid, 0, 1);
        return section;
    }

    private Control BuildCurrencyCard(string label, string key, Color accent)
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(25, 28, 34),
            Padding = new Padding(16, 12, 16, 12)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));

        var name = new Label
        {
            Text = $"●  {label}",
            ForeColor = accent,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var input = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 2_000_000_000,
            ThousandsSeparator = true,
            Dock = DockStyle.Fill,
            TextAlign = HorizontalAlignment.Right,
            BackColor = Color.FromArgb(35, 39, 47),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI Semibold", 11F)
        };
        _currencyInputs[key] = input;
        layout.Controls.Add(name, 0, 0);
        layout.Controls.Add(input, 1, 0);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildActions()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var reload = CreateButton("Reload save", Color.FromArgb(42, 46, 55), Color.White);
        reload.Click += (_, _) => LoadSave();
        var maxAll = CreateButton("Max all", Color.FromArgb(42, 46, 55), Color.White);
        maxAll.Click += (_, _) => SetAll(999_999);
        var backup = CreateButton("Backup", Color.FromArgb(42, 46, 55), Color.White);
        backup.Click += (_, _) => CreateBackup();
        var restore = CreateButton("Restore…", Color.FromArgb(42, 46, 55), Color.White);
        restore.Click += (_, _) => RestoreBackup();
        var launch = CreateButton("Launch game", Color.FromArgb(69, 91, 71), Color.White);
        launch.Click += (_, _) => LaunchGame();

        panel.Controls.Add(reload, 0, 0);
        panel.Controls.Add(maxAll, 1, 0);
        panel.Controls.Add(backup, 2, 0);
        panel.Controls.Add(restore, 3, 0);
        panel.Controls.Add(launch, 4, 0);

        _automaticBackup.Text = "Create a backup before applying";
        _automaticBackup.Checked = true;
        _automaticBackup.AutoSize = true;
        _automaticBackup.ForeColor = Color.FromArgb(178, 182, 189);
        _automaticBackup.Anchor = AnchorStyles.Left;
        panel.Controls.Add(_automaticBackup, 0, 1);
        panel.SetColumnSpan(_automaticBackup, 3);

        _applyButton.Dock = DockStyle.Fill;
        _applyButton.Margin = new Padding(7, 0, 0, 0);
        panel.Controls.Add(_applyButton, 3, 1);
        panel.SetColumnSpan(_applyButton, 2);
        return panel;
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        return new Button
        {
            Text = text,
            BackColor = background,
            ForeColor = foreground,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            UseVisualStyleBackColor = false
        }.WithFlatBorder(Color.FromArgb(65, 70, 80));
    }

    private void LoadSave()
    {
        try
        {
            _save = SaveGame.Load(SaveGame.DefaultSavePath);
            foreach (var currency in Currencies)
            {
                _currencyInputs[currency.Key].Value = _save.GetInt32(currency.Key);
            }

            _saveStatus.Text = $"Save: loaded  •  {_save.EntryCount:N0} fields";
            _saveStatus.ForeColor = Color.FromArgb(117, 203, 133);
            SetMessage("Values reloaded from disk.", success: true);
            RefreshGameStatus();
        }
        catch (Exception exception)
        {
            _save = null;
            _saveStatus.Text = "Save: unavailable";
            _saveStatus.ForeColor = Color.FromArgb(238, 100, 74);
            SetMessage(exception.Message, success: false);
        }
    }

    private void ApplyChanges()
    {
        if (!EnsureGameClosed())
        {
            return;
        }

        try
        {
            var save = SaveGame.Load(SaveGame.DefaultSavePath);
            foreach (var currency in Currencies)
            {
                save.SetInt32(currency.Key, decimal.ToInt32(_currencyInputs[currency.Key].Value));
            }

            var backupPath = save.Save(_automaticBackup.Checked);
            _save = SaveGame.Load(SaveGame.DefaultSavePath);
            SetMessage(
                string.IsNullOrEmpty(backupPath)
                    ? "Changes applied successfully."
                    : $"Changes applied. Backup: {System.IO.Path.GetFileName(backupPath)}",
                success: true);
        }
        catch (Exception exception)
        {
            SetMessage($"Could not apply changes: {exception.Message}", success: false);
            MessageBox.Show(this, exception.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetAll(int value)
    {
        foreach (var input in _currencyInputs.Values)
        {
            input.Value = value;
        }

        SetMessage($"All currency fields set to {value:N0}. Click Apply changes to save.", success: true);
    }

    private void CreateBackup()
    {
        if (!EnsureGameClosed())
        {
            return;
        }

        try
        {
            var save = SaveGame.Load(SaveGame.DefaultSavePath);
            var backupPath = save.CreateBackup();
            SetMessage($"Backup created: {System.IO.Path.GetFileName(backupPath)}", success: true);
        }
        catch (Exception exception)
        {
            SetMessage($"Backup failed: {exception.Message}", success: false);
        }
    }

    private void RestoreBackup()
    {
        if (!EnsureGameClosed())
        {
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(SaveGame.DefaultSavePath) ?? string.Empty;
        using var dialog = new OpenFileDialog
        {
            Title = "Restore a Tears of Metal trainer backup",
            InitialDirectory = directory,
            Filter = "Trainer backups (*.trainer-backup.*.txt)|*.trainer-backup.*.txt|Text files (*.txt)|*.txt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            "Restore this backup over the current save? A safety backup of the current save will be created first.",
            "Restore backup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var current = SaveGame.Load(SaveGame.DefaultSavePath);
            _ = current.CreateBackup();
            SaveGame.RestoreBackup(dialog.FileName, SaveGame.DefaultSavePath);
            LoadSave();
            SetMessage($"Restored {System.IO.Path.GetFileName(dialog.FileName)}.", success: true);
        }
        catch (Exception exception)
        {
            SetMessage($"Restore failed: {exception.Message}", success: false);
        }
    }

    private void LaunchGame()
    {
        RefreshGameStatus();
        if (_gameRunning)
        {
            SetMessage("The game is already running.", success: false);
            return;
        }

        var executable = FindGameExecutable();
        if (executable is null)
        {
            SetMessage("ToM.exe was not found beside the Trainer folder.", success: false);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = System.IO.Path.GetDirectoryName(executable),
                UseShellExecute = true
            });
            SetMessage("Launching Tears of Metal…", success: true);
        }
        catch (Exception exception)
        {
            SetMessage($"Launch failed: {exception.Message}", success: false);
        }
    }

    private void OpenSaveFolder()
    {
        var directory = System.IO.Path.GetDirectoryName(SaveGame.DefaultSavePath);
        if (directory is null || !Directory.Exists(directory))
        {
            SetMessage("The save folder does not exist yet.", success: false);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
    }

    private void RefreshGameStatus()
    {
        var processes = Process.GetProcessesByName("ToM");
        int? processId;
        try
        {
            _gameRunning = processes.Length > 0;
            processId = DamageBlockPatch.FindGameProcessId();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        _gameStatus.Text = _gameRunning ? "●  Game running" : "●  Game closed";
        _gameStatus.ForeColor = _gameRunning
            ? Color.FromArgb(238, 100, 74)
            : Color.FromArgb(117, 203, 133);
        _applyButton.Enabled = !_gameRunning && _save is not null;
        _applyButton.BackColor = _applyButton.Enabled
            ? Color.FromArgb(178, 52, 44)
            : Color.FromArgb(67, 55, 55);
        RefreshDamageBlockStatus(processId);
    }

    private void RefreshDamageBlockStatus(int? processId)
    {
        DamageBlockState state;
        try
        {
            state = _damageBlockPatch.GetState(processId);
        }
        catch (Exception exception)
        {
            _damageBlockStatus.Text = exception.Message;
            _damageBlockStatus.ForeColor = Color.FromArgb(238, 120, 100);
            _damageBlockButton.Enabled = false;
            return;
        }

        switch (state)
        {
            case DamageBlockState.Enabled:
                _damageBlockStatus.Text = $"●  HP subtraction blocked  •  PID {processId}";
                _damageBlockStatus.ForeColor = Color.FromArgb(117, 203, 133);
                _damageBlockButton.Text = "Disable no damage";
                _damageBlockButton.BackColor = Color.FromArgb(126, 60, 57);
                _damageBlockButton.Enabled = true;
                break;
            case DamageBlockState.Disabled:
                _damageBlockStatus.Text = "Ready  •  offline play only";
                _damageBlockStatus.ForeColor = Color.FromArgb(112, 188, 230);
                _damageBlockButton.Text = "Enable no damage";
                _damageBlockButton.BackColor = Color.FromArgb(55, 93, 124);
                _damageBlockButton.Enabled = true;
                break;
            case DamageBlockState.Unsupported:
                _damageBlockStatus.Text = "Unsupported game build";
                _damageBlockStatus.ForeColor = Color.FromArgb(238, 120, 100);
                _damageBlockButton.Text = "Signature mismatch";
                _damageBlockButton.Enabled = false;
                break;
            default:
                _damageBlockStatus.Text = "Launch the game to enable";
                _damageBlockStatus.ForeColor = Color.FromArgb(151, 160, 171);
                _damageBlockButton.Text = "Enable no damage";
                _damageBlockButton.BackColor = Color.FromArgb(55, 93, 124);
                _damageBlockButton.Enabled = false;
                break;
        }
    }

    private void ToggleDamageBlock()
    {
        try
        {
            var state = _damageBlockPatch.GetState();
            if (state == DamageBlockState.Enabled)
            {
                _damageBlockPatch.Disable();
                SetMessage("No-damage mode disabled; the original HP subtraction was restored.", success: true);
            }
            else if (state == DamageBlockState.Disabled)
            {
                _damageBlockPatch.Enable();
                SetMessage(
                    $"HP subtraction blocked and verified. {_damageBlockPatch.GetDiagnosticSummary()}",
                    success: true);
            }

            RefreshGameStatus();
        }
        catch (Exception exception)
        {
            SetMessage($"No-damage mode failed: {exception.Message}", success: false);
            MessageBox.Show(
                this,
                exception.Message,
                "No player damage",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RestoreDamageMethodOnExit()
    {
        try
        {
            if (_damageBlockPatch.PatchedProcessId is not null)
            {
                _damageBlockPatch.Disable();
            }
        }
        catch
        {
            // The game may have exited between the final timer tick and this close event.
        }
    }

    private bool EnsureGameClosed()
    {
        RefreshGameStatus();
        if (!_gameRunning)
        {
            return true;
        }

        SetMessage("Close the game before editing so it cannot overwrite the save.", success: false);
        MessageBox.Show(
            this,
            "Close Tears of Metal before changing its save. The game writes this file while running and could overwrite trainer changes.",
            "Game is running",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
    }

    private void SetMessage(string text, bool success)
    {
        _message.Text = text;
        _message.ForeColor = success
            ? Color.FromArgb(117, 203, 133)
            : Color.FromArgb(238, 120, 100);
    }

    private static string? FindGameExecutable()
    {
        var candidates = new[]
        {
            System.IO.Path.Combine(AppContext.BaseDirectory, "ToM.exe"),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "ToM.exe")),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "ToM.exe"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}

internal sealed class RoundedPanel : Panel
{
    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        using var path = new GraphicsPath();
        const int radius = 12;
        var bounds = new Rectangle(0, 0, Width, Height);
        path.AddArc(bounds.Left, bounds.Top, radius, radius, 180, 90);
        path.AddArc(bounds.Right - radius, bounds.Top, radius, radius, 270, 90);
        path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }
}

internal static class ButtonExtensions
{
    public static Button WithFlatBorder(this Button button, Color color)
    {
        button.FlatAppearance.BorderColor = color;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(
            Math.Min(255, button.BackColor.R + 12),
            Math.Min(255, button.BackColor.G + 12),
            Math.Min(255, button.BackColor.B + 12));
        return button;
    }
}
