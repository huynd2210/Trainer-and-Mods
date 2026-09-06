using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using FallenAces;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Key = UnityEngine.InputSystem.Key;

namespace FallenAcesTrainer
{
    [BepInPlugin(Id, "Fallen Aces Trainer", "1.0.0")]
    public sealed class TrainerPlugin : BaseUnityPlugin
    {
        public const string Id = "local.codex.fallenacestrainer";

        // A damage figure large enough to survive a full toughness bar, which absorbs
        // up to 75% of an incoming hit.
        private const int OneHitKillMultiplier = 10;

        // Width of the cursor gutter to the left of every row label.
        private const float Gutter = 16f;

        // Panel fill, accent and dimmed text are the values the sibling mods in this
        // folder already use: the near-black panel and 15px white rows come from the
        // Kill Tracker overlay, and gold marks "this is the current one" exactly as the
        // Expanded Inventory list uses it.
        private static readonly Color PanelFill = new Color(0.025f, 0.03f, 0.045f, 0.9f);
        private static readonly Color Accent = new Color(1f, 0.8f, 0.25f);
        private static readonly Color Dim = new Color(0.6f, 0.62f, 0.68f);

        private Harmony harmony;
        private Feature[] features;
        private ConfigEntry<Key> overlayKey, upKey, downKey, decreaseKey, increaseKey;
        private ConfigEntry<bool> showActiveCheats;
        private ConfigEntry<float> uiScale;
        private Texture2D panelTexture;
        private GUIStyle panelStyle, titleStyle, rowStyle, headerStyle, valueStyle;
        private bool overlayOpen;
        private int cursor;
        private Player lastPlayer;

        private void Awake()
        {
            features = TrainerCheats.Build();

            overlayKey = Config.Bind("Hotkeys", "Overlay", Key.Insert,
                "Opens and closes the trainer overlay.");
            upKey = Config.Bind("Hotkeys", "SelectPrevious", Key.UpArrow,
                "Move the overlay cursor up.");
            downKey = Config.Bind("Hotkeys", "SelectNext", Key.DownArrow,
                "Move the overlay cursor down.");
            decreaseKey = Config.Bind("Hotkeys", "Decrease", Key.LeftArrow,
                "Switch the selected entry off, or step its value down.");
            increaseKey = Config.Bind("Hotkeys", "Increase", Key.RightArrow,
                "Switch the selected entry on, run it, or step its value up.");
            showActiveCheats = Config.Bind("Display", "ShowActiveCheats", true,
                "List the cheats that are currently on in the corner of the screen.");
            uiScale = Config.Bind("Display", "Scale", 1f,
                new ConfigDescription("Size of the trainer overlay.", new AcceptableValueRange<float>(0.65f, 2f)));

            // Each feature owns its bindings, so the config section is filled by walking
            // the registry rather than by a hand-kept list that can drift out of date.
            foreach (var feature in features)
                foreach (var binding in feature.Bindings)
                    binding.Hotkey = Config.Bind("Hotkeys", binding.Name, binding.DefaultKey,
                        binding.Description + " Set to None to unbind.");

            WarnAboutKeyConflicts();

            harmony = new Harmony(Id);
            try
            {
                harmony.PatchAll(typeof(TrainerPlugin).Assembly);
                Logger.LogInfo("Loaded. " + overlayKey.Value + " opens the trainer; "
                    + features.Length + " entries, " + CountBindings() + " direct hotkeys.");
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-trainer-smoke-test") >= 0)
                    StartCoroutine(TrainerSmokeTest.Run(Logger, features));
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                CombatOverrides.Clear();
                Logger.LogError("Trainer could not initialize; patches removed. " + ex);
                enabled = false;
            }
        }

