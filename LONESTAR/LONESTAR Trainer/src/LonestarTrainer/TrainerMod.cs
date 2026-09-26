using System;
using System.Collections.Generic;
using HarmonyLib;
using Mods;
using UnityEngine;

namespace LonestarTrainer
{
    /// <summary>
    /// Entry point. ModManager finds this by reflection (the one UserMod subclass in
    /// the assembly), instantiates it, and calls OnLoad then OnAllModsLoad.
    /// </summary>
    public class TrainerMod : UserMod
    {
        /// <summary>
        /// Must be named exactly "config": UserMod.LoadModSettings and ModSettingPanel
        /// both look this field up by name.
        /// </summary>
        public TrainerConfig config = new TrainerConfig();

        public static TrainerMod Instance { get; private set; }

        /// <summary>Live config. Null before the mod finishes loading.</summary>
        public static TrainerConfig Config => Instance?.config;

        public override void OnLoad()
        {
            Instance = this;

            // Reads config.json into `config` and runs harmony.PatchAll on this assembly.
            base.OnLoad();

            CheatRegistry.Build();
            Debug.Log("[LonestarTrainer] loaded " + CheatRegistry.All.Count + " cheats");
        }

        public override void OnAllModsLoad(IReadOnlyList<UserMod> mods)
        {
            base.OnAllModsLoad(mods);

            if (config != null && config.enableDevConsole)
                EnableDevConsole();

            TrainerRunner.Spawn();
        }

        /// <summary>
        /// Flips Define.gameMaster, which gates the game's own GM console (Enter to open).
        /// It is `static readonly`, so it needs an IL-level write rather than reflection;
        /// Launcher already creates the GameMasterManager host object either way.
        /// </summary>
        private static void EnableDevConsole()
        {
            try
            {
                ref bool gameMaster = ref AccessTools.StaticFieldRefAccess<bool>(typeof(Define), "gameMaster");
                gameMaster = true;
                Debug.Log("[LonestarTrainer] developer console enabled - press Enter in-game");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LonestarTrainer] could not enable the developer console: " + e);
            }
        }
    }
}
