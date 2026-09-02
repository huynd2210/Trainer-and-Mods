using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TheLastStand.Controller.Unit.Enemy;
using TheLastStand.Definition.Unit;
using TheLastStand.Manager;
using TheLastStand.Model;
using TheLastStand.Model.Unit.Enemy;
using TPLib;
using UnityEngine;

namespace TLSKillTracker;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class KillTrackerPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.trainer.thelastspell.killtracker";
    public const string PluginName = "The Last Spell Kill Tracker";
    public const string PluginVersion = "1.1.0";

    internal static ManualLogSource Log;

    public class EnemyTypeStats
    {
        public string Id;
        public string Name;
        public long Night;
        public long Session;
    }

    // enemy template id -> stats
    public static readonly Dictionary<string, EnemyTypeStats> Types = new();

    // Umbrella counters
    public static long SessionKills;
    public static long NightKills;
    public static long RunKills;
    public static long BestNightKills;
    public static long SessionSouls;

    private ConfigEntry<bool> cfgShowOverlay;
    private ConfigEntry<bool> cfgKillPopups;
    private ConfigEntry<KeyCode> cfgToggleKey;
    private ConfigEntry<int> cfgMaxRows;

    private bool _showOverlay = true;
    private Vector2 _pos = new Vector2(-1f, -1f); // -1 = default top-right
    private Rect _rect;
    private KeyCode _toggleKey;
    private Vector2 _scroll;

    private const int MaxPopups = 6;
    private static readonly (string text, float until)[] _popups = new (string, float)[MaxPopups];
    private static int _popupIndex;

    // Night/run change detection
    private bool _hadGame;
    private int _lastDayNumber = -1;
    private Game.E_Cycle _lastCycle = Game.E_Cycle.Day;

    private GUIStyle _style;
    private GUIStyle _titleStyle;
    private GUIStyle _headStyle;
    private GUIStyle _popupStyle;

    private void Awake()
    {
        Log = Logger;
        cfgShowOverlay = Config.Bind("UI", "ShowOverlay", true, "Show the kill tracker panel");
        cfgKillPopups = Config.Bind("UI", "KillPopups", true, "Briefly pop up a counter on each kill");
        cfgToggleKey = Config.Bind("UI", "ToggleKey", KeyCode.F11, "Key to show/hide the kill tracker panel");
        cfgMaxRows = Config.Bind("UI", "MaxRows", 14, "Max enemy types shown before scrolling");

        _showOverlay = cfgShowOverlay.Value;
        _toggleKey = cfgToggleKey.Value;

        var h = new Harmony(PluginGuid);
        h.PatchAll(typeof(Patches));
        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        try
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showOverlay = !_showOverlay;
                cfgShowOverlay.Value = _showOverlay;
            }
            if (Input.GetKeyDown(KeyCode.F12))
            {
                ResetNight();
                RunKills = 0;
                Log.LogInfo("Kill counters reset (F12).");
            }

            DetectNightOrRunChange();
        }
        catch (Exception e)
        {
            Log.LogWarning($"Update error: {e}");
        }
    }

    private static void ResetNight()
    {
        NightKills = 0;
        foreach (var t in Types.Values) t.Night = 0;
    }

    private void DetectNightOrRunChange()
    {
        Game game = null;
        try { game = TPSingleton<GameManager>.Instance?.Game; } catch { }
        if (game == null)
        {
            _hadGame = false;
            return;
        }

        // New run detected: day counter went backwards (fresh run starts at day 1).
        if (_hadGame && game.DayNumber < _lastDayNumber)
        {
            RunKills = 0;
        }

        // New night begins: Day -> Night transition.
        if (_hadGame && game.Cycle == Game.E_Cycle.Night && _lastCycle == Game.E_Cycle.Day)
        {
            ResetNight();
        }

        _hadGame = true;
        _lastDayNumber = game.DayNumber;
        _lastCycle = game.Cycle;
    }

    public static void OnKill(EnemyUnit enemy)
    {
        string id = enemy.Id ?? "Unknown";
        if (!Types.TryGetValue(id, out var t))
        {
            string name;
            try { name = string.IsNullOrEmpty(enemy.Name) ? id : enemy.Name; }
            catch { name = id; }
            t = new EnemyTypeStats { Id = id, Name = name };
            Types[id] = t;
        }
        t.Night++;
        t.Session++;

        SessionKills++;
        NightKills++;
        RunKills++;
        if (NightKills > BestNightKills) BestNightKills = NightKills;

        try
        {
            SessionSouls += (long)Mathf.RoundToInt(
                enemy.UnitStatsController.UnitStats.Stats[UnitStatDefinition.E_Stat.DamnedSoulsEarned].FinalClamped);
        }
        catch { }

        if (_instance != null && _instance.cfgKillPopups.Value)
        {
            _popups[_popupIndex % MaxPopups] = ($"{t.Name} +1  ({NightKills} this night)", Time.unscaledTime + 1.6f);
            _popupIndex++;
        }
    }

    private static KillTrackerPlugin _instance;
    private void Start() => _instance = this;

    // ---------- Overlay UI ----------

    private void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _headStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Italic, richText = true };
            _popupStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            _popupStyle.normal.textColor = new Color(1f, 0.55f, 0.35f);
        }

        // Kill popups (top center)
        float y = 80f;
        for (int i = 0; i < MaxPopups; i++)
        {
            var p = _popups[i];
            if (p.text == null || p.until == 0f) continue;
            if (Time.unscaledTime < p.until)
            {
                var r = new Rect((Screen.width - 360f) / 2f, y, 360f, 24f);
                GUI.Label(r, p.text, _popupStyle);
                y += 24f;
            }
            else
            {
                _popups[i] = (null, 0f);
            }
        }

        if (!_showOverlay)
        {
            GUI.Label(new Rect(Screen.width - 170f, 5f, 160f, 22f), $"[F11] kills: {SessionKills}", _style);
            return;
        }

        float w = 320f;
        float x = _pos.x < 0 ? Screen.width - w - 12f : _pos.x;
        float py = _pos.y < 0 ? 12f : _pos.y;
        _rect = new Rect(x, py, w, 0f); // height auto-sized by GUILayout
        _rect = GUILayout.Window(424243, _rect, DrawWindow, "Kill Tracker");
        _pos = new Vector2(_rect.x, _rect.y);
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label("Kill Tracker v" + PluginVersion, _titleStyle);
        GUILayout.Label($"<b>Totals</b>  night {NightKills} · run {RunKills} · session {SessionKills} · souls {SessionSouls}", _headStyle);

        GUILayout.Space(2f);
        GUILayout.Label("<b>Enemy type</b>                <b>Night</b>  <b>Session</b>", _headStyle);

        var rows = Types.Values.OrderByDescending(t => t.Session).ToList();
        int visible = Mathf.Min(rows.Count, cfgMaxRows.Value);
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUILayout.Height(20f * Mathf.Max(visible, 1) + 6f));
        foreach (var t in rows)
        {
            GUILayout.Label(Row(t), _style);
        }
        if (rows.Count == 0)
            GUILayout.Label("(no kills yet — start a night)", _style);
        GUILayout.EndScrollView();

        if (rows.Count > cfgMaxRows.Value)
            GUILayout.Label($"showing top {cfgMaxRows.Value} of {rows.Count} — scroll for more", _headStyle);

        GUILayout.Space(2f);
        GUILayout.Label("F11 hide — F12 reset night+run — drag to move", _headStyle);
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private static string Row(EnemyTypeStats t)
    {
        string name = t.Name.Length > 22 ? t.Name.Substring(0, 21) + "…" : t.Name;
        return $"{name.PadRight(24)} {t.Night,4}  {t.Session,7}";
    }
}

[HarmonyPatch]
internal static class Patches
{
    // Single hook covering every enemy type (bosses/elites inherit this override).
    // PrepareForDeath early-returns when the unit is already dead, so each enemy
    // is counted exactly once, whatever killed it.
    [HarmonyPatch(typeof(EnemyUnitController), nameof(EnemyUnitController.PrepareForDeath))]
    [HarmonyPostfix]
    private static void PrepareForDeathPostfix(EnemyUnitController __instance)
    {
        try
        {
            KillTrackerPlugin.OnKill(__instance.EnemyUnit);
        }
        catch (Exception e)
        {
            KillTrackerPlugin.Log?.LogWarning($"OnKill: {e.Message}");
        }
    }
}
