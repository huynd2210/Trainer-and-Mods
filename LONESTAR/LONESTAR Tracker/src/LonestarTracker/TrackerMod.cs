using System;
using System.Collections.Generic;
using System.IO;
using Mods;
using Tool.Database;
using UnityEngine;
using UnityEngine.Events;

namespace LonestarTracker
{
    /// <summary>
    /// Entry point. ModManager finds this by reflection (the one UserMod subclass in the
    /// assembly), instantiates it, and calls OnLoad then OnAllModsLoad.
    /// </summary>
    public class TrackerMod : UserMod
    {
        /// <summary>
        /// Must be named exactly "config": UserMod.LoadModSettings and ModSettingPanel
        /// both look this field up by name.
        /// </summary>
        public TrackerConfig config = new TrackerConfig();

        public static TrackerMod Instance { get; private set; }

        /// <summary>Live config. Null before the mod finishes loading.</summary>
        public static TrackerConfig Config { get { return Instance != null ? Instance.config : null; } }

        /// <summary>
        /// The logs live beside the saves, not inside the mod folder: reinstalling or
        /// updating the mod then cannot take the history with it.
        /// </summary>
        public static string DataDirectory
        {
            get { return Path.Combine(Application.persistentDataPath, "Tracker"); }
        }

        public override void OnLoad()
        {
            Instance = this;
            base.OnLoad();
        }

        public override void OnAllModsLoad(IReadOnlyList<UserMod> mods)
        {
            base.OnAllModsLoad(mods);

            Tracker.Init(DataDirectory);
            Subscribe();
            TrackerUI.Spawn();
        }

        /// <summary>
        /// The game announces both battle outcomes through its own event centre, with the
        /// enemy's data row attached - name, pilot and tier all come straight from there.
        /// </summary>
        private static void Subscribe()
        {
            try
            {
                EventCenter events = Singleton<EventCenter>.Instance();
                events.AddEventListener<DataEnemyShip>(EventName.battleVictory, OnVictory);
                events.AddEventListener<DataEnemyShip>(EventName.battleFail, OnDefeat);
            }
            catch (Exception e)
            {
                Debug.LogError("[LonestarTracker] could not subscribe to battle events: " + e);
            }
        }

        private static readonly UnityAction<DataEnemyShip> OnVictory = delegate (DataEnemyShip enemy)
        {
            TrackerConfig cfg = Config;
            if (cfg == null || !cfg.logBattles) return;
            Tracker.OnVictory(enemy);
        };

        private static readonly UnityAction<DataEnemyShip> OnDefeat = delegate (DataEnemyShip enemy)
        {
            TrackerConfig cfg = Config;
            if (cfg == null || !cfg.logBattles) return;
            Tracker.OnDefeat(enemy);
        };
    }
}
