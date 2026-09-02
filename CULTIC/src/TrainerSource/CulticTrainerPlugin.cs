using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("local.codex.cultictrainer", "CULTIC Hotkey Trainer", "1.2.0")]
public sealed class CulticTrainerPlugin : BaseUnityPlugin
{
    private ConfigEntry<KeyboardShortcut> noDamageKey;
    private ConfigEntry<KeyboardShortcut> refillHealthKey;
    private ConfigEntry<KeyboardShortcut> addAmmoKey;
    private ConfigEntry<KeyboardShortcut> addArmorKey;
    private ConfigEntry<KeyboardShortcut> unlockAllGearKey;
    private ConfigEntry<KeyboardShortcut> completeLevelKey;

    private bool noDamageEnabled;
    private static bool noDamagePatchEnabled;
    private static float suppressConsoleUntil;
    private float statusUntil;
    private string statusText = "";
    private Harmony harmony;
    private static readonly FieldInfo IsGodField = typeof(scrPlayerControl).GetField("isGod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo ConsoleStringField = typeof(scrPlayerHUD).GetField("consoleString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo NewInputStringField = typeof(scrPlayerHUD).GetField("newInputString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private void Awake()
    {
        noDamageKey = Config.Bind("Hotkeys", "NoDamage", new KeyboardShortcut(KeyCode.F1), "Toggle no damage.");
        refillHealthKey = Config.Bind("Hotkeys", "RefillHealth", new KeyboardShortcut(KeyCode.F2), "Refill health once.");
        addAmmoKey = Config.Bind("Hotkeys", "AddAmmo", new KeyboardShortcut(KeyCode.F3), "Add 100 ammo to the current gun and fill its magazine.");
        addArmorKey = Config.Bind("Hotkeys", "AddArmor", new KeyboardShortcut(KeyCode.F4), "Top up armor.");
        unlockAllGearKey = Config.Bind("Hotkeys", "UnlockAllGear", new KeyboardShortcut(KeyCode.End), "Unlock every weapon and usable equipment item, and refill their ammo.");
        completeLevelKey = Config.Bind("Hotkeys", "CompleteLevel", new KeyboardShortcut(KeyCode.PageDown), "Complete the current level through the normal results and progression flow.");

        harmony = new Harmony("local.codex.cultictrainer");
        harmony.PatchAll();

        Logger.LogInfo("CULTIC trainer loaded. F1 no damage, F2 health, F3 ammo, F4 armor, End all gear, Page Down complete level.");
    }

    private void OnDestroy()
    {
        noDamagePatchEnabled = false;
        if (harmony != null)
        {
            harmony.UnpatchSelf();
        }
    }

    private void Update()
    {
        if (noDamageKey.Value.IsDown())
        {
            SuppressDevConsole();
            noDamageEnabled = !noDamageEnabled;
            ApplyNoDamage(noDamageEnabled);
            if (noDamageEnabled)
            {
                RefillHealth();
            }
            ShowStatus("No damage: " + (noDamageEnabled ? "ON" : "OFF"));
        }

        if (refillHealthKey.Value.IsDown())
        {
            SuppressDevConsole();
            RefillHealth();
            ShowStatus("Health refilled");
        }

        if (addAmmoKey.Value.IsDown())
        {
            SuppressDevConsole();
            ShowStatus(AddCurrentGunAmmo());
        }

        if (addArmorKey.Value.IsDown())
        {
            SuppressDevConsole();
            AddArmor();
            ShowStatus("Armor topped up");
        }

        if (unlockAllGearKey.Value.IsDown())
        {
            SuppressDevConsole();
            ShowStatus(UnlockAllGear());
        }

        if (completeLevelKey.Value.IsDown())
        {
            SuppressDevConsole();
            ShowStatus(CompleteCurrentLevel());
        }

        if (noDamageEnabled)
        {
            ApplyNoDamage(true);
        }

    }

    private void OnGUI()
    {
        if (Time.unscaledTime > statusUntil || string.IsNullOrEmpty(statusText))
        {
            return;
        }

        GUI.Label(new Rect(18f, 18f, 520f, 32f), statusText);
    }

    private void ApplyNoDamage(bool enabled)
    {
        scrPlayerControl player = FindPlayerControl();
        if (player != null && IsGodField != null)
        {
            IsGodField.SetValue(player, enabled);
        }

        noDamagePatchEnabled = enabled;
    }

    private void RefillHealth()
    {
        scrGameControl game;
        scrPlayerControl control;
        scrPlayer player = TryGetLocalPlayer(out game, out control);
        if (player == null)
        {
            return;
        }

        int maxHealth = GetMaxHealth(game, player);
        if (player.playerHP < maxHealth)
        {
            player.playerHP = maxHealth;
        }
    }

    private string AddCurrentGunAmmo()
    {
        scrGameControl game;
        scrPlayerControl control;
        scrPlayer player = TryGetLocalPlayer(out game, out control);
        if (player == null || control == null)
        {
            return "Ammo unavailable";
        }

        if (control.tempWeapon != null)
        {
            control.tempWeapon.ammo = Mathf.Clamp(control.tempWeapon.ammo + 100, 0, 999);
            if (game.hudScript != null)
            {
                game.hudScript.refreshAmmo(-1, control.tempWeapon.ammo,
                    control.tempWeapon.ammoIcon);
            }
            return "+100 temporary-weapon ammo";
        }

        if (player.playerLoadout == null || control.weapon < 0 ||
            control.weapon >= player.playerLoadout.Count)
        {
            return "No current weapon";
        }

        scrWeapon weapon = player.playerLoadout[control.weapon];
        if (weapon == null)
        {
            return "No current weapon data";
        }

        bool changed = false;
        if (weapon.weaponChargeRate != 0f)
        {
            weapon.weaponCharge = 100f;
            changed = true;
        }

        if (!weapon.weaponIgnoresAmmo && player.ammo != null &&
            weapon.weaponAmmoID >= 0 && weapon.weaponAmmoID < player.ammo.Length)
        {
            int ammoId = weapon.weaponAmmoID;
            // Match CULTIC's own developer-console ammo command: change the
            // current reserve directly. Do not permanently raise maxAmmo,
            // which is a capacity/upgrade value rather than the live count.
            player.ammo[ammoId] = Mathf.Clamp(player.ammo[ammoId] + 100, 0, 999);
            changed = true;
        }

        if (weapon.weaponCanReload)
        {
            int capacity = GetWeaponCapacity(weapon, player);
            if (capacity > 0)
            {
                weapon.weaponCurrentAmmo = capacity;
                changed = true;
            }
        }

        // inventoryUpdate() does not touch the ammo counter. CULTIC's own
        // console command calls refreshAmmo(), so do the same immediately.
        if (game.hudScript != null)
        {
            game.hudScript.refreshAmmo(control.weapon);
        }

        return changed ? "+100 ammo; weapon refilled" :
            "Current weapon does not use ammo";
    }

    private static int GetWeaponCapacity(scrWeapon weapon, scrPlayer player)
    {
        int capacity = weapon.weaponCapacity;
        if (weapon.weaponCapacityIncrease > 0 && weapon.weaponCapacityIndex >= 0 &&
            player != null && player.playerUpgrades != null &&
            weapon.weaponUpgradeIndex >= 0 &&
            weapon.weaponUpgradeIndex < player.playerUpgrades.GetLength(0) &&
            weapon.weaponCapacityIndex < player.playerUpgrades.GetLength(1))
        {
            capacity += weapon.weaponCapacityIncrease *
                player.playerUpgrades[weapon.weaponUpgradeIndex,
                    weapon.weaponCapacityIndex];
        }

        return Mathf.Clamp(capacity, 0, 999);
    }

    private void AddArmor()
    {
        scrGameControl game;
        scrPlayerControl control;
        scrPlayer player = TryGetLocalPlayer(out game, out control);
        if (player == null)
        {
            return;
        }

        if (player.playerArmor != null)
        {
            for (int i = 0; i < player.playerArmor.Length; i++)
            {
                player.playerArmor[i] = Mathf.Clamp(player.playerArmor[i] + 100, 0, 100);
                if (game.hudScript != null)
                {
                    game.hudScript.flashArmorWheel(i);
                }
            }
        }

        if (control != null && control.shieldHP > 0)
        {
            control.shieldHP = Math.Max(control.shieldHP, scrDroppedShield.shieldMaxHP);
            player.playerShieldHP = Math.Max(player.playerShieldHP, control.shieldHP);
        }
    }

    private string UnlockAllGear()
    {
        scrGameControl game;
        scrPlayerControl control;
        scrPlayer player = TryGetLocalPlayer(out game, out control);
        if (player == null || control == null || game.weaponTable == null ||
            player.playerLoadout == null)
        {
            return "Gear unavailable";
        }

        int playerId = control.playerID;
        int weaponsAdded = 0;
        for (int i = 0; i < game.weaponTable.Length; i++)
        {
            scrWeapon weapon = game.weaponTable[i];
            if (weapon == null)
            {
                continue;
            }

            bool alreadyOwned = game.playerHasWeaponOfID(weapon.weaponTableID,
                playerId);
            game.giveWeaponToPlayer(playerId, weapon.weaponAmmoID, 0, i, -1);
            if (!alreadyOwned && game.playerHasWeaponOfID(weapon.weaponTableID,
                playerId))
            {
                weaponsAdded++;
            }
        }

        if (player.ammo != null && player.maxAmmo != null)
        {
            int ammoCount = Math.Min(player.ammo.Length, player.maxAmmo.Length);
            for (int i = 0; i < ammoCount; i++)
            {
                player.ammo[i] = Math.Max(player.ammo[i], player.maxAmmo[i]);
            }
        }

        for (int i = 0; i < player.playerLoadout.Count; i++)
        {
            scrWeapon weapon = player.playerLoadout[i];
            if (weapon == null)
            {
                continue;
            }

            if (weapon.weaponChargeRate != 0f)
            {
                weapon.weaponCharge = 100f;
            }

            if (weapon.weaponCanReload)
            {
                int capacity = GetWeaponCapacity(weapon, player);
                if (capacity > 0)
                {
                    weapon.weaponCurrentAmmo = capacity;
                }
            }
        }

        int equipmentAdded = UnlockUsableEquipment(game, player);
        if (game.hudScript != null)
        {
            game.hudScript.arsenalUpdate();
            game.hudScript.inventoryUpdate();
            game.hudScript.refreshAmmo(control.weapon);
        }

        return "All gear unlocked (" + weaponsAdded + " weapons, " +
            equipmentAdded + " equipment added)";
    }

    private static int UnlockUsableEquipment(scrGameControl game,
        scrPlayer player)
    {
        if (game.equipmentTable == null || player.playerEquipment == null)
        {
            return 0;
        }

        int added = 0;
        for (int i = 0; i < game.equipmentTable.Length; i++)
        {
            scrEquipment source = game.equipmentTable[i];
            if (source == null ||
                source.equipmentType == scrEquipment.EquipmentType.Key)
            {
                continue;
            }

            scrEquipment owned = null;
            for (int j = 0; j < player.playerEquipment.Count; j++)
            {
                scrEquipment candidate = player.playerEquipment[j];
                if (candidate != null &&
                    candidate.equipmentTableID == source.equipmentTableID)
                {
                    owned = candidate;
                    break;
                }
            }

            if (owned != null)
            {
                owned.equipmentValue = Math.Max(owned.equipmentValue,
                    source.equipmentMaxValue);
                continue;
            }

            scrEquipment copy = source.DeepCopy(source);
            copy.equipmentValue = Math.Max(copy.equipmentValue,
                copy.equipmentMaxValue);
            player.playerEquipment.Add(copy);
            added++;
        }

        return added;
    }

    private string CompleteCurrentLevel()
    {
        scrGameControl game;
        scrPlayerControl control;
        scrPlayer player = TryGetLocalPlayer(out game, out control);
        if (game == null || player == null || control == null ||
            game.sceneData == null)
        {
            return "No active level to complete";
        }

        if (game.connectionStatus != scrGameControl.ConnectionStatus.Offline)
        {
            return "Complete level is offline-only";
        }

        if (game.gameState == 2 || game.gameState == 3)
        {
            return "Level completion already in progress";
        }

        try
        {
            // Match CULTIC's native developer-console "skipmap" command so
            // completion is recorded by the normal results/profile pipeline.
            game.exitPoint = 0;
            game.startLevelEnd();
            return "Completing current level";
        }
        catch (Exception exception)
        {
            Logger.LogError("Could not complete the current level: " +
                exception);
            return "Could not complete current level";
        }
    }

    private static scrPlayer TryGetLocalPlayer(out scrGameControl game, out scrPlayerControl control)
    {
        game = scrGameControl.Instance;
        control = FindPlayerControl();
        if (game == null || game.gamePlayers == null || game.gamePlayers.Length == 0)
        {
            return null;
        }

        int playerId = game.localPlayerID;
        if (control != null)
        {
            playerId = control.playerID;
        }

        if (playerId < 0 || playerId >= game.gamePlayers.Length)
        {
            playerId = 0;
        }

        return game.gamePlayers[playerId];
    }

    private static int GetMaxHealth(scrGameControl game, scrPlayer player)
    {
        int maxHealth = player.playerMaxHP;

        try
        {
            if (player.playerUpgrades != null)
            {
                maxHealth = Math.Max(maxHealth, game.diffPlayerStartingHP + (player.playerUpgrades[0, 0] * game.hpUpgradeValue));
            }
        }
        catch
        {
            maxHealth = Math.Max(maxHealth, game.diffPlayerStartingHP);
        }

        if (maxHealth <= 0)
        {
            maxHealth = Math.Max(100, player.playerHP);
        }

        return Mathf.Clamp(maxHealth, 1, 999);
    }


    private static scrPlayerControl FindPlayerControl()
    {
        scrGameControl game = scrGameControl.Instance;
        if (game != null)
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
            }

            if (game.gamePlayers != null && game.localPlayerID >= 0 && game.localPlayerID < game.gamePlayers.Length)
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

        return UnityEngine.Object.FindObjectOfType<scrPlayerControl>();
    }

    private static void SuppressDevConsole()
    {
        suppressConsoleUntil = Time.unscaledTime + 0.35f;

        scrGameControl game = scrGameControl.Instance;
        if (game != null && game.hudScript != null)
        {
            CloseDevConsole(game.hudScript);
        }
    }

    private static bool ShouldSuppressDevConsole()
    {
        return Time.unscaledTime <= suppressConsoleUntil;
    }

    private static void CloseDevConsole(scrPlayerHUD hud)
    {
        if (hud == null)
        {
            return;
        }

        // F1 is also CULTIC's default "Dev Console" input. Depending on
        // script update order, the game can process that same press after the
        // trainer and set gameState to 1. Closing only the HUD then leaves
        // gameplay input locked behind an invisible console. Remember whether
        // the console actually owned that state and release it along with the
        // UI; do not touch unrelated menus or map states.
        bool consoleWasOpen = hud.inConsole;
        try
        {
            hud.closeConsole();
        }
        catch
        {
            hud.inConsole = false;
        }

        hud.inConsole = false;
        if (ConsoleStringField != null)
        {
            ConsoleStringField.SetValue(hud, "");
        }

        if (NewInputStringField != null)
        {
            NewInputStringField.SetValue(hud, "");
        }

        if (consoleWasOpen)
        {
            scrGameControl game = scrGameControl.Instance;
            if (game != null && game.gameState == 1)
            {
                game.gameState = 0;
            }
        }
    }

    private void ShowStatus(string text)
    {
        statusText = "[Trainer] " + text;
        statusUntil = Time.unscaledTime + 2.5f;
        Logger.LogInfo(text);
    }

    [HarmonyPatch(typeof(scrPlayerControl), "takeDamage")]
    private static class TakeDamagePatch
    {
        private static bool Prefix(scrPlayerControl __instance)
        {
            if (!noDamagePatchEnabled)
            {
                return true;
            }

            scrGameControl game = scrGameControl.Instance;
            if (game != null && __instance != null && game.localPlayerID >= 0 && __instance.playerID != game.localPlayerID)
            {
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(scrPlayerHUD), "Update")]
    private static class HudConsolePatch
    {
        private static void Postfix(scrPlayerHUD __instance)
        {
            if (ShouldSuppressDevConsole())
            {
                CloseDevConsole(__instance);
            }
        }
    }
}
