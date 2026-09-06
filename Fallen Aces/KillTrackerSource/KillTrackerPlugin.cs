using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using FallenAces;
using FallenAces.EnemyScripts;
using FallenAces.NewWorldGen;
using FallenAces.NonEnemyNPC;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Key = UnityEngine.InputSystem.Key;

namespace FallenAcesKillTracker
{
    [BepInPlugin(Id, "Fallen Aces Kill Tracker", Version)]
    public sealed class KillTrackerPlugin : BaseUnityPlugin
    {
        public const string Id = "local.codex.fallenaceskilltracker";
        public const string Version = "1.1.0";

        private const int PlayerKnockedOutNpcEvent = 70;
        private const int PlayerKilledNpcEvent = 71;
        private const int NpcAwakenedEvent = 72;

        private static KillTrackerPlugin instance;

        private readonly KillTrackerState tracker = new KillTrackerState();
        private Harmony harmony;
        private KillTrackerStore store;
        private GlobalEventManager subscribedGlobalEvents;
        private ConfigEntry<bool> visibleOnStart;
        private ConfigEntry<Key> toggleKey;
        private ConfigEntry<float> uiScale;
        private bool visible;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle killedStyle;
        private GUIStyle unconsciousStyle;
        private Texture2D panelTexture;

        private void Awake()
        {
            instance = this;
            visibleOnStart = Config.Bind("Display", "VisibleOnStart", true,
                "Show the kill tracker when gameplay starts.");
            toggleKey = Config.Bind("Hotkeys", "ToggleTracker", Key.F8,
                "Input System key used to show or hide the tracker.");
            uiScale = Config.Bind("Display", "Scale", 1f,
                new ConfigDescription("HUD scale.", new AcceptableValueRange<float>(0.65f, 2f)));
            visible = visibleOnStart.Value;
            store = new KillTrackerStore(System.IO.Path.Combine(Paths.ConfigPath, "local.codex.fallenaceskilltracker.counts.xml"));
            try
            {
                store.Load(tracker);
            }
            catch (Exception exception)
            {
                Logger.LogError("Could not restore tracker totals. Existing data will be preserved. " + exception);
            }
            tracker.Changed += SaveCounts;

            PhysicalStateHandler.PlayerKnockedOutEnemy += OnLegacyEnemyKnockedOut;
            PhysicalStateHandler.PlayerKilledEnemy += OnLegacyEnemyKilled;
            WorldLoader.WorldStartPart1 += OnWorldStarted;

            harmony = new Harmony(Id);
            try
            {
                harmony.PatchAll(typeof(KillTrackerPlugin).Assembly);
                Logger.LogInfo("Loaded. F8 toggles the tracker; cumulative counts persist across levels and restarts.");
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-killtracker-smoke-test") >= 0)
                {
                    StartCoroutine(SmokeTest());
                }
            }
            catch (Exception exception)
            {
                Logger.LogError("Kill Tracker could not initialize. " + exception);
                enabled = false;
            }
        }

        private void Update()
        {
            RefreshGlobalEventSubscription();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && toggleKey.Value != Key.None && keyboard[toggleKey.Value].wasPressedThisFrame)
            {
                visible = !visible;
                Logger.LogInfo("Tracker " + (visible ? "shown." : "hidden."));
            }
        }

        private void OnDestroy()
        {
            PhysicalStateHandler.PlayerKnockedOutEnemy -= OnLegacyEnemyKnockedOut;
            PhysicalStateHandler.PlayerKilledEnemy -= OnLegacyEnemyKilled;
            WorldLoader.WorldStartPart1 -= OnWorldStarted;
            UnsubscribeGlobalEvents();
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
            if (panelTexture != null)
            {
                Destroy(panelTexture);
            }
            if (instance == this)
            {
                instance = null;
            }
        }

