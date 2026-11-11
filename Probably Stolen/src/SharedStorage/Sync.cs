using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SharedStorage
{
    // Keeps the save's Shared Storage Bay and the shared file in step.
    //
    // The shared file is the truth. When a save loads (or a run starts), whatever the save
    // remembers in the bay is a stale copy: it is thrown away and the bay is filled from the
    // file. Each time the game saves, the bay's contents are written back to the file.
    internal static class Sync
    {
        // The bay this session syncs, and whether writing it back is safe.
        private static GameItem bay;
        private static bool enabled;
        private static string problem;

        internal static bool Ready => PlayerStore.Instance != null && EmporiumEntry.Instance != null;

        internal static void Detach()
        {
            bay = null;
            enabled = false;
            problem = null;
        }

        // Finds the save's bay (or brings a new one) and fills it from the shared file.
        internal static void Attach()
        {
            Detach();
            if (!Ready)
                return;
            try
            {
                bay = FindOrBringBay();
                if (bay == null)
                    return;
                switch (SharedFile.Read(out SharedData data))
                {
                    case ReadStatus.Missing:
                        // First use: whatever this bay holds becomes the shared contents.
                        enabled = true;
                        break;
                    case ReadStatus.Ok:
                        enabled = Load(bay, data);
                        break;
                    default:
                        problem = "Not syncing: the shared storage file could not be read. Nothing has been overwritten; see ModData\\SharedStorage.";
                        break;
                }
            }
            catch (Exception e)
            {
                enabled = false;
                problem = "Not syncing: loading the shared contents failed. Nothing has been overwritten.";
                Debug.LogError("[SharedStorage] Attach failed: " + e);
            }
        }

        // Called right after the game has written its save slot.
        internal static void Write()
        {
            if (!enabled || !Ready)
                return;
            try
            {
                if (IsGone(bay))
                {
                    // Sold, trashed or otherwise destroyed, with whatever was inside: those
                    // items are gone from every save. Written once; the next load brings a
                    // fresh, empty bay.
                    SharedFile.Write(new SharedData());
                    Debug.Log("[SharedStorage] The shared bay left the store; its contents are gone.");
                    Detach();
                    return;
                }
                SharedFile.Write(Encode(bay));
            }
            catch (Exception e)
            {
                Debug.LogError("[SharedStorage] Writing the shared contents failed: " + e);
            }
        }

        // The tooltip line for a bay that is not in sync, or null.
        internal static string Problem(GameItem item)
        {
            if (bay == null || item == bay)
                return problem;
            return "Not syncing: this save already has a Shared Storage Bay. This one is an ordinary storage bay.";
        }

        private static GameItem FindOrBringBay()
        {
            GameItem found = EmporiumEntry.Instance.GetAllItems().FirstOrDefault(Bay.Is);
            if (found != null)
                return found;

            GameItem fresh = DirectoryMaster.Item(Bay.ItemId);
            if (!Bay.Is(fresh) || Bay.Inventory(fresh) == null)
            {
                Debug.LogError("[SharedStorage] The shared bay item is not registered.");
                fresh?.Destroy();
                return null;
            }
            GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.invElement, fresh);
            if (fresh.parentInventory == null)
            {
                // Every store inventory is full; try again on the next load.
                Debug.LogWarning("[SharedStorage] No room in the store for the shared bay.");
                return null;
            }
            return fresh;
        }

        private static bool IsGone(GameItem item)
        {
            if (item == null)
                return true;
            if (item is GameItemElement element && element.IsDestroyed())
                return true;
            // Sold items wait in the store's sold inventory until the customer takes them.
            GameInventory sold = EmporiumEntry.Instance.soldElement;
            return sold != null && GraphUtils.FindAllChildrenType<GameItem>(sold).Contains(item);
        }

        internal static SharedData Encode(GameItem item)
        {
            GameInventory inventory = Bay.Inventory(item);
            if (inventory == null || !inventory.children.OfType<GameItem>().Any())
                return new SharedData();

            // The game's encoder hands item features to the save in progress; take ours back
            // out so the save slot's list is exactly what the game put there.
            List<ItemFeature> saved = PlayerStore.Instance.savedItemFeatureList;
            if (saved == null)
                saved = PlayerStore.Instance.savedItemFeatureList = new List<ItemFeature>();
            int before = saved.Count;
            List<SaveItemNode> nodes;
            List<ItemFeature> features;
            try
            {
                nodes = SaveManager.EncodeNodes(item);
            }
            finally
            {
                features = saved.GetRange(before, saved.Count - before);
                saved.RemoveRange(before, saved.Count - before);
            }
            return new SharedData
            {
                items = JsonUtility.ToJson(new SaveState { saveItems = nodes }),
                features = Convert.ToBase64String(ES3.Serialize(features)),
            };
        }

        // Replaces the bay's contents with the shared ones. Returns whether it is safe to
        // write the bay back on the next save.
        internal static bool Load(GameItem target, SharedData data)
        {
            GameInventory inventory = Bay.Inventory(target);
            List<ItemFeature> features = null;
            bool safe = true;
            if (!string.IsNullOrEmpty(data.features))
            {
                try
                {
                    features = ES3.Deserialize<List<ItemFeature>>(Convert.FromBase64String(data.features));
                }
                catch (Exception e)
                {
                    // Load the items anyway, but don't overwrite the stored features.
                    Debug.LogError("[SharedStorage] Could not read the item features: " + e.Message);
                    problem = "Not syncing: the shared items' features could not be read. Nothing has been overwritten.";
                    safe = false;
                }
            }

            foreach (GameItem stale in inventory.children.OfType<GameItem>().ToList())
                stale.Destroy();
            if (string.IsNullOrEmpty(data.items))
                return safe;

            // The game's own loader: rebuilds the items, runs its load fix-ups and puts them
            // where they were in the bay.
            PlayerStore.Instance.DecodeSaveItem(data.items, inventory);

            // The items keep the ids of the save they were stored from, which can clash with
            // this save's. Give them fresh ones, and move their features across.
            Dictionary<int, GameItem> byOldId = new Dictionary<int, GameItem>();
            foreach (GameItem loaded in GraphUtils.FindAllChildrenType<GameItem>(target))
            {
                int oldId = loaded.uniqueId;
                if (oldId != 0 && !byOldId.ContainsKey(oldId))
                    byOldId.Add(oldId, loaded);
                loaded.uniqueId = 0;
                loaded.GetUniqueID();
                if (features != null)
                    loaded.RemoveAllFeature();
            }
            if (features != null)
            {
                foreach (ItemFeature feature in features)
                {
                    if (byOldId.TryGetValue(feature.parentItemUniqueId, out GameItem owner))
                    {
                        owner.itemFeatures.Add(feature);
                        feature.parentItemUniqueId = owner.uniqueId;
                    }
                }
            }
            return safe;
        }
    }
}
