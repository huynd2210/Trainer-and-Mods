using System.Collections.Generic;
using System.Linq;

namespace TheaterCommand.Core
{
    /// <summary>A cell is supplied for a side if friendly-held land links it to that side's HQ.</summary>
    public static class Supply
    {
        public static HashSet<int> SuppliedCells(Theater t, Side side)
        {
            var result = new HashSet<int>();
            Town hq = t.HQ(side);
            if (hq == null || t.Cell(hq.cell).owner != side)
            {
                return result;
            }
            var queue = new Queue<int>();
            queue.Enqueue(hq.cell);
            result.Add(hq.cell);
            while (queue.Count > 0)
            {
                foreach (int n in t.Cell(queue.Dequeue()).neighbors)
                {
                    Cell c = t.Cell(n);
                    if (c.owner == side && c.terrain != TerrainKind.Water && result.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
            return result;
        }

        public static void Update(Theater t)
        {
            HashSet<int> blue = SuppliedCells(t, Side.Blue);
            HashSet<int> red = SuppliedCells(t, Side.Red);
            foreach (Formation f in t.formations.Where(f => !f.destroyed))
            {
                f.supplied = (f.side == Side.Blue ? blue : red).Contains(f.cell);
            }
        }

        /// <summary>Steps from each cell to the nearest cell of this side bordering enemy land.</summary>
        public static int[] DistanceToFront(Theater t, Side side)
        {
            var dist = Enumerable.Repeat(int.MaxValue, t.cells.Count).ToArray();
            var queue = new Queue<int>();
            foreach (Cell c in t.cells)
            {
                if (c.owner == side && c.terrain != TerrainKind.Water && c.neighbors.Any(n => t.Cell(n).owner != side))
                {
                    dist[c.id] = 0;
                    queue.Enqueue(c.id);
                }
            }
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (int n in t.Cell(cur).neighbors)
                {
                    if (dist[n] == int.MaxValue)
                    {
                        dist[n] = dist[cur] + 1;
                        queue.Enqueue(n);
                    }
                }
            }
            return dist;
        }
    }
}
