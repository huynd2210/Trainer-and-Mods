using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using birdseye.Regiments;
using birdseye.Regiments.SaveStates;
using Newtonsoft.Json;
using TheaterCommand.Core;
using UnityEngine;

namespace TheaterCommand.Game
{
    /// <summary>One of the player's formations as a real War Path force: its full roster and points.</summary>
    [Serializable]
    public class BlueForce
    {
        public int formation;
        // Force value when raised; theater strength is current value relative to this.
        public float fullValue;
        public string playerSave;
    }

    /// <summary>Red formation details the theater model has no use for.</summary>
    [Serializable]
    public class RedForce
    {
        public int formation;
        public string iconGuid;
    }

    /// <summary>
    /// A Theater Command campaign. It lives beside a War Path save slot: the slot provides the
    /// enemy front, settings and the battle plumbing, this file the theater.
    /// </summary>
    [Serializable]
    public class Campaign
    {
        // 2: the grid theater. Files from the sector prototype (1) are ignored.
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        // rngSeed of the War Path run this belongs to; a cleared and reused slot gets a new seed,
        // so a stale file is ignored rather than deleted.
        public int runSeed;
        public Theater theater;
        public List<BlueForce> forces = new List<BlueForce>();
        public List<RedForce> red = new List<RedForce>();
        // Battle being fought in game right now, and which formation fights it.
        public int activeBattle = -1;
        public int activeFormation = -1;
        public int launchPhase = -1;
        // Formation open in the regiment screen for reinforcing, or -1.
        public int managedFormation = -1;

        public BlueForce Force(int formation) => forces.FirstOrDefault(f => f.formation == formation);

        public RedForce Red(int formation) => red.FirstOrDefault(f => f.formation == formation);
    }

    public static class CampaignStore
    {
        private static readonly Dictionary<eSaveSlot, Campaign> cache = new Dictionary<eSaveSlot, Campaign>();

        private static string PathOf(eSaveSlot slot) => Path.Combine(SaveUtils.defaultFolder, "TheaterCommand_" + slot + ".json");

        /// <summary>The campaign in a War Path slot, or null if the slot holds an ordinary War Path run.</summary>
        public static Campaign Get(ProcOpSaveSlotRecord rec)
        {
            if (rec == null || rec.isEmptySlot)
            {
                return null;
            }
            if (!cache.TryGetValue(rec.saveSlot, out Campaign c))
            {
                string path = PathOf(rec.saveSlot);
                c = File.Exists(path) ? JsonConvert.DeserializeObject<Campaign>(File.ReadAllText(path)) : null;
                cache[rec.saveSlot] = c;
            }
            return c != null && c.version == Campaign.CurrentVersion && c.runSeed == rec.procOpSave.rngSeed ? c : null;
        }

        public static void Save(eSaveSlot slot, Campaign c)
        {
            string path = PathOf(slot);
            string tmp = path + ".tmp";
            // Unity's JsonUtility cannot see nested classes from a mod assembly, so Newtonsoft it is.
            File.WriteAllText(tmp, JsonConvert.SerializeObject(c));
            if (File.Exists(path))
            {
                // Keep the previous turn as a backup.
                File.Copy(path, path + ".bak", true);
                File.Delete(path);
            }
            File.Move(tmp, path);
            cache[slot] = c;
        }
    }
}