        // Checks the keys actually in effect, not just the shipped defaults: a stale or
        // hand-edited config can put a hotkey on top of the game, another mod, or a
        // second trainer entry, and none of those would otherwise say anything.
        private void WarnAboutKeyConflicts()
        {
            var claimed = new Dictionary<Key, string>();
            foreach (var entry in new[] { overlayKey, upKey, downKey, decreaseKey, increaseKey })
                Check(entry.Definition.Key, entry.Value);
            foreach (var feature in features)
                foreach (var binding in feature.Bindings)
                    Check(binding.Name, binding.Current);

            void Check(string name, Key key)
            {
                if (key == Key.None) return;
                string owner = ReservedKeys.OwnerOf(key);
                if (owner != null)
                    Logger.LogWarning("Hotkey " + key + " (" + name + ") is already used by "
                        + owner + "; both will fire.");
                if (claimed.TryGetValue(key, out var other))
                    Logger.LogWarning("Hotkey " + key + " is bound to both " + other + " and "
                        + name + "; both will fire.");
                else claimed[key] = name;
            }
        }

        private int CountBindings()
        {
            int count = 0;
            foreach (var feature in features)
                foreach (var binding in feature.Bindings)
                    if (binding.Current != Key.None) count++;
            return count;
        }

        private void OnDestroy()
        {
            // Leave the game as we found it: switched-on cheats are undone rather than
            // left burnt into a session that outlives the plugin.
            if (features != null)
                foreach (var feature in features)
                    Safely(feature.Restore);
            CombatOverrides.Clear();
            if (panelTexture != null) Destroy(panelTexture);
            if (harmony != null) harmony.UnpatchSelf();
        }

        private void Safely(Action action)
        {
            try { action(); }
            catch (Exception ex) { Logger.LogWarning("Trainer cleanup failed: " + ex.Message); }
        }

        private void Update()
        {
            RefreshPlayerBoundState();
            if (!Game.InGameplay()) return;
            foreach (var feature in features) feature.Tick();

            var keyboard = Keyboard.current;
            // Ignore every trainer key while the pause menu or the game's own developer
            // console has the screen, so arrows and F-keys stay theirs.
            if (keyboard == null || Game.MenuIsOpen()) return;

            if (Pressed(keyboard, overlayKey.Value)) overlayOpen = !overlayOpen;
            if (overlayOpen) HandleOverlayInput(keyboard);
            HandleDirectHotkeys(keyboard);
        }

        // Flags such as InfiniteStamina and SuperPunch live on the Player, which is
        // rebuilt on every level load and save load. Re-assert them whenever a different
        // Player turns up rather than writing them every frame.
        private void RefreshPlayerBoundState()
        {
            Player current = Player.TryGetPlayer(out var player) ? player : null;
            if (ReferenceEquals(current, lastPlayer)) return;
            lastPlayer = current;
            if (current == null) return;
            foreach (var feature in features) Safely(feature.Reapply);
        }

        private void HandleOverlayInput(Keyboard keyboard)
        {
            if (Pressed(keyboard, upKey.Value)) cursor = TrainerMath.WrapIndex(cursor - 1, features.Length);
            if (Pressed(keyboard, downKey.Value)) cursor = TrainerMath.WrapIndex(cursor + 1, features.Length);
            cursor = TrainerMath.WrapIndex(cursor, features.Length);
            if (Pressed(keyboard, increaseKey.Value)) Safely(features[cursor].Increase);
            if (Pressed(keyboard, decreaseKey.Value)) Safely(features[cursor].Decrease);
        }

        private void HandleDirectHotkeys(Keyboard keyboard)
        {
            foreach (var feature in features)
                foreach (var binding in feature.Bindings)
                    if (Pressed(keyboard, binding.Current)) Safely(binding.Press);
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        private void OnGUI()
        {
            if (!Game.InGameplay() || Game.MenuIsOpen()) return;
            EnsureStyles();
            float scale = Mathf.Clamp(uiScale.Value, 0.65f, 2f);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            try
            {
                if (overlayOpen) DrawOverlay(Screen.height / scale);
                else if (showActiveCheats.Value) DrawActiveCheats(Screen.height / scale);
            }
            finally { GUI.matrix = previousMatrix; }
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;

            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTexture.SetPixel(0, 0, PanelFill);
            panelTexture.Apply();

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.normal.textColor = Color.white;
            panelStyle.padding = new RectOffset(0, 0, 0, 0);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            titleStyle.normal.textColor = Color.white;

            rowStyle = new GUIStyle(titleStyle) { fontSize = 14, fontStyle = FontStyle.Normal };
            headerStyle = new GUIStyle(rowStyle) { fontSize = 12 };
            // Built once: OnGUI runs at least twice a frame, so allocating a style per
            // row per pass would be steady garbage for the collector.
            valueStyle = new GUIStyle(rowStyle) { alignment = TextAnchor.UpperRight };
        }

        // The always-on layer earns its place by listing only what is switched on, and
        // disappearing entirely when nothing is. Bottom-left, clear of the Kill Tracker
        // panel top-right and the Expanded Inventory list on the right.
        private void DrawActiveCheats(float screenHeight)
        {
            var active = new List<string>();
            foreach (var feature in features)
            {
                string text = feature.Ambient;
                if (!string.IsNullOrEmpty(text)) active.Add(text);
            }
            if (active.Count == 0) return;

            const float row = 20f;
            const float width = 250f;
            const float x = 18f;
            float height = row * active.Count + 16f;
            float y = screenHeight - height - 18f;
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, panelStyle);
            rowStyle.normal.textColor = Accent;
            for (int i = 0; i < active.Count; i++)
                GUI.Label(new Rect(x + 13f, y + 8f + row * i, width - 26f, row), active[i], rowStyle);
            rowStyle.normal.textColor = Color.white;
        }

