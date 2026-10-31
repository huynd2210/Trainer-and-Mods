using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TheaterCommand.Game
{
    [BepInPlugin(Mod.Id, "Theater Command", "1.0.0")]
    public class TheaterCommandPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Mod.Init(Logger.LogInfo);
        }
    }

    public static class Mod
    {
        public const string Id = "com.regiments.theatercommand";

        public static Action<string> Log = s => Debug.Log("[TheaterCommand] " + s);

        public static void Init(Action<object> log = null)
        {
            if (log != null)
            {
                Log = s => log(s);
            }
            new Harmony(Id).PatchAll(typeof(Mod).Assembly);
            Log("patched");
        }

        /// <summary>
        /// Development entry, called through DevBridge's "run" with a freshly built copy of this
        /// assembly: drop the old copy's patches and screen, then install this one.
        /// </summary>
        public static void Reload(Action<string> log)
        {
            Harmony.UnpatchID(Id);
            TheaterScreen.DestroyExisting();
            Log = s => Debug.Log("[TheaterCommand] " + s);
            new Harmony(Id).PatchAll(typeof(Mod).Assembly);
            log("Theater Command reloaded");
        }
    }
}
