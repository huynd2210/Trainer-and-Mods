using HarmonyLib;

namespace LonestarTrainer
{
    /// <summary>
    /// The single chokepoint for battle damage: DamageMaker and every combat line
    /// resolve through ShipController.TakeDamage.
    /// </summary>
    [HarmonyPatch(typeof(ShipController), nameof(ShipController.TakeDamage))]
    internal static class Patch_TakeDamage
    {
        private static bool Prefix(ShipController __instance, ref int damage, ref int __result)
        {
            TrainerConfig cfg = TrainerMod.Config;
            if (cfg == null || __instance == null || damage <= 0) return true;

            if (__instance.playerflag)
            {
                if (!cfg.godMode) return true;
                __result = 0;
                return false;   // skip the original entirely
            }

            if (cfg.oneHitKill)
            {
                ShipData enemy = __instance.shipData;
                if (enemy != null)
                {
                    int lethal = enemy.currentHP + enemy.currentShield;
                    if (lethal > damage) damage = lethal;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Backstop for god mode: a handful of skills and battle events call SetHP_Battle
    /// directly rather than going through TakeDamage.
    /// </summary>
    [HarmonyPatch(typeof(ShipData), nameof(ShipData.SetHP_Battle))]
    internal static class Patch_SetHP_Battle
    {
        private static bool Prefix(ShipData __instance, int offset, ref int __result)
        {
            TrainerConfig cfg = TrainerMod.Config;
            if (cfg == null || !cfg.godMode) return true;
            if (offset >= 0) return true;                 // heals always pass
            if (!(__instance is PlayerShipData)) return true;

            __result = 0;
            return false;
        }
    }

    /// <summary>
    /// Out-of-battle hull loss (events, hazards, shop trades). Patched rather than
    /// setting Define.defineValue.noLoseHPOut, because the TS_NoLoseHPOut treasure
    /// owns that flag and would fight us over it.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShipData), nameof(PlayerShipData.SetHP_Normal))]
    internal static class Patch_SetHP_Normal
    {
        private static bool Prefix(int offset)
        {
            TrainerConfig cfg = TrainerMod.Config;
            if (cfg == null || !cfg.noHullLossOutsideBattle) return true;
            return offset >= 0;
        }
    }

    /// <summary>
    /// Events ask this before offering a choice that would cost hull. Without it the
    /// option is greyed out even though the damage could never land.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShipData), nameof(PlayerShipData.CheckNormalHP))]
    internal static class Patch_CheckNormalHP
    {
        private static bool Prefix(ref bool __result)
        {
            TrainerConfig cfg = TrainerMod.Config;
            if (cfg == null || !cfg.noHullLossOutsideBattle) return true;
            __result = true;
            return false;
        }
    }

    /// <summary>
    /// PowerManager.Init copies the per-battle energy budget out of Define.defineValue.
    /// Adding the bonus here keeps the defineValue fields untouched for the talents and
    /// treasures that write them.
    /// </summary>
    [HarmonyPatch(typeof(PowerManager), nameof(PowerManager.Init))]
    internal static class Patch_PowerManagerInit
    {
        private static void Postfix(PowerManager __instance)
        {
            TrainerConfig cfg = TrainerMod.Config;
            if (cfg == null || __instance == null) return;

            if (cfg.bonusEnergyFirstTurn > 0)
                __instance.powerInitNum += cfg.bonusEnergyFirstTurn;

            if (cfg.bonusEnergyPerTurn > 0)
                __instance.powerNumEveryTurn += cfg.bonusEnergyPerTurn;
        }
    }
}