        private void DrawOverlay(float screenHeight)
        {
            const float row = 21f;
            const float width = 410f;
            const float x = 18f;
            const float pad = 13f;
            int lines = features.Length + CountSections() + 2;
            float height = row * lines + 24f;
            float y = Mathf.Max(18f, (screenHeight - height) * 0.5f);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, panelStyle);

            float textX = x + pad;
            float textWidth = width - pad * 2f;
            float line = y + 10f;

            GUI.Label(new Rect(textX, line, textWidth, row), "TRAINER", titleStyle);
            line += row;

            string section = null;
            for (int i = 0; i < features.Length; i++)
            {
                var feature = features[i];
                if (feature.Section != section)
                {
                    section = feature.Section;
                    headerStyle.normal.textColor = Dim;
                    GUI.Label(new Rect(textX, line + 4f, textWidth, row), section.ToUpperInvariant(), headerStyle);
                    line += row;
                }
                bool selected = i == cursor;
                // Gold marks the cursor on the left and "switched on" on the right, so a
                // glance down the value column reads which cheats are live without
                // having to find the cursor first.
                rowStyle.normal.textColor = selected ? Accent : Color.white;
                valueStyle.normal.textColor = IsNeutral(feature) ? Dim : Accent;
                // The marker gets its own gutter rather than being padded with spaces:
                // in a proportional font "> " is narrower than the spaces standing in for
                // it, which would shunt the selected row left of every other row.
                if (selected) GUI.Label(new Rect(textX, line, Gutter, row), ">", rowStyle);
                GUI.Label(new Rect(textX + Gutter, line, textWidth - Gutter - 96f, row),
                    feature.Label, rowStyle);
                GUI.Label(new Rect(textX, line, textWidth, row), feature.Value, valueStyle);
                line += row;
            }

            headerStyle.normal.textColor = Dim;
            // Separated by the same middle dot the Expanded Inventory list uses; plain
            // spaces let the three hints run together into one unreadable string.
            GUI.Label(new Rect(textX, line + 4f, textWidth, row),
                Name(upKey) + "/" + Name(downKey) + " select  ·  "
                + Name(decreaseKey) + "/" + Name(increaseKey) + " change  ·  "
                + Name(overlayKey) + " close", headerStyle);
            rowStyle.normal.textColor = Color.white;
        }

        // A feature is "neutral" when it is contributing nothing, which is exactly what
        // the ambient strip uses to decide whether to list it.
        private static bool IsNeutral(Feature feature)
        {
            return string.IsNullOrEmpty(feature.Ambient);
        }

        private int CountSections()
        {
            int count = 0;
            string section = null;
            foreach (var feature in features)
                if (feature.Section != section) { section = feature.Section; count++; }
            return count;
        }

        // "UpArrow" and friends are the only key names long enough to push the hint line
        // past the panel edge, and "Up" is no less clear. Everything else prints as-is.
        private static string Name(ConfigEntry<Key> entry)
        {
            string name = entry.Value.ToString();
            return name.EndsWith("Arrow", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "Arrow".Length)
                : name;
        }

