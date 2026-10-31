using System;
using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    public struct P2
    {
        public float x;
        public float y;

        public P2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public class Line
    {
        public List<P2> points = new List<P2>();
        public bool major;
    }

    /// <summary>
    /// Generated land: hills, forests, lakes, rivers, towns and the roads between them. The same
    /// seed always gives the same land, so only the seed is saved; the rules read the grid cells
    /// built from it and the map image is drawn from the same fields and lines.
    /// </summary>
    public class Terrain
    {
        public readonly int seed;
        public readonly int cols;
        public readonly int rows;
        public readonly float aspect;
        public readonly List<Line> rivers = new List<Line>();
        public readonly List<Line> roads = new List<Line>();
        public float forestLevel;
        public float waterLevel;

        public Terrain(int seed, int cols, int rows)
        {
            this.seed = seed;
            this.cols = cols;
            this.rows = rows;
            aspect = (float)cols / rows;
            // Levels chosen so about a third of the land is forest and a little is lake.
            var hs = new List<float>();
            var fs = new List<float>();
            for (int i = 0; i < 120; i++)
            {
                for (int j = 0; j < 60; j++)
                {
                    float x = (i + 0.5f) / 120f * aspect, y = (j + 0.5f) / 60f;
                    hs.Add(Height(x, y));
                    fs.Add(ForestValue(x, y));
                }
            }
            hs.Sort();
            fs.Sort();
            waterLevel = hs[(int)(hs.Count * 0.025f)];
            forestLevel = fs[(int)(fs.Count * 0.64f)];
        }

        public float Height(float x, float y) => Noise.Fbm(x * 2.1f + 13.7f, y * 2.1f + 5.3f, seed, 5);

        public float ForestValue(float x, float y) => Noise.Fbm(x * 7.5f, y * 7.5f, seed + 977, 4) + 0.35f * (Height(x, y) - 0.5f);

        public bool IsWater(float x, float y) => Height(x, y) < waterLevel;

        public bool IsForest(float x, float y) => ForestValue(x, y) > forestLevel;

        /// <summary>The land of a saved theater, rebuilt from its seed for drawing.</summary>
        public static Terrain Of(Theater t)
        {
            var terrain = new Terrain(t.seed, t.cols, t.rows);
            terrain.Build(new Theater { seed = t.seed }, t.towns.Count, i => "");
            return terrain;
        }

        public float CellX(int col) => (col + 0.5f) / cols * aspect;

        public float CellY(int row) => (row + 0.5f) / rows;

        /// <summary>Lay out the grid, rivers, towns and roads of a theater.</summary>
        public void Build(Theater t, int townCount, Func<int, string> townName)
        {
            var rng = new Random(seed);
            t.cols = cols;
            t.rows = rows;
            t.aspect = aspect;
            t.cells.Clear();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    t.cells.Add(new Cell { id = r * cols + c, col = c, row = r, terrain = Sample(c, r) });
                }
            }
            foreach (Cell cell in t.cells)
            {
                foreach (Cell n in new[] { t.CellAt(cell.col + 1, cell.row), t.CellAt(cell.col - 1, cell.row), t.CellAt(cell.col, cell.row + 1), t.CellAt(cell.col, cell.row - 1) })
                {
                    if (n != null)
                    {
                        cell.neighbors.Add(n.id);
                    }
                }
            }

            BuildRivers(t, rng);
            BuildTowns(t, rng, townCount, townName);
            BuildRoads(t);
        }

        private TerrainKind Sample(int c, int r)
        {
            int water = 0, forest = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float x = (c + (i + 0.5f) / 3f) / cols * aspect, y = (r + (j + 0.5f) / 3f) / rows;
                    if (IsWater(x, y)) water++;
                    else if (IsForest(x, y)) forest++;
                }
            }
            return water >= 5 ? TerrainKind.Water : forest >= 5 ? TerrainKind.Forest : TerrainKind.Open;
        }

        // --- Rivers: follow the valleys from one map edge to another --------------------------

        private void BuildRivers(Theater t, Random rng)
        {
            // A main river crossing north to south through the middle of the theater, and a
            // tributary from the west that joins it.
            int start = t.CellAt(cols * 2 / 5 + rng.Next(cols / 5), 0).id;
            var ends = new HashSet<int>(Enumerable.Range(0, cols).Select(c => t.CellAt(c, rows - 1).id));
            List<int> main = Valley(t, start, ends);
            var mainSet = new HashSet<int>(main);
            int tribStart = t.CellAt(0, rows / 4 + rng.Next(rows / 2)).id;
            List<int> trib = Valley(t, tribStart, mainSet);
            foreach (List<int> path in new[] { main, trib })
            {
                foreach (int id in path)
                {
                    t.cells[id].river = true;
                }
                rivers.Add(new Line { points = Smooth(Wiggle(path.Select(id => new P2(CellX(t.cells[id].col), CellY(t.cells[id].row))).ToList(), 0.35f), 3), major = path == main });
            }
        }

        private List<int> Valley(Theater t, int start, HashSet<int> targets)
        {
            return Route(t, start, targets, (from, to) => 1f + 14f * Height(CellX(t.cells[to].col), CellY(t.cells[to].row)), diagonal: false);
        }

        // --- Towns ---------------------------------------------------------------------------

        private void BuildTowns(Theater t, Random rng, int count, Func<int, string> townName)
        {
            t.towns.Clear();
            List<Cell> candidates = t.cells.Where(c => c.terrain != TerrainKind.Water && c.col > 0 && c.row > 0 && c.col < cols - 1 && c.row < rows - 1)
                .OrderBy(c => rng.NextDouble() - (c.terrain == TerrainKind.Open ? 0.25 : 0) - (c.river ? 0.2 : 0)).ToList();
            foreach (Cell c in candidates)
            {
                if (t.towns.Count >= count)
                {
                    break;
                }
                if (t.towns.Any(o => t.Distance(o.cell, c.id) < 4))
                {
                    continue;
                }
                int i = t.towns.Count;
                t.towns.Add(new Town
                {
                    id = i,
                    name = townName(i),
                    cell = c.id,
                    x = CellX(c.col) + (float)(rng.NextDouble() - 0.5) * 0.2f / rows,
                    y = CellY(c.row) + (float)(rng.NextDouble() - 0.5) * 0.2f / rows,
                    size = i < 3 ? 2 : i < 11 ? 1 : 0,
                });
                c.town = i;
                c.terrain = TerrainKind.Urban;
            }
        }

        // --- Roads: a network joining every town, with one motorway across ---------------------

        private void BuildRoads(Theater t)
        {
            List<Town> towns = t.towns;
            var edges = new List<int[]>();
            // Minimum spanning tree, then a link to each town's second-nearest neighbour for loops.
            var inTree = new HashSet<int> { 0 };
            while (inTree.Count < towns.Count)
            {
                int[] best = null;
                float bestD = float.MaxValue;
                foreach (int a in inTree)
                {
                    for (int b = 0; b < towns.Count; b++)
                    {
                        if (inTree.Contains(b)) continue;
                        float d = Dist(towns[a], towns[b]);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = new[] { a, b };
                        }
                    }
                }
                inTree.Add(best[1]);
                edges.Add(best);
            }
            foreach (Town a in towns)
            {
                Town b = towns.Where(o => o != a).OrderBy(o => Dist(a, o)).Skip(1).FirstOrDefault();
                if (b != null && !edges.Any(e => (e[0] == a.id && e[1] == b.id) || (e[0] == b.id && e[1] == a.id)))
                {
                    edges.Add(new[] { a.id, b.id });
                }
            }

            // The motorway crosses the whole theater: westernmost town to the middle city to the
            // easternmost town.
            List<Town> byX = towns.OrderBy(x => x.x).ToList();
            Town mid = towns.Where(x => x.size == 2).OrderBy(x => Math.Abs(x.x - aspect / 2)).FirstOrDefault() ?? byX[byX.Count / 2];
            RoadBetween(t, byX.First(), mid, major: true);
            RoadBetween(t, mid, byX.Last(), major: true);
            foreach (int[] e in edges.OrderBy(e => Dist(towns[e[0]], towns[e[1]])))
            {
                RoadBetween(t, towns[e[0]], towns[e[1]], major: false);
            }
            BuildRoadLines(t);
        }

        private void RoadBetween(Theater t, Town a, Town b, bool major)
        {
            List<int> path = Route(t, a.cell, new HashSet<int> { b.cell }, (from, to) =>
            {
                Cell c = t.cells[to];
                if (c.road) return 0.2f;
                float cost = c.terrain == TerrainKind.Water ? 30f : c.terrain == TerrainKind.Forest ? 2.2f : 1f;
                if (c.river) cost += 3f;
                float dh = Math.Abs(Height(CellX(c.col), CellY(c.row)) - Height(CellX(t.cells[from].col), CellY(t.cells[from].row)));
                return cost + dh * 25f;
            }, diagonal: true);
            // A diagonal step also paves one of the two cells beside it, so the road stays joined
            // for movement, which only goes square to square.
            var full = new List<int> { path[0] };
            for (int i = 1; i < path.Count; i++)
            {
                Cell p = t.cells[path[i - 1]], q = t.cells[path[i]];
                if (p.col != q.col && p.row != q.row)
                {
                    Cell side1 = t.CellAt(q.col, p.row), side2 = t.CellAt(p.col, q.row);
                    full.Add((side1.terrain != TerrainKind.Water ? side1 : side2).id);
                }
                full.Add(q.id);
            }
            foreach (int id in full)
            {
                if (t.cells[id].terrain != TerrainKind.Water)
                {
                    t.cells[id].road = true;
                }
            }
            for (int i = 1; i < path.Count; i++)
            {
                long key = EdgeKey(path[i - 1], path[i]);
                roadEdges[key] = major || (roadEdges.TryGetValue(key, out bool m) && m);
            }
        }

        // Road segments between cell centres; true for the motorway.
        private readonly Dictionary<long, bool> roadEdges = new Dictionary<long, bool>();

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // Join the segments into lines that run from junction to junction, so roads sharing a
        // stretch draw as one road that forks, instead of parallel strands.
        private void BuildRoadLines(Theater t)
        {
            // Where roads cut a corner beside another road, keep the two square sides and drop
            // the diagonal, so forks don't leave little triangles.
            foreach (long key in roadEdges.Keys.ToList())
            {
                int a = (int)(key >> 32), b = (int)(key & 0xFFFFFFFF);
                Cell ca = t.cells[a], cb = t.cells[b];
                if (ca.col == cb.col || ca.row == cb.row) continue;
                int s1 = t.CellAt(cb.col, ca.row).id, s2 = t.CellAt(ca.col, cb.row).id;
                foreach (int side in new[] { s1, s2 })
                {
                    if (roadEdges.ContainsKey(EdgeKey(a, side)) && roadEdges.ContainsKey(EdgeKey(side, b)))
                    {
                        bool major = roadEdges[key];
                        roadEdges.Remove(key);
                        roadEdges[EdgeKey(a, side)] |= major;
                        roadEdges[EdgeKey(side, b)] |= major;
                        break;
                    }
                }
            }
            var adj = new Dictionary<int, List<int>>();
            foreach (long key in roadEdges.Keys)
            {
                int a = (int)(key >> 32), b = (int)(key & 0xFFFFFFFF);
                if (!adj.ContainsKey(a)) adj[a] = new List<int>();
                if (!adj.ContainsKey(b)) adj[b] = new List<int>();
                adj[a].Add(b);
                adj[b].Add(a);
            }
            bool Junction(int n) => adj[n].Count != 2 || t.cells[n].town >= 0 || roadEdges[EdgeKey(n, adj[n][0])] != roadEdges[EdgeKey(n, adj[n][1])];
            var done = new HashSet<long>();
            foreach (int start in adj.Keys.Where(Junction).Concat(adj.Keys).ToList())
            {
                foreach (int first in adj[start])
                {
                    if (done.Contains(EdgeKey(start, first))) continue;
                    var chain = new List<int> { start };
                    int prev = start, cur = first;
                    done.Add(EdgeKey(prev, cur));
                    chain.Add(cur);
                    while (!Junction(cur) && cur != start)
                    {
                        int next = adj[cur][0] == prev ? adj[cur][1] : adj[cur][0];
                        if (!done.Add(EdgeKey(cur, next))) break;
                        prev = cur;
                        cur = next;
                        chain.Add(cur);
                    }
                    var pts = chain.Select(Point).ToList();
                    roads.Add(new Line { points = Smooth(pts, 3), major = roadEdges[EdgeKey(chain[0], chain[1])] });
                }
            }

            P2 Point(int id)
            {
                Cell c = t.cells[id];
                return c.town >= 0 ? new P2(t.towns[c.town].x, t.towns[c.town].y) : new P2(CellX(c.col), CellY(c.row));
            }
        }

        private static float Dist(Town a, Town b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));

        // Cheapest path on the grid from start to any target.
        private List<int> Route(Theater t, int start, HashSet<int> targets, Func<int, int, float> cost, bool diagonal)
        {
            int n = t.cells.Count;
            var dist = new float[n];
            var parent = new int[n];
            for (int i = 0; i < n; i++)
            {
                dist[i] = float.MaxValue;
                parent[i] = -1;
            }
            dist[start] = 0f;
            var open = new SortedSet<(float, int)> { (0f, start) };
            int found = -1;
            while (open.Count > 0)
            {
                (float d, int cur) = open.Min;
                open.Remove(open.Min);
                if (targets.Contains(cur) && cur != start)
                {
                    found = cur;
                    break;
                }
                Cell c = t.cells[cur];
                for (int dc = -1; dc <= 1; dc++)
                {
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        if ((dc == 0 && dr == 0) || (!diagonal && dc != 0 && dr != 0)) continue;
                        Cell nb = t.CellAt(c.col + dc, c.row + dr);
                        if (nb == null) continue;
                        float nd = d + cost(cur, nb.id) * (dc != 0 && dr != 0 ? 1.41f : 1f);
                        if (nd < dist[nb.id])
                        {
                            if (dist[nb.id] != float.MaxValue) open.Remove((dist[nb.id], nb.id));
                            dist[nb.id] = nd;
                            parent[nb.id] = cur;
                            open.Add((nd, nb.id));
                        }
                    }
                }
            }
            var path = new List<int>();
            for (int at = found; at != -1; at = parent[at])
            {
                path.Add(at);
            }
            path.Reverse();
            return path;
        }

        // Push the interior points of a cell-centre path sideways a little so lines don't follow the grid.
        private List<P2> Wiggle(List<P2> pts, float amount)
        {
            var output = new List<P2>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (i == 0 || i == pts.Count - 1)
                {
                    output.Add(pts[i]);
                    continue;
                }
                float jx = (Noise.Value(pts[i].x * 9f, pts[i].y * 9f, seed + 31) - 0.5f) * amount / rows * 2f;
                float jy = (Noise.Value(pts[i].x * 9f, pts[i].y * 9f, seed + 37) - 0.5f) * amount / rows * 2f;
                output.Add(new P2(pts[i].x + jx, pts[i].y + jy));
            }
            return output;
        }

        // Chaikin corner cutting.
        private static List<P2> Smooth(List<P2> pts, int passes)
        {
            for (int p = 0; p < passes; p++)
            {
                if (pts.Count < 3) break;
                var output = new List<P2> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    P2 a = pts[i], b = pts[i + 1];
                    output.Add(new P2(a.x * 0.75f + b.x * 0.25f, a.y * 0.75f + b.y * 0.25f));
                    output.Add(new P2(a.x * 0.25f + b.x * 0.75f, a.y * 0.25f + b.y * 0.75f));
                }
                output.Add(pts[pts.Count - 1]);
                pts = output;
            }
            return pts;
        }
    }
}
