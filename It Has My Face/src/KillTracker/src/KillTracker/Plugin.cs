using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillTracker
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.ithasmyface.killtracker";
        public const string Name = "KillTracker";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static Ledger Ledger;

        internal static ConfigEntry<Key> PeekKey;
        internal static ConfigEntry<bool> HoldToPeek;
        internal static ConfigEntry<bool> ShowKillLine;
        internal static ConfigEntry<int> MaxRawRecords;

        private void Awake()
        {
            Log = Logger;

            PeekKey = Config.Bind("Controls", "Key", Key.F4,
                "Key that shows the kill table. The game binds no F-keys.");
            HoldToPeek = Config.Bind("Controls", "HoldToPeek", true,
                "true: the table shows while the key is held. false: the key toggles it.");
            ShowKillLine = Config.Bind("Display", "ShowKillLine", true,
                "Show a short line under the crosshair each time you kill something.");
            MaxRawRecords = Config.Bind("Data", "MaxRawRecords", 5000,
                "Individual kill records kept in the file; the oldest are dropped first. Totals stay exact. 0 = totals only.");

            Ledger = new Ledger(Path.Combine(Paths.ConfigPath, Name, "kills.json"));
            Ledger.Load();

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

            var host = new GameObject("KillTracker");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<KillSources>();
            host.AddComponent<KillHud>();

            Log.LogInfo($"{Name} {Version} loaded: {Ledger.LifetimeTotal} lifetime kills. Hold {PeekKey.Value} to see them.");
        }
    }
}
