using System.Collections.Generic;
using UnityEngine;

namespace ModItemKeeper
{
    // KeeperCore's view of the game's saved item node.
    internal sealed class SaveNodeAccess : INodeAccess<SaveItemNode>
    {
        internal static readonly SaveNodeAccess Instance = new SaveNodeAccess();

        public string Id(SaveItemNode n) => n.identifier ?? "";
        public long Uuid(SaveItemNode n) => n.uuid;
        public void SetUuid(SaveItemNode n, long uuid) => n.uuid = uuid;
        public int UniqueId(SaveItemNode n) => n.uniqueId;
        public List<long> Children(SaveItemNode n) => n.childItems ?? (n.childItems = new List<long>());
        public void SetChildren(SaveItemNode n, List<long> children) => n.childItems = children;
        public List<int> ChildInventories(SaveItemNode n) => n.childItemInventoryNode ?? (n.childItemInventoryNode = new List<int>());
        public void SetChildInventories(SaveItemNode n, List<int> inventories) => n.childItemInventoryNode = inventories;

        // A deep copy, through the same serializer the game saves with.
        internal static SaveItemNode Clone(SaveItemNode n) => JsonUtility.FromJson<SaveItemNode>(JsonUtility.ToJson(n));

        // Whether the game can build this item right now (its mod is installed).
        internal static bool IsKnown(SaveItemNode n)
        {
            if (string.IsNullOrEmpty(n?.identifier))
                return true; // nothing to restore it as either; leave it to the game
            if (n.itemType == typeof(GameCharacterItem).FullName)
                return DirectoryMaster.Has<GameCharacterItem>(n.identifier);
            return DirectoryMaster.Has<GameItem>(n.identifier);
        }

        // The item directories are filled in before a save loads; if they aren't, every id
        // would look unknown, so do nothing at all.
        internal static bool DirectoriesReady()
        {
            try
            {
                List<string> ids = DirectoryMaster.GetIdentifierList<GameItem>();
                return ids != null && ids.Count > 0 && DirectoryMaster.Has<GameItem>("save_bag");
            }
            catch
            {
                return false;
            }
        }
    }
}
