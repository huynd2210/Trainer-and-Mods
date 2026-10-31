using System;
using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    /// <summary>What each branch of service is good at: movement, attacking, holding close terrain.</summary>
    public class KindStats
    {
        public float mp;
        public float attack;
        // Defence multiplier in forest and towns, where infantry digs in and tanks are blind.
        public float closeDefense;
    }

    /// <summary>
    /// Battle odds and auto-resolve. One formula serves the odds shown on the map, the enemy
    /// AI's planning and the auto-resolve roll, so what the player is shown is what happens.
    /// </summary>
    public static class Combat
    {
        public const float DefenderBonus = 1.15f;
        public const float EntrenchPerLevel = 0.08f;
        public const float RiverBonus = 0.15f;
        public const float UnsuppliedFactor = 0.7f;
        public const float ExperiencePerLevel = 0.06f;
        public const float Sharpness = 2.2f;

        public static readonly Dictionary<UnitKind, KindStats> Kinds = new Dictionary<UnitKind, KindStats>
        {
            { UnitKind.Armour, new KindStats { mp = 4f, attack = 1.15f, closeDefense = 0.9f } },
            { UnitKind.Mechanized, new KindStats { mp = 4f, attack = 1f, closeDefense = 1f } },
            { UnitKind.Infantry, new KindStats { mp = 3f, attack = 0.9f, closeDefense = 1.15f } },
            { UnitKind.Recon, new KindStats { mp = 5f, attack = 0.75f, closeDefense = 0.85f } },
            { UnitKind.Airborne, new KindStats { mp = 3f, attack = 1f, closeDefense = 1.1f } },
        };

        // Extra defence the ground itself gives.
        public static readonly Dictionary<TerrainKind, float> Cover = new Dictionary<TerrainKind, float>
        {
            { TerrainKind.Open, 0f },
            { TerrainKind.Forest, 0.25f },
            { TerrainKind.Urban, 0.35f },
            { TerrainKind.Water, 0f },
        };

        public static float Power(Formation f)
        {
            float p = f.strength * (1f + ExperiencePerLevel * f.experience);
            return f.supplied ? p : p * UnsuppliedFactor;
        }

        public static float CoverOf(Theater t, int cell)
        {
            Cell c = t.Cell(cell);
            return Cover[c.terrain] + (c.river ? RiverBonus : 0f);
        }

        public static float AttackPower(IEnumerable<Formation> attackers) => attackers.Sum(f => Power(f) * Kinds[f.kind].attack);

        public static float DefensePower(Theater t, int cell, IEnumerable<Formation> defenders)
        {
            Cell c = t.Cell(cell);
            bool close = c.terrain == TerrainKind.Forest || c.terrain == TerrainKind.Urban;
            float sum = 0f;
            foreach (Formation f in defenders)
            {
                float mult = (DefenderBonus + EntrenchPerLevel * f.entrench + CoverOf(t, cell)) * (close ? Kinds[f.kind].closeDefense : 1f);
                sum += Power(f) * mult;
            }
            return sum;
        }

        public static float AttackerWinChance(float attack, float defense)
        {
            if (defense <= 0f)
            {
                return 1f;
            }
            if (attack <= 0f)
            {
                return 0f;
            }
            double r = Math.Pow(attack / defense, Sharpness);
            return (float)(r / (r + 1.0));
        }

        /// <summary>Odds for these attackers against whoever holds the cell.</summary>
        public static float AttackerWinChance(Theater t, IEnumerable<Formation> attackers, int cell)
        {
            Formation def = t.At(cell);
            return AttackerWinChance(AttackPower(attackers), def == null ? 0f : DefensePower(t, cell, new[] { def }));
        }

        public static float AttackerWinChance(Theater t, Battle b)
        {
            float atk = AttackPower(b.attackers.Select(t.Formation).Where(f => !f.destroyed));
            float def = DefensePower(t, b.cell, b.defenders.Select(t.Formation).Where(f => !f.destroyed));
            return AttackerWinChance(atk, def);
        }

        /// <summary>Expected strength fractions lost by each side, given who wins.</summary>
        public static void ExpectedLosses(float attackerWinChance, bool attackerWins, out float attackerLoss, out float defenderLoss)
        {
            // A lopsided fight costs the stronger side little; an even one bleeds both.
            float edge = attackerWins ? attackerWinChance : 1f - attackerWinChance;
            float winnerLoss = 0.06f + 0.16f * (1f - edge);
            float loserLoss = 0.22f + 0.18f * edge;
            attackerLoss = attackerWins ? winnerLoss : loserLoss;
            defenderLoss = attackerWins ? loserLoss : winnerLoss;
        }

        public static void AutoResolve(Theater t, Battle b)
        {
            float p = AttackerWinChance(t, b);
            Random rng = Dice.For(t, 7001, b.id);
            bool attackerWins = rng.NextDouble() < p;
            ExpectedLosses(p, attackerWins, out float aLoss, out float dLoss);
            // +-25% noise on losses so identical fights don't read as scripted.
            aLoss *= 0.75f + 0.5f * (float)rng.NextDouble();
            dLoss *= 0.75f + 0.5f * (float)rng.NextDouble();
            b.attackerWon = attackerWins;
            b.attackerLoss = aLoss;
            b.defenderLoss = dLoss;
            b.resolution = BattleResolution.AutoResolved;
        }
    }
}
