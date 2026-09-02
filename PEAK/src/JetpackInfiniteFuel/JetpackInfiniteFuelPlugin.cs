using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace JetpackInfiniteFuel
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public sealed class JetpackInfiniteFuelPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<bool> InfiniteFuel { get; private set; }

        internal static ConfigEntry<float> FuelMultiplier { get; private set; }

        internal static ConfigEntry<float> SpeedMultiplier { get; private set; }

        private void Awake()
        {
            Log = Logger;

            InfiniteFuel = Config.Bind(
                "Fuel",
                "InfiniteFuel",
                true,
                "When enabled, the jetpack never runs out of fuel. This overrides FuelMultiplier.");

            FuelMultiplier = Config.Bind(
                "Fuel",
                "FuelMultiplier",
                5f,
                new ConfigDescription(
                    "Jetpack duration compared with the normal game when InfiniteFuel is false. " +
                    "For example, 1 is normal fuel and 5 gives five times the normal flight time.",
                    new AcceptableValueRange<float>(0.01f, 1000f)));

            SpeedMultiplier = Config.Bind(
                "Movement",
                "SpeedMultiplier",
                1f,
                new ConfigDescription(
                    "Multiplies jetpack upward thrust. For example, 1 is normal speed and 2 is double thrust.",
                    new AcceptableValueRange<float>(0f, 100f)));

            new Harmony(PluginInfo.Guid).PatchAll(typeof(JetpackInfiniteFuelPlugin).Assembly);

            Log.LogInfo(
                $"{PluginInfo.Name} {PluginInfo.Version} loaded. " +
                $"InfiniteFuel={InfiniteFuel.Value}, FuelMultiplier={FuelMultiplier.Value}, " +
                $"SpeedMultiplier={SpeedMultiplier.Value}");
        }
    }

    internal static class PluginInfo
    {
        internal const string Guid = "com.peakmods.jetpackinfinitefuel";
        internal const string Name = "Jetpack Fuel and Speed";
        internal const string Version = "2.0.0";
    }
}
