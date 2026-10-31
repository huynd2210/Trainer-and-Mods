using System;
using birdseye;
using birdseye.Localization;
using birdseye.Regiments;
using birdseye.Regiments.Menu.GUI.CampaignMenu;
using birdseye.Regiments.SaveStates;
using HarmonyLib;
using TheaterCommand.Core;

namespace TheaterCommand.Game
{
    /// <summary>
    /// Opens one of the player's formations in the game's own regiment screen, to reinforce,
    /// upgrade and buy task forces with its Operational Authority between battles. The roster is
    /// loaded into the War Path slot the same way a battle loads it; whatever the player changed
    /// there goes back to the formation when they return to the map.
    /// </summary>
    public static class RosterBridge
    {
        private const string BackLabel = "TC_Back to map";

        // Why a formation cannot be opened, first match wins; none means it can.
        private static readonly (Func<Campaign, Formation, bool> blocks, Func<Formation, string> why)[] Refusals =
        {
            ((c, f) => f == null || f.destroyed, f => ""),
            ((c, f) => f.side != c.theater.playerSide, f => "Only your own formations can be reinforced"),
            ((c, f) => c.theater.finished, f => "The campaign is over"),
            ((c, f) => c.Force(f.id) == null, f => f.name + " has no regiment of its own to reinforce"),
        };

        /// <summary>Why the formation cannot be opened in the regiment screen, or null if it can.</summary>
        public static string WhyNot(Campaign c, Formation f)
        {
            foreach (var r in Refusals)
            {
                if (r.blocks(c, f))
                {
                    return r.why(f);
                }
            }
            return null;
        }

        public static bool CanOpen(Campaign c, Formation f) => WhyNot(c, f) == null;

        public static void Open(ProcOpSaveSlotRecord slot, Campaign c, Formation f)
        {
            if (!CanOpen(c, f))
            {
                return;
            }
            int phase = slot.procOpSave.phaseProgress;
            BlueForce bf = c.Force(f.id);
            ProcOpPhaseSaveState adj = slot.adjPhaseSaves[phase];
            ProcOpPhaseSaveState src = slot.srcPhaseSaves[phase];
            adj.playerSave = GameForces.Load(bf);
            src.playerSave = GameForces.Load(bf);
            adj.hasChanges = false;
            c.managedFormation = f.id;
            CampaignStore.Save(slot.saveSlot, c);
            BattleBridge.OpenRegimentScreen(slot, phase);
        }

        /// <summary>Back on the map: the formation keeps what was bought for it.</summary>
        public static void Close(ProcOpSaveSlotRecord slot, Campaign c)
        {
            if (c.managedFormation < 0)
            {
                return;
            }
            Formation f = c.theater.formations.Find(x => x.id == c.managedFormation);
            BlueForce bf = f != null ? c.Force(f.id) : null;
            int phase = slot.procOpSave.phaseProgress;
            if (bf != null && slot.adjPhaseSaves.Count > phase)
            {
                ProcOpPhaseSaveState adj = slot.adjPhaseSaves[phase];
                float before = f.strength01;
                GameForces.Store(bf, adj.playerSave);
                GameForces.SyncStrength(f, bf);
                // A battle already set up in the slot gets its own formation's roster back.
                BlueForce fighting = c.activeBattle >= 0 ? c.Force(c.activeFormation) : null;
                if (fighting != null)
                {
                    adj.playerSave = GameForces.Load(fighting);
                }
                slot.srcPhaseSaves[phase].LoadFromJson(adj.ToJson());
                adj.hasChanges = false;
                if (f.strength01 > before + 0.005f)
                {
                    c.theater.Log(f.name + " is reinforced to " + UnityEngine.Mathf.RoundToInt(f.strength01 * 100f) + "%");
                }
            }
            c.managedFormation = -1;
            CampaignStore.Save(slot.saveSlot, c);
        }

        private static bool Managing(ProcOpSaveSlotRecord slot)
        {
            Campaign c = CampaignStore.Get(slot);
            return c != null && c.managedFormation >= 0;
        }

        // --- Patches -------------------------------------------------------------------------

        // Only a theater battle may be launched from the regiment screen.
        [HarmonyPatch(typeof(ProcOpRegimentManager), nameof(ProcOpRegimentManager.Launch))]
        private static class LaunchPatch
        {
            private static bool Prefix(ProcOpRegimentManager __instance) => !Managing(__instance.slotRecord);
        }

        // While managing, the screen's forward button reads BACK TO MAP and returns there.
        [HarmonyPatch(typeof(RegimentManagerPanelBase), "ToEventsButtonHandler")]
        private static class ToEventsPatch
        {
            private static bool Prefix(RegimentManagerPanelBase __instance)
            {
                if (!(__instance is ProcOpRegimentManagerPanel p) || !Managing(p.regMan.slotRecord))
                {
                    return true;
                }
                TheaterScreen.Safe(() => TheaterScreen.Show(p.regMan.slotRecord));
                return false;
            }
        }

        [HarmonyPatch(typeof(ProcOpRegimentManagerPanel), "Update")]
        private static class LabelPatch
        {
            private static readonly AccessTools.FieldRef<L10NTextReplacer, string[]> ids = AccessTools.FieldRefAccess<L10NTextReplacer, string[]>("strings");
            private static string[] original;

            private static void Postfix(ProcOpRegimentManagerPanel __instance)
            {
                if (GameQueries.isInScene || __instance.regMan.slotRecord == null)
                {
                    return;
                }
                L10NTextReplacer label = __instance.toEventsButton.GetComponentInChildren<L10NTextReplacer>(true);
                if (label == null)
                {
                    return;
                }
                bool managing = Managing(__instance.regMan.slotRecord);
                string[] now = ids(label);
                bool showsBack = now != null && now.Length == 1 && now[0] == BackLabel;
                if (managing && !showsBack)
                {
                    original = now;
                    label.SetID(BackLabel);
                }
                else if (!managing && showsBack && original != null)
                {
                    label.SetID(original);
                }
            }
        }
    }
}
