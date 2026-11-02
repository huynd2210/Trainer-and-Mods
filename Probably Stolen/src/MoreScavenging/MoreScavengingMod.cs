using System;
using HarmonyLib;

namespace MoreScavenging
{
    public class MoreScavengingMod : IMod
    {
        internal const string Id = "MoreScavenging";

        internal static ConfigEntry<int> ExtraAttempts;
        internal static ConfigEntry<bool> CapInjuryRisk;

        public void Init(ModManifest manifest)
        {
            ModConfig config = ModHelper.GetConfig(Id);
            ExtraAttempts = config.Define(
                "extraAttempts", 1000, "General",
                () => "Extra scavenging attempts",
                0, 10000,
                () => "Added to the attempts you normally get at the dumping ground each night.");
            CapInjuryRisk = config.Define(
                "capInjuryRisk", true, "General",
                () => "Keep injury risk normal",
                () => "The game raises the injury chance with every attempt (+1% minor, +0.25% major), which makes injury near-certain after a few dozen. On: the chance stops rising after your normal attempts. Off: the vanilla curve.");
        }

        public void OnEnable() { }

        public void OnDisable() { }
    }

    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.GetMaxScavAttempts))]
    internal static class MaxAttemptsPatch
    {
        // The game's own limit (5, adjusted by starting perks and dumping ground events),
        // as of the last time the game asked.
        internal static int VanillaMax = 5;

        private static void Postfix(ref int __result)
        {
            VanillaMax = __result;
            __result += MoreScavengingMod.ExtraAttempts.Value;
        }
    }

    // Both wound chances read PlayerStore.scavengingAttempts. While they are computed (for
    // the roll and for the percentages shown at the dumping ground), the count is held at
    // the last normal attempt, then put back.
    internal static class InjuryCap
    {
        internal static int Enter()
        {
            int actual = PlayerStore.Instance.scavengingAttempts;
            if (!MoreScavengingMod.CapInjuryRisk.Value)
                return actual;
            ScavHelper.GetMaxScavAttempts();
            int cap = Math.Max(0, MaxAttemptsPatch.VanillaMax - 1);
            PlayerStore.Instance.scavengingAttempts = Math.Min(actual, cap);
            return actual;
        }

        internal static void Exit(int actual) => PlayerStore.Instance.scavengingAttempts = actual;
    }

    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.GetMinorWoundChance))]
    internal static class MinorWoundPatch
    {
        private static void Prefix(out int __state) => __state = InjuryCap.Enter();
        private static void Postfix(int __state) => InjuryCap.Exit(__state);
    }

    [HarmonyPatch(typeof(ScavHelper), nameof(ScavHelper.GetMajorWoundChance))]
    internal static class MajorWoundPatch
    {
        private static void Prefix(out int __state) => __state = InjuryCap.Enter();
        private static void Postfix(int __state) => InjuryCap.Exit(__state);
    }
}
