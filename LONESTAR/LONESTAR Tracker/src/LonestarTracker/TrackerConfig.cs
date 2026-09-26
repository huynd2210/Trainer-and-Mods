using System.Collections.Generic;
using Mods;
using UnityEngine.InputSystem;

namespace LonestarTracker
{
    /// <summary>
    /// Persisted to config.json next to the mod and rendered by the game's own
    /// ModSettingPanel (the gear on the mod's row in the Mods menu).
    ///
    /// Every field carrying a ModSettings attribute MUST have a non-null title:
    /// ModSettingPanel.Show() calls title.Replace(...) without a null check.
    /// </summary>
    public class TrackerConfig
    {
        [Label("Panel")]
        public string _h_panel = "";

        [ChoiceField(new[] { "F10", "F9", "F8", "F7", "F6", "F5", "F4", "F3", "F2", "F1", "Off" },
            "Open Tracker With", "Key that opens and closes the tracker panel. Set to Off to use the log files only.")]
        public string panelKey = "F10";

        [IntField(70, 160, "Panel Text Size", "Percent of the default size. Raise it on a 4K screen.")]
        public int textScalePercent = 100;

        [Label("Voyage Readout")]
        public string _h_hud = "";

        [BoolField("Show Voyage Readout", "A one-line kill count for the voyage in progress, on screen while you play.")]
        public bool showHud = true;

        [ChoiceField(new[] { "Bottom right", "Bottom left", "Top right", "Top left" },
            "Readout Corner", "Where the voyage readout sits. Bottom left is where the LONESTAR Trainer overlay goes, if you run both.")]
        public string hudCorner = "Bottom right";

        [Label("Logging")]
        public string _h_log = "";

        [BoolField("Log Battles", "Append a row to battles.csv for every battle won or lost. Turning this off stops new rows; nothing already written is removed.")]
        public bool logBattles = true;

        [BoolField("Log Voyages", "Append a row to runs.csv when a voyage ends.")]
        public bool logRuns = true;

        [Label("Logs live in AppData\\LocalLow\\Shuxi\\LONESTAR\\Tracker", "battles.csv and runs.csv. Open them in any spreadsheet. The mod only ever appends to them.")]
        public string _h_where = "";

        // Where the panel was left. Persisted with the rest of the config, but carrying no
        // ModSettings attribute, so it round-trips through config.json without appearing
        // as a setting to fiddle with. Stored by name rather than by index: reordering the
        // views or the filters must not silently change what a saved choice means.
        public string lastView = "";
        public string lastFilter = "";
        public string lastOrder = "";

        /// <summary>Panel hotkey as a key code. Key.None means the panel is disabled.</summary>
        public Key PanelKey
        {
            get
            {
                Key key;
                return Keys.TryGetValue(panelKey ?? "", out key) ? key : Key.F10;
            }
        }

        private static readonly Dictionary<string, Key> Keys = new Dictionary<string, Key>
        {
            { "F1", Key.F1 }, { "F2", Key.F2 }, { "F3", Key.F3 }, { "F4", Key.F4 },
            { "F5", Key.F5 }, { "F6", Key.F6 }, { "F7", Key.F7 }, { "F8", Key.F8 },
            { "F9", Key.F9 }, { "F10", Key.F10 }, { "Off", Key.None }
        };
    }
}