        private void RefreshGlobalEventSubscription()
        {
            GlobalEventManager current = GlobalEventManager.Instance;
            if (current == subscribedGlobalEvents)
            {
                return;
            }

            UnsubscribeGlobalEvents();
            if (current == null)
            {
                return;
            }

            subscribedGlobalEvents = current;
            subscribedGlobalEvents.StartListening(PlayerKnockedOutNpcEvent, OnNpcKnockedOut);
            subscribedGlobalEvents.StartListening(PlayerKilledNpcEvent, OnNpcKilled);
            subscribedGlobalEvents.StartListening(NpcAwakenedEvent, OnNpcAwakened);
        }

        private void UnsubscribeGlobalEvents()
        {
            if (subscribedGlobalEvents == null)
            {
                return;
            }

            subscribedGlobalEvents.StopListening(PlayerKnockedOutNpcEvent, OnNpcKnockedOut);
            subscribedGlobalEvents.StopListening(PlayerKilledNpcEvent, OnNpcKilled);
            subscribedGlobalEvents.StopListening(NpcAwakenedEvent, OnNpcAwakened);
            subscribedGlobalEvents = null;
        }

        private void OnWorldStarted()
        {
            tracker.BeginWorld();
            Logger.LogDebug("New world: cumulative counts preserved.");
        }

        private void SaveCounts()
        {
            try { store.Save(tracker); }
            catch (Exception exception) { Logger.LogError("Could not save tracker totals. " + exception); }
        }

        private void OnLegacyEnemyKnockedOut(Enemy enemy)
        {
            if (enemy != null)
            {
                tracker.MarkUnconscious(enemy.GetInstanceID(), CategoryFor(enemy));
            }
        }

        private void OnLegacyEnemyKilled(Enemy enemy)
        {
            if (enemy != null)
            {
                tracker.MarkKilled(enemy.GetInstanceID(), CategoryFor(enemy));
            }
        }

        private void OnNpcKnockedOut(Arguments arguments)
        {
            NPC npc = GetNpc(arguments);
            if (npc != null)
            {
                tracker.MarkUnconscious(npc.GetInstanceID(), CategoryFor(npc));
            }
        }

        private void OnNpcKilled(Arguments arguments)
        {
            NPC npc = GetNpc(arguments);
            if (npc != null)
            {
                tracker.MarkKilled(npc.GetInstanceID(), CategoryFor(npc));
            }
        }

        private void OnNpcAwakened(Arguments arguments)
        {
            NPC npc = GetNpc(arguments);
            if (npc != null)
            {
                tracker.MarkAwake(npc.GetInstanceID());
            }
        }

        private static NPC GetNpc(Arguments arguments)
        {
            return arguments == null ? null : arguments.GetArgument(0) as NPC;
        }

        private static string CategoryFor(Enemy enemy)
        {
            return CategoryFor(enemy.Config, enemy.gameObject.name);
        }

        private static string CategoryFor(NPC npc)
        {
            return CategoryFor(npc.Config, npc.gameObject.name);
        }

        private static string CategoryFor(EnemyConfig config, string fallback)
        {
            if (config != null)
            {
                if (!string.IsNullOrWhiteSpace(config.PrintName))
                {
                    return config.PrintName.Trim();
                }
                if (config.EnemyId != Enemy.EnemyId.NoneDefined)
                {
                    return SplitPascalCase(config.EnemyId.ToString());
                }
                if (!string.IsNullOrWhiteSpace(config.name))
                {
                    return CleanObjectName(config.name);
                }
            }
            return CleanObjectName(fallback);
        }

