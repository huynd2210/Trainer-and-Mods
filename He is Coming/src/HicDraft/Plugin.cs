using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace HicDraft
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "dev.heiscoming.draft";
        public const string Name = "He is Coming - Equipment Draft";
        public const string Version = "1.0.0";

        public static new ManualLogSource Log { get; private set; }

        public static ConfigEntry<string> Hotkey { get; private set; }
        public static ConfigEntry<bool> CloseAfterDraft { get; private set; }
        public static ConfigEntry<bool> CloneDraftedItem { get; private set; }

        public override void Load()
        {
            Log = base.Log;

            Hotkey = Config.Bind("Hotkeys", "OpenDraft", "F7",
                "Key that opens and closes the draft menu. Uses Unity Input System key names, e.g. F7, Backquote, Insert, P.");
            CloseAfterDraft = Config.Bind("Draft", "CloseAfterDraft", false,
                "Close the draft menu after a successful pick. Off means you can draft a whole loadout in one visit.");
            CloneDraftedItem = Config.Bind("Draft", "CloneDraftedItem", true,
                "Hand the run a fresh copy of the item instead of the pooled instance. Leave on unless drafted items behave oddly.");

            AddComponent<DraftBehaviour>();

            try
            {
                Harmony.CreateAndPatchAll(typeof(CompendiumPatches), Guid);
                Log.LogInfo("[Draft] Compendium patches applied.");
            }
            catch (Exception e)
            {
                Log.LogError($"[Draft] Harmony patching failed - the draft menu will open but picks will do nothing: {e}");
            }

            Log.LogInfo($"{Name} v{Version} loaded. Press {Hotkey.Value} to open the draft menu.");
        }

        public override bool Unload()
        {
            HarmonyLib.Harmony.UnpatchID(Guid);
            return true;
        }
    }
}
