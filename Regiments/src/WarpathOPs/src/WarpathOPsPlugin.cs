using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace WarpathOPs
{
    [BepInPlugin(Guid, Name, Version)]
    public class WarpathOPsPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.regiments.warpathops";
        public const string Name = "Warpath OPs";
        public const string Version = "1.1.0";
        private const int MaxOPs = 99999;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> TargetOPs;
        internal static ConfigEntry<bool> AlsoCampaign;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Keep Operation Points topped up in Warpath runs.");
            // The OP counter shows five digits; a sixth wraps onto a second line. The range also
            // clamps the old 999999 default in existing config files.
            TargetOPs = Config.Bind("General", "TargetOPs", MaxOPs,
                new ConfigDescription("Your OP balance is held at this amount whenever the game reads it.",
                    new AcceptableValueRange<int>(0, MaxOPs)));
            AlsoCampaign = Config.Bind("General", "AlsoCampaign", false,
                "Also apply to the regular (scripted) campaign operations, not just Warpath.");

            new Harmony(Guid).PatchAll(typeof(Patches));
            Log.LogInfo(Name + " " + Version + " loaded (target " + TargetOPs.Value + " OPs, campaign " + AlsoCampaign.Value + ")");
        }
    }
}
