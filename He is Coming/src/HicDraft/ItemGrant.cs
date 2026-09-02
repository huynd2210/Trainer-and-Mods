using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HicDraft
{
    /// <summary>Outcome of a draft attempt, so the caller can report it in the compendium header.</summary>
    internal readonly struct GrantResult
    {
        public readonly bool Ok;
        public readonly string Message;
        private GrantResult(bool ok, string message) { Ok = ok; Message = message; }
        public static GrantResult Success(string m) => new GrantResult(true, m);
        public static GrantResult Failure(string m) => new GrantResult(false, m);
    }

    /// <summary>
    /// Puts a drafted item into the live run. Every ItemType goes through its own handler in
    /// <see cref="Handlers"/> - adding a type means adding an entry, never editing a branch chain.
    /// </summary>
    internal static class ItemGrant
    {
        private static readonly Dictionary<ItemType, Func<OverworldUIManager, InventoryItem, GrantResult>> Handlers =
            new Dictionary<ItemType, Func<OverworldUIManager, InventoryItem, GrantResult>>
            {
                { ItemType.WEAPON,    GrantWeapon },
                { ItemType.BAG,       GrantBag },
                { ItemType.EDGE,      GrantEdge },
                { ItemType.INVENTORY, GrantInventory },
                { ItemType.SET,       GrantSet },
            };

        public static GrantResult Give(EffectBase effect)
        {
            if (effect == null) return GrantResult.Failure("No item on that entry.");

            var overworld = GameRefs.Overworld;
            if (overworld == null)
                return GrantResult.Failure("Not in a run - start or load a run first.");

            var item = effect.TryCast<InventoryItem>();
            if (item == null)
                return GrantResult.Failure($"'{effect.nameTag}' is not an inventory item.");

            var copy = MakeRunCopy(item);
            if (copy == null)
                return GrantResult.Failure($"Could not build a run copy of '{item.nameTag}'.");

            if (!Handlers.TryGetValue(copy.itemType, out var handler))
                return GrantResult.Failure($"'{copy.nameTag}' is of type {copy.itemType}, which cannot be drafted.");

            try
            {
                return handler(overworld, copy);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Draft] Granting '{copy.nameTag}' ({copy.itemType}) threw: {e}");
                return GrantResult.Failure($"Granting '{copy.nameTag}' failed - see the BepInEx log.");
            }
        }

        /// <summary>
        /// The compendium lists the item *prefabs* (the shared ScriptableObject assets). Handing one of
        /// those straight to the inventory would let run-time state - oils, edges, trigger counters -
        /// stick to the asset and leak into later runs, so draft from the per-run working pool where
        /// one exists, and always hand over a fresh Instantiate of it.
        /// </summary>
        private static InventoryItem MakeRunCopy(InventoryItem prefab)
        {
            var source = prefab;
            var items = GameRefs.Items;
            if (items != null)
            {
                try
                {
                    var working = items.GetWorkingItemOrSetByName(prefab.nameTag);
                    if (working != null) source = working;
                    else Plugin.Log.LogInfo($"[Draft] No working copy for '{prefab.nameTag}'; drafting from the prefab.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[Draft] GetWorkingItemOrSetByName('{prefab.nameTag}') threw: {e.Message}");
                }
            }

            if (!Plugin.CloneDraftedItem.Value) return source;

            var copy = Object.Instantiate(source);
            copy.name = source.name; // Instantiate appends "(Clone)"; keep asset names comparable
            return copy;
        }

        // ---------- per-type handlers ----------

        private static GrantResult GrantWeapon(OverworldUIManager overworld, InventoryItem item)
        {
            overworld.AddWeaponToInventory(item, false);
            return GrantResult.Success($"Equipped weapon: {item.nameTag}");
        }

        private static GrantResult GrantBag(OverworldUIManager overworld, InventoryItem item)
        {
            overworld.AddBackpackToInventory(item, false);
            return GrantResult.Success($"Equipped bag: {item.nameTag}");
        }

        private static GrantResult GrantEdge(OverworldUIManager overworld, InventoryItem item)
        {
            if (overworld.GetWeapon() == null)
                return GrantResult.Failure("Equip a weapon before drafting an edge.");
            overworld.AddEffectToWeapon(item, false);
            return GrantResult.Success($"Applied edge: {item.nameTag}");
        }

        private static GrantResult GrantSet(OverworldUIManager overworld, InventoryItem item)
        {
            overworld.AddSetBonus(item);
            return GrantResult.Success($"Added set bonus: {item.nameTag}");
        }

        private static GrantResult GrantInventory(OverworldUIManager overworld, InventoryItem item)
        {
            int index = FirstFreeInventorySlot(overworld);
            if (index < 0)
                return GrantResult.Failure("Inventory is full - drop something first.");

            overworld.AddItemToInventory(item, index, false);
            return GrantResult.Success($"Added to slot {index}: {item.nameTag}");
        }

        /// <summary>
        /// Lowest slot index with nothing in it, or -1 when the inventory is full. Occupancy comes from
        /// the game's own positional map so combined/additive slots are accounted for the same way the
        /// game accounts for them.
        /// </summary>
        private static int FirstFreeInventorySlot(OverworldUIManager overworld)
        {
            int slotCount = overworld.inventorySlots != null ? overworld.inventorySlots.Count : 0;
            if (slotCount <= 0)
            {
                Plugin.Log.LogWarning("[Draft] OverworldUIManager reports no inventory slots.");
                return -1;
            }

            var occupied = new HashSet<int>();
            try
            {
                var positional = overworld.GetAllEquippedTypeInventoryItemsPositional(true, false);
                if (positional != null)
                    foreach (var kv in positional)
                        occupied.Add(kv.Key);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[Draft] Could not read occupied slots: {e.Message}");
            }

            Plugin.Log.LogInfo($"[Draft] {slotCount} inventory slots; occupied: [{string.Join(", ", occupied)}]");

            for (int i = 0; i < slotCount; i++)
                if (!occupied.Contains(i)) return i;

            return -1;
        }
    }
}
