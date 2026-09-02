using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CulticGodMode
{
    [BepInPlugin("local.codex.culticgodmode", "CULTIC God Mode", "1.0.1")]
    public sealed class GodModePlugin : BaseUnityPlugin
    {
        private const float CameraLerpRate = 0.85f;

        private static GodModePlugin instance;
        private static readonly FieldInfo IsGodField = typeof(scrPlayerControl).GetField(
            "isGod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<bool> showHud;
        private ConfigEntry<float> flightSpeed;
        private ConfigEntry<float> flightSprintMultiplier;
        private ConfigEntry<float> freezeScale;
        private ConfigEntry<float> speedMultiplier;

        private ConfigEntry<KeyboardShortcut> masterKey;
        private ConfigEntry<KeyboardShortcut> flightKey;
        private ConfigEntry<KeyboardShortcut> freezeKey;
        private ConfigEntry<KeyboardShortcut> speedKey;
        private ConfigEntry<KeyboardShortcut> restockKey;
        private ConfigEntry<KeyboardShortcut> ascendKey;
        private ConfigEntry<KeyboardShortcut> descendKey;

        private bool masterEnabled;
        private bool flightEnabled;
        private bool freezeEnabled;
        private bool speedEnabled;
        private bool clockOwned;

        private scrPlayerControl flightPlayer;
        private int savedPlayerState;
        private bool savedColliderEnabled;
        private bool savedIsKinematic;
        private bool savedUseGravity;

        private scrPlayerControl godPlayer;
        private bool savedGodValue;

        private string statusText = "";
        private float statusUntil;
        private Harmony harmony;

        private void Awake()
        {
            instance = this;

            enabledOnStart = Config.Bind("General", "EnabledOnStart", false,
                "Start with the core God Mode features enabled.");
            showHud = Config.Bind("General", "ShowHud", true,
                "Show a compact persistent mode readout while God Mode is enabled.");

            flightSpeed = Config.Bind("Flight", "Speed", 14f,
                new ConfigDescription("Base no-clip flight speed in world units per real second.",
                    new AcceptableValueRange<float>(1f, 100f)));
            flightSprintMultiplier = Config.Bind("Flight", "SprintMultiplier", 3f,
                new ConfigDescription("Flight multiplier while the normal Run control is held.",
                    new AcceptableValueRange<float>(1f, 10f)));
            freezeScale = Config.Bind("Time", "FrozenTimeScale", 0.01f,
                new ConfigDescription(
                    "World time scale while frozen. A tiny non-zero value avoids breaking game code that divides by delta time.",
                    new AcceptableValueRange<float>(0.001f, 0.1f)));
            speedMultiplier = Config.Bind("Speed", "Multiplier", 3f,
                new ConfigDescription("Walk and run speed multiplier.",
                    new AcceptableValueRange<float>(1f, 10f)));

            masterKey = Config.Bind("Hotkeys", "ToggleGodMode", new KeyboardShortcut(KeyCode.Insert),
                "Toggle the whole developer-style God Mode suite.");
            flightKey = Config.Bind("Hotkeys", "ToggleFlight", new KeyboardShortcut(KeyCode.F10),
                "Toggle flight/no-clip. Enabling a sub-feature also enables God Mode.");
            freezeKey = Config.Bind("Hotkeys", "ToggleFreeze", new KeyboardShortcut(KeyCode.F11),
                "Freeze or unfreeze the world.");
            speedKey = Config.Bind("Hotkeys", "ToggleSuperSpeed", new KeyboardShortcut(KeyCode.F12),
                "Toggle super speed.");
            restockKey = Config.Bind("Hotkeys", "Restock", new KeyboardShortcut(KeyCode.Home),
                "Refill health, armor, ability energy, ammo, and weapon magazines.");
            ascendKey = Config.Bind("Flight", "Ascend", new KeyboardShortcut(KeyCode.Space),
                "Ascend while flying.");
            descendKey = Config.Bind("Flight", "Descend", new KeyboardShortcut(KeyCode.LeftControl),
                "Descend while flying.");

            masterEnabled = enabledOnStart.Value;
            harmony = new Harmony("local.codex.culticgodmode");
            harmony.PatchAll();
            StartCoroutine(EndOfFrameClockLoop());

            Logger.LogInfo(
                "CULTIC God Mode loaded. Insert master, F10 flight, F11 freeze, F12 speed, Home restock.");
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            SetMaster(false, false);
            ReleaseClock();
            RestoreFlight();
            RestoreGodState();
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
            instance = null;
        }

        private void Update()
        {
            if (ShortcutDown(masterKey.Value))
            {
                SetMaster(!masterEnabled, true);
            }

            if (ShortcutDown(flightKey.Value))
            {
                EnableMasterForFeature();
                flightEnabled = !flightEnabled;
                if (!flightEnabled)
                {
                    RestoreFlight();
                }
                ShowStatus("Flight/no-clip: " + OnOff(flightEnabled));
            }

            if (ShortcutDown(freezeKey.Value))
            {
                EnableMasterForFeature();
                freezeEnabled = !freezeEnabled;
                if (!freezeEnabled)
                {
                    ReleaseClock();
                }
                ShowStatus("World freeze: " + OnOff(freezeEnabled));
            }

            if (ShortcutDown(speedKey.Value))
            {
                EnableMasterForFeature();
                speedEnabled = !speedEnabled;
                ShowStatus("Super speed: " + OnOff(speedEnabled));
            }

            if (ShortcutDown(restockKey.Value))
            {
                EnableMasterForFeature();
                ShowStatus(Restock());
            }

            if (!masterEnabled || !IsOfflineGameplay())
            {
                RestoreFlight();
                RestoreGodState();
                if (!IsOfflineGameplay())
                {
                    ReleaseClock();
                }
                return;
            }

            scrPlayerControl player = FindLocalPlayerControl();
            if (player == null || player.isDead)
            {
                RestoreFlight();
                RestoreGodState();
                return;
            }

            ApplyGodState(player);
            MaintainDeveloperResources(player);

            if (flightEnabled)
            {
                if (flightPlayer != player)
                {
                    RestoreFlight();
                    BeginFlight(player);
                }
                MaintainFlightState(player);
            }
            else
            {
                RestoreFlight();
            }
        }

        private void LateUpdate()
        {
            if (!masterEnabled || !flightEnabled || !IsOfflineGameplay())
            {
                return;
            }

            scrPlayerControl player = FindLocalPlayerControl();
            if (player == null || player != flightPlayer || player.isDead)
            {
                return;
            }

            scrGameControl game = scrGameControl.Instance;
            if (game == null || player.playerCamera == null)
            {
                return;
            }

            float vertical = 0f;
            if (ascendKey.Value.IsPressed())
            {
                vertical += 1f;
            }
            if (descendKey.Value.IsPressed())
            {
                vertical -= 1f;
            }

            Transform cameraTransform = player.playerCamera.transform;
            Vector3 direction = cameraTransform.forward * game.moveInput.y +
                cameraTransform.right * game.moveInput.x + Vector3.up * vertical;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            float speed = flightSpeed.Value;
            try
            {
                if (game.buttonIsDown("Run"))
                {
                    speed *= flightSprintMultiplier.Value;
                }
            }
            catch
            {
            }

            Vector3 delta = direction * speed * Time.unscaledDeltaTime;
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 newPosition = player.transform.position + delta;
            player.transform.position = newPosition;
            if (player.rigidBody != null)
            {
                player.rigidBody.position = newPosition;
            }
            if (player.playerCameraParent != null)
            {
                player.playerCameraParent.transform.position += delta;
            }
        }

        private IEnumerator EndOfFrameClockLoop()
        {
            WaitForEndOfFrame wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                if (masterEnabled && freezeEnabled && IsOfflineGameplay())
                {
                    Time.timeScale = freezeScale.Value;
                    clockOwned = true;
                }
                else
                {
                    ReleaseClock();
                }
            }
        }

        private void SetMaster(bool enabled, bool notify)
        {
            masterEnabled = enabled;
            if (!enabled)
            {
                flightEnabled = false;
                freezeEnabled = false;
                speedEnabled = false;
                RestoreFlight();
                RestoreGodState();
                ReleaseClock();
            }

            if (notify)
            {
                ShowStatus("God Mode: " + OnOff(enabled));
            }
        }

        private void EnableMasterForFeature()
        {
            if (!masterEnabled)
            {
                masterEnabled = true;
            }
        }

        private void BeginFlight(scrPlayerControl player)
        {
            if (player == null)
            {
                return;
            }

            flightPlayer = player;
            savedPlayerState = player.state;
            savedColliderEnabled = player.myCollider != null && player.myCollider.enabled;
            savedIsKinematic = player.rigidBody != null && player.rigidBody.isKinematic;
            savedUseGravity = player.rigidBody == null || player.rigidBody.useGravity;
            MaintainFlightState(player);
        }

        private static void MaintainFlightState(scrPlayerControl player)
        {
            player.state = 69;
            player.fallSpeed = 0f;
            player.knockSpeed = Vector3.zero;
            player.baseMoveSpeed = Vector3.zero;
            player.moveSpeed = Vector2.zero;
            player.isGrounded = false;
            if (player.myCollider != null)
            {
                player.myCollider.enabled = false;
            }
            if (player.rigidBody != null)
            {
                player.rigidBody.linearVelocity = Vector3.zero;
                player.rigidBody.angularVelocity = Vector3.zero;
                player.rigidBody.useGravity = false;
                player.rigidBody.isKinematic = true;
            }
        }

        private void RestoreFlight()
        {
            if (flightPlayer == null)
            {
                flightPlayer = null;
                return;
            }

            if (flightPlayer.state == 69)
            {
                flightPlayer.state = savedPlayerState;
            }
            if (flightPlayer.myCollider != null)
            {
                flightPlayer.myCollider.enabled = savedColliderEnabled;
            }
            if (flightPlayer.rigidBody != null)
            {
                flightPlayer.rigidBody.isKinematic = savedIsKinematic;
                flightPlayer.rigidBody.useGravity = savedUseGravity;
                flightPlayer.rigidBody.linearVelocity = Vector3.zero;
                flightPlayer.rigidBody.angularVelocity = Vector3.zero;
            }
            flightPlayer.fallSpeed = 0f;
            flightPlayer.knockSpeed = Vector3.zero;
            flightPlayer = null;
        }

        private void ApplyGodState(scrPlayerControl player)
        {
            if (IsGodField == null)
            {
                return;
            }

            if (godPlayer != player)
            {
                RestoreGodState();
                godPlayer = player;
                savedGodValue = (bool)IsGodField.GetValue(player);
            }
            IsGodField.SetValue(player, true);
        }

        private void RestoreGodState()
        {
            if (godPlayer != null && IsGodField != null)
            {
                IsGodField.SetValue(godPlayer, savedGodValue);
            }
            godPlayer = null;
        }

        private static void MaintainDeveloperResources(scrPlayerControl player)
        {
            player.stamina = 150f;
            scrGameControl game = scrGameControl.Instance;
            if (game == null || game.gamePlayers == null || player.playerID < 0 ||
                player.playerID >= game.gamePlayers.Length)
            {
                return;
            }

            scrPlayer data = game.gamePlayers[player.playerID];
            if (data != null && data.playerMaxEnergy > 0)
            {
                data.playerEnergy = data.playerMaxEnergy;
            }
        }

        private string Restock()
        {
            scrGameControl game = scrGameControl.Instance;
            scrPlayerControl control = FindLocalPlayerControl();
            if (game == null || control == null || game.gamePlayers == null ||
                control.playerID < 0 || control.playerID >= game.gamePlayers.Length)
            {
                return "Restock unavailable outside gameplay";
            }

            scrPlayer player = game.gamePlayers[control.playerID];
            if (player == null)
            {
                return "Restock unavailable";
            }

            player.playerHP = GetMaxHealth(game, player);
            if (player.playerMaxEnergy > 0)
            {
                player.playerEnergy = player.playerMaxEnergy;
            }

            if (player.playerArmor != null)
            {
                for (int i = 0; i < player.playerArmor.Length; i++)
                {
                    player.playerArmor[i] = 100;
                    if (game.hudScript != null)
                    {
                        game.hudScript.flashArmorWheel(i);
                    }
                }
            }

            if (player.ammo != null && player.maxAmmo != null)
            {
                int count = Math.Min(player.ammo.Length, player.maxAmmo.Length);
                for (int i = 0; i < count; i++)
                {
                    player.ammo[i] = Math.Max(player.ammo[i], player.maxAmmo[i]);
                }
            }

            if (player.playerLoadout != null)
            {
                foreach (scrWeapon weapon in player.playerLoadout)
                {
                    if (weapon == null)
                    {
                        continue;
                    }
                    if (weapon.weaponCanReload)
                    {
                        weapon.weaponCurrentAmmo = GetWeaponCapacity(weapon, player);
                    }
                    if (weapon.weaponChargeRate != 0f)
                    {
                        weapon.weaponCharge = 100f;
                    }
                }
            }

            if (control.tempWeapon != null)
            {
                control.tempWeapon.ammo = 999;
            }
            if (control.shieldHP > 0)
            {
                control.shieldHP = Math.Max(control.shieldHP, scrDroppedShield.shieldMaxHP);
                player.playerShieldHP = Math.Max(player.playerShieldHP, control.shieldHP);
            }

            if (game.hudScript != null)
            {
                game.hudScript.inventoryUpdate();
                game.hudScript.refreshAmmo(control.weapon);
            }
            return "Health, armor, energy, and ammo restocked";
        }

        private static int GetMaxHealth(scrGameControl game, scrPlayer player)
        {
            int maxHealth = player.playerMaxHP;
            try
            {
                if (player.playerUpgrades != null)
                {
                    maxHealth = Math.Max(maxHealth,
                        game.diffPlayerStartingHP + player.playerUpgrades[0, 0] * game.hpUpgradeValue);
                }
            }
            catch
            {
                maxHealth = Math.Max(maxHealth, game.diffPlayerStartingHP);
            }
            return Mathf.Clamp(Math.Max(maxHealth, 100), 1, 999);
        }

        private static int GetWeaponCapacity(scrWeapon weapon, scrPlayer player)
        {
            int capacity = weapon.weaponCapacity;
            if (weapon.weaponCapacityIncrease > 0 && weapon.weaponCapacityIndex >= 0 &&
                player.playerUpgrades != null && weapon.weaponUpgradeIndex >= 0 &&
                weapon.weaponUpgradeIndex < player.playerUpgrades.GetLength(0) &&
                weapon.weaponCapacityIndex < player.playerUpgrades.GetLength(1))
            {
                capacity += weapon.weaponCapacityIncrease *
                    player.playerUpgrades[weapon.weaponUpgradeIndex, weapon.weaponCapacityIndex];
            }
            return Mathf.Clamp(capacity, 0, 999);
        }

        private void ReleaseClock()
        {
            if (!clockOwned)
            {
                return;
            }

            scrGameControl game = scrGameControl.Instance;
            if (game != null && game.gameState == 0)
            {
                Time.timeScale = 1f;
            }
            clockOwned = false;
        }

        private static bool IsOfflineGameplay()
        {
            scrGameControl game = scrGameControl.Instance;
            return game != null && game.gameState == 0 &&
                game.connectionStatus == scrGameControl.ConnectionStatus.Offline;
        }

        private static bool FeatureActive()
        {
            return instance != null && instance.masterEnabled && IsOfflineGameplay();
        }

        private static bool FlightActiveFor(scrPlayerControl player)
        {
            return instance != null && instance.masterEnabled && instance.flightEnabled &&
                instance.flightPlayer == player && IsOfflineGameplay();
        }

        private static bool SpeedActiveFor(scrPlayerControl player)
        {
            if (instance == null || !instance.masterEnabled || !instance.speedEnabled ||
                !IsOfflineGameplay() || player == null)
            {
                return false;
            }
            scrGameControl game = scrGameControl.Instance;
            return game != null && player.playerID == game.localPlayerID;
        }

        private static bool FreezeCameraActiveFor(scrPlayerControl player)
        {
            if (instance == null || !instance.masterEnabled || !instance.freezeEnabled ||
                !IsOfflineGameplay() || player == null)
            {
                return false;
            }
            scrGameControl game = scrGameControl.Instance;
            return game != null && player.playerID == game.localPlayerID;
        }

        private static scrPlayerControl FindLocalPlayerControl()
        {
            scrGameControl game = scrGameControl.Instance;
            if (game != null && game.localPlayerID >= 0)
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
            }
            return UnityEngine.Object.FindFirstObjectByType<scrPlayerControl>();
        }

        private static string OnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }

        private static bool ShortcutDown(KeyboardShortcut shortcut)
        {
            try
            {
                return shortcut.IsDown();
            }
            catch (NullReferenceException)
            {
                // Unity batch/headless startup has no keyboard device. Normal
                // interactive play initializes it before these calls.
                return false;
            }
        }

        private void ShowStatus(string text)
        {
            statusText = "[God Mode] " + text;
            statusUntil = Time.unscaledTime + 3f;
            Logger.LogInfo(text);
        }

        private void OnGUI()
        {
            bool showStatus = !string.IsNullOrEmpty(statusText) && Time.unscaledTime <= statusUntil;
            bool showPanel = masterEnabled && showHud.Value;
            if (!showStatus && !showPanel)
            {
                return;
            }

            float width = 390f;
            float x = Mathf.Max(12f, Screen.width - width - 18f);
            if (showPanel)
            {
                string panel = "GOD MODE  [Insert]  |  keys ignored\n" +
                    "Flight [F10]: " + OnOff(flightEnabled) +
                    "   Freeze [F11]: " + OnOff(freezeEnabled) +
                    "   Speed [F12]: " + OnOff(speedEnabled) + "\n" +
                    (flightEnabled ? "Fly: movement + Space/Ctrl; hold Run to sprint\n" : "") +
                    "Restock: Home";
                GUI.Box(new Rect(x, 18f, width, flightEnabled ? 86f : 68f), panel);
            }
            if (showStatus)
            {
                GUI.Box(new Rect(x, showPanel ? 112f : 18f, width, 30f), statusText);
            }
        }

        [HarmonyPatch(typeof(scrPlayerControl), "takeDamage")]
        private static class DamagePatch
        {
            private static bool Prefix(scrPlayerControl __instance)
            {
                if (!FeatureActive())
                {
                    return true;
                }
                scrGameControl game = scrGameControl.Instance;
                return game == null || __instance == null ||
                    __instance.playerID != game.localPlayerID;
            }
        }

        [HarmonyPatch(typeof(scrPlayerControl), "FixedUpdate")]
        private static class FlightPhysicsPatch
        {
            private static bool Prefix(scrPlayerControl __instance)
            {
                return !FlightActiveFor(__instance);
            }
        }

        private struct SpeedState
        {
            public bool Applied;
            public float WalkSpeed;
            public float RunSpeed;
        }

        [HarmonyPatch(typeof(scrPlayerControl), "Update")]
        private static class SuperSpeedPatch
        {
            private static void Prefix(scrPlayerControl __instance, out SpeedState __state)
            {
                __state = new SpeedState();
                if (!SpeedActiveFor(__instance))
                {
                    return;
                }
                __state.Applied = true;
                __state.WalkSpeed = __instance.walkSpeed;
                __state.RunSpeed = __instance.runSpeed;
                __instance.walkSpeed *= instance.speedMultiplier.Value;
                __instance.runSpeed *= instance.speedMultiplier.Value;
            }

            private static void Postfix(scrPlayerControl __instance, SpeedState __state)
            {
                if (!__state.Applied || __instance == null)
                {
                    return;
                }
                __instance.walkSpeed = __state.WalkSpeed;
                __instance.runSpeed = __state.RunSpeed;
            }
        }

        private struct CameraState
        {
            public bool Captured;
            public Quaternion Rotation;
        }

        [HarmonyPatch(typeof(scrPlayerControl), "Update")]
        private static class FrozenCameraPatch
        {
            private static void Prefix(scrPlayerControl __instance, out CameraState __state)
            {
                __state = new CameraState();
                if (!FreezeCameraActiveFor(__instance) || __instance.playerCamera == null)
                {
                    return;
                }
                __state.Captured = true;
                __state.Rotation = __instance.playerCamera.transform.rotation;
            }

            private static void Postfix(scrPlayerControl __instance, CameraState __state)
            {
                if (!__state.Captured || __instance == null || __instance.playerCamera == null)
                {
                    return;
                }

                float scaledStep = Time.deltaTime * 60f;
                float unscaledStep = Time.unscaledDeltaTime * 60f;
                if (unscaledStep <= scaledStep || scaledStep <= 0.000001f)
                {
                    return;
                }

                float applied = CameraLerpRate * scaledStep * __instance.turnPenalty;
                if (applied <= 0.0001f)
                {
                    return;
                }
                float wanted = Mathf.Min(CameraLerpRate * unscaledStep * __instance.turnPenalty, 1f);
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

        [HarmonyPatch(typeof(scrDoor), "Interact")]
        private static class DoorUnlockPatch
        {
            private static void Prefix(scrDoor __instance)
            {
                if (!FeatureActive() || __instance == null || !__instance.isLocked || __instance.isStuck)
                {
                    return;
                }
                // true also avoids dereferencing a null custom unlock string on
                // unusually authored doors; the game's localized default is safe.
                __instance.unlockDoor(true);
                if (__instance.pairedDoor != null && __instance.pairedDoor.isLocked)
                {
                    __instance.pairedDoor.unlockDoorNoNotify();
                }
            }
        }

        [HarmonyPatch(typeof(scrKeypad), "Interact")]
        private static class KeypadUnlockPatch
        {
            private static void Prefix(scrKeypad __instance)
            {
                if (FeatureActive() && __instance != null && __instance.isLocked)
                {
                    __instance.unlockDoor(false);
                }
            }
        }

        [HarmonyPatch]
        private static class GenericKeyCheckPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (Type type in typeof(scrDoor).Assembly.GetTypes())
                {
                    MethodInfo method = type.GetMethod("checkForKey",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly,
                        null, Type.EmptyTypes, null);
                    if (method != null && method.ReturnType == typeof(bool))
                    {
                        yield return method;
                    }
                }
            }

            private static bool Prefix(ref bool __result)
            {
                if (!FeatureActive())
                {
                    return true;
                }
                __result = true;
                return false;
            }
        }
    }
}
