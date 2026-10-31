using System.Collections;
using System.Collections.Generic;
using System.Linq;
using birdseye;
using birdseye.Regiments;
using birdseye.Regiments.GameModes;
using birdseye.Regiments.Menu.GUI.CampaignMenu;
using birdseye.Regiments.SaveStates;
using birdseye.Regiments.SceneManagement;
using birdseye.Regiments.Zones;
using HarmonyLib;
using TheaterCommand.Core;
using UnityEngine;

namespace TheaterCommand.Game
{
    /// <summary>
    /// Fights a theater battle as a one-phase War Path stage. Before the battle the War Path run
    /// is pointed at the sector's map, a mission matching the battle, the enemy formation's
    /// battlegroup and the player formation's roster; the player then gets the usual regiment
    /// screen and battle. Afterwards the surviving roster and the outcome flow back to the theater.
    /// </summary>
    public static class BattleBridge
    {
        // Outcome of the battle that just ended, read in ProcessEnding while the map is still loaded.
        private static bool? lastWon;

        public static eProcOpMissionTypes MissionFor(Theater t, Battle b) => b.attackerSide == t.playerSide ? eProcOpMissionTypes.Attack : eProcOpMissionTypes.Defend;

        /// <summary>The player's formation that commands a battle: the strongest one in it.</summary>
        public static Formation Commander(Theater t, Battle b)
        {
            IEnumerable<int> ids = b.attackerSide == t.playerSide ? b.attackers : b.defenders;
            return ids.Select(t.Formation).Where(f => !f.destroyed).OrderByDescending(Combat.Power).FirstOrDefault();
        }

        public static void Launch(ProcOpSaveSlotRecord slot, Campaign c, Battle b)
        {
            Theater t = c.theater;
            Formation f = Commander(t, b);
            BlueForce bf = f != null ? c.Force(f.id) : null;
            if (bf == null)
            {
                Mod.Log("battle " + b.id + " has no real force to fight it");
                return;
            }
            ProcOpRunDataSaveState rd = slot.runData;
            int phase = slot.procOpSave.phaseProgress;
            int stage = slot.adjPhaseSaves[phase].stageID;

            var st = new GenStageSaveState(BuildStage(slot, c, b));
            // The battle is stage k; stage k+1 must exist so the game can step past it afterwards.
            while (rd.stageSaves.Count < stage + 2) rd.stageSaves.Add(st);
            while (rd.stageSaves_alt.Count < stage + 2) rd.stageSaves_alt.Add(st);
            rd.stageSaves[stage] = rd.stageSaves_alt[stage] = st;
            rd.stageSaves[stage + 1] = rd.stageSaves_alt[stage + 1] = st;
            ClearStageCache(rd);
            while (rd.phaseDescs.Count < phase + 2)
            {
                rd.phaseDescs.Add(rd.phaseDescs[rd.phaseDescs.Count - 1]);
            }

            foreach (ProcOpPhaseSaveState ps in new[] { slot.adjPhaseSaves[phase], slot.srcPhaseSaves[phase] })
            {
                ps.playerSave = GameForces.Load(bf);
                ps.stageID = stage;
                ps.phaseCountOnStage = 0;
                ps.usesAltStage = false;
                ps.stageSave = new ProcOpStageSaveState(rd.GetStage(stage, isAlt: false), stage, makeInitalZoneState: true);
                ps.stageSave_alt = new ProcOpStageSaveState(rd.GetStage(stage, isAlt: true), stage, makeInitalZoneState: true);
                ps.platoonSaves = new List<PlatoonSaveState>();
                ps.bgSaves = new BattlegroupsSaveState();
                ps.isCleanFile = true;
                ps.didRnR = true;
                ps.hasChanges = false;
            }
            slot.SaveToFiles(dbgsilent: true);

            c.activeBattle = b.id;
            c.activeFormation = f.id;
            c.launchPhase = phase;
            CampaignStore.Save(slot.saveSlot, c);
            OpenRegimentScreen(slot, phase);
        }

        public static void OpenRegimentScreen(ProcOpSaveSlotRecord slot, int phase)
        {
            TheaterScreen.Hide();
            // Normally set by the main menu's War Paths button; a battle needs it set.
            IRegimentManager.activeRegMan = ProcOpRegimentManager.active;
            ProcOpMenuPhasesList.active.InitPhases(slot.saveSlot);
            FadeGroupTransitionManager.active.TransitionTo(eCanvasMarkers.ProcOpPhaseManagement);
            ProcOpRegimentManager.active.Init(slot, phase);
        }

