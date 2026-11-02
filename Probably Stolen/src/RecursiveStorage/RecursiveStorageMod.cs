using HarmonyLib;

namespace RecursiveStorage
{
    public class RecursiveStorageMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable() { }

        public void OnDisable() { }
    }

    // Every storage bay (plain, large, makeshift, machine and hypertech) asks this before
    // taking a container. Vanilla only lets storable packages into a storage bay; this lets
    // in any storage item too. The bays' other rules still apply: the gunsmith and chemist
    // bays only take their own goods, and backpacks stay out.
    [HarmonyPatch(typeof(ContainerHelper), nameof(ContainerHelper.IsNestingAllowed))]
    internal static class NestingPatch
    {
        private const string Storage = "STORAGE";

        private static void Postfix(GameItem addedItem, GameItem parentItem, ref bool __result)
        {
            if (__result || addedItem == null || parentItem == null)
                return;
            if (!addedItem.IsGameItemType(Storage) || !parentItem.IsGameItemType(Storage))
                return;
            if (addedItem.IsTag("BACKPACK_TAG") || IsSelfOrAncestor(addedItem, parentItem))
                return;
            __result = true;
        }

        // A bay must not end up inside itself. The grid already refuses that; this guards
        // the other inventory types too.
        private static bool IsSelfOrAncestor(GameItem addedItem, GameItem parentItem)
        {
            GameItem current = parentItem;
            for (int depth = 0; current != null && depth < 64; depth++)
            {
                if (current == addedItem)
                    return true;
                current = current.parentInventory?.GetParentItem();
            }
            return false;
        }
    }
}
