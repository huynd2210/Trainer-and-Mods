using System;
using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    /// <summary>
    /// Plays one side's moves on the grid. First it gangs formations up on enemy formations it
    /// can beat, best prize first; then the rest pull back to refit, garrison a threatened
    /// headquarters, or close on the objective field (enemy towns, weighted by their points,
    /// and the front) while preferring cover and not bunching. Side-agnostic: the simulator also
    /// uses it to play NATO.
    /// </summary>
    public static class StrategicAI
    {
        /// <param name="aggression">1 holds its line; higher attacks at worse odds and drives deeper.</param>
        public static void Play(Theater t, Side side, float aggression)
        {
            Side enemy = Theater.Other(side);
            Random rng = Dice.For(t, side == Side.Blue ? 11 : 13);
            var reach = new Dictionary<int, Reach>();
            foreach (Formation f in t.Alive(side))
            {
                reach[f.id] = Rules.Reachable(t, f);
            }

            // Refit the badly worn ones that are not in contact.
            foreach (Formation f in t.Alive(side))
            {
                bool contact = Rules.InEnemyZone(t, f.cell, side);
                if (f.supplied && !contact && f.strength01 < 0.55f)
                {
                    f.refit = true;
                }
                else if (f.strength01 >= 0.9f || contact)
                {
                    f.refit = false;
                }
            }

            PlanAttacks(t, side, aggression, reach);
            MoveRest(t, side, aggression, rng);
        }

        private static void PlanAttacks(Theater t, Side side, float aggression, Dictionary<int, Reach> reach)
        {
            float minChance = Math.Max(0.4f, Math.Min(0.75f, 0.66f - 0.14f * (aggression - 1f)));
            while (true)
            {
                var free = t.Alive(side).Where(f => !f.attacked && !f.refit && f.strength01 >= 0.35f).ToList();
                float bestScore = 0f;
                List<KeyValuePair<Formation, int>> best = null;
                int bestTarget = -1;
                foreach (Formation target in t.Alive(Theater.Other(side)))
                {
                    // Who can get next to it, each on its own cell.
                    var used = new HashSet<int>(t.formations.Where(f => !f.destroyed).Select(f => f.cell));
                    var group = new List<KeyValuePair<Formation, int>>();
                    Battle existing = t.BattleAt(target.cell);
                    List<Formation> already = existing == null ? new List<Formation>() : existing.attackers.Select(t.Formation).ToList();
                    foreach (Formation f in free.OrderByDescending(Combat.Power))
                    {
                        Reach r = Rules.Reachable(t, f);
                        reach[f.id] = r;
                        int from = Rules.AttackCells(t, f, target.cell, r)
                            .Where(c => c == f.cell || !used.Contains(c))
                            .OrderByDescending(c => c == f.cell ? 1f : 0f).ThenByDescending(c => Combat.CoverOf(t, c))
                            .DefaultIfEmpty(-1).First();
                        if (from < 0)
                        {
                            continue;
                        }
                        group.Add(new KeyValuePair<Formation, int>(f, from));
                        used.Add(from);
                        float pg = Combat.AttackerWinChance(t, already.Concat(group.Select(g => g.Key)), target.cell);
                        if (pg >= 0.85f)
                        {
                            break;
                        }
                    }
                    if (group.Count == 0)
                    {
                        continue;
                    }
                    float p = Combat.AttackerWinChance(t, already.Concat(group.Select(g => g.Key)), target.cell);
                    if (p < minChance)
                    {
                        continue;
                    }
                    Town town = t.TownAt(target.cell);
                    float prize = 1f + (1f - target.strength01) + (town != null ? town.vp * 0.5f : 0f) + (town != null && town.hq ? 4f : 0f);
                    // Fewer formations committed for the same odds is better.
                    float score = p * prize * aggression / (0.6f + 0.4f * group.Count);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = group;
                        bestTarget = target.cell;
                    }
                }
                if (best == null)
                {
                    return;
                }
                foreach (KeyValuePair<Formation, int> g in best)
                {
                    Rules.DeclareAttack(t, g.Key, bestTarget, g.Value, Rules.Reachable(t, g.Key));
                }
            }
        }

        private static void MoveRest(Theater t, Side side, float aggression, Random rng)
        {
            Side enemy = Theater.Other(side);
            float[] goal = ObjectiveField(t, side, aggression);
            Town hq = t.HQ(side);
            bool hqThreatened = t.Alive(enemy).Any(e => t.Distance(e.cell, hq.cell) <= 6);
            Formation hqGuard = hqThreatened
                ? t.Alive(side).Where(f => !f.attacked).OrderBy(f => t.Distance(f.cell, hq.cell)).FirstOrDefault()
                : null;

            // Those closest to the front go first, so the line forms before the reserves arrive.
            int[] frontDist = Supply.DistanceToFront(t, side);
            foreach (Formation f in t.Alive(side).Where(f => !f.attacked).OrderBy(f => frontDist[f.cell]).ToList())
            {
                Reach r = Rules.Reachable(t, f);
                float bestScore = float.MinValue;
                int bestCell = f.cell;
                foreach (KeyValuePair<int, float> kv in r.cost)
                {
                    int c = kv.Key;
                    if (c != f.cell && t.At(c) != null)
                    {
                        continue;
                    }
                    float score;
                    if (f == hqGuard)
                    {
                        score = -t.Distance(c, hq.cell) * 3f;
                    }
                    else if (f.refit)
                    {
                        // Stay put to refit unless in danger; then step back.
                        score = c == f.cell ? 1f : -1f;
                        score -= Danger(t, c, side) * 3f;
                    }
                    else
                    {
                        score = -goal[c];
                        score += Combat.CoverOf(t, c) * 2.5f;
                        float danger = Danger(t, c, side);
                        score -= danger * (1.6f - f.strength01) * 2f / aggression;
                        score -= t.Alive(side).Count(o => o != f && t.Distance(o.cell, c) <= 1) * 0.6f;
                        if (c == f.cell) score += 0.4f + 0.3f * f.entrench;
                    }
                    score += (float)rng.NextDouble() * 0.05f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCell = c;
                    }
                }
                if (bestCell != f.cell)
                {
                    Rules.Move(t, f, bestCell, r);
                }
            }
        }

        // Movement cost from every cell to the nearest objective: enemy towns (closer for more
        // points) and the cells along the front. A cautious side only seeks the front and its own
        // threatened towns.
        private static float[] ObjectiveField(Theater t, Side side, float aggression)
        {
            int n = t.cells.Count;
            var dist = Enumerable.Repeat(float.MaxValue, n).ToArray();
            var open = new SortedSet<(float, int)>();
            void Seed(int cell, float d)
            {
                if (d < dist[cell])
                {
                    if (dist[cell] != float.MaxValue) open.Remove((dist[cell], cell));
                    dist[cell] = d;
                    open.Add((d, cell));
                }
            }
            float push = Math.Max(0f, aggression - 1f);
            foreach (Town town in t.towns)
            {
                if (t.Cell(town.cell).owner != side)
                {
                    Seed(town.cell, Math.Max(0f, 6f - push * (town.vp * 2f + (town.hq ? 6f : 0f))));
                }
            }
            foreach (Cell c in t.cells)
            {
                if (c.owner == side && c.terrain != TerrainKind.Water && c.neighbors.Any(x => t.Cell(x).owner != side))
                {
                    Seed(c.id, 2f + push * 2f);
                }
            }
            while (open.Count > 0)
            {
                (float d, int cur) = open.Min;
                open.Remove(open.Min);
                foreach (int nb in t.Cell(cur).neighbors)
                {
                    if (t.Cell(nb).terrain == TerrainKind.Water) continue;
                    float nd = d + Rules.StepCost(t, nb, cur);
                    if (nd < dist[nb])
                    {
                        if (dist[nb] != float.MaxValue) open.Remove((dist[nb], nb));
                        dist[nb] = nd;
                        open.Add((nd, nb));
                    }
                }
            }
            return dist;
        }

        // Enemy attack power that could reach this cell next turn, relative to a full formation.
        private static float Danger(Theater t, int cell, Side side)
        {
            float sum = 0f;
            foreach (Formation e in t.Alive(Theater.Other(side)))
            {
                int d = t.Distance(e.cell, cell);
                if (d <= Combat.Kinds[e.kind].mp + 1)
                {
                    sum += Combat.Power(e) / 1000f * (d <= 1 ? 1f : 0.5f);
                }
            }
            return sum;
        }
    }
}
