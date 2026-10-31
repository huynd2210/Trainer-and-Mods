using HarmonyLib;
using birdseye.Regiments.Menu.GUI.CampaignMenu;
using birdseye.Regiments.SaveStates;

namespace WarpathOPs
{
    /// <summary>
    /// Every OP purchase (point packs, task forces, upgrades, restoring platoons) goes through
    /// RegManagerEconomics in the between-battle regiment manager, which reads the balance from
    /// the phase save. Topping that save up before each read/check/deduct means the balance
    /// never runs out and the saved run keeps the high value. A balance above the target (a run
    /// saved under the old 999999 default) is lowered to it so the counter fits. The in-battle
    /// OP counter is loaded from the same save, so it follows.
    /// </summary>
    internal static class Patches
    {
        private static readonly AccessTools.FieldRef<RegManagerEconomics, IEconDataProvider> EconProvider =
            AccessTools.FieldRefAccess<RegManagerEconomics, IEconDataProvider>("econProvider");

        private static void TopUp(IEconDataProvider provider)
        {
            if (!WarpathOPsPlugin.Enabled.Value || provider == null)
            {
                return;
            }
            int target = WarpathOPsPlugin.TargetOPs.Value;
            if (provider is ProcOpPhaseSaveState warpath)
            {
                if (warpath.playerSave != null && warpath.playerSave.OPs != target)
                {
                    warpath.playerSave.OPs = target;
                }
            }
            else if (provider is PhaseSaveState campaign && WarpathOPsPlugin.AlsoCampaign.Value)
            {
                if (campaign.playerSave != null && campaign.playerSave.OPs != target)
                {
                    campaign.playerSave.OPs = target;
                }
            }
        }

        [HarmonyPatch(typeof(RegManagerEconomics), nameof(RegManagerEconomics.OPs), MethodType.Getter)]
        [HarmonyPrefix]
        private static void BeforeReadOPs(RegManagerEconomics __instance)
        {
            TopUp(EconProvider(__instance));
        }

        [HarmonyPatch(typeof(RegManagerEconomics), nameof(RegManagerEconomics.IsAffordable))]
        [HarmonyPrefix]
        private static void BeforeIsAffordable(RegManagerEconomics __instance)
        {
            TopUp(EconProvider(__instance));
        }

        [HarmonyPatch(typeof(RegManagerEconomics), nameof(RegManagerEconomics.DeductOP))]
        [HarmonyPrefix]
        private static void BeforeDeduct(RegManagerEconomics __instance)
        {
            TopUp(EconProvider(__instance));
        }

        // A fresh Warpath run starts at the target instead of the difficulty's 250-450 OPs.
        [HarmonyPatch(typeof(ProcOpPlayerSaveState), nameof(ProcOpPlayerSaveState.Init))]
        [HarmonyPostfix]
        private static void AfterNewRun(ProcOpPlayerSaveState __instance)
        {
            if (WarpathOPsPlugin.Enabled.Value && __instance.OPs < WarpathOPsPlugin.TargetOPs.Value)
            {
                __instance.OPs = WarpathOPsPlugin.TargetOPs.Value;
            }
        }
    }
}
