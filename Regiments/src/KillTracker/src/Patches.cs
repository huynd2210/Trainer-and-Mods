using HarmonyLib;
using birdseye.Regiments;
using birdseye.Regiments.Commanders;
using birdseye.Regiments.Dismounts;
using birdseye.Regiments.Platoons;

namespace KillTracker
{
    /// <summary>
    /// The battle's lifetime is the lifetime of PlatoonEvents: it creates the death event lists in
    /// Awake and clears them in OnDestroy, once per battle scene.
    /// </summary>
    [HarmonyPatch(typeof(PlatoonEvents), "Awake")]
    internal static class BattleStartPatch
    {
        private static void Postfix()
        {
            BattleTracker.Begin();
        }
    }

    [HarmonyPatch(typeof(PlatoonEvents), "OnDestroy")]
    internal static class BattleUnloadPatch
    {
        private static void Prefix()
        {
            BattleTracker.End();
        }
    }

    // Victory, defeat or quitting all pass through here, before the post-battle screen.
    [HarmonyPatch(typeof(GameController), nameof(GameController.EndSession),
        new[] { typeof(eSides), typeof(eGameResult), typeof(eSessionEndType) })]
    internal static class SessionEndPatch
    {
        private static void Postfix(eSides side, eGameResult gResult, eSessionEndType endType)
        {
            BattleTracker.Finish(side, gResult, endType);
        }
    }

    /// <summary>
    /// The game's soldier-death event (PlatoonEvents.BoidRemoved) does not say who fired. Every
    /// infantry casualty from aimed or area fire comes through one of these two overloads, which
    /// do know, and the death event is raised synchronously inside them, so the shooter is held
    /// for the duration of the call. A finalizer pops it even if the game code throws.
    /// </summary>
    [HarmonyPatch(typeof(PlatoonBoidDamage), "ApplyCasualty",
        new[] { typeof(BoidSquad), typeof(BoidCore), typeof(Commander), typeof(PlatoonCore) })]
    internal static class AimedCasualtyPatch
    {
        private static void Prefix(Commander killingCmdr, PlatoonCore killer)
        {
            Attribution.Push(killer, killingCmdr);
        }

        private static void Finalizer()
        {
            Attribution.Pop();
        }
    }

    [HarmonyPatch(typeof(PlatoonBoidDamage), "ApplyCasualty",
        new[] { typeof(Commander), typeof(PlatoonCore) })]
    internal static class AreaCasualtyPatch
    {
        private static void Prefix(Commander killingCmdr, PlatoonCore killer)
        {
            Attribution.Push(killer, killingCmdr);
        }

        private static void Finalizer()
        {
            Attribution.Pop();
        }
    }

    // Soldiers who die along with their vehicle are removed inside KillUnit; credit them to
    // whoever destroyed it.
    [HarmonyPatch(typeof(PlatoonHealth), nameof(PlatoonHealth.KillUnit))]
    internal static class KillUnitPatch
    {
        private static void Prefix(Commander killingCmdr, PlatoonCore killer)
        {
            Attribution.Push(killer, killingCmdr);
        }

        private static void Finalizer()
        {
            Attribution.Pop();
        }
    }
}
