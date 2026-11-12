using System;
using System.Collections.Generic;
using System.Globalization;
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

        // A fresh item the pawner brings, valuable and bulky enough, or null. Boxes come first
        // as often as the box chance says; either list falls back to the other.
        internal static GameItem Spawn(IRng rng, PawnerKind kind, int minValue)
        {
            bool box = rng.Range(1, 100) <= PawnCounterMod.BoxChance;
            string[] first = box ? kind.Boxes : kind.Items;
            string[] second = box ? kind.Items : kind.Boxes;
            return SpawnFrom(rng, first, minValue) ?? SpawnFrom(rng, second, minValue);
        }

        // The lists are curated; the check runs on the live item so a game update that makes
        // something cheap or small just drops it.
        private static GameItem SpawnFrom(IRng rng, string[] list, int minValue)
        {
            List<string> ids = list.Where(id => !rejected.Contains(id)).ToList();
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
                if (IsBox(item))
                    PrepareBox(rng, item);
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

        // A box: one of the game's crates, locked or not (the tag stays after unlocking).
        internal static bool IsBox(GameItem item)
        {
            return item.IsTag(LockedContainerTag) && item.contentWindow != null;
        }

        // The owner packed it, so it is not loot: unlocking must not reroll what's inside
        // (the game refills unscanned crates for a luckier player). Half come unlocked, and
        // what's inside belongs to the owner until you take the deal.
        private static void PrepareBox(IRng rng, GameItem box)
        {
            PreBuiltItemHelper.MarkCrateScanned(box);
            if (rng.Range(1, 100) > PawnCounterMod.LockedChance)
                LockHelper.UnlockContainer(box, force: true);
            foreach (GameItem child in box.FindAllChildItems())
                GeneralHelper.SetItemOwned(child, false);
        }

        // What is inside a box, as "uid:units,uid:units", so the owner can check it later.
        internal static string Snapshot(GameItem box)
        {
            return string.Join(",", box.FindAllChildItems()
                .Select(c => c.GetUniqueID().ToString(CultureInfo.InvariantCulture) + ":" + c.unitCount.ToString(CultureInfo.InvariantCulture)));
        }

        // Everything the owner packed is still in the box, at least as many of each.
        // Unlocking doesn't matter; extra things you put in go with the box.
        internal static bool Intact(Pawn pawn, GameItem box)
        {
            if (string.IsNullOrEmpty(pawn.Contents))
                // Pawned before contents were recorded: the old rule, a crate must stay locked.
                return !(box.IsTag(LockedContainerTag) && !LockHelper.IsCurrentlyLocked(box));
            Dictionary<int, int> inside = new Dictionary<int, int>();
            foreach (GameItem child in box.FindAllChildItems())
                inside[child.uniqueId] = child.unitCount;
            return Pawn.ParseContents(pawn.Contents).All(c => inside.TryGetValue(c.Key, out int units) && units >= c.Value);
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
