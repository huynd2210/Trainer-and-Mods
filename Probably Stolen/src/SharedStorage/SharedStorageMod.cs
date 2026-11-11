using HarmonyLib;
using UnityEngine;

// The offline test harness (not shipped) calls the internal logic directly.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SharedStorageHarness")]

namespace SharedStorage
{
    // Shared Storage: one storage bay whose contents are shared by every save and every new
    // run. The contents live in the game's per-mod data folder, outside any save slot; each
    // save is only a window onto them.
    public class SharedStorageMod : IMod
    {
        internal const string Id = "SharedStorage";

        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            // The game builds its mod item directory when the game scene loads. If that has
            // already happened, register straight away as well.
            ModHook.OnModItemDirectoryInit += Register;
            ModItemDirectory live = Object.FindObjectOfType<ModItemDirectory>();
            if (live != null && live.factoryDictionary != null)
                Register(live);

            ModHook.OnNewGameStarted += OnNewGame;
            ModHook.OnGameLoadedLate += Sync.Attach;
            ModHook.OnCreateTooltipLate += Bay.AddTooltip;
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= Register;
            ModHook.OnNewGameStarted -= OnNewGame;
            ModHook.OnGameLoadedLate -= Sync.Attach;
            ModHook.OnCreateTooltipLate -= Bay.AddTooltip;
            // Stop syncing, but leave the bay and the shared file alone.
            Sync.Detach();
        }

        private static void Register(ModItemDirectory directory)
        {
            directory.Remove(Bay.ItemId);
            directory.Add(Bay.ItemId, Bay.Create);
        }

        private static void OnNewGame(string scenarioId)
        {
            Sync.Attach();
        }
    }

    // The game writes the save slot first; the shared contents follow straight after, so the
    // slot and the shared file always describe the same moment.
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
    internal static class SaveGamePatch
    {
        private static void Postfix()
        {
            Sync.Write();
        }
    }
}
