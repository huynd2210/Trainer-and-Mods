using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TheLastStand.Controller.Unit;
using TheLastStand.Controller.Unit.Enemy;
using TheLastStand.Manager;
using TheLastStand.Manager.Unit;
using TheLastStand.Model;
using TPLib;
using UnityEngine;

namespace TLSTrainer;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class TrainerPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.trainer.thelastspell";
    public const string PluginName = "The Last Spell Trainer";
    public const string PluginVersion = "1.0.0";

    internal static ManualLogSource Log;

    // Toggles
    public static bool GodMode;
    public static bool OneHitKill;
    public static bool InfiniteMove;

    private ConfigEntry<bool> cfgGodMode;
    private ConfigEntry<bool> cfgOneHitKill;
    private ConfigEntry<bool> cfgInfiniteMove;
    private ConfigEntry<int> cfgGoldAmount;
    private ConfigEntry<int> cfgMaterialsAmount;
    private ConfigEntry<int> cfgXpAmount;
    private ConfigEntry<int> cfgDamnedSoulsAmount;
    private ConfigEntry<bool> cfgShowOverlay;

    private bool _showOverlay = true;
    private Vector2 _overlayPos = new Vector2(20f, 20f);
    private Rect _overlayRect;
    private string _toast = "";
    private float _toastUntil;
    private GUIStyle _labelStyle;
    private GUIStyle _toastStyle;
    private GUIStyle _titleStyle;

    private void Awake()
    {
        _instance = this;
        Log = Logger;

        cfgGodMode = Config.Bind("Toggles", "GodMode", false, "Heroes never lose health (also toggled with F3)");
        cfgOneHitKill = Config.Bind("Toggles", "OneHitKill", false, "Enemies die instantly when damaged (also toggled with F4)");
        cfgInfiniteMove = Config.Bind("Toggles", "InfiniteMove", false, "Heroes never spend movement points (also toggled with F8)");
        cfgGoldAmount = Config.Bind("Amounts", "Gold", 10000, "Gold added by F1");
        cfgMaterialsAmount = Config.Bind("Amounts", "Materials", 1000, "Materials added by F2");
        cfgXpAmount = Config.Bind("Amounts", "Xp", 500, "XP added to every hero by F6");
        cfgDamnedSoulsAmount = Config.Bind("Amounts", "DamnedSouls", 1000, "Tainted essence (Damned Souls) added by F10");
        cfgShowOverlay = Config.Bind("UI", "ShowOverlay", true, "Show the trainer overlay (also toggled with Insert)");

        GodMode = cfgGodMode.Value;
        OneHitKill = cfgOneHitKill.Value;
        InfiniteMove = cfgInfiniteMove.Value;
        _showOverlay = cfgShowOverlay.Value;

        var h = new Harmony(PluginGuid);
        h.PatchAll(typeof(Patches));
        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        try
        {
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                _showOverlay = !_showOverlay;
                cfgShowOverlay.Value = _showOverlay;
            }
            if (Input.GetKeyDown(KeyCode.F1))
                GiveGold(cfgGoldAmount.Value);
            if (Input.GetKeyDown(KeyCode.F2))
                GiveMaterials(cfgMaterialsAmount.Value);
            if (Input.GetKeyDown(KeyCode.F3))
                Toggle(ref GodMode, cfgGodMode, "God Mode");
            if (Input.GetKeyDown(KeyCode.F4))
                Toggle(ref OneHitKill, cfgOneHitKill, "One-Hit Kill");
            if (Input.GetKeyDown(KeyCode.F5))
                FullHeal();
            if (Input.GetKeyDown(KeyCode.F6))
                GiveXp(cfgXpAmount.Value);
            if (Input.GetKeyDown(KeyCode.F7))
                LevelUpAll();
            if (Input.GetKeyDown(KeyCode.F8))
                Toggle(ref InfiniteMove, cfgInfiniteMove, "Infinite Movement");
            if (Input.GetKeyDown(KeyCode.F9))
                RefillWorkers();
            if (Input.GetKeyDown(KeyCode.F10))
                GiveDamnedSouls(cfgDamnedSoulsAmount.Value);
        }
        catch (Exception e)
        {
            Log.LogWarning($"Update error: {e}");
        }
    }

    private static void Toggle(ref bool field, ConfigEntry<bool> entry, string name)
    {
        field = !field;
        entry.Value = field;
        ToastStatic($"{name}: {(field ? "ON" : "OFF")}");
    }

    internal static void ToastStatic(string msg) => _instance?.Toast(msg);

    private static TrainerPlugin _instance;
    private void Toast(string msg)
    {
        _toast = msg;
        _toastUntil = Time.unscaledTime + 2.5f;
    }

    // ---------- Cheat actions ----------

    private static void GiveGold(int amount)
    {
        var rm = TPSingleton<ResourceManager>.Instance;
        if (rm == null) { ToastStatic("Not in a run (no ResourceManager)."); return; }
        rm.SetGold(rm.Gold + amount, updateGoldMetaConditions: false);
        ToastStatic($"+{amount} Gold (total {rm.Gold})");
    }

    private static void GiveMaterials(int amount)
    {
        var rm = TPSingleton<ResourceManager>.Instance;
        if (rm == null) { ToastStatic("Not in a run (no ResourceManager)."); return; }
        rm.Materials += amount;
        ToastStatic($"+{amount} Materials (total {rm.Materials})");
    }

    private static void RefillWorkers()
    {
        var rm = TPSingleton<ResourceManager>.Instance;
        if (rm == null) { ToastStatic("Not in a run (no ResourceManager)."); return; }
        rm.RefillWorkers();
        ToastStatic($"Workers refilled ({rm.Workers}/{rm.MaxWorkers})");
    }

    private static void GiveDamnedSouls(int amount)
    {
        // Tainted essence (meta-currency): ApplicationManager.Application.DamnedSouls.
        var app = ApplicationManager.Application;
        if (app == null) { ToastStatic("No application state (main menu not ready)."); return; }
        uint add = amount > 0 ? (uint)amount : 0u;
        app.DamnedSouls += add;
        // Refresh the Dark Shop counter if it is on screen.
        try
        {
            var darkShop = TPSingleton<DarkShopManager>.Instance;
            AccessTools.Method(typeof(DarkShopManager), "UpdateDamnedSoulsText")
                ?.Invoke(darkShop, null);
        }
        catch { /* not in a scene with the dark shop */ }
        ToastStatic($"+{add} Damned Souls (total {app.DamnedSouls})");
    }

    private static void FullHeal()
    {
        var pum = TPSingleton<PlayableUnitManager>.Instance;
        if (pum?.PlayableUnits == null || pum.PlayableUnits.Count == 0)
        {
            ToastStatic("No heroes to heal.");
            return;
        }
        int n = 0;
        foreach (var unit in pum.PlayableUnits)
        {
            var c = unit?.PlayableUnitController;
            if (c == null || unit.IsDead) continue;
            try { c.GainHealth(99999f); } catch { }
            try { c.GainMana(99999f); } catch { }
            n++;
        }
        ToastStatic($"Healed & mana refilled: {n} hero(es)");
    }

    private static void GiveXp(int amount)
    {
        var pum = TPSingleton<PlayableUnitManager>.Instance;
        if (pum?.PlayableUnits == null || pum.PlayableUnits.Count == 0)
        {
            ToastStatic("No heroes to receive XP.");
            return;
        }
        int n = 0;
        foreach (var unit in pum.PlayableUnits)
        {
            var c = unit?.PlayableUnitController;
            if (c == null || unit.IsDead) continue;
            c.GainExperience(amount);
            n++;
        }
        ToastStatic($"+{amount} XP to {n} hero(es)");
    }

    private static void LevelUpAll()
    {
        var pum = TPSingleton<PlayableUnitManager>.Instance;
        if (pum?.PlayableUnits == null || pum.PlayableUnits.Count == 0)
        {
            ToastStatic("No heroes to level up.");
            return;
        }
        int n = 0;
        foreach (var unit in pum.PlayableUnits)
        {
            var c = unit?.PlayableUnitController;
            if (c == null || unit.IsDead) continue;
            c.LevelUp();
            n++;
        }
        ToastStatic($"Level up: {n} hero(es)");
    }

    // ---------- Overlay UI ----------

    private void OnGUI()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _toastStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        }

        if (_toast.Length > 0 && Time.unscaledTime < _toastUntil)
        {
            var w = 500f;
            var r = new Rect((Screen.width - w) / 2f, 40f, w, 30f);
            GUI.Label(r, _toast, _toastStyle);
        }
        else if (_toast.Length > 0) _toast = "";

        if (!_showOverlay)
        {
            GUI.Label(new Rect(10f, 5f, 300f, 22f), "[Insert] trainer", _labelStyle);
            return;
        }

        _overlayRect = new Rect(_overlayPos.x, _overlayPos.y, 300f, 258f);
        _overlayRect = GUILayout.Window(0xC0FFEE, _overlayRect, DrawWindow, "The Last Spell — Trainer");
        _overlayPos = new Vector2(_overlayRect.x, _overlayRect.y);
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label("TLS Trainer v" + PluginVersion, _titleStyle);
        GUILayout.Label(Row("F1", $"Gold +{cfgGoldAmount.Value}"));
        GUILayout.Label(Row("F2", $"Materials +{cfgMaterialsAmount.Value}"));
        GUILayout.Label(Row("F3", $"God Mode  [ {(GodMode ? "ON" : "off")} ]"));
        GUILayout.Label(Row("F4", $"One-Hit Kill  [ {(OneHitKill ? "ON" : "off")} ]"));
        GUILayout.Label(Row("F5", "Full heal + mana (all heroes)"));
        GUILayout.Label(Row("F6", $"XP +{cfgXpAmount.Value} (all heroes)"));
        GUILayout.Label(Row("F7", "Level up (all heroes)"));
        GUILayout.Label(Row("F8", $"Infinite Movement  [ {(InfiniteMove ? "ON" : "off")} ]"));
        GUILayout.Label(Row("F9", "Refill workers"));
        GUILayout.Label(Row("F10", $"Tainted essence +{cfgDamnedSoulsAmount.Value}"));
        GUILayout.Space(4f);
        GUILayout.Label("Insert: show/hide overlay — drag title bar to move", _labelStyle);
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private static string Row(string key, string desc) =>
        $"<b>{key}</b>  {desc}";
}

