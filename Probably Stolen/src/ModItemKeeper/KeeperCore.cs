using System;
using System.Collections.Generic;
using System.Linq;

namespace ModItemKeeper
{
    // How the core reads and writes one saved item node. The game's SaveItemNode in the mod,
    // a JSON-backed node in the offline tests.
    internal interface INodeAccess<T>
    {
        string Id(T node);
        long Uuid(T node);
        void SetUuid(T node, long uuid);
        int UniqueId(T node);
        List<long> Children(T node);
        void SetChildren(T node, List<long> children);
        List<int> ChildInventories(T node);
        void SetChildInventories(T node, List<int> inventories);
    }

    // The contents of one container whose mod is missing, cut out of the save list.
    // Nodes[0] is the container as saved (children intact); the rest are everything inside it.
    internal sealed class Cut<T>
    {
        public int ParentUniqueId;
        public string ParentId;
        public List<T> Nodes;
    }

    // Pure list surgery on the game's saved item graph (a flat list of nodes; each node lists
    // its children by uuid plus which of its inventories each child sits in). No game code, so
    // it can be tested offline against real saves.
    internal static class KeeperCore
    {
        // Cuts the contents out of every container the game can't build (its mod is missing):
        // the game would build it as an empty placeholder and drop everything inside. The
        // container node itself stays in the list, emptied, so its placeholder keeps its place.
        // Index 0 is the save's root bag and is never cut.
        internal static List<Cut<T>> CutUnknownContents<T>(List<T> nodes, INodeAccess<T> a, Func<T, bool> isKnown,
            Func<T, T> clone)
        {
            List<Cut<T>> cuts = new List<Cut<T>>();
            if (nodes == null || nodes.Count < 2)
                return cuts;

            Dictionary<long, T> byUuid = Index(nodes, a);
            HashSet<long> removed = new HashSet<long>();
            for (int i = 1; i < nodes.Count; i++)
            {
                T node = nodes[i];
                if (node == null || removed.Contains(a.Uuid(node)) || isKnown(node))
                    continue;
                List<long> children = a.Children(node);
                if (children == null || children.Count == 0)
                    continue;

                List<T> inside = Descendants(node, byUuid, a);
                Cut<T> cut = new Cut<T>
                {
                    ParentUniqueId = a.UniqueId(node),
                    ParentId = a.Id(node),
                    Nodes = new List<T> { clone(node) },
                };
                cut.Nodes.AddRange(inside);
                cuts.Add(cut);

                foreach (T d in inside)
                    removed.Add(a.Uuid(d));
                a.SetChildren(node, new List<long>());
                a.SetChildInventories(node, new List<int>());
            }
            if (removed.Count > 0)
                nodes.RemoveAll(n => n != null && removed.Contains(a.Uuid(n)));
            return cuts;
        }

        // Puts a cut's contents back into its container, which must be in this list, buildable
        // again (isKnown) and still empty. Uuids are renumbered past this list's, since the game
        // numbers them afresh on every save. Returns false and changes nothing otherwise.
        // On success the cut is used up: its nodes now belong to the list (renumbered).
        internal static bool Restore<T>(List<T> nodes, Cut<T> cut, INodeAccess<T> a, Func<T, bool> isKnown)
        {
            if (nodes == null || cut?.Nodes == null || cut.Nodes.Count == 0)
                return false;
            T parent = nodes.FirstOrDefault(n => n != null && a.UniqueId(n) == cut.ParentUniqueId && a.Id(n) == cut.ParentId);
            if (parent == null || !isKnown(parent))
                return false;
            if (a.Children(parent) != null && a.Children(parent).Count > 0)
                return false;

            T saved = cut.Nodes[0];
            List<T> inside = cut.Nodes.Skip(1).ToList();
            Dictionary<long, long> map = Renumber(inside, nodes, a);

            a.SetChildren(parent, (a.Children(saved) ?? new List<long>()).Select(u => map[u]).ToList());
            a.SetChildInventories(parent, new List<int>(a.ChildInventories(saved) ?? new List<int>()));
            nodes.AddRange(inside);
            return true;
        }

        // The cut's contents as separate trees, one per item that sat directly in the container:
        // each list starts with that item and holds everything inside it. Used when the container
        // itself is gone and its contents are put loose into the store.
        internal static List<List<T>> DirectChildTrees<T>(Cut<T> cut, INodeAccess<T> a)
        {
            List<List<T>> trees = new List<List<T>>();
            Dictionary<long, T> byUuid = Index(cut.Nodes.Skip(1), a);
            foreach (long uuid in a.Children(cut.Nodes[0]) ?? new List<long>())
            {
                if (!byUuid.TryGetValue(uuid, out T child))
                    continue;
                List<T> tree = new List<T> { child };
                tree.AddRange(Descendants(child, byUuid, a));
                trees.Add(tree);
            }
            return trees;
        }

        // Unique ids of the items that sat directly in the container.
        internal static List<int> DirectChildUniqueIds<T>(Cut<T> cut, INodeAccess<T> a)
        {
            Dictionary<long, T> byUuid = Index(cut.Nodes.Skip(1), a);
            return (a.Children(cut.Nodes[0]) ?? new List<long>())
                .Where(byUuid.ContainsKey).Select(u => a.UniqueId(byUuid[u])).ToList();
        }

        // Gives the nodes fresh uuids after the highest one in `existing` and rewrites their
        // child references; returns old -> new.
        internal static Dictionary<long, long> Renumber<T>(List<T> moving, IEnumerable<T> existing, INodeAccess<T> a)
        {
            long next = existing.Where(n => n != null).Select(a.Uuid).DefaultIfEmpty(-1).Max() + 1;
            Dictionary<long, long> map = new Dictionary<long, long>();
            foreach (T n in moving)
                map[a.Uuid(n)] = next++;
            foreach (T n in moving)
            {
                a.SetUuid(n, map[a.Uuid(n)]);
                List<long> children = a.Children(n);
                if (children != null)
                    a.SetChildren(n, children.Select(c => map.TryGetValue(c, out long m) ? m : c).ToList());
            }
            return map;
        }

        private static Dictionary<long, T> Index<T>(IEnumerable<T> nodes, INodeAccess<T> a)
        {
            Dictionary<long, T> byUuid = new Dictionary<long, T>();
            foreach (T n in nodes)
                if (n != null && !byUuid.ContainsKey(a.Uuid(n)))
                    byUuid.Add(a.Uuid(n), n);
            return byUuid;
        }

        // Everything inside a node, breadth first, each once (the graph should be a tree; a
        // cycle or a shared child is not followed twice).
        private static List<T> Descendants<T>(T node, Dictionary<long, T> byUuid, INodeAccess<T> a)
        {
            List<T> result = new List<T>();
            HashSet<long> seen = new HashSet<long> { a.Uuid(node) };
            Queue<T> queue = new Queue<T>();
            queue.Enqueue(node);
            while (queue.Count > 0)
            {
                T current = queue.Dequeue();
                foreach (long uuid in a.Children(current) ?? new List<long>())
                {
                    if (!seen.Add(uuid) || !byUuid.TryGetValue(uuid, out T child))
                        continue;
                    result.Add(child);
                    queue.Enqueue(child);
                }
            }
            return result;
        }
    }
}
