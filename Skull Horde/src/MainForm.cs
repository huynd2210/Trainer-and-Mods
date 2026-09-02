using System.Diagnostics;
using System.Globalization;

namespace SkullHordeTrainer;

internal sealed class MainForm : Form
{
    private readonly Color _background = Color.FromArgb(22, 24, 29);
    private readonly Color _panel = Color.FromArgb(32, 35, 42);
    private readonly Color _accent = Color.FromArgb(204, 151, 55);
    private readonly Color _muted = Color.FromArgb(166, 171, 181);

    private readonly Label _status = new() { AutoSize = false, Height = 42 };
    private readonly ComboBox _character = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly NumericUpDown _skillPoints = MakeNumberInput();
    private readonly NumericUpDown _xp = MakeNumberInput();
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly TextBox _advancedValue = new() { Width = 260 };
    private readonly Label _advancedType = new() { AutoSize = true };
    private readonly ComboBox _skillCharacter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _skill = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 500 };
    private readonly NumericUpDown _skillLevel = new() { Width = 150, Maximum = 10_000, ThousandsSeparator = true };
    private readonly Label _skillCurrent = new() { AutoSize = true };
    private readonly Label _liveStatus = new() { AutoSize = false, Height = 44, Width = 700 };
    private DefoldSave? _save;
    private LuaValue? _selectedValue;
    private bool _dirty;
    private LiveDebugHotkeys? _liveHotkeys;

    public MainForm()
    {
        Text = "Skull Horde Trainer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 620);
        Size = new Size(900, 700);
        BackColor = _background;
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10F);

        Controls.Add(BuildLayout());
        Shown += (_, _) => LoadSave(showErrors: true);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "SKULL HORDE  •  PROGRESSION TRAINER",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 17F),
            ForeColor = _accent,
            Margin = new Padding(0, 0, 0, 12),
        };
        root.Controls.Add(title, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildQuickTab());
        tabs.TabPages.Add(BuildSkillLabTab());
        tabs.TabPages.Add(BuildLiveHotkeysTab());
        tabs.TabPages.Add(BuildAdvancedTab());
        root.Controls.Add(tabs, 0, 1);

        _status.ForeColor = _muted;
        _status.Padding = new Padding(2, 8, 2, 0);
        root.Controls.Add(_status, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        buttons.Controls.Add(MakeButton("Reload", (_, _) => LoadSave(showErrors: true)));
        buttons.Controls.Add(MakeButton("Save Changes", (_, _) => SaveChanges(), primary: true));
        buttons.Controls.Add(MakeButton("Restore Latest Backup", (_, _) => RestoreLatestBackup()));
        buttons.Controls.Add(MakeButton("Open Save Folder", (_, _) => OpenSaveFolder()));
        buttons.Controls.Add(MakeButton("Launch Game", (_, _) => LaunchGame()));
        root.Controls.Add(buttons, 0, 3);

        return root;
    }

    private TabPage BuildQuickTab()
    {
        var page = MakeTab("Quick Trainer");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var note = new Label
        {
            Text = "Close the game before saving. Every write creates a timestamped backup first.",
            AutoSize = true,
            ForeColor = _muted,
            Margin = new Padding(0, 0, 0, 14),
        };
        root.Controls.Add(note, 0, 0);

        var progression = new GroupBox
        {
            Text = "Character progression",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12),
            ForeColor = Color.WhiteSmoke,
        };
        var fields = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Top };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.Controls.Add(MakeLabel("Character"), 0, 0);
        fields.Controls.Add(MakeLabel("Skill points"), 1, 0);
        fields.Controls.Add(MakeLabel("XP"), 2, 0);
        fields.Controls.Add(_character, 0, 1);
        fields.Controls.Add(_skillPoints, 1, 1);
        fields.Controls.Add(_xp, 2, 1);
        fields.Controls.Add(MakeButton("Apply selected values", (_, _) => ApplyCharacterValues(), primary: true), 0, 2);
        fields.SetColumnSpan(fields.GetControlFromPosition(0, 2)!, 3);
        _character.SelectedIndexChanged += (_, _) => RefreshCharacterValues();
        progression.Controls.Add(fields);
        root.Controls.Add(progression, 0, 1);

        var actions = new GroupBox
        {
            Text = "Quick actions",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12),
            Margin = new Padding(0, 14, 0, 0),
            ForeColor = Color.WhiteSmoke,
        };
        var actionButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        actionButtons.Controls.Add(MakeButton("999 skill points (all)", (_, _) => SetAllCharacters("skill_points", 999)));
        actionButtons.Controls.Add(MakeButton("100,000 XP (all)", (_, _) => SetAllCharacters("character_xp", 100_000)));
        actionButtons.Controls.Add(MakeButton("Unlock known content", (_, _) => SetBooleanSection("unlocks", true)));
        actionButtons.Controls.Add(MakeButton("Unlock achievements", (_, _) => SetBooleanSection("achievements", true)));
        actionButtons.Controls.Add(MakeButton("Skip tutorials", (_, _) => SetBooleanSection("tutorial", true)));
        actionButtons.Controls.Add(MakeButton("UNLOCK EVERYTHING + CHARACTERS", (_, _) => UnlockEverything(), primary: true));
        actionButtons.Controls.Add(MakeButton("MAX PROGRESSION PRESET", (_, _) => ApplyMaxPreset(), primary: true));
        actions.Controls.Add(actionButtons);
        root.Controls.Add(actions, 0, 2);

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildSkillLabTab()
    {
        var page = MakeTab("Unlimited Skill Lab");
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
        };
        root.Controls.Add(new Label
        {
            Text = "Directly set any skill to any level (0–10,000). This bypasses prerequisites, mutually-exclusive branches, and the original cap. Repeated entries are applied one-by-one when a run starts.",
            AutoSize = false,
            Width = 790,
            Height = 55,
            ForeColor = _muted,
        });
        root.Controls.Add(MakeLabel("Character"));
        root.Controls.Add(_skillCharacter);
        root.Controls.Add(MakeLabel("Skill"));
        root.Controls.Add(_skill);
        root.Controls.Add(_skillCurrent);
        root.Controls.Add(MakeLabel("New total level"));
        root.Controls.Add(_skillLevel);
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 790 };
        buttons.Controls.Add(MakeButton("Set selected level", (_, _) => SetSelectedSkillLevel(), primary: true));
        buttons.Controls.Add(MakeButton("+1 selected", (_, _) => AddSelectedSkillLevels(1)));
        buttons.Controls.Add(MakeButton("+10 selected", (_, _) => AddSelectedSkillLevels(10)));
        buttons.Controls.Add(MakeButton("Every skill → level 1", (_, _) => SetEverySkillLevel(1)));
        root.Controls.Add(buttons);
        root.Controls.Add(new Label
        {
            Text = "Very high levels can make cooldowns, costs, or one-time effects behave strangely. Increase gradually and keep the automatic backups.",
            AutoSize = false,
            Width = 790,
            Height = 45,
            ForeColor = Color.FromArgb(235, 178, 90),
        });
        _skillCharacter.Items.AddRange(GameCatalog.Characters.Cast<object>().ToArray());
        _skillCharacter.SelectedIndexChanged += (_, _) => PopulateSkills();
        _skill.SelectedIndexChanged += (_, _) => RefreshSkillLevel();
        _skillCharacter.SelectedIndex = 0;
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildLiveHotkeysTab()
    {
        var page = MakeTab("Live F-Hotkeys");
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
        };
        root.Controls.Add(new Label
        {
            Text = "These global hotkeys drive Skull Horde's built-in developer console. Keep this trainer running, then use them in a run.",
            AutoSize = false,
            Width = 790,
            Height = 45,
            ForeColor = _muted,
        });
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
        string[] rows =
        [
            "F2  Toggle automatic +999 XP", "F3  Toggle automatic +999 ducats",
            "F5  Toggle developer console", "F6  +999 XP (triggers level-ups)",
            "F7  +999 ducats", "F8  Enable invincibility",
            "F9  Reveal full map", "F10  Drop legendary item",
            "F11  Massive combat power", "F12  Advance to next floor",
        ];
        for (int i = 0; i < rows.Length; i++)
            grid.Controls.Add(new Label { Text = rows[i], AutoSize = true, Margin = new Padding(8, 6, 28, 6) }, i % 2, i / 2);
        root.Controls.Add(grid);
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 790 };
        buttons.Controls.Add(MakeButton("+999 XP", (_, _) => _liveHotkeys?.Run("givexp"), primary: true));
        buttons.Controls.Add(MakeButton("+999 ducats", (_, _) => _liveHotkeys?.Run("givemoney"), primary: true));
        buttons.Controls.Add(MakeButton("Invincible", (_, _) => _liveHotkeys?.Run("cheatinvincible")));
        buttons.Controls.Add(MakeButton("Reveal map", (_, _) => _liveHotkeys?.Run("seemap")));
        buttons.Controls.Add(MakeButton("Legendary drop", (_, _) => _liveHotkeys?.Run("droplegendary")));
        root.Controls.Add(buttons);
        _liveStatus.ForeColor = _muted;
        _liveStatus.Text = "Waiting for Skull Horde.";
        root.Controls.Add(_liveStatus);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildAdvancedTab()
    {
        var page = MakeTab("Advanced Editor");
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = _panel,
        };
        split.Resize += (_, _) =>
        {
            if (split.Width > 600)
                split.SplitterDistance = split.Width - 290;
        };
        _tree.BackColor = _panel;
        _tree.ForeColor = Color.WhiteSmoke;
        _tree.BorderStyle = BorderStyle.FixedSingle;
        _tree.AfterSelect += (_, e) =>
        {
            if (e.Node is not null) SelectAdvancedNode(e.Node);
        };
        split.Panel1.Controls.Add(_tree);

        var editor = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(12),
            WrapContents = false,
        };
        editor.Controls.Add(new Label { Text = "Selected value", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        editor.Controls.Add(_advancedType);
        editor.Controls.Add(_advancedValue);
        editor.Controls.Add(MakeButton("Apply field", (_, _) => ApplyAdvancedValue(), primary: true));
        editor.Controls.Add(new Label
        {
            Text = "Boolean values accept true/false. Numbers use a dot or your locale's decimal separator.",
            AutoSize = false,
            Width = 260,
            Height = 80,
            ForeColor = _muted,
        });
        split.Panel2.Controls.Add(editor);
        page.Controls.Add(split);
        return page;
    }

    private void LoadSave(bool showErrors)
    {
        try
        {
            if (!File.Exists(TrainerPaths.SaveFile))
                throw new FileNotFoundException("Start Skull Horde once so it creates a save file.", TrainerPaths.SaveFile);

            _save = DefoldSaveCodec.Read(TrainerPaths.SaveFile);
            _dirty = false;
            PopulateCharacters();
            RebuildTree();
            RefreshSkillLevel();
            SetStatus($"Loaded {TrainerPaths.SaveFile}");
        }
        catch (Exception ex)
        {
            _save = null;
            SetStatus(ex.Message, error: true);
            if (showErrors) ShowError("Could not load the save", ex);
        }
    }

    private LuaTable Savefile => _save?.Root.GetTable("savefile")
        ?? throw new InvalidOperationException("No save is loaded.");

    private void PopulateCharacters()
    {
        if (_save is null) return;
        string? selected = _character.SelectedItem as string;
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            "base", "tank", "low_tier", "yorick", "zombie"
        };

        foreach (string sectionName in new[] { "skill_points", "character_xp", "character_level" })
        {
            LuaTable? section = Savefile.GetTable(sectionName);
            if (section is null) continue;
            foreach (LuaEntry entry in section.Entries.Where(e => e.Key.Type == LuaType.String))
                names.Add((string)entry.Key.Value);
        }

        LuaTable? skills = Savefile.GetTable("skills");
        if (skills is not null)
        {
            foreach (LuaEntry entry in skills.Entries.Where(e => e.Value.Type == LuaType.Table))
            {
                LuaEntry? character = ((LuaTable)entry.Value.Value).Find("character");
                if (character?.Value is { Type: LuaType.String } value)
                    names.Add((string)value.Value);
            }
        }

        _character.BeginUpdate();
        _character.Items.Clear();
        _character.Items.AddRange(names.OrderBy(x => x).Cast<object>().ToArray());
        _character.EndUpdate();
        int oldIndex = selected is null ? -1 : _character.Items.IndexOf(selected);
        _character.SelectedIndex = oldIndex >= 0 ? oldIndex : 0;
    }

    private void RefreshCharacterValues()
    {
        if (_save is null || _character.SelectedItem is not string name) return;
        SetNumeric(_skillPoints, Savefile.GetTable("skill_points")?.GetNumber(name) ?? 0);
        SetNumeric(_xp, Savefile.GetTable("character_xp")?.GetNumber(name) ?? 0);
    }

    private void ApplyCharacterValues()
    {
        if (_save is null || _character.SelectedItem is not string name) return;
        Savefile.GetOrCreateTable("skill_points").SetNumber(name, decimal.ToDouble(_skillPoints.Value));
        Savefile.GetOrCreateTable("character_xp").SetNumber(name, decimal.ToDouble(_xp.Value));
        MarkDirty($"Updated progression for {name}. Click Save Changes to write it.");
    }

    private void SetAllCharacters(string sectionName, double value)
    {
        if (_save is null) return;
        LuaTable section = Savefile.GetOrCreateTable(sectionName);
        foreach (string character in _character.Items.Cast<string>())
            section.SetNumber(character, value);
        RefreshCharacterValues();
        MarkDirty($"Set {sectionName} to {value:N0} for {_character.Items.Count} characters.");
    }

    private void SetBooleanSection(string sectionName, bool value)
    {
        if (_save is null) return;
        LuaTable? section = Savefile.GetTable(sectionName);
        if (section is null)
        {
            SetStatus($"This save has no '{sectionName}' section.", error: true);
            return;
        }
        int changed = section.SetBooleanLeaves(value);
        MarkDirty($"Updated {changed} values in {sectionName}.");
    }

    private void ApplyMaxPreset()
    {
        if (_save is null) return;
        SetAllCharacters("skill_points", 999);
        SetAllCharacters("character_xp", 100_000);
        foreach (string section in new[] { "unlocks", "achievements", "tutorial" })
            Savefile.GetTable(section)?.SetBooleanLeaves(true);
        MarkDirty("Max progression preset is ready. Click Save Changes to write it.");
    }

    private void UnlockEverything()
    {
        if (_save is null) return;
        SaveEdits.UnlockEverything(_save);
        PopulateCharacters();
        RefreshCharacterValues();
        MarkDirty("Every known achievement, reward, playable character, bestiary entry, and progression gate is unlocked. Click Save Changes.");
    }

    private void PopulateSkills()
    {
        _skill.Items.Clear();
        if (_skillCharacter.SelectedItem is not string character) return;
        _skill.Items.AddRange(GameCatalog.Skills.Where(s => s.Character == character).Cast<object>().ToArray());
        if (_skill.Items.Count > 0) _skill.SelectedIndex = 0;
    }

    private void RefreshSkillLevel()
    {
        if (_save is null || _skill.SelectedItem is not SkillDefinition skill)
        {
            _skillCurrent.Text = "";
            return;
        }
        int level = SaveEdits.GetSkillLevel(_save, skill);
        _skillCurrent.Text = $"Current saved level: {level:N0}";
        _skillLevel.Value = Math.Min(_skillLevel.Maximum, level);
    }

    private void SetSelectedSkillLevel()
    {
        if (_save is null || _skill.SelectedItem is not SkillDefinition skill) return;
        SaveEdits.SetSkillLevel(_save, skill, decimal.ToInt32(_skillLevel.Value));
        MarkDirty($"{skill.Id} set to level {_skillLevel.Value:N0}. Click Save Changes.");
        RefreshSkillLevel();
    }

    private void AddSelectedSkillLevels(int amount)
    {
        if (_save is null || _skill.SelectedItem is not SkillDefinition skill) return;
        int level = Math.Min(10_000, SaveEdits.GetSkillLevel(_save, skill) + amount);
        SaveEdits.SetSkillLevel(_save, skill, level);
        MarkDirty($"{skill.Id} increased to level {level:N0}. Click Save Changes.");
        RefreshSkillLevel();
    }

    private void SetEverySkillLevel(int level)
    {
        if (_save is null) return;
        foreach (SkillDefinition skill in GameCatalog.Skills)
            SaveEdits.SetSkillLevel(_save, skill, level);
        MarkDirty($"All {GameCatalog.Skills.Count} character/skill combinations set to level {level}. Click Save Changes.");
        RefreshSkillLevel();
    }

    private void SaveChanges()
    {
        if (_save is null) return;
        if (IsGameRunning())
        {
            MessageBox.Show(this, "Close Skull Horde before saving so the game cannot overwrite or corrupt the file.",
                "Game is running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            string backup = SaveStorage.WriteWithBackup(_save, TrainerPaths.SaveFile, TrainerPaths.BackupDirectory);
            _dirty = false;
            RebuildTree();
            SetStatus($"Saved successfully. Backup: {backup}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
            ShowError("Could not save changes", ex);
        }
    }

    private void RestoreLatestBackup()
    {
        if (IsGameRunning())
        {
            MessageBox.Show(this, "Close Skull Horde before restoring a backup.", "Game is running",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            RestoreResult result = SaveStorage.RestoreLatest(
                TrainerPaths.SaveFile, TrainerPaths.BackupDirectory);
            LoadSave(showErrors: true);
            SetStatus($"Restored {Path.GetFileName(result.RestoredBackup)}. Previous save backed up as {Path.GetFileName(result.SafetyBackup)}.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
            ShowError("Could not restore a backup", ex);
        }
    }

    private void RebuildTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        _selectedValue = null;
        _advancedValue.Clear();
        _advancedType.Text = string.Empty;
        if (_save is not null)
        {
            var root = new TreeNode("root");
            AddTableNodes(root, _save.Root);
            _tree.Nodes.Add(root);
            root.Expand();
        }
        _tree.EndUpdate();
    }

    private static void AddTableNodes(TreeNode parent, LuaTable table)
    {
        foreach (LuaEntry entry in table.Entries.OrderBy(e => e.Key.Display, StringComparer.OrdinalIgnoreCase))
        {
            var node = new TreeNode($"{entry.Key.Display} = {entry.Value.Display}") { Tag = entry.Value };
            parent.Nodes.Add(node);
            if (entry.Value.Type == LuaType.Table)
                AddTableNodes(node, (LuaTable)entry.Value.Value);
        }
    }

    private void SelectAdvancedNode(TreeNode node)
    {
        _selectedValue = node.Tag as LuaValue;
        if (_selectedValue is null || _selectedValue.Type == LuaType.Table)
        {
            _advancedType.Text = _selectedValue is null ? string.Empty : "Type: table (not directly editable)";
            _advancedValue.Text = string.Empty;
            _advancedValue.Enabled = false;
            return;
        }
        _advancedType.Text = $"Type: {_selectedValue.Type}";
        _advancedValue.Text = _selectedValue.Display;
        _advancedValue.Enabled = _selectedValue.Type is LuaType.Boolean or LuaType.Number or LuaType.String;
    }

    private void ApplyAdvancedValue()
    {
        if (_selectedValue is null || !_advancedValue.Enabled) return;
        try
        {
            object value = _selectedValue.Type switch
            {
                LuaType.Boolean => bool.Parse(_advancedValue.Text.Trim()),
                LuaType.Number => ParseNumber(_advancedValue.Text.Trim()),
                LuaType.String => _advancedValue.Text,
                _ => throw new InvalidOperationException("This value type cannot be edited."),
            };
            _selectedValue.Value = value;
            MarkDirty("Advanced value updated. Click Save Changes to write it.");
            RebuildTree();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Invalid value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static double ParseNumber(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double local) && double.IsFinite(local))
            return local;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariant) && double.IsFinite(invariant))
            return invariant;
        throw new FormatException("Enter a finite number.");
    }

    private void MarkDirty(string message)
    {
        _dirty = true;
        SetStatus(message);
    }

    private void OpenSaveFolder()
    {
        Directory.CreateDirectory(TrainerPaths.SaveDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", TrainerPaths.SaveDirectory) { UseShellExecute = true });
    }

    private void LaunchGame()
    {
        string game = Path.Combine(AppContext.BaseDirectory, "SkullHorde.exe");
        if (!File.Exists(game))
        {
            game = Path.Combine(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? string.Empty, "SkullHorde.exe");
        }
        if (!File.Exists(game))
        {
            using var dialog = new OpenFileDialog { Filter = "Skull Horde|SkullHorde.exe", Title = "Locate SkullHorde.exe" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            game = dialog.FileName;
        }
        Process.Start(new ProcessStartInfo(game) { WorkingDirectory = Path.GetDirectoryName(game), UseShellExecute = true });
    }

    private static bool IsGameRunning() => Process.GetProcessesByName("SkullHorde").Length > 0;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _liveHotkeys = new LiveDebugHotkeys(Handle, SetLiveStatus);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _liveHotkeys?.Dispose();
        _liveHotkeys = null;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == LiveDebugHotkeys.MessageId)
            _liveHotkeys?.Handle(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    private void SetLiveStatus(string message, bool error)
    {
        _liveStatus.ForeColor = error ? Color.FromArgb(235, 102, 102) : _muted;
        _liveStatus.Text = message;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.ForeColor = error ? Color.FromArgb(235, 102, 102) : _muted;
        _status.Text = (_dirty ? "UNSAVED • " : string.Empty) + message;
    }

    private static void ShowError(string title, Exception ex) => MessageBox.Show(
        ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private Button MakeButton(string text, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 36,
            Padding = new Padding(10, 3, 10, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? _accent : Color.FromArgb(51, 55, 65),
            ForeColor = primary ? Color.FromArgb(20, 20, 20) : Color.WhiteSmoke,
            Margin = new Padding(4),
        };
        button.FlatAppearance.BorderColor = primary ? _accent : Color.FromArgb(78, 83, 96);
        button.Click += click;
        return button;
    }

    private TabPage MakeTab(string text) => new(text) { BackColor = _background, ForeColor = Color.WhiteSmoke };

    private Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = _muted,
        Margin = new Padding(4, 5, 4, 3),
    };

    private static NumericUpDown MakeNumberInput() => new()
    {
        Width = 180,
        Maximum = 1_000_000_000,
        DecimalPlaces = 0,
        ThousandsSeparator = true,
    };

    private static void SetNumeric(NumericUpDown input, double value)
    {
        decimal converted = value >= decimal.ToDouble(decimal.MaxValue) ? input.Maximum : (decimal)Math.Max(0, value);
        input.Value = Math.Min(input.Maximum, Math.Max(input.Minimum, converted));
    }
}
