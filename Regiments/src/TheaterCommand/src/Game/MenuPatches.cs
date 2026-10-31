using System.Collections.Generic;
using birdseye;
using birdseye.Fadeables;
using birdseye.Regiments;
using birdseye.Regiments.GameModes;
using birdseye.Regiments.Menu.GUI;
using birdseye.Regiments.Menu.GUI.CampaignMenu;
using birdseye.Regiments.SaveStates;
using HarmonyLib;
using UnityEngine;

namespace TheaterCommand.Game
{
    /// <summary>
    /// Where Theater Command meets the War Paths menus: a Mode choice on the new-run screen, slots
    /// that open the theater instead of the phase list, and slot labels that show the turn.
    /// </summary>
    public static class MenuPatches
    {
        private const string ModeRowName = "TC_ModeOptions";
        private static OptionsController modeRow;
        private static bool creatingTheater;
        private static eSaveSlot? phasesSlot;

        private static int TurnsFor(int lengthOption) => 8 + 4 * Mathf.Clamp(lengthOption, 0, 2);

        [HarmonyPatch(typeof(ProcOpRunSetupPanel), nameof(ProcOpRunSetupPanel.SetCurrentSlot))]
        private static class SetupPatch
        {
            private static void Postfix(ProcOpRunSetupPanel __instance)
            {
                if (modeRow == null)
                {
                    // Copy an existing option row so it looks and behaves like its neighbours.
                    GameObject src = __instance.priceTypeOptions.gameObject;
                    GameObject row = Object.Instantiate(src, src.transform.parent);
                    row.name = ModeRowName;
                    row.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
                    modeRow = row.GetComponent<OptionsController>();
                }
                modeRow.Init("TC_Mode", new List<string> { "TC_Mode_WarPath", "TC_Mode_Theater" }, 0);
            }
        }

        [HarmonyPatch(typeof(ProcOpRunSetupPanel), "AcceptButtonHandler")]
        private static class AcceptPatch
        {
            private static void Prefix()
            {
                creatingTheater = modeRow != null && modeRow.GetIndex() == 1;
            }

            private static void Postfix(ProcOpRunSetupPanel __instance)
            {
                if (!creatingTheater)
                {
                    return;
                }
                creatingTheater = false;
                ProcOpSaveSlotRecord slot = AccessTools.Field(typeof(ProcOpRunSetupPanel), "slotRecord").GetValue(__instance) as ProcOpSaveSlotRecord;
                Campaign c = CampaignFactory.Create(slot, __instance.curRegiment, TurnsFor(__instance.dateOptions.GetIndex()));
                CampaignStore.Save(slot.saveSlot, c);
                slot.SaveToFiles(dbgsilent: true);
                TheaterScreen.Show(slot);
            }
        }

        [HarmonyPatch(typeof(ProcOpSaveSlotItem), "ClickHandler")]
        private static class SlotClickPatch
        {
            private static bool Prefix(ProcOpSaveSlotItem __instance)
            {
                ProcOpSaveSlotRecord rec = SlotOf(__instance);
                if (CampaignStore.Get(rec) == null)
                {
                    return true;
                }
                TheaterScreen.Show(rec);
                return false;
            }
        }

        [HarmonyPatch(typeof(ProcOpSaveSlotItem), "UpdateLabels")]
        private static class SlotLabelsPatch
        {
            private static void Postfix(ProcOpSaveSlotItem __instance)
            {
                ProcOpSaveSlotRecord rec = SlotOf(__instance);
                Campaign c = CampaignStore.Get(rec);
                if (c == null)
                {
                    return;
                }
                Core.Theater t = c.theater;
                if (t.finished)
                {
                    __instance.timeLabel.SetID("TC_Theater Command\n" + (t.winner == t.playerSide ? "Victory" : "Defeat"));
                }
                else
                {
                    __instance.timeLabel.SetID("TC_Theater Command\nTurn " + (t.turn + 1) + " of " + t.maxTurns);
                }
                __instance.stageLabel.SetID("TC_VP " + t.blueVP + " : " + t.redVP);
            }
        }

        private static ProcOpSaveSlotRecord SlotOf(ProcOpSaveSlotItem item)
        {
            return AccessTools.Field(typeof(ProcOpSaveSlotItem), "slotRecord").GetValue(item) as ProcOpSaveSlotRecord;
        }

        [HarmonyPatch(typeof(ProcOpMenuPhasesList), nameof(ProcOpMenuPhasesList.InitPhases))]
        private static class PhasesListPatch
        {
            private static void Postfix(eSaveSlot slot) => phasesSlot = slot;
        }

        /// <summary>A theater campaign has no phase list: anything heading there goes to the map.</summary>
        [HarmonyPatch(typeof(FadeGroupTransitionManager), nameof(FadeGroupTransitionManager.TransitionTo), typeof(FadeGroup), typeof(float))]
        private static class PhasesRedirectPatch
        {
            private static bool Prefix(FadeGroup targetCG)
            {
                if (phasesSlot == null || targetCG == null || targetCG != FadeGroupMarkerManager.GetFadeGroup(eCanvasMarkers.ProcOpPhases))
                {
                    return true;
                }
                ProcOpSaveSlotRecord rec = ProcOpSaveManager.active.GetSlot(phasesSlot.Value);
                if (CampaignStore.Get(rec) == null)
                {
                    return true;
                }
                TheaterScreen.Show(rec);
                return false;
            }
        }

        // Rest and Retreat would move the War Path run on without a battle; in a theater campaign
        // refitting is a theater order instead.
        [HarmonyPatch(typeof(ProcOpRegimentManager), nameof(ProcOpRegimentManager.RestRefit))]
        private static class RestPatch
        {
            private static bool Prefix(ProcOpRegimentManager __instance) => CampaignStore.Get(__instance.slotRecord) == null;
        }

        [HarmonyPatch(typeof(ProcOpRegimentManager), nameof(ProcOpRegimentManager.Retreat))]
        private static class RetreatPatch
        {
            private static bool Prefix(ProcOpRegimentManager __instance) => CampaignStore.Get(__instance.slotRecord) == null;
        }
    }
}
