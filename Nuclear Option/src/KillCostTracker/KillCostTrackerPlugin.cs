using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NuclearOptionKillCostTracker
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class KillCostTrackerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nuclearoption.killcosttracker";
        public const string PluginName = "Kill and Cost Tracker";
        public const string PluginVersion = "1.4.0";

        private const int SaveFormatVersion = 2;
        private const float FactionSyncIntervalSeconds = 0.5f;
        private const float AutosaveIntervalSeconds = 3f;

        internal static KillCostTrackerPlugin Instance { get; private set; }

        private readonly Dictionary<string, CategoryStats> _factionStats =
            new Dictionary<string, CategoryStats>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _observedFactionWeaponCosts =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private readonly CategoryStats _playerStats = new CategoryStats();
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<bool> _visibleOnStartup;
        private ConfigEntry<bool> _visibleOnMissionStart;
        private ConfigEntry<int> _maxDetailRows;
        private Harmony _harmony;
        private Rect _windowRect = new Rect(Screen.width - 474f, 48f, 450f, 600f);
        private Vector2 _scroll;
        private bool _visible;
        private bool _wasInMission;
        private GameState _lastGameState = GameState.Uninitialized;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _rightStyle;
        private string _statsPath;
        private string _statsTextPath;
        private float _nextFactionSyncTime;
        private float _nextAutosaveTime;
        private bool _dirty;
        private bool _savingDisabled;
        private bool _legacyInputUnavailable;
        private bool _loggedFirstUpdate;
        private bool _loggedFirstOnGui;
        private bool _loggedFirstKill;
        private bool _loggedFirstPlayerWeapon;
        private bool _applicationQuitting;
        private bool _shutdown;
        private int _lastToggleFrame = -1;
        private int _lastRuntimeUpdateFrame = -1;
        private GameObject _runtimeObject;
        private GameObject _canvasObject;
        private Text _canvasText;

        private void Awake()
        {
            Instance = this;
            _toggleKey = Config.Bind("Display", "Toggle key", KeyCode.F7,
                "Shows or hides the kill and cost tracker.");
            _visibleOnStartup = Config.Bind("Display", "Visible on startup", true,
                "Shows the tracker immediately at startup, including in the main menu.");
            _visibleOnMissionStart = Config.Bind("Display", "Visible on mission start", true,
                "Automatically opens the tracker when a mission begins.");
            _maxDetailRows = Config.Bind("Display", "Maximum target detail rows", 8,
                "Maximum number of destroyed-unit types shown under each category. Set to 0 to hide details.");

            _statsPath = Path.Combine(Paths.ConfigPath, PluginGuid + ".stats.json");
            _statsTextPath = Path.Combine(Paths.ConfigPath, PluginGuid + ".stats.txt");
            LoadStats();
            if (!File.Exists(_statsPath) && !_savingDisabled)
            {
                SaveStats(true);
            }
            _visible = _visibleOnStartup.Value;
            _nextFactionSyncTime = Time.realtimeSinceStartup + FactionSyncIntervalSeconds;
            _nextAutosaveTime = Time.realtimeSinceStartup + AutosaveIntervalSeconds;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(KillCostTrackerPlugin).Assembly);

            // Nuclear Option destroys BepInEx_Manager during its initial scene load even
            // though BepInEx marks it DontDestroyOnLoad. Keep the actual frame driver on
            // a separate root object and do not rely on Unity messages on this component.
            _runtimeObject = new GameObject("KillCostTracker.Runtime");
            DontDestroyOnLoad(_runtimeObject);
            _runtimeObject.AddComponent<KillCostTrackerRuntime>().Initialize(this);

            // Game-owned update methods are an independent fallback if Unity ever stops
            // dispatching messages to the standalone runtime object.
            PatchUpdateDriver(typeof(MainMenu));
            PatchUpdateDriver(typeof(MissionManager));
            PatchUpdateDriver(typeof(SteamManager));
            PatchUpdateDriver(typeof(HUDAppManager));

            Application.quitting += OnApplicationQuitting;
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Press " + _toggleKey.Value + " to toggle.");
        }

        // Keep these Unity messages directly on the BepInEx plugin component. This is
        // the same lifecycle used by the working CULTIC BepInEx 5 overlays.
        private void Update()
        {
            RuntimeUpdate();
        }

        private void OnGUI()
        {
            RuntimeOnGUI();
        }

        private void OnDestroy()
        {
            SaveStats(true);
            if (!_applicationQuitting && !_shutdown)
            {
                Logger.LogWarning("BepInEx plugin host was destroyed during startup; " +
                    "the standalone tracker runtime will continue across scenes.");
                return;
            }

            Shutdown();
        }

        private void OnApplicationQuitting()
        {
            _applicationQuitting = true;
            Shutdown();
        }

        internal void RuntimeHostDestroyed()
        {
            if (_applicationQuitting)
            {
                Shutdown();
            }
        }

        private void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }

            _shutdown = true;
            SaveStats(true);
            Application.quitting -= OnApplicationQuitting;
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        internal void RuntimeUpdate()
        {
            if (_lastRuntimeUpdateFrame == Time.frameCount)
            {
                return;
            }

            _lastRuntimeUpdateFrame = Time.frameCount;
            if (!_loggedFirstUpdate)
            {
                _loggedFirstUpdate = true;
                Logger.LogInfo("Tracker runtime update is active.");
            }

            if (_canvasObject == null)
            {
                CreateCanvasOverlay();
                RefreshCanvasOverlay();
                Logger.LogInfo("Tracker Canvas created from an active game frame at " +
                    Screen.width + "x" + Screen.height + ".");
            }

            bool inMission = IsMissionState(GameManager.gameState);
            if (inMission && (!_wasInMission || GameManager.gameState != _lastGameState))
            {
                _observedFactionWeaponCosts.Clear();
                _visible = _visibleOnMissionStart.Value;
                RefreshCanvasOverlay();
            }

            if (WasToggleKeyPressedThisFrame())
            {
                ToggleVisibility("input polling");
            }

            float now = Time.realtimeSinceStartup;
            if (now >= _nextFactionSyncTime)
            {
                SyncFactionWeaponCosts();
                _nextFactionSyncTime = now + FactionSyncIntervalSeconds;
                RefreshCanvasOverlay();
            }

            if (_dirty && now >= _nextAutosaveTime)
            {
                SaveStats(false);
                _nextAutosaveTime = now + AutosaveIntervalSeconds;
            }

            _wasInMission = inMission;
            _lastGameState = GameManager.gameState;
        }

        private void PatchUpdateDriver(Type driverType)
        {
            MethodInfo original = AccessTools.DeclaredMethod(driverType, "Update", Type.EmptyTypes);
            MethodInfo postfix = AccessTools.DeclaredMethod(typeof(KillCostTrackerPlugin),
                "UpdateDriverPostfix", Type.EmptyTypes);
            if (original == null || postfix == null)
            {
                Logger.LogWarning("Could not locate update driver " + driverType.FullName + ".Update.");
                return;
            }

            _harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            Logger.LogInfo("Installed update driver patch: " + driverType.FullName + ".Update.");
        }

        private static void UpdateDriverPostfix()
        {
            KillCostTrackerPlugin instance = Instance;
            // The native BaseUnityPlugin component is destroyed by the first scene load,
            // but its managed state remains the tracker controller for the persistent host.
            if (!ReferenceEquals(instance, null))
            {
                instance.RuntimeUpdate();
            }
        }

        private void ToggleVisibility(string source)
        {
            if (_lastToggleFrame == Time.frameCount)
            {
                return;
            }

            _lastToggleFrame = Time.frameCount;
            _visible = !_visible;
            RefreshCanvasOverlay();
            Logger.LogInfo("Tracker " + (_visible ? "shown" : "hidden") + " via " + _toggleKey.Value +
                " (source: " + source + ", game state: " + GameManager.gameState + ").");
        }

        private void CreateCanvasOverlay()
        {
            _canvasObject = new GameObject("KillCostTracker.Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(_canvasObject.transform, false);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-20f, -20f);
            panel.sizeDelta = new Vector2(470f, 690f);

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0.035f, 0.045f, 0.06f, 0.94f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 14f);
            textRect.offsetMax = new Vector2(-16f, -14f);

            _canvasText = textObject.GetComponent<Text>();
            _canvasText.font = Font.CreateDynamicFontFromOSFont("Segoe UI", 15);
            _canvasText.fontSize = 15;
            _canvasText.lineSpacing = 1.05f;
            _canvasText.alignment = TextAnchor.UpperLeft;
            _canvasText.color = Color.white;
            _canvasText.supportRichText = true;
            _canvasText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _canvasText.verticalOverflow = VerticalWrapMode.Truncate;
            _canvasText.raycastTarget = false;
        }

        private void RefreshCanvasOverlay()
        {
            if (_canvasObject == null || _canvasText == null)
            {
                return;
            }

            _canvasObject.SetActive(_visible);
            if (!_visible)
            {
                return;
            }

            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("<b>KILL &amp; COST TRACKER</b>");
            builder.Append(_toggleKey.Value).AppendLine(" toggles  •  cumulative totals");
            builder.AppendLine();
            AppendCanvasCategory(builder, "PLAYER (DIRECT)", _playerStats, true);

            HashSet<string> displayedFactions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<FactionHQ> factions = GetFactionsSorted();
            for (int i = 0; i < factions.Count; i++)
            {
                FactionHQ hq = factions[i];
                if (hq == null || hq.faction == null)
                {
                    continue;
                }

                string key = hq.faction.factionName ?? "Unknown faction";
                displayedFactions.Add(key);
                CategoryStats stats;
                if (!_factionStats.TryGetValue(key, out stats))
                {
                    stats = CategoryStats.Empty;
                }

                string displayName = string.IsNullOrEmpty(hq.faction.factionExtendedName)
                    ? key
                    : hq.faction.factionExtendedName;
                AppendCanvasCategory(builder, displayName.ToUpperInvariant(), stats, false);
            }

            List<string> historicalFactions = new List<string>(_factionStats.Keys);
            historicalFactions.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < historicalFactions.Count; i++)
            {
                string factionName = historicalFactions[i];
                if (!displayedFactions.Contains(factionName))
                {
                    AppendCanvasCategory(builder, factionName.ToUpperInvariant(), _factionStats[factionName], false);
                }
            }

            _canvasText.text = builder.ToString();
        }

        private void AppendCanvasCategory(StringBuilder builder, string title, CategoryStats stats, bool player)
        {
            builder.Append("<b>").Append(title).AppendLine("</b>");
            builder.Append("Kills: ").AppendLine(stats.Kills.ToString("N0"));
            builder.Append("Destroyed value: ").AppendLine(FormatMoney(stats.DestroyedCost));
            builder.Append("Weapons expended: ").AppendLine(FormatMoney(stats.WeaponCost));
            builder.Append("Total cost impact: ").AppendLine(FormatMoney(stats.DestroyedCost + stats.WeaponCost));
            if (player)
            {
                builder.Append("Missiles ").Append(stats.MissilesFired.ToString("N0"))
                    .Append("   Bombs ").Append(stats.BombsDropped.ToString("N0"))
                    .Append("   Other rounds ").AppendLine(stats.OtherRoundsFired.ToString("N0"));
            }

            int detailLimit = Mathf.Max(0, _maxDetailRows.Value);
            if (detailLimit > 0 && stats.Targets.Count > 0)
            {
                List<TargetStats> details = stats.GetTargetsSorted();
                int count = Mathf.Min(detailLimit, details.Count);
                for (int i = 0; i < count; i++)
                {
                    TargetStats detail = details[i];
                    builder.Append("  ").Append(detail.Count.ToString("N0")).Append("x ")
                        .Append(detail.Name).Append("  ").AppendLine(FormatMoney(detail.Value));
                }
            }

            builder.AppendLine();
        }

        private bool WasToggleKeyPressedThisFrame()
        {
            if (Rewired.ReInput.isReady)
            {
                Rewired.Keyboard rewiredKeyboard = Rewired.ReInput.controllers.Keyboard;
                if (rewiredKeyboard != null && rewiredKeyboard.GetKeyDown(_toggleKey.Value))
                {
                    return true;
                }
            }

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            Key inputSystemKey;
            if (keyboard != null && TryMapInputSystemKey(_toggleKey.Value, out inputSystemKey) &&
                keyboard[inputSystemKey].wasPressedThisFrame)
            {
                return true;
            }

            if (_legacyInputUnavailable)
            {
                return false;
            }

            try
            {
                return UnityEngine.Input.GetKeyDown(_toggleKey.Value);
            }
            catch (InvalidOperationException exception)
            {
                _legacyInputUnavailable = true;
                Logger.LogWarning("Legacy input is unavailable; the tracker will use Unity's Input System. " +
                    exception.Message);
                return false;
            }
        }

        private static bool TryMapInputSystemKey(KeyCode keyCode, out Key key)
        {
            string name = keyCode.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal))
            {
                name = "Digit" + name.Substring("Alpha".Length);
            }
            else if (name.StartsWith("Keypad", StringComparison.Ordinal))
            {
                name = "Numpad" + name.Substring("Keypad".Length);
            }

            switch (name)
            {
                case "Return": name = "Enter"; break;
                case "UpArrow": name = "UpArrow"; break;
                case "DownArrow": name = "DownArrow"; break;
                case "LeftArrow": name = "LeftArrow"; break;
                case "RightArrow": name = "RightArrow"; break;
            }

            return Enum.TryParse(name, true, out key) && key != Key.None;
        }

        internal void RuntimeOnGUI()
        {
            bool firstOnGui = !_loggedFirstOnGui;
            if (firstOnGui)
            {
                _loggedFirstOnGui = true;
                Logger.LogInfo("Tracker OnGUI callback is active at " + Screen.width + "x" + Screen.height + ".");
            }

            Event currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == _toggleKey.Value)
            {
                ToggleVisibility("IMGUI event");
                currentEvent.Use();
            }

            if (!_visible)
            {
                return;
            }

            // The startup Canvas is the primary renderer. IMGUI remains only as a
            // fallback for environments where Canvas creation did not succeed.
            if (_canvasObject != null)
            {
                return;
            }

            GUI.depth = -500;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(scale, scale, 1f));
            float viewWidth = Screen.width / scale;
            float viewHeight = Screen.height / scale;
            if (firstOnGui)
            {
                _windowRect.x = Mathf.Max(12f, viewWidth - _windowRect.width - 24f);
                _windowRect.y = 24f;
            }
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f,
                Mathf.Max(0f, viewWidth - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, viewHeight - 40f));
            _windowRect.height = Mathf.Min(650f, viewHeight - _windowRect.y - 12f);
            _windowRect = GUILayout.Window(834102, _windowRect, DrawWindow, "KILL & COST TRACKER");
            GUI.matrix = previousMatrix;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_toggleKey.Value + " toggles • cumulative totals", _mutedStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Hide", GUILayout.Width(55f)))
            {
                _visible = false;
            }
            if (GUILayout.Button("Reset all", GUILayout.Width(80f)))
            {
                ResetStats();
            }
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll, false, true);
            DrawCategory("PLAYER (DIRECT)", _playerStats, _playerStats.WeaponCost, true, Color.white);

            HashSet<string> displayedFactions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<FactionHQ> factions = GetFactionsSorted();
            for (int i = 0; i < factions.Count; i++)
            {
                FactionHQ hq = factions[i];
                if (hq == null || hq.faction == null)
                {
                    continue;
                }

                string key = hq.faction.factionName ?? "Unknown faction";
                displayedFactions.Add(key);
                CategoryStats stats;
                if (!_factionStats.TryGetValue(key, out stats))
                {
                    stats = CategoryStats.Empty;
                }

                string displayName = string.IsNullOrEmpty(hq.faction.factionExtendedName)
                    ? key
                    : hq.faction.factionExtendedName;
                DrawCategory(displayName.ToUpperInvariant(), stats, stats.WeaponCost, false, hq.faction.color);
            }

            List<string> historicalFactions = new List<string>(_factionStats.Keys);
            historicalFactions.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < historicalFactions.Count; i++)
            {
                string factionName = historicalFactions[i];
                if (!displayedFactions.Contains(factionName))
                {
                    CategoryStats stats = _factionStats[factionName];
                    DrawCategory(factionName.ToUpperInvariant(), stats, stats.WeaponCost, false, Color.gray);
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 80f, 24f));
        }

        private void DrawCategory(string title, CategoryStats stats, float weaponCost, bool player, Color color)
        {
            GUILayout.Space(5f);
            Color previous = GUI.color;
            GUI.color = Color.Lerp(Color.white, color, 0.55f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, _headerStyle);
            GUI.color = previous;

            DrawValueRow("Kills", stats.Kills.ToString("N0"));
            DrawValueRow("Destroyed value", FormatMoney(stats.DestroyedCost));
            DrawValueRow("Weapons expended", FormatMoney(weaponCost));
            DrawValueRow("Total cost impact", FormatMoney(stats.DestroyedCost + weaponCost));

            if (player)
            {
                GUILayout.Label(
                    "Missiles " + stats.MissilesFired.ToString("N0") +
                    "   Bombs " + stats.BombsDropped.ToString("N0") +
                    "   Other rounds " + stats.OtherRoundsFired.ToString("N0"),
                    _mutedStyle);
            }

            int detailLimit = Mathf.Max(0, _maxDetailRows.Value);
            if (detailLimit > 0 && stats.Targets.Count > 0)
            {
                GUILayout.Space(2f);
                GUILayout.Label("Destroyed units", _subHeaderStyle);
                List<TargetStats> details = stats.GetTargetsSorted();
                int count = Mathf.Min(detailLimit, details.Count);
                for (int i = 0; i < count; i++)
                {
                    TargetStats detail = details[i];
                    DrawValueRow(detail.Count.ToString("N0") + "x  " + detail.Name, FormatMoney(detail.Value));
                }

                if (details.Count > count)
                {
                    GUILayout.Label("+ " + (details.Count - count) + " more unit types", _mutedStyle);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawValueRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, _rightStyle, GUILayout.MinWidth(100f));
            GUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            _mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.72f, 0.72f, 0.72f, 1f) }
            };
            _rightStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight
            };
        }

        private List<FactionHQ> GetFactionsSorted()
        {
            List<FactionHQ> result = new List<FactionHQ>();
            foreach (FactionHQ hq in FactionRegistry.GetAllHQs())
            {
                if (hq != null)
                {
                    result.Add(hq);
                }
            }

            result.Sort(delegate(FactionHQ a, FactionHQ b)
            {
                bool aLocal = GameManager.IsLocalHQ(a);
                bool bLocal = GameManager.IsLocalHQ(b);
                if (aLocal != bLocal)
                {
                    return aLocal ? -1 : 1;
                }

                string aName = a.faction != null ? a.faction.factionName : string.Empty;
                string bName = b.faction != null ? b.faction.factionName : string.Empty;
                return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private void SyncFactionWeaponCosts()
        {
            if (!IsMissionState(GameManager.gameState))
            {
                return;
            }

            foreach (FactionHQ hq in FactionRegistry.GetAllHQs())
            {
                if (hq == null || hq.faction == null || hq.missionStatsTracker == null)
                {
                    continue;
                }

                string factionName = hq.faction.factionName ?? "Unknown faction";
                float current = Mathf.Max(0f, hq.missionStatsTracker.value.total.spent);
                float previous;
                if (!_observedFactionWeaponCosts.TryGetValue(factionName, out previous))
                {
                    previous = 0f;
                }

                _observedFactionWeaponCosts[factionName] = current;
                float increase = current - previous;
                if (increase > 0f)
                {
                    GetOrCreateFactionStats(factionName).WeaponCost += increase;
                    MarkDirty();
                }
            }
        }

        private static string FormatMoney(float value)
        {
            return "$" + value.ToString("N2") + "m";
        }

        private static bool IsMissionState(GameState state)
        {
            return state == GameState.SinglePlayer || state == GameState.Multiplayer;
        }

        private void ResetStats()
        {
            _playerStats.Clear();
            _factionStats.Clear();
            _observedFactionWeaponCosts.Clear();
            foreach (FactionHQ hq in FactionRegistry.GetAllHQs())
            {
                if (hq == null || hq.faction == null || hq.missionStatsTracker == null)
                {
                    continue;
                }

                string factionName = hq.faction.factionName ?? "Unknown faction";
                _observedFactionWeaponCosts[factionName] =
                    Mathf.Max(0f, hq.missionStatsTracker.value.total.spent);
            }

            _scroll = Vector2.zero;
            MarkDirty();
            SaveStats(true);
        }

        private CategoryStats GetOrCreateFactionStats(string factionName)
        {
            CategoryStats stats;
            if (!_factionStats.TryGetValue(factionName, out stats))
            {
                stats = new CategoryStats();
                _factionStats.Add(factionName, stats);
            }

            return stats;
        }

        internal void RecordKill(PersistentID killerId, PersistentID killedId)
        {
            PersistentUnit killer;
            PersistentUnit killed;
            if (!UnitRegistry.TryGetPersistentUnit(killerId, out killer) ||
                !UnitRegistry.TryGetPersistentUnit(killedId, out killed) ||
                killer == null || killed == null || killed.definition == null)
            {
                return;
            }

            FactionHQ killerHq = killer.GetHQ();
            float value = Mathf.Max(0f, killed.definition.value);
            string targetName = !string.IsNullOrEmpty(killed.unitName)
                ? killed.unitName
                : killed.definition.unitName;

            if (killerHq != null && killerHq.faction != null)
            {
                string factionName = killerHq.faction.factionName ?? "Unknown faction";
                GetOrCreateFactionStats(factionName).AddKill(targetName, value);
            }

            if (killer.player != null && GameManager.IsLocalPlayer(killer.player))
            {
                _playerStats.AddKill(targetName, value);
            }

            if (!_loggedFirstKill)
            {
                _loggedFirstKill = true;
                Logger.LogInfo("First kill event accepted: " + targetName + " (value " +
                    FormatMoney(value) + ", faction " +
                    (killerHq != null && killerHq.faction != null
                        ? killerHq.faction.factionName
                        : "unknown") + ").");
            }

            MarkDirty();
        }

        internal void RecordWeaponFire(Unit owner, WeaponInfo info, int rounds)
        {
            if (rounds <= 0 || owner == null || info == null)
            {
                return;
            }

            bool changed = false;
            Player player = owner.GetPlayer();
            bool localPlayerWeapon = owner.LocalSim && player != null &&
                GameManager.IsLocalPlayer(player);
            if (localPlayerWeapon)
            {
                _playerStats.AddWeapon(info, rounds, true);
                changed = true;
                if (!_loggedFirstPlayerWeapon)
                {
                    _loggedFirstPlayerWeapon = true;
                    Logger.LogInfo("First local-player weapon event accepted: " +
                        CategoryStats.GetWeaponDisplayName(info) + ", " + rounds +
                        " round(s), cost " +
                        FormatMoney(Mathf.Max(0f, info.costPerRound) * rounds) + ".");
                }
            }

            // The host/server observes all authoritative faction fire. A non-host client
            // can still persist exact per-weapon detail for its own aircraft.
            FactionHQ hq = owner.NetworkHQ;
            if (hq != null && hq.faction != null && (owner.IsServer || localPlayerWeapon))
            {
                string factionName = hq.faction.factionName ?? "Unknown faction";
                // Faction aggregate cost is synchronized from MissionStatsTracker.spent;
                // this call adds only counts and per-weapon cost detail.
                GetOrCreateFactionStats(factionName).AddWeapon(info, rounds, false);
                changed = true;
            }

            if (changed)
            {
                MarkDirty();
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
            RefreshCanvasOverlay();
        }

        private void LoadStats()
        {
            if (!File.Exists(_statsPath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_statsPath);
                TrackerSaveData data = DeserializeSaveData(json);
                if (data == null)
                {
                    throw new InvalidDataException("The tracker save contained no readable data.");
                }

                if (data.version > SaveFormatVersion)
                {
                    _savingDisabled = true;
                    Logger.LogWarning("Tracker save format " + data.version +
                        " is newer than supported format " + SaveFormatVersion +
                        ". Existing data was left untouched and saving is disabled for this run.");
                    return;
                }

                _playerStats.LoadFrom(data.player);
                if (data.factions != null)
                {
                    for (int i = 0; i < data.factions.Count; i++)
                    {
                        FactionSaveData faction = data.factions[i];
                        if (faction == null || string.IsNullOrEmpty(faction.name))
                        {
                            continue;
                        }

                        GetOrCreateFactionStats(faction.name).LoadFrom(faction.stats);
                    }
                }

                _dirty = false;
                Logger.LogInfo("Loaded persistent totals: " + _playerStats.Kills +
                    " direct player kills across " + _factionStats.Count + " faction records.");
            }
            catch (Exception exception)
            {
                PreserveUnreadableSave();
                Logger.LogWarning("Could not read persistent tracker totals; starting with empty totals. " +
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private void PreserveUnreadableSave()
        {
            try
            {
                string recoveryPath = _statsPath + ".corrupt-" +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
                File.Copy(_statsPath, recoveryPath, false);
                Logger.LogWarning("Preserved the unreadable tracker save at " + recoveryPath);
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Could not create a recovery copy of the unreadable tracker save: " +
                    exception.Message);
            }
        }

        private void SaveStats(bool force)
        {
            if (_savingDisabled || (!force && !_dirty) || string.IsNullOrEmpty(_statsPath))
            {
                return;
            }

            try
            {
                TrackerSaveData data = new TrackerSaveData
                {
                    version = SaveFormatVersion,
                    player = _playerStats.ToSaveData(),
                    factions = new List<FactionSaveData>()
                };

                List<string> factionNames = new List<string>(_factionStats.Keys);
                factionNames.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < factionNames.Count; i++)
                {
                    string factionName = factionNames[i];
                    data.factions.Add(new FactionSaveData
                    {
                        name = factionName,
                        stats = _factionStats[factionName].ToSaveData()
                    });
                }

                string json = SerializeSaveData(data);
                Directory.CreateDirectory(Path.GetDirectoryName(_statsPath));
                WriteFileWithBackup(_statsPath, json);
                WriteFileWithBackup(_statsTextPath, BuildTextReport(data));

                _dirty = false;
            }
            catch (Exception exception)
            {
                Logger.LogError("Failed to save persistent tracker totals: " +
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void WriteFileWithBackup(string path, string contents)
        {
            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path);
                return;
            }

            try
            {
                File.Replace(temporaryPath, path, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, backupPath, true);
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
        }

        private static string BuildTextReport(TrackerSaveData data)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("KILL AND COST TRACKER - PERSISTENT REPORT");
            builder.Append("Generated UTC: ")
                .AppendLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            builder.Append("Save format: ").AppendLine(data.version.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
            AppendTextCategory(builder, "PLAYER (DIRECT)", data.player);

            if (data.factions != null)
            {
                for (int i = 0; i < data.factions.Count; i++)
                {
                    FactionSaveData faction = data.factions[i];
                    if (faction != null)
                    {
                        AppendTextCategory(builder, "FACTION: " + CleanText(faction.name), faction.stats);
                    }
                }
            }

            return builder.ToString();
        }

        private static void AppendTextCategory(StringBuilder builder, string title, CategorySaveData stats)
        {
            stats = stats ?? new CategorySaveData();
            builder.Append('[').Append(title).AppendLine("]");
            builder.Append("Kills: ").AppendLine(stats.kills.ToString("N0", CultureInfo.InvariantCulture));
            builder.Append("Destroyed value: ").AppendLine(FormatTextMoney(stats.destroyedCost));
            builder.Append("Weapon expenditure: ").AppendLine(FormatTextMoney(stats.weaponCost));
            builder.Append("Total cost impact: ")
                .AppendLine(FormatTextMoney(stats.destroyedCost + stats.weaponCost));
            builder.Append("Missiles fired: ")
                .AppendLine(stats.missilesFired.ToString("N0", CultureInfo.InvariantCulture));
            builder.Append("Bombs dropped: ")
                .AppendLine(stats.bombsDropped.ToString("N0", CultureInfo.InvariantCulture));
            builder.Append("Other rounds fired: ")
                .AppendLine(stats.otherRoundsFired.ToString("N0", CultureInfo.InvariantCulture));

            builder.AppendLine("Destroyed units:");
            if (stats.targets == null || stats.targets.Count == 0)
            {
                builder.AppendLine("  (none)");
            }
            else
            {
                for (int i = 0; i < stats.targets.Count; i++)
                {
                    TargetSaveData target = stats.targets[i];
                    if (target != null)
                    {
                        builder.Append("  ").Append(target.count.ToString("N0", CultureInfo.InvariantCulture))
                            .Append(" x ").Append(CleanText(target.name)).Append(" | ")
                            .AppendLine(FormatTextMoney(target.value));
                    }
                }
            }

            builder.AppendLine("Weapons fired:");
            if (stats.weapons == null || stats.weapons.Count == 0)
            {
                builder.AppendLine("  (none recorded)");
            }
            else
            {
                for (int i = 0; i < stats.weapons.Count; i++)
                {
                    WeaponSaveData weapon = stats.weapons[i];
                    if (weapon != null)
                    {
                        builder.Append("  ").Append(weapon.rounds.ToString("N0", CultureInfo.InvariantCulture))
                            .Append(" x ").Append(CleanText(weapon.name))
                            .Append(" [").Append(CleanText(weapon.kind)).Append("]")
                            .Append(" | spent ").Append(FormatTextMoney(weapon.cost))
                            .Append(" | id ").AppendLine(CleanText(weapon.id));
                    }
                }
            }

            builder.AppendLine();
        }

        private static string FormatTextMoney(float value)
        {
            value = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
            return "$" + value.ToString("N2", CultureInfo.InvariantCulture) + "m";
        }

        private static string CleanText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "Unknown"
                : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        }

        private static string SerializeSaveData(TrackerSaveData data)
        {
            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(TrackerSaveData));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, data);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static TrackerSaveData DeserializeSaveData(string json)
        {
            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(TrackerSaveData));
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                return serializer.ReadObject(stream) as TrackerSaveData;
            }
        }
    }

    public sealed class KillCostTrackerRuntime : MonoBehaviour
    {
        private KillCostTrackerPlugin _owner;

        internal void Initialize(KillCostTrackerPlugin owner)
        {
            _owner = owner;
        }

        public void Update()
        {
            // Do not use UnityEngine.Object's overloaded null check here. Nuclear Option
            // destroys the BepInEx component while this independent host remains alive.
            if (!ReferenceEquals(_owner, null))
            {
                _owner.RuntimeUpdate();
            }
        }

        public void OnGUI()
        {
            if (!ReferenceEquals(_owner, null))
            {
                _owner.RuntimeOnGUI();
            }
        }

        public void OnDestroy()
        {
            if (!ReferenceEquals(_owner, null))
            {
                _owner.RuntimeHostDestroyed();
            }
        }
    }

    internal sealed class CategoryStats
    {
        internal static readonly CategoryStats Empty = new CategoryStats();
        internal readonly Dictionary<string, TargetStats> Targets =
            new Dictionary<string, TargetStats>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, WeaponStats> Weapons =
            new Dictionary<string, WeaponStats>(StringComparer.OrdinalIgnoreCase);

        internal int Kills;
        internal float DestroyedCost;
        internal float WeaponCost;
        internal int MissilesFired;
        internal int BombsDropped;
        internal int OtherRoundsFired;

        internal void AddKill(string targetName, float value)
        {
            Kills++;
            DestroyedCost += value;
            targetName = string.IsNullOrEmpty(targetName) ? "Unknown unit" : targetName;

            TargetStats detail;
            if (!Targets.TryGetValue(targetName, out detail))
            {
                detail = new TargetStats(targetName);
                Targets.Add(targetName, detail);
            }

            detail.Count++;
            detail.Value += value;
        }

        internal void AddWeapon(WeaponInfo info, int rounds, bool addToAggregateCost)
        {
            float cost = Mathf.Max(0f, info.costPerRound) * rounds;
            if (addToAggregateCost)
            {
                WeaponCost += cost;
            }

            if (info.bomb)
            {
                BombsDropped += rounds;
            }
            else if (info.missile)
            {
                MissilesFired += rounds;
            }
            else
            {
                OtherRoundsFired += rounds;
            }

            string id = GetWeaponId(info);
            WeaponStats detail;
            if (!Weapons.TryGetValue(id, out detail))
            {
                detail = new WeaponStats(id, GetWeaponDisplayName(info), GetWeaponKind(info));
                Weapons.Add(id, detail);
            }

            detail.Rounds += rounds;
            detail.Cost += cost;
        }

        internal static string GetWeaponDisplayName(WeaponInfo info)
        {
            if (info == null)
            {
                return "Unknown weapon";
            }

            if (!string.IsNullOrEmpty(info.weaponName))
            {
                return info.weaponName;
            }

            if (!string.IsNullOrEmpty(info.shortName))
            {
                return info.shortName;
            }

            return string.IsNullOrEmpty(info.name) ? "Unknown weapon" : info.name;
        }

        private static string GetWeaponId(WeaponInfo info)
        {
            if (info == null)
            {
                return "unknown-weapon";
            }

            return string.IsNullOrEmpty(info.name) ? GetWeaponDisplayName(info) : info.name;
        }

        private static string GetWeaponKind(WeaponInfo info)
        {
            if (info.bomb) return "Bomb";
            if (info.missile) return "Missile";
            if (info.gun) return "Gun";
            if (info.energy) return "Energy";
            if (info.jammer) return "Jammer";
            if (info.troops) return "Troops";
            if (info.cargo) return "Cargo";
            return "Other";
        }

        internal List<TargetStats> GetTargetsSorted()
        {
            List<TargetStats> result = new List<TargetStats>(Targets.Values);
            result.Sort(delegate(TargetStats a, TargetStats b)
            {
                int byValue = b.Value.CompareTo(a.Value);
                return byValue != 0
                    ? byValue
                    : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        internal List<WeaponStats> GetWeaponsSorted()
        {
            List<WeaponStats> result = new List<WeaponStats>(Weapons.Values);
            result.Sort(delegate(WeaponStats a, WeaponStats b)
            {
                int byCost = b.Cost.CompareTo(a.Cost);
                return byCost != 0
                    ? byCost
                    : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        internal void Clear()
        {
            Kills = 0;
            DestroyedCost = 0f;
            WeaponCost = 0f;
            MissilesFired = 0;
            BombsDropped = 0;
            OtherRoundsFired = 0;
            Targets.Clear();
            Weapons.Clear();
        }

        internal CategorySaveData ToSaveData()
        {
            CategorySaveData data = new CategorySaveData
            {
                kills = Kills,
                destroyedCost = DestroyedCost,
                weaponCost = WeaponCost,
                missilesFired = MissilesFired,
                bombsDropped = BombsDropped,
                otherRoundsFired = OtherRoundsFired,
                targets = new List<TargetSaveData>(),
                weapons = new List<WeaponSaveData>()
            };

            List<TargetStats> targets = GetTargetsSorted();
            for (int i = 0; i < targets.Count; i++)
            {
                data.targets.Add(new TargetSaveData
                {
                    name = targets[i].Name,
                    count = targets[i].Count,
                    value = targets[i].Value
                });
            }

            List<WeaponStats> weapons = GetWeaponsSorted();
            for (int i = 0; i < weapons.Count; i++)
            {
                data.weapons.Add(new WeaponSaveData
                {
                    id = weapons[i].Id,
                    name = weapons[i].Name,
                    kind = weapons[i].Kind,
                    rounds = weapons[i].Rounds,
                    cost = weapons[i].Cost
                });
            }

            return data;
        }

        internal void LoadFrom(CategorySaveData data)
        {
            Clear();
            if (data == null)
            {
                return;
            }

            Kills = Mathf.Max(0, data.kills);
            DestroyedCost = SafeNonNegative(data.destroyedCost);
            WeaponCost = SafeNonNegative(data.weaponCost);
            MissilesFired = Mathf.Max(0, data.missilesFired);
            BombsDropped = Mathf.Max(0, data.bombsDropped);
            OtherRoundsFired = Mathf.Max(0, data.otherRoundsFired);
            if (data.targets != null)
            {
                for (int i = 0; i < data.targets.Count; i++)
                {
                    TargetSaveData savedTarget = data.targets[i];
                    if (savedTarget == null || string.IsNullOrEmpty(savedTarget.name))
                    {
                        continue;
                    }

                    TargetStats target;
                    if (!Targets.TryGetValue(savedTarget.name, out target))
                    {
                        target = new TargetStats(savedTarget.name);
                        Targets.Add(savedTarget.name, target);
                    }

                    target.Count += Mathf.Max(0, savedTarget.count);
                    target.Value += SafeNonNegative(savedTarget.value);
                }
            }

            if (data.weapons != null)
            {
                for (int i = 0; i < data.weapons.Count; i++)
                {
                    WeaponSaveData savedWeapon = data.weapons[i];
                    if (savedWeapon == null ||
                        (string.IsNullOrEmpty(savedWeapon.id) && string.IsNullOrEmpty(savedWeapon.name)))
                    {
                        continue;
                    }

                    string id = string.IsNullOrEmpty(savedWeapon.id) ? savedWeapon.name : savedWeapon.id;
                    WeaponStats weapon;
                    if (!Weapons.TryGetValue(id, out weapon))
                    {
                        weapon = new WeaponStats(id,
                            string.IsNullOrEmpty(savedWeapon.name) ? id : savedWeapon.name,
                            string.IsNullOrEmpty(savedWeapon.kind) ? "Other" : savedWeapon.kind);
                        Weapons.Add(id, weapon);
                    }

                    weapon.Rounds += Mathf.Max(0, savedWeapon.rounds);
                    weapon.Cost += SafeNonNegative(savedWeapon.cost);
                }
            }
        }

        private static float SafeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }
    }

    internal sealed class TargetStats
    {
        internal readonly string Name;
        internal int Count;
        internal float Value;

        internal TargetStats(string name)
        {
            Name = name;
        }
    }

    internal sealed class WeaponStats
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly string Kind;
        internal int Rounds;
        internal float Cost;

        internal WeaponStats(string id, string name, string kind)
        {
            Id = id;
            Name = name;
            Kind = kind;
        }
    }

    [DataContract]
    public sealed class TrackerSaveData
    {
        [DataMember(Order = 1)]
        public int version;

        [DataMember(Order = 2)]
        public CategorySaveData player;

        [DataMember(Order = 3)]
        public List<FactionSaveData> factions;
    }

    [DataContract]
    public sealed class FactionSaveData
    {
        [DataMember(Order = 1)]
        public string name;

        [DataMember(Order = 2)]
        public CategorySaveData stats;
    }

    [DataContract]
    public sealed class CategorySaveData
    {
        [DataMember(Order = 1)]
        public int kills;

        [DataMember(Order = 2)]
        public float destroyedCost;

        [DataMember(Order = 3)]
        public float weaponCost;

        [DataMember(Order = 4)]
        public int missilesFired;

        [DataMember(Order = 5)]
        public int bombsDropped;

        [DataMember(Order = 6)]
        public int otherRoundsFired;

        [DataMember(Order = 7)]
        public List<TargetSaveData> targets;

        [DataMember(Order = 8, EmitDefaultValue = false)]
        public List<WeaponSaveData> weapons;
    }

    [DataContract]
    public sealed class TargetSaveData
    {
        [DataMember(Order = 1)]
        public string name;

        [DataMember(Order = 2)]
        public int count;

        [DataMember(Order = 3)]
        public float value;
    }

    [DataContract]
    public sealed class WeaponSaveData
    {
        [DataMember(Order = 1)]
        public string id;

        [DataMember(Order = 2)]
        public string name;

        [DataMember(Order = 3)]
        public string kind;

        [DataMember(Order = 4)]
        public int rounds;

        [DataMember(Order = 5)]
        public float cost;
    }

    [HarmonyPatch(typeof(MessageManager), "UserCode_RpcKillMessage_635947223")]
    internal static class KillMessagePatch
    {
        private static void Postfix(PersistentID killerID, PersistentID killedID)
        {
            KillCostTrackerPlugin plugin = KillCostTrackerPlugin.Instance;
            if (!ReferenceEquals(plugin, null))
            {
                plugin.RecordKill(killerID, killedID);
            }
        }
    }

    [HarmonyPatch(typeof(MissileLauncher), "Fire")]
    internal static class MissileLauncherFirePatch
    {
        private static void Prefix(MissileLauncher __instance, out int __state)
        {
            __state = __instance.GetAmmoTotal();
        }

        private static void Postfix(MissileLauncher __instance, Unit owner, int __state)
        {
            int rounds = Mathf.Max(0, __state - __instance.GetAmmoTotal());
            KillCostTrackerPlugin plugin = KillCostTrackerPlugin.Instance;
            if (!ReferenceEquals(plugin, null) && rounds > 0)
            {
                plugin.RecordWeaponFire(owner, __instance.info, rounds);
            }
        }
    }

    [HarmonyPatch(typeof(MountedMissile), "Fire")]
    internal static class MountedMissileFirePatch
    {
        private static readonly FieldInfo FiredField = AccessTools.Field(typeof(MountedMissile), "fired");

        private static void Prefix(MountedMissile __instance, out bool __state)
        {
            __state = FiredField != null && (bool)FiredField.GetValue(__instance);
        }

        private static void Postfix(MountedMissile __instance, Unit owner, bool __state)
        {
            bool fired = FiredField != null && (bool)FiredField.GetValue(__instance);
            KillCostTrackerPlugin plugin = KillCostTrackerPlugin.Instance;
            if (!ReferenceEquals(plugin, null) && !__state && fired)
            {
                plugin.RecordWeaponFire(owner, __instance.info, 1);
            }
        }
    }

    [HarmonyPatch(typeof(Gun), "SpawnBullet")]
    internal static class GunFirePatch
    {
        private static readonly FieldInfo AttachedUnitField = AccessTools.Field(typeof(Weapon), "attachedUnit");
        private static readonly FieldInfo MuzzlesField = AccessTools.Field(typeof(Gun), "muzzles");
        private static readonly FieldInfo GuidedProjectileField = AccessTools.Field(typeof(Gun), "guidedProjectile");

        private static void Postfix(Gun __instance)
        {
            KillCostTrackerPlugin plugin = KillCostTrackerPlugin.Instance;
            if (ReferenceEquals(plugin, null) || AttachedUnitField == null)
            {
                return;
            }

            Unit owner = AttachedUnitField.GetValue(__instance) as Unit;
            int rounds = 1;
            if (GuidedProjectileField == null || GuidedProjectileField.GetValue(__instance) == null)
            {
                Transform[] muzzles = MuzzlesField != null
                    ? MuzzlesField.GetValue(__instance) as Transform[]
                    : null;
                rounds = muzzles != null ? Mathf.Max(1, muzzles.Length) : 1;
            }

            plugin.RecordWeaponFire(owner, __instance.info, rounds);
        }
    }
}
