using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ModItemKeeper
{
    // While a save loads, the game rebuilds items from the save's item lists. An item whose mod
    // is missing comes back as an empty "unknown" placeholder, and everything inside it is
    // dropped; the next save then forgets it for good.
    //
    // Keeper steps in just before each list is rebuilt:
    //  - contents of unknown containers are cut out and kept in the save's mod data;
    //  - kept contents whose container can be built again (mod reinstalled) go back inside it.
    // When the load finishes, kept contents whose container is gone (sold, trashed, or the mod
    // came back without it) are put loose into the store.
    internal static class Keeper
    {
        private const string Tag = "[ModItemKeeper] ";
        private const string Green = "5c6f46";

        internal static bool Enabled = true;

        // The load window: from just before the game decodes the store's inventories until just
        // before its OnGameLoadedLate event. Other mods' decodes after that are left alone.
        private static bool open;
        private static List<KeyValuePair<string, StashEntry>> pending = new List<KeyValuePair<string, StashEntry>>();
        private static readonly Dictionary<string, bool> seen = new Dictionary<string, bool>(); // "uid|id" -> buildable
        private static readonly List<Restored> restored = new List<Restored>();
        private static SaveItemNode rootTemplate;
        private static int keptItems;
        private static int restoredItems;

        private sealed class Restored
        {
            public StashEntry Entry;
            public List<int> ChildUniqueIds;
        }

        private static string SeenKey(int uniqueId, string id) => uniqueId + "|" + id;

        internal static void Reset()
        {
            open = false;
            pending.Clear();
            seen.Clear();
            restored.Clear();
            rootTemplate = null;
            keptItems = 0;
            restoredItems = 0;
        }

        // ---- load window ------------------------------------------------------------------

        internal static void OpenWindow()
        {
            Reset();
            if (!Enabled || PlayerStore.Instance == null)
                return;
            try
            {
                pending = Stash.All();
                open = true;
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "Could not read the kept items: " + e);
            }
        }

        // One list of saved items, about to be rebuilt by the game.
        internal static void BeforeDecode(List<SaveItemNode> nodes)
        {
            if (!open || nodes == null || nodes.Count == 0 || !SaveNodeAccess.DirectoriesReady())
                return;
            try
            {
                SaveNodeAccess a = SaveNodeAccess.Instance;
                if (rootTemplate == null && nodes[0]?.identifier == "save_bag")
                    rootTemplate = SaveNodeAccess.Clone(nodes[0]);
                foreach (SaveItemNode n in nodes)
                    if (n != null)
                        seen[SeenKey(n.uniqueId, n.identifier)] = SaveNodeAccess.IsKnown(n);

                RestoreInto(nodes, a);
                CutAndStash(nodes, a);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "Failed on a saved item list; it loads as the game would without this mod. " + e);
            }
        }

        private static void RestoreInto(List<SaveItemNode> nodes, SaveNodeAccess a)
        {
            foreach (KeyValuePair<string, StashEntry> p in pending.ToList())
            {
                StashEntry entry = p.Value;
                if (entry.parentUniqueId <= 0 || !nodes.Any(n => n != null && n.uniqueId == entry.parentUniqueId && n.identifier == entry.parentId))
                    continue;
                Cut<SaveItemNode> cut = Stash.ToCut(entry);
                List<int> childIds = KeeperCore.DirectChildUniqueIds(cut, a);
                List<ItemFeature> features = Stash.DecodeFeatures(entry);
                int count = cut.Nodes.Count - 1;
                if (!KeeperCore.Restore(nodes, cut, a, SaveNodeAccess.IsKnown))
                    continue; // still unknown or not empty; Finish decides
                // The game attaches saved features by unique id right after decoding.
                if (features != null)
                    PlayerStore.Instance.savedItemFeatureList.AddRange(features);
                Stash.Remove(p.Key);
                pending.Remove(p);
                restored.Add(new Restored { Entry = entry, ChildUniqueIds = childIds });
                restoredItems += count;
                Debug.Log(Tag + "Put " + count + " item(s) back into " + entry.parentId + " #" + entry.parentUniqueId + ".");
            }
        }

        private static void CutAndStash(List<SaveItemNode> nodes, SaveNodeAccess a)
        {
            // The kept contents are matched back to their container by unique id; make sure it has one.
            for (int i = 1; i < nodes.Count; i++)
            {
                SaveItemNode n = nodes[i];
                if (n != null && n.uniqueId <= 0 && n.childItems != null && n.childItems.Count > 0 && !SaveNodeAccess.IsKnown(n))
                    n.uniqueId = PlayerStore.Instance.GetUniqueID();
            }

            List<Cut<SaveItemNode>> cuts = KeeperCore.CutUnknownContents(nodes, a, SaveNodeAccess.IsKnown, SaveNodeAccess.Clone);
            List<ItemFeature> saved = PlayerStore.Instance.savedItemFeatureList;
            foreach (Cut<SaveItemNode> cut in cuts)
            {
                HashSet<int> ids = new HashSet<int>(cut.Nodes.Skip(1).Select(n => n.uniqueId).Where(id => id != 0));
                List<ItemFeature> features = saved.Where(f => f != null && ids.Contains(f.parentItemUniqueId)).ToList();
                saved.RemoveAll(f => f != null && ids.Contains(f.parentItemUniqueId));
                string key = Stash.Add(new StashEntry
                {
                    parentUniqueId = cut.ParentUniqueId,
                    parentId = cut.ParentId,
                    items = Stash.EncodeItems(cut.Nodes),
                    features = Stash.EncodeFeatures(features),
                });
                keptItems += cut.Nodes.Count - 1;
                Debug.Log(Tag + "Kept " + (cut.Nodes.Count - 1) + " item(s) from inside " + cut.ParentId + " #" + cut.ParentUniqueId
                    + " (its mod is missing) as " + key + ".");
            }
        }

        // ---- after the load ---------------------------------------------------------------

        internal static void Finish()
        {
            if (!open)
                return;
            open = false;
            try
            {
                // Nothing seen means the load didn't go through the store's inventories as
                // expected; then nothing can be judged gone, so leave every entry as it is.
                if (seen.Count == 0)
                    return;

                int putLoose = 0;
                HashSet<int> present = new HashSet<int>(AllStoreItems().Select(i => i.uniqueId));

                // Put back but not there after the load (a mod's load fix-up removed it, say).
                foreach (Restored r in restored)
                {
                    List<int> missing = r.ChildUniqueIds.Where(id => id != 0 && !present.Contains(id)).ToList();
                    if (missing.Count == 0)
                        continue;
                    Cut<SaveItemNode> cut = Stash.ToCut(r.Entry);
                    List<List<SaveItemNode>> trees = KeeperCore.DirectChildTrees(cut, SaveNodeAccess.Instance)
                        .Where(t => missing.Contains(t[0].uniqueId)).ToList();
                    putLoose += Plop(trees, Stash.DecodeFeatures(r.Entry), r.Entry.parentId);
                }

                foreach (KeyValuePair<string, StashEntry> p in Stash.All())
                {
                    StashEntry entry = p.Value;
                    if (entry.parentUniqueId > 0 && seen.TryGetValue(SeenKey(entry.parentUniqueId, entry.parentId), out bool known) && !known)
                        continue; // container still here, its mod still missing: keep waiting
                    Cut<SaveItemNode> cut = Stash.ToCut(entry);
                    List<List<SaveItemNode>> trees = KeeperCore.DirectChildTrees(cut, SaveNodeAccess.Instance);
                    Stash.Remove(p.Key);
                    putLoose += Plop(trees, Stash.DecodeFeatures(entry), entry.parentUniqueId > 0 ? entry.parentId : null);
                }

                Report(putLoose);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "Finishing the load failed: " + e);
            }
            finally
            {
                pending.Clear();
                restored.Clear();
            }
        }

        private static void Report(int putLoose)
        {
            List<string> lines = new List<string>();
            if (restoredItems > 0)
                lines.Add("Mod Item Keeper: " + restoredItems + " kept item(s) are back inside their containers.");
            if (putLoose > 0)
                lines.Add("Mod Item Keeper: " + putLoose + " kept item(s) whose container is gone were put into your inventory.");
            if (keptItems > 0)
                lines.Add("Mod Item Keeper: " + keptItems + " item(s) inside containers from a missing mod are kept safe until it is back.");
            foreach (string line in lines)
            {
                Debug.Log(Tag + line);
                PlayerStore.Instance.AddNightLog(line, Green);
            }
        }

        // Rebuilds item trees loose and puts them into the store's inventories. Whatever doesn't
        // fit is kept again and tried on the next load. Returns how many items were placed.
        private static int Plop(List<List<SaveItemNode>> trees, List<ItemFeature> features, string fromId)
        {
            trees = trees.Where(t => t.Count > 0).ToList();
            if (trees.Count == 0)
                return 0;
            SaveNodeAccess a = SaveNodeAccess.Instance;

            // A save list of its own: a bag holding the trees, as the game saves its inventories.
            SaveItemNode root = rootTemplate != null ? SaveNodeAccess.Clone(rootTemplate) : new SaveItemNode { identifier = "save_bag" };
            int inventoryIndex = rootTemplate?.childItemInventoryNode?.FirstOrDefault() ?? 1;
            root.uuid = 0;
            List<SaveItemNode> list = new List<SaveItemNode> { root };
            List<SaveItemNode> moving = trees.SelectMany(t => t).ToList();
            Dictionary<long, long> map = KeeperCore.Renumber(moving, list, a);
            root.childItems = trees.Select(t => t[0].uuid).ToList();
            root.childItemInventoryNode = trees.Select(_ => inventoryIndex).ToList();
            list.AddRange(moving);

            // A container from another missing mod inside: keep its contents too.
            CutAndStash(list, a);

            GameItem holder = DirectoryMaster.Item("save_bag");
            GameInventory holderInventory = holder?.contentWindow?.child as GameInventory;
            if (holderInventory == null)
            {
                Debug.LogError(Tag + "No bag to rebuild kept items in; they stay kept.");
                Stash.Add(new StashEntry { parentUniqueId = 0, parentId = "save_bag", items = Stash.EncodeItems(list), features = Stash.EncodeFeatures(features) });
                holder?.Destroy();
                return 0;
            }

            int placed = 0;
            try
            {
                PlayerStore.Instance.DecodeSaveItem(Stash.EncodeItems(list), holderInventory);
                if (features != null)
                {
                    Dictionary<int, GameItem> byId = new Dictionary<int, GameItem>();
                    foreach (GameItem item in GraphUtils.FindAllChildrenType<GameItem>(holder))
                    {
                        if (item == holder || item.uniqueId == 0 || byId.ContainsKey(item.uniqueId))
                            continue;
                        item.RemoveAllFeature();
                        byId.Add(item.uniqueId, item);
                    }
                    foreach (ItemFeature f in features)
                        if (f != null && byId.TryGetValue(f.parentItemUniqueId, out GameItem owner))
                            owner.itemFeatures.Add(f);
                }

                EmporiumEntry store = EmporiumEntry.Instance;
                foreach (GameItem item in holderInventory.children.OfType<GameItem>().ToList())
                {
                    if (Place(item, holderInventory, store.invElement) || Place(item, holderInventory, store.backInvinvElement))
                        placed++;
                }

                if (holderInventory.children.OfType<GameItem>().Any())
                {
                    // No room: keep the rest for the next load.
                    List<ItemFeature> saved = PlayerStore.Instance.savedItemFeatureList;
                    int before = saved.Count;
                    List<SaveItemNode> rest;
                    List<ItemFeature> restFeatures;
                    try
                    {
                        rest = SaveManager.EncodeNodes(holder);
                    }
                    finally
                    {
                        restFeatures = saved.GetRange(before, saved.Count - before);
                        saved.RemoveRange(before, saved.Count - before);
                    }
                    string key = Stash.Add(new StashEntry
                    {
                        parentUniqueId = 0,
                        parentId = "save_bag",
                        items = Stash.EncodeItems(rest),
                        features = Stash.EncodeFeatures(restFeatures),
                    });
                    Debug.LogWarning(Tag + "No room for " + (rest.Count - 1) + " kept item(s); they stay kept as " + key + " and are tried again on the next load.");
                }
            }
            finally
            {
                holder.Destroy();
            }
            Debug.Log(Tag + "Put " + placed + " kept item(s)" + (fromId != null ? " from " + fromId : "") + " into the inventory.");
            return placed;
        }

        // Moves the whole item (all of its stack) into a free spot of the inventory.
        private static bool Place(GameItem item, GameInventory from, GraphNodeStorage target)
        {
            if (target == null)
                return false;
            for (int guard = 0; guard < 1000; guard++)
            {
                if (item.parentInventory != from || (item is GameItemElement e && e.IsDestroyed()))
                    return true;
                SlotMarker slot = target.TryFindOneValidInventorySlot(item);
                if (slot == null || slot.TryAcceptOnce() <= 0)
                    break;
            }
            // Part of a stack may have gone in; the rest stays in the holder.
            return item.parentInventory != from;
        }

        private static IEnumerable<GameItem> AllStoreItems()
        {
            EmporiumEntry store = EmporiumEntry.Instance;
            IEnumerable<GameItem> items = store.GetAllItems();
            foreach (GraphNodeStorage extra in new GraphNodeStorage[] { store.soldElement, store.hirelingInv, store.cassettePlayerElement })
                if (extra != null)
                    items = items.Concat(GraphUtils.FindAllChildrenType<GameItem>(extra));
            return items;
        }
    }

    // ---- hooks into the game's load -------------------------------------------------------

    [HarmonyPatch(typeof(ModHook), "FireOnGameLoadedNormal")]
    internal static class OpenWindowPatch
    {
        private static void Prefix() => Keeper.OpenWindow();
    }

    [HarmonyPatch(typeof(ModHook), "FireOnGameLoadedLate")]
    internal static class FinishPatch
    {
        private static void Prefix() => Keeper.Finish();
    }

    // Last, so other mods' fix-ups to the list (renamed ids, dropped dangling refs) come first.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
    internal static class DecodeNodesPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(List<SaveItemNode> savedItems) => Keeper.BeforeDecode(savedItems);
    }
}
