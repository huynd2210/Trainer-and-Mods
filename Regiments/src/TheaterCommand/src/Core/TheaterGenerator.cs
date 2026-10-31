using System;
using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    /// <summary>What a formation is when the campaign starts.</summary>
    public class FormationSpec
    {
        public string name;
        public UnitKind kind = UnitKind.Mechanized;
        public UnitSize size = UnitSize.Regiment;
        public float strength = 1000f;
        public int forceRef;
    }

    public class GeneratorSettings
    {
        public int cols = 40;
        public int rows = 20;
        public int towns = 24;
        public int blueFormations = 12;
        public int redFormations = 14;
        public int maxTurns = 12;
        public Func<int, string> townName = i => TownNames.Default(i);
        public Func<Side, int, FormationSpec> formation = DefaultFormation;

        private static readonly UnitKind[] KindCycle = { UnitKind.Mechanized, UnitKind.Armour, UnitKind.Mechanized, UnitKind.Infantry, UnitKind.Armour, UnitKind.Recon, UnitKind.Mechanized, UnitKind.Airborne };

        public static FormationSpec DefaultFormation(Side side, int i) => new FormationSpec
        {
            name = TownNames.Ordinal(i + 1) + (side == Side.Blue ? " NATO" : " Pact"),
            kind = KindCycle[i % KindCycle.Length],
            forceRef = i,
        };
    }

    /// <summary>
    /// Builds a theater: generated land on a square grid, NATO holding the west and the Pact the
    /// east along a ragged front, a headquarters town deep on each side, every town worth
    /// victory points, and both armies deployed a little back from the line.
    /// </summary>
    public static class TheaterGenerator
    {
        public static Theater Generate(int seed, GeneratorSettings s)
        {
            var rng = new Random(seed);
            var t = new Theater { seed = seed, maxTurns = s.maxTurns };
            var terrain = new Terrain(seed, s.cols, s.rows);
            terrain.Build(t, s.towns, s.townName);

            // A front line wandering a few cells either side of the middle.
            for (int r = 0; r < t.rows; r++)
            {
                int line = t.cols / 2 + (int)Math.Round((Noise.Fbm(r * 0.18f, 3.3f, seed + 77, 3) - 0.5f) * 10f);
                for (int c = 0; c < t.cols; c++)
                {
                    t.CellAt(c, r).owner = c < line ? Side.Blue : Side.Red;
                }
            }

            foreach (Town town in t.towns)
            {
                town.vp = town.size + 1;
            }
            MakeHQ(t, Side.Blue, x => x.x);
            MakeHQ(t, Side.Red, x => -x.x);

            Deploy(t, Side.Blue, s.blueFormations, s, rng);
            Deploy(t, Side.Red, s.redFormations, s, rng);
            Supply.Update(t);
            Rules.BeginTurn(t);
            return t;
        }

        // The headquarters is the biggest town far behind the line.
        private static void MakeHQ(Theater t, Side side, Func<Town, float> depth)
        {
            List<Town> own = t.towns.Where(x => t.Cell(x.cell).owner == side).OrderBy(depth).ToList();
            Town hq = own.Take(Math.Max(1, own.Count / 3)).OrderByDescending(x => x.size).ThenBy(depth).First();
            hq.hq = true;
            hq.hqSide = side;
            hq.vp += 2;
        }

        // Two thirds spread along the whole front one cell back from it, the rest in the towns
        // behind; one always holds the headquarters.
        private static void Deploy(Theater t, Side side, int count, GeneratorSettings s, Random rng)
        {
            int[] dist = Supply.DistanceToFront(t, side);
            Town hq = t.HQ(side);
            var slots = new List<int> { hq.cell };
            int frontCount = Math.Max(1, count * 2 / 3);
            for (int i = 0; i < frontCount; i++)
            {
                int row = (int)((i + 0.5f) * t.rows / frontCount);
                int pick = t.cells
                    .Where(c => c.owner == side && c.terrain != TerrainKind.Water && dist[c.id] >= 1 && dist[c.id] <= 2 && !slots.Contains(c.id))
                    .OrderBy(c => Math.Abs(c.row - row) * 3 + dist[c.id] - Combat.CoverOf(t, c.id) * 2 + rng.NextDouble())
                    .Select(c => c.id).DefaultIfEmpty(-1).First();
                if (pick >= 0) slots.Add(pick);
            }
            List<int> rear = t.towns.Where(x => t.Cell(x.cell).owner == side && !slots.Contains(x.cell) && dist[x.cell] >= 3)
                .OrderBy(x => dist[x.cell]).Select(x => x.cell).ToList();
            foreach (int cell in rear)
            {
                if (slots.Count >= count) break;
                slots.Add(cell);
            }
            while (slots.Count < count)
            {
                int cell = t.cells.Where(c => c.owner == side && c.terrain != TerrainKind.Water && dist[c.id] >= 2 && !slots.Contains(c.id))
                    .OrderBy(_ => rng.Next()).Select(c => c.id).First();
                slots.Add(cell);
            }

            for (int i = 0; i < count; i++)
            {
                FormationSpec spec = s.formation(side, i);
                var f = new Formation
                {
                    id = t.nextFormationId++,
                    side = side,
                    name = spec.name,
                    kind = spec.kind,
                    size = spec.size,
                    cell = slots[i],
                    strength = spec.strength,
                    maxStrength = spec.strength,
                    forceRef = spec.forceRef,
                    entrench = 1,
                };
                f.maxMp = Combat.Kinds[f.kind].mp;
                f.mp = f.maxMp;
                t.formations.Add(f);
            }
        }
    }

    public static class TownNames
    {
        // Place names of the kind found around the Fulda Gap.
        private static readonly string[] Names =
        {
            "Hersfeld", "Hünfeld", "Rasdorf", "Tann", "Hilders", "Geisa", "Vacha", "Philippsthal",
            "Schenklengsfeld", "Eiterfeld", "Burghaun", "Neuhof", "Eichenzell", "Petersberg", "Künzell", "Gersfeld",
            "Poppenhausen", "Dermbach", "Kaltennordheim", "Breitungen", "Heringen", "Friedewald", "Niederaula", "Kirchheim",
            "Schlitz", "Lauterbach", "Grebenhain", "Flieden", "Kalbach", "Ebersburg", "Wildflecken", "Bischofsheim",
        };

        public static string Default(int i) => i < Names.Length ? Names[i] : "Ort " + (i + 1);

        public static string Ordinal(int n)
        {
            string suffix = (n % 100 >= 11 && n % 100 <= 13) ? "th" : (n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th");
            return n + suffix;
        }
    }
}
