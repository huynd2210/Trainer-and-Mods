namespace MoreRunStats
{
    public class MoreRunStatsMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            ModHook.OnNewGameStarted += OnNewGameStarted;
        }

        public void OnDisable()
        {
            ModHook.OnNewGameStarted -= OnNewGameStarted;
        }

        // PlayerStore.StartNewGame resets RunStats but not modData, so the last run's
        // totals would carry over. Loading a save replaces modData with the save's own.
        private static void OnNewGameStarted(string scenarioId) => RunLedger.Clear();
    }
}
