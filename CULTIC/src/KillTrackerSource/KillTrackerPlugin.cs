using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CulticKillTracker
{
    /// <summary>
    /// Kill tracker for CULTIC: counts how many of each enemy type have died,
    /// per level, per session and all-time.
    ///
    /// Implementation notes:
    ///  - scrEnemy.enemyDeath() is the single death funnel: it is declared once on
    ///    scrEnemy, is not virtual, and every enemy script derives from scrEnemy,
    ///    so one Harmony patch covers all ~34 enemy types including bosses.
    ///  - enemyDeath() bails out early when the enemy is already dead or was
    ///    replaced by the randomizer, so the prefix records that condition into
    ///    __state and only the postfix that saw a real death counts it.
    ///  - The display name comes from scrEnemy.enemyNameLString.fallbackString
    ///    (the game's own tryGetLocalizedString always returns the fallback, so
    ///    this is exactly the string the HP tracker shows). enemyName and a
    ///    prettified script name are the fallbacks.
    ///  - All-time totals live in a plain TSV next to the config so they survive
    ///    restarts; it is written at most every few seconds and on quit.
    /// </summary>
    [BepInPlugin(PluginGuid, "CULTIC Kill Tracker", "1.1.0")]
    public sealed class KillTrackerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.codex.cultickilltracker";

        private const string StatsFileName = "CulticKillTracker.stats.tsv";
        private const string ExportFolderName = "KillTrackerExports";
        private const float SaveInterval = 5f;

        // Panel metrics (pixels).
        private const float Pad = 12f;
        private const float NameWidth = 210f;
        private const float NumWidth = 62f;
        private const float RowHeight = 18f;
        private const float TitleHeight = 24f;

        // Taken from the game's own HUD (scrEnemyHPTracker paints a killed
        // enemy's name this colour), so the panel stays on-brand.
        private static readonly Color Accent = new Color(0.8941f, 0.2313f, 0.2666f);
        private static readonly Color PanelBackground = new Color(0.04f, 0.03f, 0.03f, 0.88f);
        private static readonly Color TextColor = new Color(0.92f, 0.90f, 0.86f);
        private static readonly Color DimColor = new Color(0.62f, 0.58f, 0.54f);

        private static KillTrackerPlugin instance;

        private ConfigEntry<KeyboardShortcut> togglePanelKey;
        private ConfigEntry<KeyboardShortcut> resetSessionKey;
        private ConfigEntry<KeyboardShortcut> exportKey;
        private ConfigEntry<string> exportFolder;
        private ConfigEntry<bool> showOnStart;
        private ConfigEntry<bool> countOnlyPlayerKills;
        private ConfigEntry<float> panelScale;

        private readonly Dictionary<string, KillRecord> records =
            new Dictionary<string, KillRecord>(StringComparer.Ordinal);

        private readonly List<KillRecord> sortedBuffer = new List<KillRecord>();

        // Hotkey -> what it does, checked in order. Adding a key is one entry here
        // plus its Config.Bind; Update() never changes. Shortcuts with modifiers
        // come first, so a plain key can never shadow a modified one.
        private readonly List<KeyValuePair<ConfigEntry<KeyboardShortcut>, Action>> hotkeys =
            new List<KeyValuePair<ConfigEntry<KeyboardShortcut>, Action>>();

        private Harmony harmony;
        private string statsPath;
        private bool showPanel;
        private bool statsDirty;
        private float nextSaveTime;
        private bool hintShown;
        private string levelKey = "";
        private string levelLabel = "";

        private string statusText = "";
        private float statusUntil;

        private bool stylesReady;
        private Texture2D pixel;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle columnStyle;
        private GUIStyle columnNumberStyle;
        private GUIStyle nameStyle;
        private GUIStyle numberStyle;
        private GUIStyle totalNameStyle;
        private GUIStyle totalNumberStyle;

        private sealed class KillRecord
        {
            public string Name;
            public int Level;
            public int Session;
            public int Total;
        }

        // ----------------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------------

        private void Awake()
        {
            instance = this;

            togglePanelKey = Config.Bind("Hotkeys", "TogglePanel",
                new KeyboardShortcut(KeyCode.F7),
                "Show / hide the kill count panel.");
            resetSessionKey = Config.Bind("Hotkeys", "ResetSession",
                new KeyboardShortcut(KeyCode.F7, KeyCode.LeftShift),
                "Reset the level and session columns. All-time totals are left alone " +
                "(delete " + StatsFileName + " next to this config to clear those).");
            exportKey = Config.Bind("Hotkeys", "ExportTable",
                new KeyboardShortcut(KeyCode.F7, KeyCode.LeftControl),
                "Write the whole table to a timestamped .txt file. Never overwrites " +
                "an earlier export.");
            exportFolder = Config.Bind("General", "ExportFolder", "",
                "Where " + ExportFolderName + " exports are written. Leave empty for " +
                "<game folder>\\" + ExportFolderName + ", or give a full path such as " +
                "C:\\Users\\You\\Desktop.");
            showOnStart = Config.Bind("General", "ShowOnStart", false,
                "Whether the panel is visible when the game starts.");
            countOnlyPlayerKills = Config.Bind("General", "CountOnlyPlayerKills", false,
                "Count only enemies whose killing blow is attributed to a player. " +
                "Some deaths (drowning, crushes, infighting, some explosions) carry no " +
                "attribution and are dropped when this is on, so it undercounts.");
            panelScale = Config.Bind("General", "PanelScale", 1f,
                new ConfigDescription(
                    "Extra size multiplier for the panel. The panel already scales " +
                    "itself with screen height (1x at 1080p, 2x at 4K); this multiplies " +
                    "that if you want it bigger or smaller still.",
                    new AcceptableValueRange<float>(0.5f, 3f)));

            hotkeys.Add(new KeyValuePair<ConfigEntry<KeyboardShortcut>, Action>(resetSessionKey, ResetSession));
            hotkeys.Add(new KeyValuePair<ConfigEntry<KeyboardShortcut>, Action>(exportKey, ExportTable));
            hotkeys.Add(new KeyValuePair<ConfigEntry<KeyboardShortcut>, Action>(togglePanelKey, TogglePanel));

            statsPath = Path.Combine(Paths.ConfigPath, StatsFileName);
            LoadStats();

            showPanel = showOnStart.Value;
            nextSaveTime = Time.unscaledTime + SaveInterval;

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            Logger.LogInfo("CULTIC Kill Tracker loaded. " + togglePanelKey.Value +
                           " shows the panel. Stats file: " + statsPath);
        }

        private void OnDestroy()
        {
            SaveStats();
            instance = null;
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
            if (pixel != null)
            {
                Destroy(pixel);
                pixel = null;
            }
        }

        private void OnApplicationQuit()
        {
            SaveStats();
        }

        private void Update()
        {
            foreach (KeyValuePair<ConfigEntry<KeyboardShortcut>, Action> binding in hotkeys)
            {
                if (binding.Key.Value.IsDown())
                {
                    binding.Value();
                    break;
                }
            }

            TrackLevelChange();

            if (statsDirty && Time.unscaledTime >= nextSaveTime)
            {
                SaveStats();
            }
        }

        // ----------------------------------------------------------------------
        // Recording
        // ----------------------------------------------------------------------

        private static void RecordKill(scrEnemy enemy)
        {
            KillTrackerPlugin self = instance;
            if (self == null || enemy == null)
            {
                return;
            }

            try
            {
                self.Record(enemy);
            }
            catch (Exception e)
            {
                self.Logger.LogWarning("Failed to record a kill: " + e.Message);
            }
        }

        private void Record(scrEnemy enemy)
        {
            if (countOnlyPlayerKills.Value && !WasPlayerKill(enemy))
            {
                return;
            }

            string name = ResolveEnemyName(enemy);

            KillRecord record;
            if (!records.TryGetValue(name, out record))
            {
                record = new KillRecord { Name = name };
                records[name] = record;
            }

            record.Level++;
            record.Session++;
            record.Total++;
            statsDirty = true;

            if (!hintShown && !showPanel)
            {
                hintShown = true;
                ShowStatus(togglePanelKey.Value + " for kill counts");
            }
        }

        private static bool WasPlayerKill(scrEnemy enemy)
        {
            scrHitInfo hit = enemy.lastHit;
            if (hit == null || hit.hitBy == null)
            {
                return false;
            }

            if (hit.hitBy.GetComponent<scrPlayerControl>() != null)
            {
                return true;
            }

            // The game's own achievement code uses this prefix to spot player
            // hits, which also catches player-owned objects (thrown props, TNT).
            return hit.hitBy.name.StartsWith("prefabPlayer", StringComparison.Ordinal);
        }

        private static string ResolveEnemyName(scrEnemy enemy)
        {
            lString localized = enemy.enemyNameLString;
            if (localized != null && !string.IsNullOrEmpty(localized.fallbackString))
            {
                return localized.fallbackString.Trim();
            }

            if (!string.IsNullOrEmpty(enemy.enemyName))
            {
                return enemy.enemyName.Trim();
            }

            return PrettifyScriptName(enemy.GetType().Name);
        }

        /// <summary>"scrAxeCultist" -&gt; "Axe Cultist", "scrGLCultist" -&gt; "GL Cultist".</summary>
        private static string PrettifyScriptName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return "Unknown";
            }

            if (typeName.StartsWith("scr", StringComparison.Ordinal) && typeName.Length > 3)
            {
                typeName = typeName.Substring(3);
            }

            StringBuilder sb = new StringBuilder(typeName.Length + 8);
            for (int i = 0; i < typeName.Length; i++)
            {
                char c = typeName[i];
                if (i > 0 && char.IsUpper(c))
                {
                    char previous = typeName[i - 1];
                    bool startsWord = !char.IsUpper(previous);
                    bool endsAcronym = char.IsUpper(previous) &&
                                       i + 1 < typeName.Length &&
                                       char.IsLower(typeName[i + 1]);
                    if (startsWord || endsAcronym)
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(c);
            }

            return sb.ToString();
        }

        private void TrackLevelChange()
        {
            string key;
            string label;
            ReadCurrentLevel(out key, out label);

            if (key == levelKey)
            {
                levelLabel = label;
                return;
            }

            levelKey = key;
            levelLabel = label;
            foreach (KillRecord record in records.Values)
            {
                record.Level = 0;
            }

            // Level transitions are the natural checkpoint for the all-time file.
            if (statsDirty)
            {
                SaveStats();
            }
        }

        private static void ReadCurrentLevel(out string key, out string label)
        {
            key = "";
            label = "";

            scrGameControl game = scrGameControl.Instance;
            if (game != null && game.mapStats != null)
            {
                scrMapStats stats = game.mapStats;
                if (!string.IsNullOrEmpty(stats.sceneName))
                {
                    key = stats.sceneName;
                }
                if (stats.mapName != null && !string.IsNullOrEmpty(stats.mapName.fallbackString))
                {
                    label = stats.mapName.fallbackString;
                }
            }

            if (string.IsNullOrEmpty(key))
            {
                key = SceneManager.GetActiveScene().name;
            }
            if (string.IsNullOrEmpty(label))
            {
                label = key;
            }
        }

        private void TogglePanel()
        {
            showPanel = !showPanel;
        }

        // ----------------------------------------------------------------------
        // Export
        //
        // The stats file beside the config holds all-time totals only, because that
        // is all that needs to survive a restart. An export is the whole table as
        // you are looking at it - level, session and all-time side by side - laid
        // out in aligned columns for reading rather than parsing.
        // ----------------------------------------------------------------------

        private void ExportTable()
        {
            List<KillRecord> rows = SortRecords();
            if (rows.Count == 0)
            {
                ShowStatus("nothing to export yet");
                return;
            }

            try
            {
                string folder = ResolveExportFolder();
                Directory.CreateDirectory(folder);

                string path = UniqueExportPath(folder);
                File.WriteAllText(path, BuildExportText(rows), Encoding.UTF8);

                ShowStatus("exported to " + Path.GetFileName(path));
                Logger.LogInfo("Exported kill table to " + path);
            }
            catch (Exception e)
            {
                ShowStatus("export failed - see the BepInEx log");
                Logger.LogWarning("Export failed: " + e);
            }
        }

        private string ResolveExportFolder()
        {
            string configured = exportFolder.Value;
            if (!string.IsNullOrEmpty(configured) && configured.Trim().Length > 0)
            {
                return configured.Trim();
            }

            return Path.Combine(Paths.GameRootPath, ExportFolderName);
        }

        /// <summary>Timestamped, and never an existing file - an export can only ever add.</summary>
        private static string UniqueExportPath(string folder)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(folder, "KillTracker-" + stamp + ".txt");

            int suffix = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(folder, "KillTracker-" + stamp + "-" + suffix + ".txt");
                suffix++;
            }

            return path;
        }

        private string BuildExportText(List<KillRecord> rows)
        {
            const string NameHeader = "ENEMY";
            const string TotalName = "ALL ENEMIES";

            int levelTotal = 0;
            int sessionTotal = 0;
            int allTimeTotal = 0;
            int nameWidth = Mathf.Max(NameHeader.Length, TotalName.Length);
            foreach (KillRecord record in rows)
            {
                levelTotal += record.Level;
                sessionTotal += record.Session;
                allTimeTotal += record.Total;
                nameWidth = Mathf.Max(nameWidth, record.Name.Length);
            }

            int levelWidth = ColumnWidth("LEVEL", levelTotal);
            int sessionWidth = ColumnWidth("SESSION", sessionTotal);
            int totalWidth = ColumnWidth("TOTAL", allTimeTotal);

            StringBuilder sb = new StringBuilder(256 + rows.Count * (nameWidth + 32));
            sb.AppendLine("CULTIC Kill Tracker");
            sb.AppendLine("Exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Level:    " + (string.IsNullOrEmpty(levelLabel) ? "(none)" : levelLabel));
            sb.AppendLine("Counting: " + (countOnlyPlayerKills.Value
                ? "kills attributed to a player only"
                : "every enemy death"));
            sb.AppendLine();

            string header = Row(NameHeader, "LEVEL", "SESSION", "TOTAL",
                                nameWidth, levelWidth, sessionWidth, totalWidth);
            sb.AppendLine(header);
            sb.AppendLine(new string('-', header.Length));

            foreach (KillRecord record in rows)
            {
                sb.AppendLine(Row(record.Name,
                    record.Level.ToString(CultureInfo.InvariantCulture),
                    record.Session.ToString(CultureInfo.InvariantCulture),
                    record.Total.ToString(CultureInfo.InvariantCulture),
                    nameWidth, levelWidth, sessionWidth, totalWidth));
            }

            sb.AppendLine(new string('-', header.Length));
            sb.AppendLine(Row(TotalName,
                levelTotal.ToString(CultureInfo.InvariantCulture),
                sessionTotal.ToString(CultureInfo.InvariantCulture),
                allTimeTotal.ToString(CultureInfo.InvariantCulture),
                nameWidth, levelWidth, sessionWidth, totalWidth));

            sb.AppendLine();
            sb.AppendLine("LEVEL   = kills in the level named above (resets on level change)");
            sb.AppendLine("SESSION = kills since the game was launched");
            sb.AppendLine("TOTAL   = all-time kills");
            return sb.ToString();
        }

        private static int ColumnWidth(string header, int largestValue)
        {
            return Mathf.Max(header.Length,
                largestValue.ToString(CultureInfo.InvariantCulture).Length);
        }

        private static string Row(string name, string a, string b, string c,
                                  int nameWidth, int aWidth, int bWidth, int cWidth)
        {
            return name.PadRight(nameWidth) + "  " +
                   a.PadLeft(aWidth) + "  " +
                   b.PadLeft(bWidth) + "  " +
                   c.PadLeft(cWidth);
        }

        private void ResetSession()
        {
            foreach (KillRecord record in records.Values)
            {
                record.Level = 0;
                record.Session = 0;
            }
            ShowStatus("session counts reset (all-time totals kept)");
        }

        // ----------------------------------------------------------------------
        // Persistence
        // ----------------------------------------------------------------------

        private void LoadStats()
        {
            if (!File.Exists(statsPath))
            {
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(statsPath);
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || line[0] == '#')
                    {
                        continue;
                    }

                    int tab = line.IndexOf('\t');
                    if (tab <= 0 || tab + 1 >= line.Length)
                    {
                        continue;
                    }

                    int count;
                    if (!int.TryParse(line.Substring(0, tab).Trim(),
                                      NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                    {
                        continue;
                    }

                    string name = line.Substring(tab + 1).Trim();
                    if (name.Length == 0 || count <= 0)
                    {
                        continue;
                    }

                    KillRecord record;
                    if (!records.TryGetValue(name, out record))
                    {
                        record = new KillRecord { Name = name };
                        records[name] = record;
                    }
                    record.Total += count;
                }

                Logger.LogInfo("Loaded all-time kill totals for " + records.Count + " enemy types.");
            }
            catch (Exception e)
            {
                Logger.LogWarning("Could not read " + statsPath + ": " + e.Message);
            }
        }

        private void SaveStats()
        {
            statsDirty = false;
            nextSaveTime = Time.unscaledTime + SaveInterval;

            if (records.Count == 0)
            {
                return;
            }

            try
            {
                List<KillRecord> ordered = SortRecords();

                StringBuilder sb = new StringBuilder(64 + ordered.Count * 32);
                sb.AppendLine("# CULTIC Kill Tracker - all-time kill totals");
                sb.AppendLine("# <count><TAB><enemy name>. Delete this file to reset all-time totals.");
                foreach (KillRecord record in ordered)
                {
                    if (record.Total <= 0)
                    {
                        continue;
                    }
                    sb.Append(record.Total.ToString(CultureInfo.InvariantCulture));
                    sb.Append('\t');
                    sb.AppendLine(record.Name);
                }

                // Write beside the real file and swap, so a crash mid-write cannot
                // leave a half-written stats file behind.
                string temp = statsPath + ".tmp";
                File.WriteAllText(temp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(statsPath))
                {
                    File.Replace(temp, statsPath, null);
                }
                else
                {
                    File.Move(temp, statsPath);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("Could not write " + statsPath + ": " + e.Message);
            }
        }

        private List<KillRecord> SortRecords()
        {
            sortedBuffer.Clear();
            foreach (KillRecord record in records.Values)
            {
                if (record.Total > 0 || record.Session > 0)
                {
                    sortedBuffer.Add(record);
                }
            }

            sortedBuffer.Sort(CompareRecords);
            return sortedBuffer;
        }

        private static int CompareRecords(KillRecord a, KillRecord b)
        {
            if (a.Session != b.Session)
            {
                return b.Session.CompareTo(a.Session);
            }
            if (a.Total != b.Total)
            {
                return b.Total.CompareTo(a.Total);
            }
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        // ----------------------------------------------------------------------
        // Overlay
        // ----------------------------------------------------------------------

        private void ShowStatus(string text)
        {
            statusText = "[Kill Tracker] " + text;
            statusUntil = Time.unscaledTime + 3f;
            Logger.LogInfo(text);
        }

        private void OnGUI()
        {
            GUI.depth = -500;
            EnsureStyles();

            // IMGUI has no notion of DPI, so a panel sized in raw pixels shrinks to
            // unreadable on a 1440p or 4K screen. Scale the whole thing with screen
            // height and lay it out in the resulting virtual pixel space.
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f) * panelScale.Value;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float viewWidth = Screen.width / scale;
            float viewHeight = Screen.height / scale;

            if (showPanel)
            {
                DrawPanel(viewWidth, viewHeight);
            }

            if (!string.IsNullOrEmpty(statusText) && Time.unscaledTime <= statusUntil)
            {
                DrawStatus();
            }

            GUI.matrix = previousMatrix;
        }

        private void DrawStatus()
        {
            // The status line can land on any part of the game's HUD, so it gets its
            // own backing rather than relying on whatever is behind it.
            Vector2 size = nameStyle.CalcSize(new GUIContent(statusText));
            Rect box = new Rect(16f, 16f, size.x + 18f, size.y + 12f);
            Fill(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f), Accent);
            Fill(box, PanelBackground);
            GUI.Label(new Rect(box.x + 9f, box.y + 6f, size.x, size.y), statusText, nameStyle);
        }

        private void EnsureStyles()
        {
            if (stylesReady && pixel != null)
            {
                return;
            }

            pixel = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            pixel.hideFlags = HideFlags.HideAndDontSave;
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            titleStyle = MakeStyle(15, FontStyle.Bold, TextAnchor.MiddleLeft, Accent);
            subtitleStyle = MakeStyle(12, FontStyle.Normal, TextAnchor.MiddleRight, DimColor);
            columnStyle = MakeStyle(11, FontStyle.Normal, TextAnchor.MiddleLeft, DimColor);
            columnNumberStyle = MakeStyle(11, FontStyle.Normal, TextAnchor.MiddleRight, DimColor);
            nameStyle = MakeStyle(13, FontStyle.Normal, TextAnchor.MiddleLeft, TextColor);
            numberStyle = MakeStyle(13, FontStyle.Normal, TextAnchor.MiddleRight, TextColor);
            totalNameStyle = MakeStyle(13, FontStyle.Bold, TextAnchor.MiddleLeft, Accent);
            totalNumberStyle = MakeStyle(13, FontStyle.Bold, TextAnchor.MiddleRight, Accent);

            stylesReady = true;
        }

        private static GUIStyle MakeStyle(int size, FontStyle fontStyle, TextAnchor anchor, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.alignment = anchor;
            style.clipping = TextClipping.Clip;
            style.wordWrap = false;
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.normal.textColor = color;
            return style;
        }

        private void DrawPanel(float viewWidth, float viewHeight)
        {
            List<KillRecord> rows = SortRecords();

            float panelWidth = Pad * 2f + NameWidth + NumWidth * 3f;

            // Everything in the panel that is not an enemy row: title, both rules,
            // the column header, the separator above the total and the total row.
            float chromeHeight = Pad * 2f + TitleHeight + 5f + 1f + 5f + RowHeight + 4f + 1f + 5f
                                 + 5f + 1f + 5f + RowHeight;

            float maxPanelHeight = viewHeight - 36f;
            int fitRows = Mathf.FloorToInt((maxPanelHeight - chromeHeight) / RowHeight);
            if (fitRows < 1)
            {
                fitRows = 1;
            }

            int shownRows = rows.Count;
            int hiddenRows = 0;
            if (shownRows > fitRows)
            {
                // Never silently truncate: give up one row to say how many are left.
                shownRows = Mathf.Max(fitRows - 1, 1);
                hiddenRows = rows.Count - shownRows;
            }

            int lineCount = shownRows + (hiddenRows > 0 ? 1 : 0);
            float panelHeight = chromeHeight + lineCount * RowHeight;
            float panelX = viewWidth - panelWidth - 18f;
            float panelY = 18f;

            Fill(new Rect(panelX - 1f, panelY - 1f, panelWidth + 2f, panelHeight + 2f), Accent);
            Fill(new Rect(panelX, panelY, panelWidth, panelHeight), PanelBackground);

            float x = panelX + Pad;
            float rowWidth = panelWidth - Pad * 2f;
            float y = panelY + Pad;

            GUI.Label(new Rect(x, y, rowWidth, TitleHeight), "KILL TRACKER", titleStyle);
            GUI.Label(new Rect(x, y, rowWidth, TitleHeight), levelLabel, subtitleStyle);
            y += TitleHeight + 5f;

            Fill(new Rect(x, y, rowWidth, 1f), Accent);
            y += 1f + 5f;

            DrawRow(x, y, "ENEMY", "LEVEL", "SESSION", "TOTAL", columnStyle, columnNumberStyle);
            y += RowHeight + 4f;

            Fill(new Rect(x, y, rowWidth, 1f), DimColor * 0.5f);
            y += 1f + 5f;

            int levelTotal = 0;
            int sessionTotal = 0;
            int allTimeTotal = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                KillRecord record = rows[i];
                levelTotal += record.Level;
                sessionTotal += record.Session;
                allTimeTotal += record.Total;

                if (i < shownRows)
                {
                    DrawRow(x, y, record.Name,
                        record.Level.ToString(CultureInfo.InvariantCulture),
                        record.Session.ToString(CultureInfo.InvariantCulture),
                        record.Total.ToString(CultureInfo.InvariantCulture),
                        nameStyle, numberStyle);
                    y += RowHeight;
                }
            }

            if (hiddenRows > 0)
            {
                GUI.Label(new Rect(x, y, rowWidth, RowHeight),
                    "+" + hiddenRows + " more in " + StatsFileName, columnStyle);
                y += RowHeight;
            }

            if (rows.Count == 0)
            {
                GUI.Label(new Rect(x, y, rowWidth, RowHeight), "no kills yet", columnStyle);
                y += RowHeight;
            }

            y += 5f;
            Fill(new Rect(x, y, rowWidth, 1f), DimColor * 0.5f);
            y += 1f + 5f;

            DrawRow(x, y, "ALL ENEMIES",
                levelTotal.ToString(CultureInfo.InvariantCulture),
                sessionTotal.ToString(CultureInfo.InvariantCulture),
                allTimeTotal.ToString(CultureInfo.InvariantCulture),
                totalNameStyle, totalNumberStyle);
        }

        private static void DrawRow(float x, float y, string name, string a, string b, string c,
                                    GUIStyle labelStyle, GUIStyle valueStyle)
        {
            GUI.Label(new Rect(x, y, NameWidth, RowHeight), name, labelStyle);
            GUI.Label(new Rect(x + NameWidth, y, NumWidth, RowHeight), a, valueStyle);
            GUI.Label(new Rect(x + NameWidth + NumWidth, y, NumWidth, RowHeight), b, valueStyle);
            GUI.Label(new Rect(x + NameWidth + NumWidth * 2f, y, NumWidth, RowHeight), c, valueStyle);
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        // ----------------------------------------------------------------------
        // Death hook
        //
        // scrEnemy.enemyDeath() returns immediately when the enemy is already dead
        // or was swapped out by the randomizer, so the prefix captures whether this
        // call is the one real death and the postfix counts only then.
        // ----------------------------------------------------------------------

        [HarmonyPatch(typeof(scrEnemy), "enemyDeath")]
        private static class EnemyDeathPatch
        {
            private static void Prefix(scrEnemy __instance, out bool __state)
            {
                __state = __instance != null && !__instance.isDead && !__instance.wasRandomized;
            }

            private static void Postfix(scrEnemy __instance, bool __state)
            {
                if (__state)
                {
                    RecordKill(__instance);
                }
            }
        }
    }
}
