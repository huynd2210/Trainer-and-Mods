using System;
using System.Linq;
using birdseye;
using birdseye.Regiments;
using birdseye.Regiments.GameModes;
using birdseye.Regiments.SceneManagement;
using birdseye.Regiments.SaveStates;

namespace TheaterCommand.Game
{
    /// <summary>Development steps run through DevBridge. Slot 9 is the test slot; slot 1 is the user's.</summary>
    public static class Dev
    {
        // Turn the War Path run already in slot 9 into a fresh theater campaign and open it.
        public static void NewInSlot9(Action<string> log)
        {
            ProcOpSaveSlotRecord slot = ProcOpSaveManager.active.GetSlot(eSaveSlot.Slot9);
            if (slot.isEmptySlot)
            {
                log("slot 9 is empty; start a War Path run there first");
                return;
            }
            ProcOpForceList.ForceRecord rec = ProcOpForceList.active.forces.First(r => r.force == slot.runData.GetSourceForce());
            Campaign c = CampaignFactory.Create(slot, rec, 12);
            CampaignStore.Save(slot.saveSlot, c);
            TheaterScreen.Show(slot);
            Core.Theater t = c.theater;
            log("theater: " + t.cols + "x" + t.rows + ", " + t.towns.Count + " towns, " + t.formations.Count + " formations, " + c.forces.Count + " real forces");
            foreach (Core.Formation f in t.formations)
            {
                log("  " + f.side + " " + f.name + " = " + f.kind + " " + f.size);
            }
            log("towns: " + string.Join(", ", t.towns.Select(x => x.name + "(" + x.vp + (x.hq ? " HQ" : "") + ")")));
        }

        public static void ShowSlot9(Action<string> log)
        {
            try
            {
                TheaterScreen.Show(ProcOpSaveManager.active.GetSlot(eSaveSlot.Slot9));
            }
            catch (Exception e)
            {
                log("failed: " + e);
                return;
            }
            log("shown");
        }

        // Hover the player's frontmost formation, to see its reach.
        public static void HoverFront(Action<string> log)
        {
            Core.Theater t = TheaterScreen.Instance.Campaign.theater;
            Core.Formation f = t.Alive(Core.Side.Blue).OrderByDescending(x => t.Cell(x.cell).col).First();
            TheaterScreen.Instance.DevHover(f.id);
            log("hovering " + f.name);
        }

        // Hover the enemy formation nearest the player's frontmost one.
        public static void HoverEnemy(Action<string> log)
        {
            Core.Theater t = TheaterScreen.Instance.Campaign.theater;
            Core.Formation f = t.Alive(Core.Side.Blue).OrderByDescending(x => t.Cell(x.cell).col).First();
            Core.Formation e = t.Alive(Core.Side.Red).OrderBy(x => t.Distance(x.cell, f.cell)).First();
            TheaterScreen.Instance.DevHover(e.id);
            log("hovering " + e.name);
        }

        // Start dragging the frontmost formation onto the nearest enemy, without dropping it.
        public static void DragOntoEnemy(Action<string> log)
        {
            Core.Theater t = TheaterScreen.Instance.Campaign.theater;
            Core.Formation f = t.Alive(Core.Side.Blue).OrderByDescending(x => t.Cell(x.cell).col).First();
            Core.Formation e = t.Alive(Core.Side.Red).OrderBy(x => t.Distance(x.cell, f.cell)).First();
            TheaterScreen.Instance.DevBeginDrag(f.id, e.cell);
            log(f.name + " over " + e.name);
        }

        // Every NATO formation that can reach an enemy attacks the nearest one; then next turn.
        public static void AttackAndNext(Action<string> log)
        {
            TheaterScreen s = TheaterScreen.Instance;
            Core.Theater t = s.Campaign.theater;
            foreach (Core.Formation f in t.Alive(Core.Side.Blue).ToList())
            {
                Core.Reach r = Core.Rules.Reachable(t, f);
                Core.Formation e = t.Alive(Core.Side.Red).Where(x => Core.Rules.CanAttack(t, f, x.cell, r, out _)).OrderBy(x => t.Distance(x.cell, f.cell)).FirstOrDefault();
                if (e != null)
                {
                    log(f.name + ": " + s.DevDrag(f.id, e.cell));
                }
            }
            s.DevPrimary();
            foreach (Core.Battle b in t.battles)
            {
                log("battle " + b.id + " near " + t.NearestTown(b.cell).name + " attacker " + b.attackerSide + " p=" + Core.Combat.AttackerWinChance(t, b).ToString("0.00") + " " + b.resolution);
            }
        }

        public static void Battle(Action<string> log, int index, bool fight)
        {
            TheaterScreen s = TheaterScreen.Instance;
            Core.Battle b = s.Campaign.theater.battles.Where(x => x.resolution == Core.BattleResolution.Pending).ElementAt(index);
            s.DevBattle(b.id, fight);
            log((fight ? "fighting " : "auto-resolved ") + b.id + " -> " + b.resolution + " attackerWon=" + b.attackerWon);
        }

        public static void AutoFirst(Action<string> log) => Battle(log, 0, false);

        public static void FightFirst(Action<string> log) => Battle(log, 0, true);

        public static void Primary(Action<string> log)
        {
            TheaterScreen.Instance.DevPrimary();
            Core.Theater t = TheaterScreen.Instance.Campaign.theater;
            log("turn " + (t.turn + 1) + " phase " + t.phase + " pending " + Core.Rules.Pending(t).Count() + " VP " + t.blueVP + ":" + t.redVP);
        }

