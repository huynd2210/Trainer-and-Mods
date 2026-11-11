using System;
using UnityEngine;

namespace SharedStorage
{
    // What is in the shared bay, as stored in <persistentDataPath>\ModData\SharedStorage. This
    // folder belongs to no save slot, which is what makes the bay shared.
    [Serializable]
    public class SharedData
    {
        public int version = 1;

        // The bay and everything in it, in the game's own save format (SaveState JSON).
        // Empty when the bay is empty.
        public string items;

        // The items' features (quality, condition, plant traits...), which the game keeps
        // apart from the item nodes. Easy Save bytes, base64.
        public string features;

        public string savedAt;
    }

    internal enum ReadStatus { Missing, Ok, Unreadable }

    internal static class SharedFile
    {
        internal const string FileName = "shared-storage.json";

        // The previous version, kept one save back so a bad write never loses the only copy.
        internal const string PreviousFileName = "shared-storage.previous.json";

        internal static ReadStatus Read(out SharedData data)
        {
            data = null;
            string text;
            try
            {
                text = ModHelper.ReadModDataFileText(SharedStorageMod.Id, FileName);
            }
            catch (Exception e)
            {
                Debug.LogError("[SharedStorage] Could not read " + FileName + ": " + e.Message);
                return ReadStatus.Unreadable;
            }
            if (text == null)
                return ReadStatus.Missing;
            try
            {
                data = JsonUtility.FromJson<SharedData>(text);
            }
            catch (Exception e)
            {
                Debug.LogError("[SharedStorage] " + FileName + " is not valid: " + e.Message);
                return ReadStatus.Unreadable;
            }
            if (data == null || data.version != 1)
            {
                Debug.LogError("[SharedStorage] " + FileName + " is empty or from an unknown version.");
                data = null;
                return ReadStatus.Unreadable;
            }
            return ReadStatus.Ok;
        }

        internal static void Write(SharedData data)
        {
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string text = JsonUtility.ToJson(data);
            string old = ModHelper.ReadModDataFileText(SharedStorageMod.Id, FileName);
            if (old != null)
                ModHelper.WriteModDataFileText(SharedStorageMod.Id, PreviousFileName, old);
            ModHelper.WriteModDataFileText(SharedStorageMod.Id, FileName, text);
        }
    }
}
