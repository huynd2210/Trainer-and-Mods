using System;
using HarmonyLib;

namespace LonestarTracker
{
    /// <summary>
    /// A new voyage: ship, pilot, seed and phase plan are all set by the time this
    /// returns. WantedManager.Load handles the resumed case instead.
    /// </summary>
    [HarmonyPatch(typeof(WantedManager), nameof(WantedManager.InitData))]
    internal static class Patch_RunStart
    {
        private static void Postfix()
        {
            Tracker.OnRunStart();
        }
    }

    /// <summary>A saved voyage was loaded back in.</summary>
    [HarmonyPatch(typeof(WantedManager), nameof(WantedManager.Load), new Type[0])]
    internal static class Patch_RunResume
    {
        private static void Postfix(WantedManager __instance)
        {
            Tracker.OnRunResume();

            // Loading a save whose voyage had already ended: the game wraps it up
            // immediately, so close the row with the real outcome rather than leaving
            // it open for a voyage that no longer exists.
            WantedProcess process = __instance != null ? __instance.wantedProcess : null;
            if (process != null && process.processStatus != WantedProcessStatus.During)
                Tracker.OnRunEnd(process.processStatus);
        }
    }

    /// <summary>The results screen - the voyage is over, win or lose.</summary>
    [HarmonyPatch(typeof(WantedManager), nameof(WantedManager.ProcessOver))]
    internal static class Patch_RunEnd
    {
        private static void Prefix(WantedProcessStatus processStatus)
        {
            Tracker.OnRunEnd(processStatus);
        }
    }

    /// <summary>
    /// The voyage is being dropped from memory - quit to the menu, or a new voyage
    /// started over it. Every path that clears WantedManager.wantedProcess goes
    /// through here first.
    /// </summary>
    [HarmonyPatch(typeof(WantedProcess), nameof(WantedProcess.ClearListener))]
    internal static class Patch_RunDetached
    {
        private static void Postfix()
        {
            Tracker.OnRunDetached();
        }
    }

    /// <summary>Both ships exist and are at full state here: the start of the battle.</summary>
    [HarmonyPatch(typeof(BattleManager), nameof(BattleManager.InitBattleGrid))]
    internal static class Patch_BattleStart
    {
        private static void Postfix()
        {
            Tracker.OnBattleStart();
        }
    }
}
