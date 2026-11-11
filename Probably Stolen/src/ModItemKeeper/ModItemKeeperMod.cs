namespace ModItemKeeper
{
    // Mod Item Keeper: items inside a container from a mod you removed are kept with the save
    // and put back when the mod returns (or put into your inventory if the container is gone).
    public class ModItemKeeperMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            Keeper.Enabled = true;
            ModHook.OnGameLoadedInit += Keeper.Reset;
            ModHook.OnNewGameStarted += OnNewGame;
        }

        public void OnDisable()
        {
            Keeper.Enabled = false;
            Keeper.Reset();
            ModHook.OnGameLoadedInit -= Keeper.Reset;
            ModHook.OnNewGameStarted -= OnNewGame;
        }

        private static void OnNewGame(string scenarioId) => Keeper.Reset();
    }
}