        private static string CleanObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }
            return value.Replace("(Clone)", string.Empty).Trim();
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unknown";
            }

            System.Text.StringBuilder result = new System.Text.StringBuilder(value.Length + 4);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
                {
                    result.Append(' ');
                }
                result.Append(current);
            }
            return result.ToString();
        }

        private void OnGUI()
        {
            if (!visible || Player.Instance == null || GameworldSceneController.Instance == null ||
                GameworldSceneController.Instance.IsLoading)
            {
                return;
            }

            EnsureStyles();
            float scale = Mathf.Clamp(uiScale.Value, 0.65f, 2f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            try
            {
                const float width = 410f;
                const float rowHeight = 21f;
                float height = 82f + tracker.Categories.Count * rowHeight;
                float x = Screen.width / scale - width - 18f;
                const float y = 18f;
                GUI.Box(new Rect(x, y, width, height), GUIContent.none, panelStyle);

                GUI.Label(new Rect(x + 13f, y + 8f, width - 26f, 24f), "KILL TRACKER", titleStyle);
                GUI.Label(new Rect(x + 13f, y + 34f, 230f, 20f), "TYPE", labelStyle);
                GUI.Label(new Rect(x + 245f, y + 34f, 67f, 20f), "KILLED", killedStyle);
                GUI.Label(new Rect(x + 316f, y + 34f, 82f, 20f), "UNCON.", unconsciousStyle);

                float rowY = y + 56f;
                DrawRow(x, rowY, "ALL", tracker.Killed, tracker.Unconscious, true);
                rowY += rowHeight;
                foreach (var pair in tracker.Categories)
                {
                    DrawRow(x, rowY, pair.Key, pair.Value.Killed, pair.Value.Unconscious, false);
                    rowY += rowHeight;
                }
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawRow(float panelX, float y, string category, int killed, int unconscious, bool total)
        {
            GUIStyle nameStyle = total ? titleStyle : labelStyle;
            GUI.Label(new Rect(panelX + 13f, y, 228f, 20f), category, nameStyle);
            GUI.Label(new Rect(panelX + 245f, y, 67f, 20f), killed.ToString(), killedStyle);
            GUI.Label(new Rect(panelX + 316f, y, 82f, 20f), unconscious.ToString(), unconsciousStyle);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTexture.SetPixel(0, 0, new Color(0.025f, 0.03f, 0.045f, 0.9f));
            panelTexture.Apply();

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.normal.textColor = Color.white;
            panelStyle.padding = new RectOffset(0, 0, 0, 0);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 15;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            titleStyle.clipping = TextClipping.Clip;

            labelStyle = new GUIStyle(titleStyle);
            labelStyle.fontSize = 13;
            labelStyle.fontStyle = FontStyle.Normal;

            killedStyle = new GUIStyle(labelStyle);
            killedStyle.alignment = TextAnchor.MiddleCenter;
            killedStyle.normal.textColor = new Color(1f, 0.38f, 0.24f, 1f);

            unconsciousStyle = new GUIStyle(killedStyle);
            unconsciousStyle.normal.textColor = new Color(0.64f, 0.78f, 1f, 1f);
        }

        private IEnumerator SmokeTest()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (GlobalEventManager.Instance == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            RefreshGlobalEventSubscription();
            Logger.LogInfo("SMOKE: global-events=" + (subscribedGlobalEvents != null) +
                "; legacy-patch=" + (Harmony.GetPatchInfo(AccessTools.Method(typeof(Conciousness), nameof(Conciousness.TryWakeUp))) != null));
            Logger.LogInfo("SMOKE COMPLETE (startup integration only; combat and HUD require manual testing).");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        internal static void OnLegacyConsciousnessAwakened(Conciousness consciousness)
        {
            if (instance == null || consciousness == null)
            {
                return;
            }

            Enemy enemy = consciousness.GetComponent<Enemy>();
            if (enemy != null)
            {
                instance.tracker.MarkAwake(enemy.GetInstanceID());
            }
        }

        [HarmonyPatch(typeof(Conciousness), nameof(Conciousness.TryWakeUp))]
        private static class LegacyWakePatch
        {
            private static void Postfix(Conciousness __instance, bool __result)
            {
                if (__result)
                {
                    OnLegacyConsciousnessAwakened(__instance);
                }
            }
        }
    }
}