        private static GenStageDesc BuildStage(ProcOpSaveSlotRecord slot, Campaign c, Battle b)
        {
            Theater t = c.theater;
            System.Random rng = Dice.For(t, 6007, b.id);
            eProcOpMissionTypes type = MissionFor(t, b);
            // Fought on the map of the town the battle is named after.
            ProcOpMapDescriptor map = ProcOpMapList.active.mapList[t.NearestTown(b.cell).mapIndex].map;
            List<ProcOpMapDescriptor.MapMissionGenRecord> recs = map.validMissionGenRecords.Where(r => r.missionDesc.missionType == type).ToList();
            if (recs.Count == 0)
            {
                // Town maps are chosen to support both missions; this is a safety net.
                ProcOpMapList.MapRecord alt = ProcOpMapList.active.GetValidMaps(type)[0];
                map = alt.map;
                recs = map.validMissionGenRecords.Where(r => r.missionDesc.missionType == type).ToList();
            }
            ProcOpMapDescriptor.MapMissionGenRecord rec = recs[rng.Next(recs.Count)];

            FrontForceParameters eny = CampaignFactory.EnemyFront(slot);
            FrontForceParameters ally = GUIDManager.GuidToSO<FrontForceParameters>(slot.runData.alliedForceGuid);
            IEnumerable<int> redIds = b.attackerSide == Side.Red ? b.attackers : b.defenders;
            Formation redLead = redIds.Select(t.Formation).OrderByDescending(Combat.Power).FirstOrDefault();

            var g = new GenStageDesc
            {
                redNatForces = eny,
                redNatForceID = redLead != null ? redLead.forceRef % eny.natRecords.Count : 0,
                blueNatForces = ally,
                blueNatForceID = rng.Next(ally.natRecords.Count),
                RedTAPerSecond = 0.1f,
                poMapDesc = map,
                locID = map.mapDesc.nameLocID,
                missionDesc = rec.missionDesc,
                scripts = rec.scripts,
                pointArrayPrefab = rec.pointArrayPrefab,
                pointArrayIndexToUse = rec.pointArrayIndexToUse,
                timeLimit = 1,
                isLast = false,
            };
            g.redSpawnList = g.redForce.atkFS.GetSL();
            g.blueSpawnList = g.blueForce.atkFS.GetSL();
            g.redTAList = g.redForce.atkFS.GetTL();
            g.blueTAList = g.blueForce.atkFS.GetTL();
            return g;
        }

        private static void ClearStageCache(ProcOpRunDataSaveState rd)
        {
            AccessTools.Field(typeof(ProcOpRunDataSaveState), "_stages").SetValue(rd, null);
            AccessTools.Field(typeof(ProcOpRunDataSaveState), "_stages_alt").SetValue(rd, null);
        }

        public static Campaign ActiveCampaign(eSaveSlot slot, out ProcOpSaveSlotRecord rec)
        {
            rec = ProcOpSaveManager.active.GetSlot(slot);
            Campaign c = CampaignStore.Get(rec);
            return c != null && c.activeBattle >= 0 ? c : null;
        }

        /// <summary>
        /// Take the fought battle's result into the theater: the formation's roster as it left the
        /// field, who won, and losses for everyone else involved.
        /// </summary>
        public static void Absorb(ProcOpSaveSlotRecord slot, Campaign c, bool won)
        {
            Theater t = c.theater;
            Battle b = t.battles.FirstOrDefault(x => x.id == c.activeBattle);
            Formation f = t.formations.FirstOrDefault(x => x.id == c.activeFormation);
            int after = c.launchPhase + 1;
            if (b != null && f != null && slot.adjPhaseSaves.Count > after)
            {
                ProcOpPlayerSaveState ps = slot.adjPhaseSaves[after].playerSave;
                // Whatever was still on the field comes home to its stack.
                for (int i = 0; i < ps.forceSave.boughtTFs.Count && i < ps.forceSave.tfStacks.Length; i++)
                {
                    foreach (StackSaveState s in ps.forceSave.tfStacks[i]?.stacks ?? new StackSaveState[0])
                    {
                        if (s != null && s.stackCount >= 0 && s.deployedCount > 0)
                        {
                            s.stackCount += s.deployedCount;
                        }
                        if (s != null)
                        {
                            s.deployedCount = 0;
                            s.hasDeployedPlt = false;
                        }
                    }
                }
                BlueForce bf = c.Force(f.id);
                GameForces.Store(bf, ps);
                GameForces.SyncStrength(f, bf);

                b.resolution = BattleResolution.Fought;
                b.foughtBy = f.id;
                b.attackerWon = (b.attackerSide == Side.Blue) == won;
                float p = Combat.AttackerWinChance(t, b);
                Combat.ExpectedLosses(p, b.attackerWon, out float aLoss, out float dLoss);
                b.attackerLoss = aLoss;
                b.defenderLoss = dLoss;
                t.Log(f.name + (won ? " wins" : " loses") + " the battle at " + t.NearestTown(b.cell).name);
                Rules.ApplyBattle(t, b, new GameForces(c, slot));
            }
            c.activeBattle = -1;
            c.activeFormation = -1;
            c.launchPhase = -1;
            CampaignStore.Save(slot.saveSlot, c);
        }

        // --- Patches -------------------------------------------------------------------------

