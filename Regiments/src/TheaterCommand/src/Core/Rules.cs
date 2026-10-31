using System;
using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    /// <summary>
    /// What the game side does to real forces. The default only scales the abstract strength;
    /// the in-game adapter applies losses and refits to the player's actual platoon stacks.
    /// </summary>
    public interface IForceHooks
    {
        void ApplyLoss(Theater t, Formation f, float fraction, Battle b);
        void Refit(Theater t, Formation f);
        Formation Reinforce(Theater t, Side side, int cell);
    }

    public class AbstractForceHooks : IForceHooks
    {
        public virtual void ApplyLoss(Theater t, Formation f, float fraction, Battle b)
        {
            f.strength = Math.Max(0f, f.strength * (1f - fraction));
        }

        public virtual void Refit(Theater t, Formation f)
        {
            f.strength = Math.Min(f.maxStrength, f.strength + f.maxStrength * Rules.RefitFraction);
        }

        public virtual Formation Reinforce(Theater t, Side side, int cell)
        {
            float str = t.Alive(side).Select(x => x.maxStrength).DefaultIfEmpty(1000f).Average();
            var f = new Formation
            {
                id = t.nextFormationId++,
                side = side,
                name = (side == Side.Blue ? "Blue reserve " : "Red reserve ") + t.nextFormationId,
                cell = cell,
                kind = UnitKind.Mechanized,
                size = UnitSize.Regiment,
                strength = str,
                maxStrength = str,
            };
            f.maxMp = Combat.Kinds[f.kind].mp;
            t.formations.Add(f);
            return f;
        }
    }

    /// <summary>Where a formation can get to this phase: cost and the previous cell of the cheapest path.</summary>
    public class Reach
    {
        public readonly Dictionary<int, float> cost = new Dictionary<int, float>();
        public readonly Dictionary<int, int> prev = new Dictionary<int, int>();

        public bool Has(int cell) => cost.ContainsKey(cell);

        public List<int> PathTo(int cell)
        {
            var path = new List<int>();
            for (int at = cell; ; at = prev[at])
            {
                path.Add(at);
                if (!prev.ContainsKey(at))
                {
                    break;
                }
            }
            path.Reverse();
            return path;
        }
    }

    /// <summary>
    /// The turn. Your formations move and declare attacks; your attacks are fought or
    /// auto-resolved; the enemy moves and declares; its attacks are fought or auto-resolved;
    /// then refit, entrenchment, supply, victory points and reinforcements.
    /// </summary>
    public static class Rules
    {
        public const float RefitFraction = 0.25f;
        public const float DestroyedBelow = 0.12f;
        public const float UnsuppliedAttrition = 0.05f;
        public const float RoadCost = 0.5f;
        public const float RiverCost = 1f;

        public static readonly Dictionary<TerrainKind, float> MoveCost = new Dictionary<TerrainKind, float>
        {
            { TerrainKind.Open, 1f },
            { TerrainKind.Forest, 2f },
            { TerrainKind.Urban, 1f },
            { TerrainKind.Water, float.PositiveInfinity },
        };

        public static Side Enemy(Theater t) => Theater.Other(t.playerSide);

        // --- Movement ------------------------------------------------------------------------

        public static float StepCost(Theater t, int from, int to)
        {
            Cell a = t.Cell(from), b = t.Cell(to);
            if (a.road && b.road)
            {
                return RoadCost;
            }
            return MoveCost[b.terrain] + (b.river && !a.river ? RiverCost : 0f);
        }

        /// <summary>A cell next to an enemy formation: moving into it ends the move.</summary>
        public static bool InEnemyZone(Theater t, int cell, Side side) => t.Cell(cell).neighbors.Any(n => t.EnemyAt(n, side));

        public static Reach Reachable(Theater t, Formation f)
        {
            var reach = new Reach();
            reach.cost[f.cell] = 0f;
            var open = new SortedSet<(float, int)> { (0f, f.cell) };
            while (open.Count > 0)
            {
                (float d, int cur) = open.Min;
                open.Remove(open.Min);
                if (cur != f.cell && InEnemyZone(t, cur, f.side))
                {
                    continue;
                }
                foreach (int n in t.Cell(cur).neighbors)
                {
                    if (t.Cell(n).terrain == TerrainKind.Water || t.EnemyAt(n, f.side))
                    {
                        continue;
                    }
                    float nd = d + StepCost(t, cur, n);
                    if (nd > f.mp + 1e-4f)
                    {
                        continue;
                    }
                    if (!reach.cost.TryGetValue(n, out float old) || nd < old)
                    {
                        if (reach.cost.ContainsKey(n))
                        {
                            open.Remove((old, n));
                        }
                        reach.cost[n] = nd;
                        reach.prev[n] = cur;
                        open.Add((nd, n));
                    }
                }
            }
            return reach;
        }

        public static bool CanAct(Theater t, Formation f, out string why)
        {
            why = "";
            if (f.destroyed) why = "destroyed";
            else if (t.finished) why = "the campaign is over";
            else if (f.side != t.acting || (t.phase != Phase.PlayerMoves && f.side == t.playerSide)) why = "not your move";
            else if (f.attacked) why = "already attacked this turn";
            return why == "";
        }

        public static bool CanMove(Theater t, Formation f, int cell, Reach reach, out string why)
        {
            if (!CanAct(t, f, out why))
            {
                return false;
            }
            Formation there = t.At(cell);
            if (cell == f.cell) why = "already there";
            else if (!reach.Has(cell)) why = "out of reach";
            else if (there != null) why = "occupied";
            return why == "";
        }

        public static MoveRecord Move(Theater t, Formation f, int cell, Reach reach = null)
        {
            reach = reach ?? Reachable(t, f);
            if (!CanMove(t, f, cell, reach, out string why))
            {
                throw new InvalidOperationException(f.name + ": " + why);
            }
            MoveRecord rec = Record(f);
            foreach (int c in reach.PathTo(cell).Skip(1))
            {
                if (t.Cell(c).owner != f.side && t.At(c) == null)
                {
                    t.Cell(c).owner = f.side;
                    rec.flipped.Add(c);
                }
            }
            f.mp -= reach.cost[cell];
            f.cell = cell;
            f.moved = true;
            f.entrench = 0;
            f.refit = false;
            t.moves.Add(rec);
            return rec;
        }

        private static MoveRecord Record(Formation f) => new MoveRecord
        {
            formation = f.id,
            from = f.cell,
            mp = f.mp,
            moved = f.moved,
            entrench = f.entrench,
            refit = f.refit,
            attacked = f.attacked,
        };

        public static bool CanUndo(Theater t) => t.moves.Count > 0 && t.acting == t.playerSide && t.phase == Phase.PlayerMoves;

        /// <summary>Take back the last move or attack order this phase.</summary>
        public static void Undo(Theater t)
        {
            if (!CanUndo(t))
            {
                return;
            }
            MoveRecord rec = t.moves[t.moves.Count - 1];
            t.moves.RemoveAt(t.moves.Count - 1);
            Formation f = t.Formation(rec.formation);
            if (rec.battle >= 0)
            {
                Battle b = t.battles.First(x => x.id == rec.battle);
                b.attackers.Remove(f.id);
                if (rec.createdBattle || b.attackers.Count == 0)
                {
                    t.battles.Remove(b);
                }
            }
            foreach (int c in rec.flipped)
            {
                t.Cell(c).owner = Theater.Other(f.side);
            }
            f.cell = rec.from;
            f.mp = rec.mp;
            f.moved = rec.moved;
            f.entrench = rec.entrench;
            f.refit = rec.refit;
            f.attacked = rec.attacked;
        }

        public static void SetRefit(Theater t, Formation f, bool on)
        {
            if (!CanAct(t, f, out _) || f.moved)
            {
                return;
            }
            f.refit = on;
        }

        // --- Attacks -------------------------------------------------------------------------

        /// <summary>Free cells this formation can reach and attack the target cell from.</summary>
        public static List<int> AttackCells(Theater t, Formation f, int target, Reach reach)
        {
            return t.Cell(target).neighbors
                .Where(c => reach.Has(c) && (c == f.cell || t.At(c) == null))
                .ToList();
        }

        public static bool CanAttack(Theater t, Formation f, int target, Reach reach, out string why)
        {
            if (!CanAct(t, f, out why))
            {
                return false;
            }
            if (!t.EnemyAt(target, f.side)) why = "no enemy there";
            else if (f.refit) why = "refitting";
            else if (AttackCells(t, f, target, reach).Count == 0) why = "can't get next to it";
            return why == "";
        }

        /// <summary>The cell to attack from: stay if already adjacent, else the best-covered reachable cell.</summary>
        public static int BestAttackCell(Theater t, Formation f, int target, Reach reach)
        {
            List<int> cells = AttackCells(t, f, target, reach);
            if (cells.Contains(f.cell))
            {
                return f.cell;
            }
            return cells.OrderByDescending(c => Combat.CoverOf(t, c)).ThenBy(c => reach.cost[c]).First();
        }

        /// <summary>Move next to the target if needed and join (or open) the attack on it.</summary>
        public static Battle DeclareAttack(Theater t, Formation f, int target, int from = -1, Reach reach = null)
        {
            reach = reach ?? Reachable(t, f);
            if (!CanAttack(t, f, target, reach, out string why))
            {
                throw new InvalidOperationException(f.name + ": " + why);
            }
            from = from >= 0 ? from : BestAttackCell(t, f, target, reach);
            MoveRecord rec;
            if (from != f.cell)
            {
                rec = Move(t, f, from, reach);
                // Moving and attacking undo together.
                t.moves.RemoveAt(t.moves.Count - 1);
            }
            else
            {
                rec = Record(f);
            }
            Battle b = t.BattleAt(target);
            if (b == null)
            {
                b = new Battle
                {
                    id = t.nextBattleId++,
                    cell = target,
                    attackerSide = f.side,
                    defenders = new List<int> { t.At(target).id },
                };
                t.battles.Add(b);
                rec.createdBattle = true;
            }
            b.attackers.Add(f.id);
            rec.battle = b.id;
            f.attacked = true;
            f.mp = 0f;
            f.refit = false;
            t.moves.Add(rec);
            return b;
        }

        // --- Phases --------------------------------------------------------------------------

        public static IEnumerable<Battle> Pending(Theater t) => t.battles.Where(b => b.resolution == BattleResolution.Pending);

        public static void BeginSide(Theater t, Side side)
        {
            t.acting = side;
            t.moves.Clear();
            foreach (Formation f in t.Alive(side))
            {
                f.maxMp = Combat.Kinds[f.kind].mp;
                f.mp = f.supplied ? f.maxMp : Math.Max(1f, f.maxMp - 1f);
                f.moved = false;
                f.attacked = false;
            }
        }

        /// <summary>Start of a campaign or of a new turn: the player is to move.</summary>
        public static void BeginTurn(Theater t)
        {
            t.battles.Clear();
            foreach (Formation f in t.formations)
            {
                f.fought = false;
            }
            t.phase = Phase.PlayerMoves;
            BeginSide(t, t.playerSide);
        }

        /// <summary>
        /// The player is done with this step. Moves → their battles (if any) → the enemy moves
        /// → its battles (if any) → end of turn → the player's next moves. Stops wherever battles
        /// wait for a decision.
        /// </summary>
        public static void Advance(Theater t, IForceHooks hooks)
        {
            // Attacks declared during the moves are what the battles step is for.
            if (t.finished || (t.phase != Phase.PlayerMoves && Pending(t).Any()))
            {
                return;
            }
            if (t.phase == Phase.PlayerMoves)
            {
                SettleFront(t);
                t.phase = Phase.PlayerBattles;
                t.moves.Clear();
                if (Pending(t).Any())
                {
                    return;
                }
            }
            if (t.phase == Phase.PlayerBattles)
            {
                if (t.finished) return;
                BeginSide(t, Enemy(t));
                Dictionary<int, int> before = t.Alive(Enemy(t)).ToDictionary(f => f.id, f => f.cell);
                StrategicAI.Play(t, Enemy(t), t.aiAggression);
                t.enemyMoves = t.Alive(Enemy(t)).Where(f => before.ContainsKey(f.id) && before[f.id] != f.cell)
                    .Select(f => new[] { before[f.id], f.cell }).ToList();
                SettleFront(t);
                t.acting = t.playerSide;
                t.moves.Clear();
                t.phase = Phase.EnemyBattles;
                if (Pending(t).Any())
                {
                    return;
                }
            }
            if (t.phase == Phase.EnemyBattles)
            {
                if (t.finished) return;
                EndTurn(t, hooks);
                if (!t.finished)
                {
                    BeginTurn(t);
                }
            }
        }

        /// <summary>Apply a battle the moment it is decided, fought or auto-resolved.</summary>
        public static void ApplyBattle(Theater t, Battle b, IForceHooks hooks)
        {
            List<Formation> atk = b.attackers.Select(t.Formation).Where(f => !f.destroyed).ToList();
            List<Formation> def = b.defenders.Select(t.Formation).Where(f => !f.destroyed).ToList();
            foreach (Formation f in atk.Concat(def))
            {
                f.fought = true;
                f.refit = false;
            }
            foreach (Formation f in atk) hooks.ApplyLoss(t, f, b.attackerLoss, b);
            foreach (Formation f in def) hooks.ApplyLoss(t, f, b.defenderLoss, b);
            List<Formation> winners = b.attackerWon ? atk : def;
            Random rng = Dice.For(t, 9001, b.id);
            foreach (Formation w in winners.Where(w => w.experience < 3 && rng.NextDouble() < 0.35))
            {
                w.experience++;
            }

            string place = t.NearestTown(b.cell).name;
            Side winSide = b.attackerWon ? b.attackerSide : Theater.Other(b.attackerSide);
            t.Log(SideName(winSide) + " wins the battle at " + place);
            DestroyBroken(t);

            if (b.attackerWon)
            {
                foreach (Formation l in def.Where(l => !l.destroyed))
                {
                    Retreat(t, l, atk);
                }
                Formation lead = atk.Where(w => !w.destroyed).OrderByDescending(Combat.Power).FirstOrDefault();
                if (lead != null && t.At(b.cell) == null)
                {
                    lead.cell = b.cell;
                    lead.entrench = 0;
                    t.Cell(b.cell).owner = lead.side;
                }
            }
            CheckVictory(t);
        }

        public static void AutoResolve(Theater t, Battle b, IForceHooks hooks)
        {
            Combat.AutoResolve(t, b);
            ApplyBattle(t, b, hooks);
        }

        // Fall back to a free neighbouring cell of its own side, as far from the attackers as
        // possible; with nowhere to go the formation is overrun.
        private static void Retreat(Theater t, Formation f, List<Formation> attackers)
        {
            // Up to two cells back, through its own land and past friends but not enemies.
            var near = new HashSet<int>();
            foreach (int a in t.Cell(f.cell).neighbors)
            {
                if (t.Cell(a).owner != f.side || t.Cell(a).terrain == TerrainKind.Water || t.EnemyAt(a, f.side)) continue;
                near.Add(a);
                foreach (int b in t.Cell(a).neighbors)
                {
                    if (b != f.cell && t.Cell(b).owner == f.side && t.Cell(b).terrain != TerrainKind.Water) near.Add(b);
                }
            }
            List<int> options = near
                .Where(c => t.At(c) == null)
                .OrderByDescending(c => attackers.Sum(a => t.Distance(a.cell, c)))
                .ThenBy(c => t.Cell(c).neighbors.Count(n => t.EnemyAt(n, f.side)))
                .ToList();
            if (options.Count == 0)
            {
                f.destroyed = true;
                t.Log(f.name + " is cut off and overrun");
                return;
            }
            f.cell = options[0];
            f.entrench = 0;
            t.Log(f.name + " falls back");
        }

        private static void DestroyBroken(Theater t)
        {
            foreach (Formation f in t.formations.Where(f => !f.destroyed && f.strength01 < DestroyedBelow))
            {
                f.destroyed = true;
                t.Log(f.name + " is no longer combat effective");
            }
        }

        /// <summary>
        /// Land follows the formations: an empty cell touched by formations of only one side goes
        /// to that side, and a pocket mostly surrounded by the other side's land goes with it.
        /// </summary>
        public static void SettleFront(Theater t)
        {
            var hqCells = new HashSet<int>(t.towns.Where(x => x.hq).Select(x => x.cell));
            foreach (Cell c in t.cells)
            {
                if (c.terrain == TerrainKind.Water || t.At(c.id) != null || hqCells.Contains(c.id))
                {
                    continue;
                }
                bool blue = c.neighbors.Any(n => t.At(n)?.side == Side.Blue);
                bool red = c.neighbors.Any(n => t.At(n)?.side == Side.Red);
                if (blue != red)
                {
                    c.owner = blue ? Side.Blue : Side.Red;
                }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Cell c in t.cells)
                {
                    if (t.At(c.id) != null || hqCells.Contains(c.id))
                    {
                        continue;
                    }
                    int other = c.neighbors.Count(n => t.Cell(n).owner != c.owner);
                    if (other >= 3 || (other == c.neighbors.Count && other > 0))
                    {
                        c.owner = Theater.Other(c.owner);
                    }
                }
            }
        }

        private static void EndTurn(Theater t, IForceHooks hooks)
        {
            Supply.Update(t);
            foreach (Formation f in t.formations.Where(f => !f.destroyed))
            {
                if (f.refit && f.supplied && !f.fought && !f.moved)
                {
                    hooks.Refit(t, f);
                }
                if (!f.moved && !f.fought)
                {
                    f.entrench = Math.Min(3, f.entrench + 1);
                }
            }
            foreach (Formation f in t.formations.Where(f => !f.destroyed && !f.supplied))
            {
                hooks.ApplyLoss(t, f, UnsuppliedAttrition, null);
            }
            DestroyBroken(t);

            foreach (Town town in t.towns)
            {
                if (t.Cell(town.cell).owner == Side.Blue) t.blueVP += town.vp;
                else t.redVP += town.vp;
            }

            if (t.reinforcementTurns.Contains(t.turn + 1))
            {
                foreach (Side side in new[] { Side.Blue, Side.Red })
                {
                    Town hq = t.HQ(side);
                    if (t.Cell(hq.cell).owner != side)
                    {
                        continue;
                    }
                    int cell = NearestFree(t, hq.cell, side);
                    if (cell >= 0)
                    {
                        Formation f = hooks.Reinforce(t, side, cell);
                        t.Log(f.name + " arrives at " + hq.name);
                    }
                }
            }

            CheckVictory(t);
            t.turn++;
            if (!t.finished && t.turn >= t.maxTurns)
            {
                t.finished = true;
                t.winner = t.blueVP > t.redVP ? Side.Blue : t.redVP > t.blueVP ? Side.Red : Theater.Other(t.playerSide);
                t.endReason = "Time is up: " + t.blueVP + " to " + t.redVP + " victory points";
            }
            Supply.Update(t);
        }

        /// <summary>The nearest empty land cell of this side, or -1.</summary>
        public static int NearestFree(Theater t, int from, Side side)
        {
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                Cell c = t.Cell(cur);
                if (c.owner == side && c.terrain != TerrainKind.Water && t.At(cur) == null)
                {
                    return cur;
                }
                foreach (int n in c.neighbors)
                {
                    if (t.Cell(n).owner == side && seen.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
            return -1;
        }

        private static void CheckVictory(Theater t)
        {
            if (t.finished)
            {
                return;
            }
            foreach (Side side in new[] { Side.Blue, Side.Red })
            {
                Town hq = t.HQ(side);
                if (t.Cell(hq.cell).owner != side)
                {
                    t.finished = true;
                    t.winner = Theater.Other(side);
                    t.endReason = SideName(side) + " headquarters at " + hq.name + " has fallen";
                    return;
                }
                if (!t.Alive(side).Any())
                {
                    t.finished = true;
                    t.winner = Theater.Other(side);
                    t.endReason = SideName(side) + " has no formations left";
                    return;
                }
            }
        }

        public static string SideName(Side s) => s == Side.Blue ? "NATO" : "Pact";
    }
}