[HarmonyPatch]
internal static class Patches
{
    // God mode: heroes never lose health.
    [HarmonyPatch(typeof(PlayableUnitController), nameof(PlayableUnitController.LoseHealth))]
    [HarmonyPrefix]
    private static bool GodModePrefix() => !TrainerPlugin.GodMode;

    // One-hit kill: enemies go straight to death handling.
    [HarmonyPatch(typeof(EnemyUnitController), nameof(EnemyUnitController.LoseHealth))]
    [HarmonyPrefix]
    private static bool OneHitKillPrefix(EnemyUnitController __instance, ISkillCaster attacker, string skillName)
    {
        if (!TrainerPlugin.OneHitKill) return true;
        try
        {
            __instance.PrepareForDeath(attacker, skillName);
        }
        catch (Exception e)
        {
            TrainerPlugin.Log?.LogWarning($"OneHitKill: {e.Message}");
        }
        return false;
    }

    // Infinite movement: heroes never spend move points.
    [HarmonyPatch(typeof(UnitController), nameof(UnitController.SpendMovePoints))]
    [HarmonyPrefix]
    private static bool InfiniteMovePrefix(UnitController __instance, ref float __result)
    {
        if (!TrainerPlugin.InfiniteMove || !(__instance is PlayableUnitController)) return true;
        __result = 0f;
        return false;
    }
}
