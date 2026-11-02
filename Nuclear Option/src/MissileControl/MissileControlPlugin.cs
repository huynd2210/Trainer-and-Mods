using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace NuclearOptionMissileControl
{
    [BepInPlugin(Id, "Manual Missile Control", "1.0.0")]
    public sealed class MissileControlPlugin : BaseUnityPlugin
    {
        internal const string Id = "nuclearoption.missilecontrol";
        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> StickTurnRate;
        internal static ConfigEntry<float> LookSensitivity;
        internal static ConfigEntry<float> MaxLeadDegrees;
        internal static ConfigEntry<float> CameraDistance;
        internal static ConfigEntry<float> ReturnDelay;
        internal static ConfigEntry<float> AimPointDistance;

        // Controlled is the missile being steered; Active stays true through the post-impact
        // pause until the view is back in the cockpit, and holds the aircraft's controls neutral.
        internal static Missile Controlled;
        internal static bool Active;
        internal static readonly AimState Aim = new AimState();
        static readonly MissileCamState CamState = new MissileCamState();
        static float lostAt = -1f;
        static bool finsChecked;
        static float lastError = -100f;

        void Awake()
        {
            Log = Logger;
            ToggleKey = Config.Bind("Controls", "ToggleKey", new KeyboardShortcut(KeyCode.F6),
                "Take control of your newest missile in flight; press again to hand it back to its own guidance.");
            StickTurnRate = Config.Bind("Controls", "StickTurnRateDegrees", 35f,
                new ConfigDescription("How fast full stick (Pitch, Roll/Yaw axes) swings the commanded direction, degrees per second.", new AcceptableValueRange<float>(5f, 180f)));
            LookSensitivity = Config.Bind("Controls", "LookSensitivity", 1f,
                new ConfigDescription("Multiplier on the game's view sensitivity for mouse/view-axis steering.", new AcceptableValueRange<float>(0.1f, 5f)));
            MaxLeadDegrees = Config.Bind("Controls", "MaxLeadDegrees", 40f,
                new ConfigDescription("Furthest the commanded direction may lead the missile's actual flight path.", new AcceptableValueRange<float>(5f, 90f)));
            CameraDistance = Config.Bind("Camera", "DistanceScale", 1f,
                new ConfigDescription("Chase camera distance relative to the default (3 missile lengths, at least 6 m).", new AcceptableValueRange<float>(0.3f, 10f)));
            ReturnDelay = Config.Bind("Camera", "ReturnDelaySeconds", 2f,
                new ConfigDescription("Seconds to watch the impact before the view returns to the cockpit.", new AcceptableValueRange<float>(0f, 10f)));
            AimPointDistance = Config.Bind("Guidance", "AimPointDistanceMetres", 10000f,
                new ConfigDescription("Distance ahead along the commanded direction given to the missile's autopilot as its aim point.", new AcceptableValueRange<float>(500f, 50000f)));
            new Harmony(Id).PatchAll(typeof(MissileControlPlugin).Assembly);
            Log.LogInfo("Manual Missile Control loaded. Toggle: " + ToggleKey.Value);
            // The game destroys the BepInEx host during startup, so the per-frame driver is attached to the camera.
        }

        static void Report(string text)
        {
            var report = SceneSingleton<AircraftActionsReport>.i;
            if (report != null) report.ReportText(text, 3f);
        }

        internal static void Toggle()
        {
            if (Active) { Release(); return; }
            if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft.disabled) return;
            Missile newest = null;
            bool remoteOnly = false;
            foreach (var unit in UnitRegistry.allUnits)
            {
                if (!(unit is Missile missile) || missile == null || missile.disabled || missile.ownerID != aircraft.persistentID) continue;
                if (!missile.LocalSim) { remoteOnly = true; continue; }
                if (newest == null || missile.timeSinceSpawn < newest.timeSinceSpawn) newest = missile;
            }
            if (newest == null)
            {
                Report(remoteOnly ? "Missile control: only the host can steer missiles" : "Missile control: no missile in flight");
                return;
            }
            TakeControl(newest);
        }

        static void TakeControl(Missile missile)
        {
            var cam = SceneSingleton<CameraStateManager>.i;
            if (cam == null) return;
            Controlled = missile;
            Active = true;
            finsChecked = false;
            lostAt = -1f;
            Vector3 velocity = missile.rb != null ? missile.rb.velocity : Vector3.zero;
            Aim.Set(velocity.sqrMagnitude > 2500f ? velocity : missile.transform.forward);
            cam.SetFollowingUnit(missile);
            cam.SwitchState(CamState);
            Log.LogInfo($"Controlling {missile.GetWeaponInfo()?.weaponName ?? missile.name} ({missile.GetSeekerType()}) at {missile.speed:F0} m/s");
        }

        // Hands the missile back to its seeker (which resumes its own guidance) and the view to the cockpit.
        internal static void Release()
        {
            Controlled = null;
            Active = false;
            lostAt = -1f;
            var cam = SceneSingleton<CameraStateManager>.i;
            if (cam != null && GameManager.GetLocalAircraft(out var aircraft) && !aircraft.disabled)
            {
                cam.SetFollowingUnit(aircraft);
                cam.SwitchState(cam.cockpitState);
            }
        }

        static void Reset()
        {
            Controlled = null;
            Active = false;
            lostAt = -1f;
        }

        internal static void Tick()
        {
            if (!InputFieldChecker.InsideInputField && ToggleKey.Value.IsDown()) Toggle();
            if (!Active) return;
            var cam = SceneSingleton<CameraStateManager>.i;
            if (Controlled != null && !Controlled.disabled)
            {
                // Anything else taking the view (map click, spectate, death) ends manual control.
                if (cam == null || cam.currentState != CamState) Reset();
                return;
            }
            if (lostAt < 0f)
            {
                lostAt = Time.unscaledTime;
                Controlled = null;
            }
            if (cam != null && cam.currentState != cam.freeState && cam.currentState != CamState) { Reset(); return; }
            if (Time.unscaledTime - lostAt >= ReturnDelay.Value) Release();
        }

        // Runs inside the missile's server-side FixedUpdate, after its seeker set its own aim point.
        static void Guide(Missile missile)
        {
            if (!missile.IsTangible() && missile.timeSinceSpawn > 0.5f) missile.SetTangible(true);
            if (!missile.IsArmed() && missile.timeSinceSpawn > 1f) missile.Arm();
            // Fins spawn at 10% area; seekers normally deploy them, but one that has lost its target may not.
            if (!finsChecked && missile.timeSinceSpawn > 1f)
            {
                finsChecked = true;
                if (CurrentFinArea(missile) < 0.5f * missile.GetFinArea()) missile.DeployFins();
            }
            // Point the nose past the command by the flight-path error, so the actual path (sagging under
            // gravity, lagging in turns) converges onto the crosshair instead of the nose.
            Vector3 command = Aim.Direction;
            Vector3 velocity = missile.rb.velocity;
            if (velocity.sqrMagnitude > 2500f) command = (2f * command - velocity.normalized).normalized;
            missile.SetAimpoint(missile.GlobalPosition() + command * AimPointDistance.Value, Vector3.zero);
        }

        static readonly AccessTools.FieldRef<Missile, float> CurrentFinArea = AccessTools.FieldRefAccess<Missile, float>("currentFinArea");

        static void LogThrottled(Exception error)
        {
            if (Time.unscaledTime - lastError < 5f) return;
            lastError = Time.unscaledTime;
            Log.LogError(error);
        }

        // Tiny per-scene driver for input and the overlay; the camera manager lives for the whole mission.
        sealed class Driver : MonoBehaviour
        {
            void Update()
            {
                try { Tick(); }
                catch (Exception error) { Reset(); LogThrottled(error); }
            }

            void OnGUI()
            {
                if (Event.current.type != EventType.Repaint || !Active) return;
                try { Overlay.Draw(Controlled, Aim.Direction); }
                catch (Exception error) { LogThrottled(error); }
            }

            void OnDestroy() => Reset();
        }

        [HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
        static class CameraPatch
        {
            static void Prefix(CameraStateManager __instance)
            {
                if (__instance.GetComponent<Driver>() == null) __instance.gameObject.AddComponent<Driver>();
            }
        }

        [HarmonyPatch(typeof(Missile), "Steering")]
        static class SteeringPatch
        {
            static void Prefix(Missile __instance)
            {
                if (__instance != Controlled || __instance == null) return;
                try { Guide(__instance); }
                catch (Exception error) { LogThrottled(error); }
            }
        }

        // Seekers self-destruct when they lose their target or think it was missed. Under manual control
        // only real impacts and the proximity fuse (all raised inside DetectCollisions) may detonate.
        [HarmonyPatch(typeof(Missile), "DetectCollisions")]
        static class CollisionScope
        {
            internal static bool Inside;
            static void Prefix() => Inside = true;
            static Exception Finalizer(Exception __exception) { Inside = false; return __exception; }
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate), typeof(Vector3), typeof(bool), typeof(bool))]
        static class DetonatePatch
        {
            static bool Prefix(Missile __instance, bool hitArmor, bool hitTerrain)
            {
                return __instance != Controlled || CollisionScope.Inside || hitArmor || hitTerrain;
            }
        }

        // The aircraft gets neutral stick and no weapon/gear/eject commands while you steer the missile.
        [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
        static class AxisPatch
        {
            static readonly AccessTools.FieldRef<PilotBaseState, ControlInputs> Inputs = AccessTools.FieldRefAccess<PilotBaseState, ControlInputs>("controlInputs");

            static bool Prefix(PilotPlayerState __instance)
            {
                if (!Active) return true;
                var inputs = Inputs(__instance);
                if (inputs != null) { inputs.pitch = 0f; inputs.roll = 0f; inputs.yaw = 0f; }
                return false;
            }
        }

        [HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
        static class ButtonPatch
        {
            static bool Prefix() => !Active;
        }
    }
}
