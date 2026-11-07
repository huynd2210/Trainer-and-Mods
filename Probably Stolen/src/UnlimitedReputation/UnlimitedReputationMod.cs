using System;
using HarmonyLib;

namespace UnlimitedReputation
{
    public class UnlimitedReputationMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable() { }

        public void OnDisable() { }
    }

    // StoreReputation.CheckReputation() clamps every faction to [-200, 200] after each
    // change (ModReputation, ModReputationRaw, SetReputation). Keep the -200 floor and
    // lift the ceiling. int.MaxValue rather than +infinity: GetReputation() and the
    // per-faction getters cast the double to int, which turns anything past that negative.
    [HarmonyPatch(typeof(StoreReputation), "CheckReputation")]
    internal static class CheckReputationPatch
    {
        private static bool Prefix(ref double ___amount)
        {
            ___amount = Math.Min(Math.Max(___amount, -200.0), int.MaxValue);
            StoreReputation.UpdateReputation();
            return false;
        }
    }

    // GetReputationStatus() has no label above 200 and returns "". Past the old cap you
    // stay at the top tier.
    [HarmonyPatch(typeof(StoreReputation), nameof(StoreReputation.GetReputationStatus))]
    internal static class ReputationStatusPatch
    {
        private static void Postfix(int amount, ref string __result)
        {
            if (amount > 200)
                __result = LocHelper.GetLocalizedMechanic("rep_prestigious");
        }
    }

    // The reputation panel: UpdateBar() ignores values outside [-200, 200], which leaves
    // the bar at the prefab's default height, and the number reads "N/200". Show the bar
    // full and the number on its own.
    [HarmonyPatch(typeof(ReputationElement), nameof(ReputationElement.Init))]
    internal static class ReputationElementPatch
    {
        private static void Postfix(ReputationElement __instance, StoreReputation storeReputation)
        {
            __instance.reputationNumber.text = storeReputation.GetReputationExact().ToString("F2");
            if (storeReputation.GetReputation() > 200)
                __instance.UpdateBar(200);
        }
    }
}
