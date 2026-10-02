using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NuclearOptionAutoMissileDefense
{
    [BepInPlugin(Id, "Auto-missile Defense", "1.0.0")]
    public sealed class AutoMissileDefensePlugin : BaseUnityPlugin
    {
        internal const string Id = "nuclearoption.automissiledefense";
        internal static ManualLogSource Log;
        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> StartEnabled;
        internal static bool Armed;
        internal static Aircraft CurrentAircraft;
        static readonly HashSet<Missile> Engaged = new HashSet<Missile>();
        static readonly List<Missile> Stale = new List<Missile>();
        static readonly FieldInfo WeaponIndex = AccessTools.Field(typeof(WeaponStation), "weaponIndex");
        static float lastCleanup;
        static int lastFrame = -1;
        static float lastError = -100f;

        void Awake()
        {
            Log = Logger;
            Range = Config.Bind("Defense", "EngagementRangeMetres", 5000f,
                new ConfigDescription("Fire when an incoming ARH/SARH missile is strictly closer than this range and ahead of the aircraft nose.", new AcceptableValueRange<float>(1f, 100000f)));
            ToggleKey = Config.Bind("Controls", "ToggleKey", new KeyboardShortcut(KeyCode.F8), "Toggle automatic missile defense while flying.");
            StartEnabled = Config.Bind("Defense", "EnabledOnEnteringAircraft", false, "Initial state each time you enter a different aircraft.");
            new Harmony(Id).PatchAll(typeof(AutoMissileDefensePlugin).Assembly);
            Log.LogInfo("Auto-missile Defense loaded. Toggle: " + ToggleKey.Value + "; range: " + Range.Value + "m.");
            // The game destroys the BepInEx host during startup. Static HUD hooks survive it.
        }

        internal static bool CanControl()
        {
            return CurrentAircraft != null && !CurrentAircraft.disabled &&
                GameManager.GetLocalAircraft(out var local) && local == CurrentAircraft &&
                !local.remoteSim && Time.timeScale > 0f &&
                (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer);
        }

        internal static void Toggle()
        {
            if (!CanControl()) return;
            Armed = !Armed;
            NativeDefenseButton.RefreshAll();
            var report = SceneSingleton<AircraftActionsReport>.i;
            if (report != null) report.ReportText("Auto-missile Defense " + (Armed ? "ON" : "OFF"), 3f);
        }

        internal static bool IsRadarMissile(Missile missile)
        {
            string seeker = missile.GetSeekerType();
            return seeker == "SARH" || seeker == "ARH";
        }

        internal static bool IsIRStation(WeaponStation station)
        {
            var info = station?.WeaponInfo;
            return info != null && info.missile && info.weaponPrefab != null &&
                info.weaponPrefab.GetComponent<IRSeeker>() != null;
        }

        internal static Weapon NextRound(WeaponStation station)
        {
            int index = (int)WeaponIndex.GetValue(station);
            return index >= 0 && index < station.Weapons.Count ? station.Weapons[index] : null;
        }

        internal static WeaponStation FindStation(Aircraft aircraft)
        {
            foreach (var station in aircraft.weaponStations)
            {
                if (!IsIRStation(station) || station.SalvoInProgress || !station.Ready() || station.SafetyIsOn(aircraft)) continue;
                var round = NextRound(station);
                if (round is MountedMissile && round.IsAttached() && round.GetAmmoLoaded() > 0) return station;
            }
            return null;
        }

        internal static void Tick(CombatHUD hud)
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft != hud.aircraft || aircraft.disabled)
            {
                if (CurrentAircraft != null) { CurrentAircraft = null; Armed = false; Engaged.Clear(); }
                NativeDefenseButton.SetVisible(false);
                return;
            }
            if (CurrentAircraft != aircraft)
            {
                CurrentAircraft = aircraft;
                Engaged.Clear();
                Armed = StartEnabled.Value;
                lastCleanup = Time.unscaledTime;
            }
            NativeDefenseButton.Ensure(hud);
            NativeDefenseButton.SetVisible(true);
            if (CanControl() && !Typing() && ToggleKey.Value.IsDown()) Toggle();
            NativeDefenseButton.RefreshAll();
            if (!CanControl() || !Armed) return;

            if (Time.unscaledTime - lastCleanup > 2f)
            {
                Stale.Clear();
                foreach (var missile in Engaged) if (missile == null || missile.disabled) Stale.Add(missile);
                foreach (var missile in Stale) Engaged.Remove(missile);
                lastCleanup = Time.unscaledTime;
            }
            var station = FindStation(aircraft);
            if (station == null) return;
            Missile nearest = null;
            float nearestDistance = float.MaxValue;
            Vector3 forward = aircraft.transform.forward;
            GlobalPosition position = aircraft.GlobalPosition();
            // No warning-system detection delay or target-selection side effects.
            for (int i = 0; i < UnitRegistry.allUnits.Count; i++)
            {
                if (!(UnitRegistry.allUnits[i] is Missile missile) || missile == null || missile.disabled) continue;
                if (missile.targetID != aircraft.persistentID || Engaged.Contains(missile)) continue;
                Vector3 offset = missile.GlobalPosition() - position;
                float distance = offset.sqrMagnitude;
                if (distance >= nearestDistance || !DefenseRules.Eligible(Armed, true, true,
                    IsRadarMissile(missile), false, Vector3.Dot(forward, offset), distance, Range.Value)) continue;
                nearest = missile;
                nearestDistance = distance;
            }
            if (nearest == null) return;

            // The native IR seeker needs faction tracking at launch. Use the same request as MAWS;
            // reliable network ordering puts this before the native missile launch command.
            if (aircraft.NetworkHQ == null) return;
            aircraft.NetworkHQ.CmdUpdateTrackingInfo(nearest.persistentID);
            var next = NextRound(station);
            int before = next.GetAmmoLoaded();
            station.LaunchMount(aircraft, nearest, nearest.GlobalPosition());
            if (next.GetAmmoLoaded() < before)
            {
                Engaged.Add(nearest);
                Log.LogInfo($"Defensive shot: {station.WeaponInfo.weaponName} -> {nearest.GetSeekerType()} {nearest.persistentID}, range {Mathf.Sqrt(nearestDistance):F0}m");
            }
        }

        static bool Typing()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return current != null && (current.GetComponent<InputField>() != null || current.GetComponent("TMP_InputField") != null);
        }

        [HarmonyPatch(typeof(CombatHUD), "LateUpdate")]
        static class UpdatePatch
        {
            static void Postfix(CombatHUD __instance)
            {
                try { Tick(__instance); }
                catch (Exception error)
                {
                    Armed = false;
                    NativeDefenseButton.RefreshAll();
                    if (Time.unscaledTime - lastError > 5f) { lastError = Time.unscaledTime; Log.LogError(error); }
                }
            }
        }

        [HarmonyPatch(typeof(CombatHUD), "OnDestroy")]
        static class DestroyPatch
        {
            static void Postfix() { CurrentAircraft = null; Armed = false; Engaged.Clear(); NativeDefenseButton.Clear(); }
        }

        [HarmonyPatch(typeof(HUDOptions_ToggleButton), "OnPointerClick")]
        static class ButtonClickPatch
        {
            static bool Prefix(HUDOptions_ToggleButton __instance, PointerEventData eventData)
            {
                if (__instance.GetComponent<NativeDefenseButton>() == null) return true;
                if (eventData.button == PointerEventData.InputButton.Left) Toggle();
                return false; // Do not write HUD filter settings or change weapon modes.
            }
        }
    }
}