        // Both cheats that change how damage lands go through the one method every
        // damage path in the game funnels into, so there is nothing to keep in sync.
        [HarmonyPatch(typeof(Health), nameof(Health.TakeDamage))]
        private static class TakeDamagePatch
        {
            private static bool Prefix(Health __instance, ref DamageInfo info)
            {
                if (!CombatOverrides.GodMode && !CombatOverrides.OneHitKills) return true;
                bool isPlayer = Player.TryGetPlayer(out var player)
                    && player != null && ReferenceEquals(__instance, player.Health);
                if (isPlayer)
                {
                    // Mirror the rule the game's own NoDamageAllowed uses: falling out of
                    // the world still kills, because surviving it strands the player
                    // under the level with no way back.
                    return !CombatOverrides.GodMode || info.Type == DamageType.FallOutOfWorld;
                }
                if (CombatOverrides.OneHitKills && info.Damage > 0)
                    info.Damage = Mathf.Max(info.Damage, __instance.Max * OneHitKillMultiplier);
                return true;
            }
        }
    }

    // Startup checks that can be made without a level loaded. Gameplay behaviour still
    // has to be verified by hand; this only catches the assumptions that would silently
    // stop holding if the game updated.
    internal static class TrainerSmokeTest
    {

        public static System.Collections.IEnumerator Run(BepInEx.Logging.ManualLogSource log, Feature[] features)
        {
            var failures = new List<string>();

            var patched = AccessTools.Method(typeof(Health), nameof(Health.TakeDamage));
            if (patched == null || Harmony.GetPatchInfo(patched) == null)
                failures.Add("Health.TakeDamage is not patched");

            if (AccessTools.PropertySetter(typeof(Stamina), nameof(Stamina.InfiniteStamina)) == null)
                failures.Add("Stamina.InfiniteStamina setter is missing");
            if (AccessTools.Method(typeof(PlayerMovementHandler), nameof(PlayerMovementHandler.TryToggleNoclipState)) == null)
                failures.Add("PlayerMovementHandler.TryToggleNoclipState is missing");
            if (AccessTools.PropertySetter(typeof(FallenAces.AI.Awareness), nameof(FallenAces.AI.Awareness.IgnorePlayer)) == null)
                failures.Add("Awareness.IgnorePlayer setter is missing");
            if (AccessTools.Field(typeof(Inventory.Weapon), nameof(Inventory.Weapon.AmmoLeft)) == null)
                failures.Add("Inventory.Weapon.AmmoLeft is missing");

            // The trainer must never write Health._invulnerability, because that field is
            // written into save files; the damage prefix exists precisely to avoid it.
            if (AccessTools.Field(typeof(Health), "_invulnerability") == null)
                failures.Add("Health._invulnerability is missing, so the save-safety assumption cannot be checked");

            var ids = new HashSet<string>();
            var keys = new Dictionary<Key, string>();
            foreach (var feature in features)
            {
                if (!ids.Add(feature.Id)) failures.Add("Duplicate feature id: " + feature.Id);
                foreach (var binding in feature.Bindings)
                {
                    if (binding.Current == Key.None) continue;
                    if (keys.TryGetValue(binding.Current, out var alreadyBound))
                        failures.Add("Hotkey " + binding.Current + " is bound to both " + alreadyBound + " and " + binding.Name);
                    else keys[binding.Current] = binding.Name;
                    // Guards what ships. A config that has drifted onto a taken key is a
                    // user-visible warning at startup instead, not a build failure.
                    string reservedBy = ReservedKeys.OwnerOf(binding.DefaultKey);
                    if (reservedBy != null)
                        failures.Add("Default hotkey " + binding.DefaultKey + " for " + binding.Name
                            + " is already used by " + reservedBy);
                }
            }

            if (failures.Count == 0)
                log.LogInfo("SMOKE PASS: damage patch installed, game cheat APIs present, "
                    + features.Length + " features with unique ids and no conflicting hotkeys.");
            else
                foreach (var failure in failures) log.LogError("SMOKE FAIL: " + failure);

            log.LogInfo("SMOKE COMPLETE (startup only; gameplay still needs manual testing).");
            // BepInEx flushes its disk log periodically; give the results time to land.
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }
    }
}
