using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModItemKeeper
{
    // One container's kept contents, stored in the save's mod data so it travels with the save.
    [Serializable]
    internal class StashEntry
    {
        public int parentUniqueId;   // 0: loose items whose container is gone
        public string parentId;
        public string items;         // SaveState JSON: [container as saved, everything inside]
        public string features;      // the items' features (ES3, base64), as the save keeps them
    }

    internal static class Stash
    {
        internal const string Prefix = "ModItemKeeper.stash.";

        internal static Dictionary<string, string> Data => PlayerStore.Instance.ModData();

        internal static List<KeyValuePair<string, StashEntry>> All()
        {
            List<KeyValuePair<string, StashEntry>> result = new List<KeyValuePair<string, StashEntry>>();
            foreach (KeyValuePair<string, string> pair in Data.Where(p => p.Key.StartsWith(Prefix, StringComparison.Ordinal)).ToList())
            {
                try
                {
                    StashEntry entry = JsonUtility.FromJson<StashEntry>(pair.Value);
                    if (entry?.items != null)
                        result.Add(new KeyValuePair<string, StashEntry>(pair.Key, entry));
                }
                catch (Exception e)
                {
                    // Left in place untouched: better kept unreadable than deleted.
                    Debug.LogError("[ModItemKeeper] Could not read " + pair.Key + ": " + e.Message);
                }
            }
            return result;
        }

        internal static string Add(StashEntry entry)
        {
            string key = Prefix + (entry.parentUniqueId > 0 ? entry.parentUniqueId.ToString() : "loose");
            string unique = key;
            for (int n = 2; Data.ContainsKey(unique); n++)
                unique = key + "_" + n;
            Data[unique] = JsonUtility.ToJson(entry);
            return unique;
        }

        internal static void Remove(string key) => Data.Remove(key);

        internal static Cut<SaveItemNode> ToCut(StashEntry entry) => new Cut<SaveItemNode>
        {
            ParentUniqueId = entry.parentUniqueId,
            ParentId = entry.parentId,
            Nodes = JsonUtility.FromJson<SaveState>(entry.items).saveItems ?? new List<SaveItemNode>(),
        };

        internal static string EncodeItems(List<SaveItemNode> nodes) => JsonUtility.ToJson(new SaveState { saveItems = nodes });

        internal static string EncodeFeatures(List<ItemFeature> features) =>
            Convert.ToBase64String(ES3.Serialize(features ?? new List<ItemFeature>()));

        // null when they can't be read; the caller then keeps whatever features the items have.
        internal static List<ItemFeature> DecodeFeatures(StashEntry entry)
        {
            if (string.IsNullOrEmpty(entry.features))
                return new List<ItemFeature>();
            try
            {
                return ES3.Deserialize<List<ItemFeature>>(Convert.FromBase64String(entry.features));
            }
            catch (Exception e)
            {
                Debug.LogError("[ModItemKeeper] Could not read the kept items' features: " + e.Message);
                return null;
            }
        }
    }
}
