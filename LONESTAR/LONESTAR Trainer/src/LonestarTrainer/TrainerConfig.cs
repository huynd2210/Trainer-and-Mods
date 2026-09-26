using Mods;

namespace LonestarTrainer
{
    /// <summary>
    /// Persisted to config.json next to the mod, and rendered by the game's own
    /// ModSettingPanel (gear icon on the mod's row in the Mods menu).
    ///
    /// Every field carrying a ModSettings attribute MUST have a non-null title:
    /// ModSettingPanel.Show() calls title.Replace(...) without a null check.
    /// </summary>
    public class TrainerConfig
    {
        [Label("Battle")]
        public string _h_battle = "";

        [BoolField("God Mode", "Your ship takes no damage in battle.")]
        public bool godMode = false;

        [BoolField("One-Hit Kill", "Any hit that lands on the enemy ship destroys it.")]
        public bool oneHitKill = false;

        [IntField(0, 10, "Bonus Energy (first turn)", "Extra energy at the start of a battle. Applies from the next battle.")]
        public int bonusEnergyFirstTurn = 0;

        [IntField(0, 10, "Bonus Energy (every turn)", "Extra energy each turn. Applies from the next battle.")]
        public int bonusEnergyPerTurn = 0;

        [Label("Voyage")]
        public string _h_voyage = "";

        [BoolField("Infinite Star Coins", "Tops your coins back up to the cap whenever they run low.")]
        public bool infiniteCoins = false;

        [BoolField("No Hull Loss Outside Battle", "Events, shops and hazards cannot cost you HP on the map.")]
        public bool noHullLossOutsideBattle = false;

        [Label("Interface")]
        public string _h_ui = "";

        [BoolField("Show Status Overlay", "Compact list of active cheats in the bottom-left corner. Toggle in-game with F11.")]
        public bool showOverlay = true;

        [BoolField("Enable Hotkeys", "Turn off if the function keys clash with something else.")]
        public bool hotkeysEnabled = true;

        [BoolField("Enable Developer Console", "Unlocks the game's own built-in GM console. Press Enter to open it, Esc to close. Restart the game after changing this. WARNING: some of its commands overwrite your permanent save.")]
        public bool enableDevConsole = false;
    }
}
