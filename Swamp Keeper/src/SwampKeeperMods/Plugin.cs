using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Il2CppInterop.Runtime.Attributes;
using Object = UnityEngine.Object;
using GuiEvent = UnityEngine.Event;

namespace SwampKeeperMods;

[BepInPlugin("local.swampkeeper.mods", "Swamp Keeper Mods", "1.2.0")]
[HarmonyPatch]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance = null!;
    internal static ModUI UI = null!;
    internal static Storage Store = null!;
    internal static ConfigEntry<bool> KillsEnabled = null!, ItemsEnabled = null!, ShowHud = null!;
    internal static long SessionKills, RunKills;
    internal static bool Granted;
    internal static bool TestMode => Environment.GetCommandLineArgs().Contains("-swamp-mod-test");

    public override void Load()
    {
        Instance = this;
        KillsEnabled = Config.Bind("KillTracker", "Enabled", true, "Track all kills credited by the game, including bosses.");
        ItemsEnabled = Config.Bind("ItemPrepare", "Enabled", true, "Choose any items in the preparation area; grant once at run start.");
        ShowHud = Config.Bind("KillTracker", "ShowHUD", true, "Show the lifetime and current run counter.");
        var folder = Environment.GetCommandLineArgs().Contains("-coop-integration-test")?Path.Combine(Paths.GameRootPath,"ModsSource","coop-integration-data")
                    : TestMode ? Path.Combine(Paths.GameRootPath, "ModsSource", "test-results")
                              : Path.Combine(Paths.ConfigPath, "SwampKeeperMods");
        Store = new Storage(Path.Combine(folder, "progress.json"));
        if (Store.Warning.Length > 0) Log.LogWarning(Store.Warning);
        new Harmony("local.swampkeeper.mods").PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo("Installed hooks: " + string.Join(", ", Harmony.GetAllPatchedMethods().Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains("local.swampkeeper.mods") == true).Select(m => m.DeclaringType!.Name + "." + m.Name)));
        UI = AddComponent<ModUI>();
        if (TestMode) AddComponent<RuntimeVerification>();
        Log.LogInfo("Kill Tracker + Item Prepare loaded. F6 opens the panel. Data: " + Store.FilePath);
    }

    internal static bool InPreparation()
    {
        var gm = GameManager.gm;
        return Player.player && gm && !gm.current && gm.start && gm.start.gameObject.activeInHierarchy && !Granted;
    }

    internal static void NewPlayer()
    {
        RunKills = 0;
        Granted = false;
        if (TestMode) { RuntimeVerification.LastResetLifetime=Store.Data.LifetimeKills; RuntimeVerification.PlayerResets++; }
        if(!CoopIntegration.Active)UI.SchedulePreparation();
    }

    internal static void CreditKill()
    {
        if (!KillsEnabled.Value) return;
        if (Store.Data.LifetimeKills < long.MaxValue) Store.Data.LifetimeKills++;
        SessionKills++;
        RunKills++;
        if (!Store.Save()) Instance.Log.LogError(Store.Warning);
    }

    internal static bool GrantLoadout()
    {
        if (CoopIntegration.Active || Granted || !ItemsEnabled.Value) return true;
        var player = Player.player;
        var manager = ItemManager.im;
        if (!player || !manager || player.items == null) return false;
        try
        {
            foreach (var id in Store.Data.PreparedItems)
            {
                if (id < 0 || id >= manager.items.Length) continue;
                // Retrying a partial grant must never apply a stat modifier twice.
                bool alreadyOwned = false;
                for (int j = 0; j < player.items.Count; j++)
                    if (player.items[j] && player.items[j].index == id) { alreadyOwned = true; break; }
                if (alreadyOwned) continue;
                var template = manager.GetItem(id);
                if (!template) throw new InvalidOperationException("Missing item " + id);
                var item = Object.Instantiate(template);
                item.gameObject.SetActive(true);
                player.AddItem(item);
                // Match normal item availability: an owned unique item should not drop again.
                if (manager.dropItemList != null) manager.dropItemList.Remove(template);
                if (manager.lockedItemList != null) manager.lockedItemList.Remove(template);
            }
            Granted = true;
            UI.Close();
            Instance.Log.LogInfo("Prepared loadout equipped: " + string.Join(",", Store.Data.PreparedItems));
            return true;
        }
        catch (Exception ex)
        {
            UI.Status = "Could not equip the full loadout. F6 to retry. " + ex.Message;
            Instance.Log.LogError(ex);
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Start)), HarmonyPostfix]
    private static void PlayerStarted(Player __instance) {if(CoopIntegration.IsLocalActor(__instance))NewPlayer();}

    [HarmonyPatch(typeof(Player), nameof(Player.AddKillCount)), HarmonyPostfix]
    private static void KillCredited(Player __instance) {if(CoopIntegration.IsLocalActor(__instance))CreditKill();}

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.MoveStage)), HarmonyPrefix]
    private static bool LeavingPreparation(GameManager __instance)
        => __instance.current || GrantLoadout();

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.MoveWell)), HarmonyPrefix]
    private static bool EnteringWell(GameManager __instance)
        => __instance.current || GrantLoadout();

    // The game's quick-Well restart constructs this iterator directly, bypassing MoveWell.
    [HarmonyPatch(typeof(GameManager._Well_d__31), nameof(GameManager._Well_d__31.MoveNext)), HarmonyPrefix]
    private static bool QuickWell(GameManager._Well_d__31 __instance, ref bool __result)
    {
        if (__instance.__1__state != 0 || __instance.__4__this.current || GrantLoadout()) return true;
        __result = true; // Wait one frame and retry if scene objects are not ready yet.
        return false;
    }

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.OnNavigate)), HarmonyPrefix]
    private static bool BackgroundNavigation() => !CoopIntegration.MenuOpen && (UI == null || !UI.Open);

    [HarmonyPatch(typeof(UI_Pause), nameof(UI_Pause.Pause)), HarmonyPrefix]
    private static bool BackgroundPause() => !CoopIntegration.MenuOpen && (UI == null || !UI.Open);
}

