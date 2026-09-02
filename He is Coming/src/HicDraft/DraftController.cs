using System;

namespace HicDraft
{
    /// <summary>
    /// Drives the draft session: opens the game's own Item Compendium, marks it as a draft (so entry
    /// clicks grant instead of just previewing), and reports the outcome in the compendium header.
    /// The compendium is the game's real item browser - icons, descriptions, rarity/type/tag filters
    /// and controller navigation all come from the game, not from a plugin-drawn overlay.
    /// </summary>
    internal static class DraftController
    {
        /// <summary>True while a compendium we opened for drafting is on screen.</summary>
        public static bool Armed { get; private set; }

        private const string DraftTitle = "DRAFT - CLICK AN ITEM TO EQUIP IT";
        private static string _statusLine;

        public static void Toggle()
        {
            if (Armed) Close();
            else Open();
        }

        public static void Open()
        {
            var compendium = GameRefs.Compendium;
            if (compendium == null)
            {
                Plugin.Log.LogWarning("[Draft] No ItemCompendium in the scene yet - open the game to the main menu or a run first.");
                return;
            }

            var controls = GameRefs.Controls;
            if (controls == null)
            {
                Plugin.Log.LogWarning("[Draft] No PlayerControlsManager in the scene yet.");
                return;
            }

            try
            {
                // Mirror the game's own route into the compendium so its back button lands somewhere sane.
                if (InRun) compendium.SetupIngameMenuBackButton();
                else compendium.SetupMainMenuBackButton();

                compendium.OpenCompendium();
                compendium.OpenItemCompendium();
                controls.SetCompendiumState();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Draft] Failed to open the compendium: {e}");
                return;
            }

            Armed = true;
            _statusLine = InRun ? null : "Not in a run - items can be browsed but not drafted.";
            ApplyHeader();
            Plugin.Log.LogInfo($"[Draft] Draft menu opened (in run: {InRun}).");
        }

        public static void Close()
        {
            Armed = false;
            _statusLine = null;

            var compendium = GameRefs.Compendium;
            if (compendium == null) return;

            try
            {
                if (InRun) compendium.TriggerIngameMenuBackButtonCallback();
                else compendium.TriggerMainMenuBackButtonCallback();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Draft] Closing the compendium threw: {e.Message}");
            }
        }

        /// <summary>
        /// Called once per frame. The player can leave the compendium with the game's own back button,
        /// which the plugin never sees, so disarm as soon as the control state moves on.
        /// </summary>
        public static void Tick()
        {
            if (!Armed) return;

            var controls = GameRefs.Controls;
            if (controls == null) { Armed = false; return; }

            try
            {
                if (controls.GetControlsState() != ControlsState.COMPENDIUM_MENU)
                {
                    Armed = false;
                    _statusLine = null;
                    Plugin.Log.LogInfo("[Draft] Draft menu closed.");
                }
            }
            catch
            {
                Armed = false;
            }
        }

        /// <summary>Entry click handler, wired onto every compendium item button by the patches.</summary>
        public static void EntryClicked(EffectBase effect)
        {
            if (!Armed) return;

            var result = ItemGrant.Give(effect);
            _statusLine = result.Message;
            ApplyHeader();

            if (result.Ok)
            {
                Plugin.Log.LogInfo($"[Draft] {result.Message}");
                if (Plugin.CloseAfterDraft.Value) Close();
            }
            else
            {
                Plugin.Log.LogWarning($"[Draft] {result.Message}");
            }
        }

        /// <summary>
        /// Re-stamps the compendium's own page title. The game rewrites it whenever the list is rebuilt,
        /// so the patches call this again after each filter/sort.
        /// </summary>
        public static void ApplyHeader()
        {
            if (!Armed) return;

            var compendium = GameRefs.Compendium;
            if (compendium == null || compendium.pageTitle == null) return;

            try
            {
                compendium.pageTitle.text = _statusLine == null
                    ? DraftTitle
                    : DraftTitle + "\n" + _statusLine;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Draft] Could not set the compendium header: {e.Message}");
            }
        }

        /// <summary>A run is in progress when the overworld UI exists and its canvas is on screen.</summary>
        private static bool InRun
        {
            get
            {
                var overworld = GameRefs.Overworld;
                if (overworld == null) return false;
                var controls = GameRefs.Controls;
                if (controls == null || controls.overworldCanvas == null) return false;
                return controls.overworldCanvas.activeInHierarchy;
            }
        }
    }
}