        // The regiment screen's Launch button.
        public static void LaunchBattle(Action<string> log)
        {
            // The Launch button needs an event card chosen; take the first in the hand.
            if (ProcOpRegimentManager.active.eventCards.selectedCard == null)
            {
                ProcOpRegimentManager.active.eventCards.OnCardSelected(0);
            }
            log("card: " + ProcOpRegimentManager.active.eventCards.selectedCard);
            // In battle the game reads the mouse's pointer data, which only exists after a real
            // mouse move; under automation there has been none, so create it.
            var args = new object[] { -1, null, true };
            HarmonyLib.AccessTools.Method(typeof(UnityEngine.EventSystems.PointerInputModule), "GetPointerData").Invoke(AccessibleInputModule.active, args);
            ProcOpRegimentManager.active.Launch();
            log("launching");
        }

        // End the battle as if its time ran out, skipping the statistics screen.
        public static void EndBattle(Action<string> log)
        {
            GameController.active.EndSession(eSessionEndType.ProperFinish);
            GameController.active.UnloadSession();
            log("battle ended");
        }

        // The post-battle menu's Continue.
        public static void ContinueAfterBattle(Action<string> log)
        {
            ProcOpLoader.active.LoadNextPhase();
            log("continue");
        }

        // Double-click the weakest formation that has a real roster.
        public static void Manage(Action<string> log)
        {
            TheaterScreen s = TheaterScreen.Instance;
            Core.Theater t = s.Campaign.theater;
            Core.Formation f = t.Alive(Core.Side.Blue).Where(x => s.Campaign.Force(x.id) != null).OrderBy(x => x.strength01).First();
            s.OnCounterClick(f.id, UnityEngine.EventSystems.PointerEventData.InputButton.Left, 2);
            log("managing " + f.name + " at " + (int)(f.strength01 * 100) + "%, OPs " + ProcOpRegimentManager.active.phaseSave.playerSave.OPs);
        }

        // Restore every stack that is under strength, as its reinforce button would.
        public static void ReinforceAll(Action<string> log)
        {
            ProcOpRegimentManager m = ProcOpRegimentManager.active;
            var tfs = m.phaseSave.playerSave.forceSave.GetBoughtTFs();
            int n = 0;
            for (int i = 0; i < tfs.Count; i++)
            {
                StackSaveState[] stacks = m.phaseSave.playerSave.forceSave.tfStacks[i]?.stacks;
                for (int j = 0; stacks != null && j < stacks.Length && j < tfs[i].pltArticles.Count; j++)
                {
                    if (stacks[j] != null && stacks[j].maxSize > 0 && stacks[j].totalCount < stacks[j].maxSize)
                    {
                        m.RestoreStack(stacks[j], tfs[i].pltArticles[j].platoonParameters);
                        n++;
                    }
                }
            }
            log("reinforced " + n + " stacks, OPs left " + m.phaseSave.playerSave.OPs);
        }

        // The regiment screen's forward button, which reads BACK TO MAP while managing.
        public static void BackToMap(Action<string> log)
        {
            birdseye.Regiments.Menu.GUI.CampaignMenu.ProcOpRegimentManagerPanel.active.toEventsButton.onClick.Invoke();
            log("back");
        }

        // Where the weakest formation with a roster is drawn, in game-window pixels from the top
        // left, for driving the real mouse at it.
        public static void WeakestOnScreen(Action<string> log)
        {
            TheaterScreen s = TheaterScreen.Instance;
            Core.Theater t = s.Campaign.theater;
            Core.Formation f = t.Alive(Core.Side.Blue).Where(x => s.Campaign.Force(x.id) != null).OrderBy(x => x.strength01).First();
            UnityEngine.Vector2 p = s.DevScreenPoint(f.id);
            log("at " + (int)p.x + " " + (int)(UnityEngine.Screen.height - p.y) + " screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + " " + f.name);
        }

        // Two single clicks 0.4 s apart on the weakest formation, through the event system.
        public static void DoubleClickWeakest(Action<string> log)
        {
            TheaterScreen s = TheaterScreen.Instance;
            Core.Theater t = s.Campaign.theater;
            Core.Formation f = t.Alive(Core.Side.Blue).Where(x => s.Campaign.Force(x.id) != null).OrderBy(x => x.strength01).First();
            s.DevDoubleClick(f.id, 0.4f, log);
            log("double-clicking " + f.name);
        }

        // The regiment screen's back arrow and Esc: a transition to the War Path phase list.
        public static void BackArrow(Action<string> log)
        {
            FadeGroupTransitionManager.active.TransitionTo(eCanvasMarkers.ProcOpPhases);
            log("back arrow");
        }

        public static void Status(Action<string> log)
        {
            ProcOpSaveSlotRecord slot = ProcOpSaveManager.active.GetSlot(eSaveSlot.Slot9);
            Campaign c = CampaignStore.Get(slot);
            Core.Theater t = c.theater;
            log("turn " + (t.turn + 1) + " phase " + t.phase + " active battle " + c.activeBattle + " pending " + Core.Rules.Pending(t).Count());
            foreach (Core.Battle b in t.battles)
            {
                log("  battle " + b.id + " " + b.resolution + " attackerWon=" + b.attackerWon + " foughtBy=" + b.foughtBy);
            }
            foreach (Core.Formation f in t.Alive(Core.Side.Blue))
            {
                log("  " + f.name + " " + (int)(f.strength01 * 100) + "% at " + t.Cell(f.cell).col + "," + t.Cell(f.cell).row);
            }
            log("slot phaseProgress " + slot.procOpSave.phaseProgress + ", in scene " + GameQueries.isInScene);
        }    }
}
