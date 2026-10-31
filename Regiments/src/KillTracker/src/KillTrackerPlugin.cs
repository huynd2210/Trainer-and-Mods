using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VerifyKillTracker")]

namespace KillTracker
{
    [BepInPlugin(Guid, Name, Version)]
    public class KillTrackerPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.regiments.killtracker";
        public const string Name = "Kill Tracker";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> Folder;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Write a kill/loss log for every battle.");
            Folder = Config.Bind("General", "Folder", "KillTracker",
                "Where the battle logs go. A relative path is taken from the game folder (the one with Regiments.exe).");

            new Harmony(Guid).PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo(Name + " " + Version + " loaded, logs go to " + BattleLog.ResolveFolder());
        }

        private void Update()
        {
            BattleTracker.Tick();
        }
    }
}
