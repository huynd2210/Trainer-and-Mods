using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HicDraft
{
    /// <summary>
    /// Turns the game's compendium entries into draft buttons. The listener goes on the entry's own
    /// uGUI Button - not on ItemCompendium.ItemSelected, which also fires on hover and on controller
    /// navigation, and would hand out an item every time the cursor passed over one.
    /// </summary>
    [HarmonyPatch]
    internal static class CompendiumPatches
    {
        /// <summary>Instance ids of entries already wired, so a rebuild does not stack listeners.</summary>
        private static readonly HashSet<int> Wired = new HashSet<int>();

        /// <summary>
        /// Keeps the managed side of each UnityAction alive for as long as the plugin is loaded.
        /// Without this the delegate can be collected while IL2CPP still holds the trampoline.
        /// </summary>
        private static readonly List<Action> LiveHandlers = new List<Action>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemChooseEntry), nameof(ItemChooseEntry.SetupItemEntryCompendium))]
        public static void OnCompendiumEntryBuilt(ItemChooseEntry __instance)
        {
            if (__instance == null) return;

            int id = __instance.GetInstanceID();
            if (!Wired.Add(id)) return;

            Button button = null;
            try { button = __instance.GetButton(); } catch { }
            if (button == null) button = __instance.GetComponent<Button>();
            if (button == null)
            {
                Plugin.Log.LogWarning($"[Draft] Compendium entry {id} has no Button; it cannot be drafted.");
                Wired.Remove(id);
                return;
            }

            var entry = __instance;
            Action handler = () =>
            {
                try { DraftController.EntryClicked(entry.GetEffectBase()); }
                catch (Exception e) { Plugin.Log.LogError($"[Draft] Entry click failed: {e}"); }
            };
            LiveHandlers.Add(handler);
            button.onClick.AddListener((UnityAction)handler);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemCompendium), nameof(ItemCompendium.ClearPreviousEntries))]
        public static void OnEntriesCleared() => Wired.Clear();

        /// <summary>The game rewrites the page title when the list is rebuilt; put the draft header back.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemCompendium), nameof(ItemCompendium.FilterItems))]
        public static void OnItemsFiltered() => DraftController.ApplyHeader();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemCompendium), nameof(ItemCompendium.OpenItemCompendium))]
        public static void OnCompendiumOpened() => DraftController.ApplyHeader();
    }
}