        private static readonly AccessTools.FieldRef<ProcOpPostPhaseHandler, ProcOpGameModeSetup> gmSetupRef =
            AccessTools.FieldRefAccess<ProcOpPostPhaseHandler, ProcOpGameModeSetup>("gmSetup");

        /// <summary>
        /// A theater battle never ends the War Path run: the outcome is recorded and the run steps
        /// to its next stage, ready for the next battle.
        /// </summary>
        [HarmonyPatch(typeof(ProcOpPostPhaseHandler), "ProcessEnding")]
        private static class ProcessEndingPatch
        {
            private static void Postfix(ProcOpPostPhaseHandler __instance, ref ePhaseEndResult actualPhaseEnd)
            {
                ProcOpGameModeSetup setup = gmSetupRef(__instance);
                if (setup == null || ActiveCampaign(setup.saveSlotToUse, out _) == null)
                {
                    return;
                }
                bool attacking = setup.stageDesc.missionDesc.missionType == eProcOpMissionTypes.Attack;
                lastWon = Won(actualPhaseEnd, attacking);
                Mod.Log("battle ended: " + actualPhaseEnd + ", won=" + lastWon);
                if (actualPhaseEnd != ePhaseEndResult.StageEnd_TotalControl)
                {
                    actualPhaseEnd = ePhaseEndResult.StageEnd;
                }
            }
        }

        private static bool Won(ePhaseEndResult end, bool attacking)
        {
            if (end == ePhaseEndResult.StageEnd_TotalControl)
            {
                return true;
            }
            if (end == ePhaseEndResult.OperationEnd_ForceLose)
            {
                return false;
            }
            List<NodeZone> zones = OpGMZoneManager.active.playNodeZones;
            if (zones == null || zones.Count == 0)
            {
                zones = OpGMZoneManager.active.allNodeZones;
            }
            int blue = zones.Count(z => z.currentSide == eSides.Blue);
            int red = zones.Count(z => z.currentSide == eSides.Red);
            // The attacker has to end up holding more of the ground than the enemy; a defender
            // only has to not lose it.
            return attacking ? blue > red : blue >= red;
        }

        // "Phase concluded, moving to stage 2" means nothing in a theater campaign.
        [HarmonyPatch(typeof(birdseye.Regiments.Menu.GUI.SavingWindowController), nameof(birdseye.Regiments.Menu.GUI.SavingWindowController.Show))]
        private static class SavingWindowPatch
        {
            private static bool Prefix(eSaveSlot saveSlot) => ActiveCampaign(saveSlot, out _) == null;
        }

        /// <summary>
        /// The regiment screen's header (name, flag, insignia) shows the War Path run's own force;
        /// in a theater campaign it should show the formation going into battle, whose force the
        /// phase save carries. Pointed there only while the panel reads it.
        /// </summary>
        [HarmonyPatch(typeof(ProcOpRegimentManagerPanel), nameof(ProcOpRegimentManagerPanel.Init))]
        private static class RegimentHeaderPatch
        {
            private static void Prefix(ProcOpSaveSlotRecord slotRecord, ProcOpPhaseSaveState phaseSave, out string __state)
            {
                __state = null;
                string own = phaseSave?.playerSave?.forceSave?.sourceForceSetupGuid;
                if (string.IsNullOrEmpty(own) || CampaignStore.Get(slotRecord) == null)
                {
                    return;
                }
                __state = slotRecord.runData.sourceForceSetupGuid;
                slotRecord.runData.sourceForceSetupGuid = own;
            }

            private static void Finalizer(ProcOpSaveSlotRecord slotRecord, string __state)
            {
                if (__state != null)
                {
                    slotRecord.runData.sourceForceSetupGuid = __state;
                }
            }
        }

        [HarmonyPatch(typeof(ProcOpLoader), nameof(ProcOpLoader.LoadNextPhase))]
        private static class LoadNextPhasePatch
        {
            private static bool Prefix(ProcOpLoader __instance) => !ReturnToTheater(__instance);
        }

        [HarmonyPatch(typeof(ProcOpLoader), nameof(ProcOpLoader.ToResults))]
        private static class ToResultsPatch
        {
            private static bool Prefix(ProcOpLoader __instance) => !ReturnToTheater(__instance);
        }

        private static bool ReturnToTheater(ProcOpLoader loader)
        {
            if (!(GameController.active.gmSetup is ProcOpGameModeSetup setup))
            {
                return false;
            }
            Campaign c = ActiveCampaign(setup.saveSlotToUse, out ProcOpSaveSlotRecord rec);
            if (c == null)
            {
                return false;
            }
            loader.StartCoroutine(ReturnCoroutine(rec, c));
            return true;
        }

        private static IEnumerator ReturnCoroutine(ProcOpSaveSlotRecord rec, Campaign c)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            yield return MapLoadingController.active.UnloadActiveScene();
            rec.LoadFromFiles(dbgsilent: true);
            Absorb(rec, c, lastWon ?? false);
            lastWon = null;
            TheaterScreen.Show(rec);
        }
    }
}
