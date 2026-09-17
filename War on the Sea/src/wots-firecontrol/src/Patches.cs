using HarmonyLib;

namespace WoTSFireControl
{
    /// <summary>
    /// Tightens a mount's fire as soon as the ship builds it.
    ///
    /// <c>WeaponAAAMount</c> overrides <c>InitialiseAAA</c> and chains to this one, so patching
    /// the base covers both the hull batteries and the directed mounts. Applying here rather
    /// than from a periodic sweep means a ship that joins mid-battle is correct from its first
    /// burst.
    /// </summary>
    [HarmonyPatch(typeof(WeaponAAA), "InitialiseAAA")]
    internal static class InitialiseAAAPatch
    {
        private static void Postfix(WeaponAAA __instance)
        {
            if (FireControlPlugin.Enabled && FireControlPlugin.AntiAirEnabled)
            {
                AaaTuning.Apply(__instance);
            }
        }
    }

    /// <summary>
    /// Makes a player ship's fire-control solution converge immediately.
    ///
    /// <c>Director.CalculateTMAAgainstTarget</c> is the whole gunnery solution in one method. It
    /// is called once a second per tracked target and takes exactly the two numbers this mod
    /// wants to change: <c>baseRate</c>, how much solution is gained per call, and
    /// <c>maxSolution</c>, the ceiling that rate is climbing toward. Everything downstream
    /// follows from the result - the solution sets the range error fed to the gun's intercept
    /// solve (<c>1 - currentSolution</c> scales a random range error), and
    /// <c>WeaponMount</c> also reads it to pick the salvo spread and to decide whether the ship
    /// needs to walk spotting salvos onto the target first.
    ///
    /// So overriding these two parameters is the entire feature: the solution snaps to its
    /// ceiling on the first tick, the range error collapses to the residual left at 0.99, the
    /// tightest spread is chosen, and no spotting salvos are needed.
    ///
    /// What this deliberately does NOT do is remove shell dispersion. Guns still scatter; they
    /// simply stop aiming at a guess. Enemy directors are untouched.
    /// </summary>
    [HarmonyPatch(typeof(Director), "CalculateTMAAgainstTarget")]
    internal static class CalculateTMAAgainstTargetPatch
    {
        private static void Prefix(Director __instance, ref float baseRate, ref float maxSolution)
        {
            if (!FireControlPlugin.Enabled || !FireControlPlugin.GunnerySolutionEnabled)
            {
                return;
            }
            if (__instance == null || __instance.parentUnit == null ||
                __instance.parentUnit.faction != Faction.Player)
            {
                return;
            }

            baseRate = FireControlPlugin.SolutionGainPerTick;

            if (FireControlPlugin.ForceMaximumSolution)
            {
                // The method multiplies this by visibility, difficulty and the night-fight
                // modifier and then subtracts range, manoeuvre and smoke penalties before using
                // it as a ceiling. A headroom figure rather than 0.99 is what survives all of
                // that and still lets the game's own clamp decide the real maximum.
                maxSolution = FireControlPlugin.SolutionCeilingHeadroom;
            }
        }
    }
}
