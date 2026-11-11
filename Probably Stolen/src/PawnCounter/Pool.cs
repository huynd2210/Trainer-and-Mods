using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawnCounter
{
    // The game side of each pawner kind, and the items they bring.
    internal static class Pool
    {
        // Pawned items take real room on your shelves: at least this many grid cells.
        internal const int MinCells = 6;

        private static readonly Dictionary<string, Action<StoreClient>> Factions = new Dictionary<string, Action<StoreClient>>
        {
            { "lower", StoreClientFactionSetup.InitLowerLevelCitizen },
            { "tech", StoreClientFactionSetup.InitWorker },
            { "upper", StoreClientFactionSetup.InitUpperLevelCitizen },
            { "shady", StoreClientFactionSetup.InitBlackMarket },
        };

        private static readonly Dictionary<string, Func<string>> Sprites = new Dictionary<string, Func<string>>
        {
            { "lower", SpriteDict.GetRandomLowerLevelSpriteName },
            { "tech", SpriteDict.GetRandomEngSpriteName },
            { "upper", SpriteDict.GetRandomUpperSpriteName },
            { "shady", SpriteDict.GetRandomShadySpriteName },
        };

        internal static void SetFaction(StoreClient client, string kind)
        {
            if (Factions.TryGetValue(kind, out Action<StoreClient> init))
                init(client);
            else
                StoreClientFactionSetup.InitLowerLevelCitizen(client);
        }

        internal static string RandomSprite(string kind)
        {
            return Sprites.TryGetValue(kind, out Func<string> f) ? f() : SpriteDict.GetRandomLowerLevelSpriteName();
        }

        // Ids already found too cheap or too small this session (logged once).
        private static readonly HashSet<string> rejected = new HashSet<string>();

        // A fresh item from the kind's list that is valuable and bulky enough, or null.
        // The list is curated; the check runs on the live item so a game update that makes
        // something cheap or small just drops it.
        internal static GameItem Spawn(IRng rng, PawnerKind kind, int minValue)
        {
            List<string> ids = kind.Items.Where(id => !rejected.Contains(id)).ToList();
            while (ids.Count > 0)
            {
                string id = ids[rng.Range(0, ids.Count - 1)];
                ids.Remove(id);
                GameItem item = null;
                // ItemSpawner also builds the game's pre-filled locked crates (loot_crate_*).
                try { item = ItemSpawner.Spawn(id); }
                catch (Exception e) { Debug.LogWarning("[PawnCounter] could not create " + id + ": " + e.Message); }
                if (item == null)
                {
                    rejected.Add(id);
                    continue;
                }
                int worth = Worth(item);
                int cells = Cells(item);
                if (worth >= minValue && cells >= MinCells)
                    return item;
                Debug.Log("[PawnCounter] skipping " + id + ": worth " + worth + ", " + cells + " cells");
                if (cells < MinCells)
                    rejected.Add(id);
                GraphUtils.DestroyIfUnplaced(item, "pawn pool reject");
            }
            return null;
        }

        // A locked crate's own value leaves out what is locked inside it; the owner knows
        // what they packed, so it counts here (loan, value floor, and what you owe if it's gone).
        internal static int Worth(GameItem item)
        {
            long value = item.GetCurrentValue();
            if (item.IsTag(LockedContainerTag))
                foreach (GameItem child in item.FindAllChildItems())
                    if (child.IsTag(BehindLockTag))
                        value += child.GetCurrentValue(withChild: false);
            return (int)Math.Max(0L, value);
        }

        internal const string LockedContainerTag = "LOCKED_CONTAINER_TAG";
        private const string BehindLockTag = "ITEM_BEHIND_LOCK_TAG";

        // A pawned crate you broke open can't go back as it came.
        internal static bool Opened(GameItem item)
        {
            return item.IsTag(LockedContainerTag) && !LockHelper.IsCurrentlyLocked(item);
        }

        internal static int Cells(GameItem item)
        {
            GridShape shape = item.shape;
            if (shape == null)
                return 0;
            int n = 0;
            for (int y = 0; y < shape.height; y++)
                for (int x = 0; x < shape.width; x++)
                    if (shape.GetLocal(x, y) != 0)
                        n++;
            return n;
        }
    }
}
