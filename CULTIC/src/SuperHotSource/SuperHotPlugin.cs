using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CulticSuperHot
{
    /// <summary>
    /// "SuperHot" time control for CULTIC.
    ///
    /// Game time only moves while the player is doing something (moving, firing,
    /// reloading, switching weapons, ...). As soon as they go still, the world
    /// slows to a near-stop. Looking around and aiming (right mouse button) are
    /// deliberately NOT actions: the player can line up a shot in a frozen scene
    /// for as long as they like before committing to a move.
    ///
    /// Enemy projectiles get an extra nerf on top of the time freeze:
    ///  - while frozen they travel far slower than everything else (an extra
    ///    multiplier on their speed), and
    ///  - when the player starts acting again they ramp back up to full speed
    ///    over a grace period instead of snapping straight back, so the player
    ///    has a window to actually dodge.
    ///
    /// Implementation notes:
    ///  - The game (scrGameControl.Update) already lerps Time.timeScale toward a
    ///    target and compensates audio pitch + fixedDeltaTime itself. We drive
    ///    Time.timeScale in LateUpdate (which always runs after the game's Update),
    ///    so our value wins each frame and the game's own lerp never re-applies.
    ///  - We only act while in real gameplay (gameState == 0) and offline, and we
    ///    back off when the game owns time: pause menus / cutscenes / death /
    ///    level-end (gameState != 0), the weapon wheel (native slow-mo), and
    ///    while the player is dead.
    ///  - The projectile factor is a static that Harmony patches on the enemy
    ///    projectile classes read once per frame (set in LateUpdate). It is 1
    ///    whenever the mod is off or not in applicable gameplay, so the patches
    ///    are no-ops then.
    ///  - Time.timeScale is never set to exactly 0 so game code that divides by
    ///    Time.deltaTime never hits a divide-by-zero; 0.01-0.02 reads as frozen.
    /// </summary>
    [BepInPlugin("local.codex.culticsuperhot", "CULTIC SuperHot", "1.3.2")]
    public sealed class SuperHotPlugin : BaseUnityPlugin
    {
        // A real key press has to cross one frame boundary before the game's own
        // Update sees it and before our LateUpdate can act on it, so a tiny floor
        // avoids timeScale jitter around the thresholds.
        private const float MoveThreshold = 0.05f;

        // The rate scrPlayerControl.Update lerps the camera transform toward camRot
        // with: 0.85f * (Time.deltaTime * 60f) * turnPenalty.
        private const float CameraLerpRate = 0.85f;

        /// <summary>Current enemy-projectile speed multiplier (1 = no effect). Patches read this.</summary>
        public static float EnemyProjectileFactor = 1f;

        /// <summary>While true (set by the Replay mod during playback) this mod fully
        /// disengages: no time control, no toggle handling. The freshly loaded
        /// replay scene looks like real gameplay (gameState == 0), so without this
        /// the mod would freeze time whenever the viewer pressed a movement key.</summary>
        public static bool StandDown;

        /// <summary>True while this mod is the thing holding time down, so the camera
        /// catch-up patch knows the slowdown is ours to compensate for.</summary>
        private static bool compensateCameraLag;

        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<float> frozenScale;
        private ConfigEntry<float> actionGrace;
        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<float> projectileSlowFactor;
        private ConfigEntry<float> projectileRampUpTime;

        private bool modEnabled;
        private float lastActionTime = float.NegativeInfinity;
        private float projectileFactor = 1f;
        private string statusText = "";
        private float statusUntil;
        private Harmony harmony;

        // Actions that keep time flowing while held (not just on the press frame).
        // NOTE: "Alt Fire" (right mouse button / aim) is deliberately NOT here -
        // aiming is free, see NonActionNames.
        private static readonly string[] HeldActionNames =
        {
            "Fire", "Tertiary Fire", "Reload", "Interact",
            "Jump", "Crouch", "Run", "Run Toggle", "Slide", "Quick TNT",
            "Kick", "Use Ability (Ability Mode)", "Weapon Wheel"
        };

        // Actions that do not advance time at all - neither while held nor on the
        // press frame. Looking around and aiming are free: the player can study a
        // frozen scene for as long as they like before committing to a move.
        //   "Look" / "Mouse Look"  - the mouse and gamepad look axes; a Vector2
        //                            look action crosses the press point on any
        //                            mouse movement, which would otherwise keep
        //                            time running whenever the player turned
        //   "Gyro Button"          - only gates gyro aiming (scrGameControl.gyroState)
        //   "Alt Fire"             - right mouse button, the aim/ADS action
        //   "Swap Alternate"       - also bound to <Mouse>/rightButton in the
        //                            inventory-side map, which is enabled during
        //                            normal play; without this entry a right
        //                            click would still trip the press sweep and
        //                            wake time through the grace window
        private static readonly HashSet<string> NonActionNames =
            new HashSet<string> { "Look", "Mouse Look", "Gyro Button", "Alt Fire", "Swap Alternate" };

        private readonly Dictionary<string, InputAction> actionCache = new Dictionary<string, InputAction>();

        private void Awake()
        {
            toggleKey = Config.Bind("Hotkeys", "ToggleSuperHot",
                new KeyboardShortcut(KeyCode.RightBracket),
                "Toggle SuperHot time control.");
            frozenScale = Config.Bind("General", "FrozenTimeScale", 0.02f,
                new ConfigDescription(
                    "Time scale while idle. 0.02 reads as frozen; 0 is a hard freeze " +
                    "but risks divide-by-zero in game code.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)));
            actionGrace = Config.Bind("General", "ActionGrace", 0.35f,
                new ConfigDescription(
                    "Seconds of real time that keep flowing after a button press, " +
                    "so single-shot actions (weapon switch, interact, jump) play out.",
                    new AcceptableValueRange<float>(0.05f, 2f)));
            enabledOnStart = Config.Bind("General", "EnabledOnStart", true,
                "Whether SuperHot is on when the game starts.");
            projectileSlowFactor = Config.Bind("Projectiles", "SlowFactor", 0.15f,
                new ConfigDescription(
                    "Extra speed multiplier applied to enemy projectiles while time " +
                    "is frozen (on top of the global freeze). Lower = slower.",
                    new AcceptableValueRange<float>(0.01f, 1f)));
            projectileRampUpTime = Config.Bind("Projectiles", "RampUpTime", 1.5f,
                new ConfigDescription(
                    "Seconds of real time for enemy projectiles to ramp back up to " +
                    "full speed once the player starts acting again. Gives a dodge window.",
                    new AcceptableValueRange<float>(0.1f, 5f)));

            modEnabled = enabledOnStart.Value;
            harmony = new Harmony("local.codex.culticsuperhot");
            harmony.PatchAll();
            Logger.LogInfo("CULTIC SuperHot loaded. " + toggleKey.Value + " toggles. Frozen time scale: " + frozenScale.Value);
        }

        private void OnDestroy()
        {
            Disengage();
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
        }

        private void Update()
        {
            if (StandDown)
            {
                return;
            }
            if (toggleKey.Value.IsDown())
            {
                Toggle();
            }
        }

        private void LateUpdate()
        {
            if (StandDown)
            {
                Disengage();
                return;
            }
            if (!modEnabled)
            {
                Disengage();
                return;
            }

            scrGameControl game = scrGameControl.Instance;
            if (game == null)
            {
                Disengage();
                return;
            }

            // The game owns time in every non-gameplay state (pause menu, shop,
            // note/readable, cutscene, death, level end, load).
            if (game.gameState != 0)
            {
                Disengage();
                return;
            }

            // Only local play. Freezing time in online play would desync everyone.
            if (game.connectionStatus != scrGameControl.ConnectionStatus.Offline)
            {
                Disengage();
                return;
            }

            scrPlayerHUD hud = game.hudScript;
            if (hud == null)
            {
                Disengage();
                return;
            }

            // The weapon wheel has its own native slow-mo (wepWheelTimeSpeed).
            // Let it play rather than fighting it.
            if (hud.wepWheelState != 0)
            {
                Disengage();
                return;
            }

            scrPlayerControl player = FindLocalPlayerControl(game);
            if (player == null || player.isDead)
            {
                Disengage();
                return;
            }

            bool acting = IsPlayerActing(game, player, hud);
            Time.timeScale = acting ? 1f : frozenScale.Value;

            // Enemy projectile nerf: drop to the extra-slow factor while idle;
            // ramp back to full speed over a real-time grace period while acting.
            if (acting)
            {
                projectileFactor += Time.unscaledDeltaTime / projectileRampUpTime.Value;
                if (projectileFactor > 1f)
                {
                    projectileFactor = 1f;
                }
            }
            else
            {
                projectileFactor = projectileSlowFactor.Value;
            }

            EnemyProjectileFactor = projectileFactor;

            // We are the ones holding time down, so the camera catch-up patch may
            // undo the smoothing lag our own slowdown causes. (Time.timeScale set
            // here lands in the next frame's Time.deltaTime, which is the frame the
            // patch reads - the two stay in step.)
            compensateCameraLag = true;
        }

        /// <summary>Hand time (and the camera) back to the game.</summary>
        private static void Disengage()
        {
            EnemyProjectileFactor = 1f;
            compensateCameraLag = false;
        }

        private bool IsPlayerActing(scrGameControl game, scrPlayerControl player, scrPlayerHUD hud)
        {
            // Movement (keyboard / gamepad).
            if (game.moveInput.magnitude > MoveThreshold)
            {
                return true;
            }

            // Looking around and aiming are NOT actions - holding right mouse to
            // aim keeps the world frozen; only committing to a move restarts it.
            // (This is why game.lookInput / lookInputRaw are deliberately not
            // consulted, and why "Alt Fire" is not in HeldActionNames.)

            // Held gameplay actions.
            foreach (string name in HeldActionNames)
            {
                InputAction action = GetAction(game, name);
                if (action != null && action.IsPressed())
                {
                    return true;
                }
            }

            // Reload is a press-and-release action; keep time flowing for its whole
            // duration so it actually completes.
            if (player.isReloading)
            {
                return true;
            }

            // The off-hand / item wheel has no native slow-mo; keep it usable.
            if (hud.invWheelState != 0)
            {
                return true;
            }

            // Any action pressed this frame (Gameplay, Inventory and UI maps are
            // all enabled in-game, so this also catches weapon select, menu/map,
            // quicksave, etc.).
            if (WasAnyActionPressed(game))
            {
                lastActionTime = Time.unscaledTime;
                return true;
            }

            // Grace window so single-shot presses play out.
            return Time.unscaledTime - lastActionTime < actionGrace.Value;
        }

        private InputAction GetAction(scrGameControl game, string name)
        {
            InputAction cached;
            if (actionCache.TryGetValue(name, out cached))
            {
                return cached;
            }

            InputActionAsset asset = game.playerControls;
            if (asset == null)
            {
                return null;
            }

            InputAction action = asset.FindAction(name, false);
            actionCache[name] = action;
            return action;
        }

        private static bool WasAnyActionPressed(scrGameControl game)
        {
            InputActionAsset asset = game.playerControls;
            if (asset == null)
            {
                return false;
            }

            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    if (action == null || NonActionNames.Contains(action.name))
                    {
                        continue;
                    }

                    if (action.WasPressedThisFrame())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static scrPlayerControl FindLocalPlayerControl(scrGameControl game)
        {
            if (game.localPlayerID >= 0)
            {
                try
                {
                    GameObject playerObject = game.getPlayerObject(game.localPlayerID);
                    if (playerObject != null)
                    {
                        scrPlayerControl player = playerObject.GetComponent<scrPlayerControl>();
                        if (player != null)
                        {
                            return player;
                        }
                    }
                }
                catch
                {
                }

                if (game.gamePlayers != null && game.localPlayerID < game.gamePlayers.Length)
                {
                    scrPlayer data = game.gamePlayers[game.localPlayerID];
                    if (data != null && data.playerObject != null)
                    {
                        scrPlayerControl player = data.playerObject.GetComponent<scrPlayerControl>();
                        if (player != null)
                        {
                            return player;
                        }
                    }
                }
            }

            return Object.FindObjectOfType<scrPlayerControl>();
        }

        private void Toggle()
        {
            modEnabled = !modEnabled;
            if (!modEnabled)
            {
                Disengage();
            }
            ShowStatus("SuperHot: " + (modEnabled ? "ON" : "OFF"));
        }

        private void ShowStatus(string text)
        {
            statusText = "[SuperHot] " + text;
            statusUntil = Time.unscaledTime + 2.5f;
            Logger.LogInfo(text);
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > statusUntil || string.IsNullOrEmpty(statusText))
            {
                return;
            }

            GUI.Label(new Rect(18f, 18f, 520f, 32f), statusText);
        }

        // ----------------------------------------------------------------------
        // Enemy projectile patches.
        //
        // Three patch shapes, chosen to match each projectile's movement:
        //  1. Static speed field (scrBullet, scrArchonEnergyWave): scale the field
        //     for the duration of Update and restore it after, so movement AND the
        //     per-frame collision sweep stay consistent with each other.
        //  2. Dynamic speed / homing (scrHeliRocket, scrArchonBloodBall): speed is
        //     recomputed inside Update, so instead rescale the position delta after
        //     the fact (the collision sweep may overshoot by one frame's worth of
        //     movement while slowed - a minor, acceptable quirk).
        //  3. Physics-driven (scrInfectedSpit, scrVomitParticle, scrIncineratorParticle):
        //     scale the rigidbody velocity after Update; movement and the position
        //     based collision checks stay consistent.
        // All classes are enemy-owned, so no ownership filter is needed.
        // ----------------------------------------------------------------------

        // ----------------------------------------------------------------------
        // Camera catch-up.
        //
        // scrPlayerControl.Update accumulates camRot straight from raw look input
        // (no deltaTime anywhere), but then eases the camera transform onto it:
        //
        //   rotation = Quaternion.Lerp(rotation, Euler(camRot ...),
        //                              0.85f * (Time.deltaTime * 60f) * turnPenalty)
        //
        // That easing IS time-scaled. At a 0.02 time scale the per-frame step drops
        // from 85% to 1.7%, so the aim point snaps to where you pointed while the
        // visible camera crawls after it for a second or more. Turning stops feeling
        // like turning and starts feeling like drifting - which is exactly the
        // motion sickness complaint. It never showed before because looking counted
        // as acting, so time was always 1.0 whenever the mouse moved.
        //
        // The fix re-applies that same easing step at the unscaled rate. The target
        // is built from locals we cannot see, but we do not need it: capture the
        // rotation before Update and after, and the game's own step tells us the
        // direction. Scaling that step by (wanted rate / applied rate) lands exactly
        // where an unscaled frame would have. Working in Euler angles rather than
        // extrapolating the quaternion keeps it well conditioned on big flicks;
        // pitch is clamped to +-80 degrees in game, so gimbal lock is not reachable.
        //
        // If the game did not move the camera this frame (dead, spectating, a
        // cutscene) the step is zero and this is a no-op.
        // ----------------------------------------------------------------------

        private struct CameraLagState
        {
            public bool Captured;
            public Quaternion Rotation;
        }

        [HarmonyPatch(typeof(scrPlayerControl), "Update")]
        private static class CameraCatchUpPatch
        {
            private static void Prefix(scrPlayerControl __instance, out CameraLagState __state)
            {
                __state = default(CameraLagState);
                if (!compensateCameraLag || __instance.playerCamera == null)
                {
                    return;
                }

                __state.Captured = true;
                __state.Rotation = __instance.playerCamera.transform.rotation;
            }

            private static void Postfix(scrPlayerControl __instance, CameraLagState __state)
            {
                if (!__state.Captured || __instance.playerCamera == null)
                {
                    return;
                }

                float scaledStep = Time.deltaTime * 60f;
                float unscaledStep = Time.unscaledDeltaTime * 60f;
                if (unscaledStep <= scaledStep)
                {
                    // Time is running normally - nothing to undo.
                    return;
                }

                float turnPenalty = __instance.turnPenalty;
                float applied = CameraLerpRate * scaledStep * turnPenalty;
                if (applied <= 0.0001f)
                {
                    return;
                }

                // Clamped to 1 so the camera can never overshoot where it was headed.
                float wanted = Mathf.Min(CameraLerpRate * unscaledStep * turnPenalty, 1f);
                float rescale = wanted / applied;

                Transform cameraTransform = __instance.playerCamera.transform;
                Vector3 before = __state.Rotation.eulerAngles;
                Vector3 after = cameraTransform.eulerAngles;

                cameraTransform.rotation = Quaternion.Euler(
                    before.x + Mathf.DeltaAngle(before.x, after.x) * rescale,
                    before.y + Mathf.DeltaAngle(before.y, after.y) * rescale,
                    before.z + Mathf.DeltaAngle(before.z, after.z) * rescale);
            }
        }

        private static float Factor()
        {
            return EnemyProjectileFactor;
        }

        private static float SafeDivide(float v, float f)
        {
            return (f > 0f && f < 1f) ? v / f : v;
        }

        // ---- 1. scrBullet ----
        [HarmonyPatch(typeof(scrBullet), "Update")]
        private static class BulletPatch
        {
            private static void Prefix(scrBullet __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    __instance.travelSpeed *= f;
                }
            }

            private static void Postfix(scrBullet __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    __instance.travelSpeed = SafeDivide(__instance.travelSpeed, f);
                }
            }
        }

        // ---- 1. scrArchonEnergyWave ----
        [HarmonyPatch(typeof(scrArchonEnergyWave), "Update")]
        private static class EnergyWavePatch
        {
            private static void Prefix(scrArchonEnergyWave __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    __instance.travelSpeed *= f;
                }
            }

            private static void Postfix(scrArchonEnergyWave __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    __instance.travelSpeed = SafeDivide(__instance.travelSpeed, f);
                }
            }
        }

        // ---- 2. scrHeliRocket ----
        [HarmonyPatch(typeof(scrHeliRocket), "Update")]
        private static class HeliRocketPatch
        {
            private static Vector3 startPos;

            private static void Prefix(scrHeliRocket __instance)
            {
                startPos = __instance.transform.position;
            }

            private static void Postfix(scrHeliRocket __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    Vector3 delta = __instance.transform.position - startPos;
                    __instance.transform.position = startPos + delta * f;
                }
            }
        }

        // ---- 2. scrArchonBloodBall ----
        [HarmonyPatch(typeof(scrArchonBloodBall), "Update")]
        private static class BloodBallPatch
        {
            private static Vector3 startPos;

            private static void Prefix(scrArchonBloodBall __instance)
            {
                startPos = __instance.transform.position;
            }

            private static void Postfix(scrArchonBloodBall __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    Vector3 delta = __instance.transform.position - startPos;
                    __instance.transform.position = startPos + delta * f;
                }
            }
        }

        // ---- 3. Physics projectiles ----
        [HarmonyPatch(typeof(scrInfectedSpit), "Update")]
        private static class InfectedSpitPatch
        {
            private static void Postfix(scrInfectedSpit __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    Rigidbody rb = __instance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity *= f;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(scrVomitParticle), "Update")]
        private static class VomitParticlePatch
        {
            private static void Postfix(scrVomitParticle __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    Rigidbody rb = __instance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity *= f;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(scrIncineratorParticle), "Update")]
        private static class IncineratorParticlePatch
        {
            private static void Postfix(scrIncineratorParticle __instance)
            {
                float f = Factor();
                if (f < 1f)
                {
                    Rigidbody rb = __instance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity *= f;
                    }
                }
            }
        }
    }
}
