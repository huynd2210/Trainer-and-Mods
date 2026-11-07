using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PotatoPeeler;

namespace SteadyHands
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "dev.potatoplease.steadyhands";
        public const string Name = "Potato Please - Steady Hands";
        public const string Version = "1.0.0";

        internal static new ManualLogSource Log { get; private set; }

        internal static ConfigEntry<bool> RemoveTremor { get; private set; }
        internal static ConfigEntry<bool> RemoveDrift { get; private set; }
        internal static ConfigEntry<bool> RemoveExhaustShake { get; private set; }

        public override void Load()
        {
            Log = base.Log;

            RemoveTremor = Config.Bind("SteadyHands", "RemoveTremor", true,
                "Remove the fast hand tremor that shakes the peeler while you cut.");
            RemoveDrift = Config.Bind("SteadyHands", "RemoveDrift", true,
                "Remove the slow hand drift that pulls the peeler off where you aim.");
            RemoveExhaustShake = Config.Bind("SteadyHands", "RemoveExhaustShake", true,
                "Remove the shake when your peeling stamina runs out. Stamina itself still drains and refills as normal.");

            try
            {
                Harmony.CreateAndPatchAll(typeof(PeelSessionPatches), Guid);
                Log.LogInfo($"{Name} v{Version} loaded (tremor off: {RemoveTremor.Value}, drift off: {RemoveDrift.Value}, exhaust shake off: {RemoveExhaustShake.Value}).");
            }
            catch (Exception e)
            {
                Log.LogError($"Patching failed, hands will still shake: {e}");
            }
        }

        public override bool Unload()
        {
            HarmonyLib.Harmony.UnpatchID(Guid);
            return true;
        }
    }

    [HarmonyPatch(typeof(PeelSession))]
    internal static class PeelSessionPatches
    {
        private static bool _reported;
        private static bool _wasActive;
        private static float _maxNoise;

        // The game rebuilds its peel tuning (tool switch, upgrades) from many places,
        // so steady it after every rebuild and again at the start of every frame.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PeelSession.RecomputeTuning))]
        private static void AfterRecomputeTuning(PeelSession __instance) => Steady(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PeelSession.Update))]
        private static void BeforeUpdate(PeelSession __instance) => Steady(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PeelSession.LateUpdate))]
        private static void BeforeLateUpdate(PeelSession __instance)
        {
            Steady(__instance);
            Watch(__instance);
        }

        private static void Steady(PeelSession s)
        {
            try
            {
                if (s == null) return;
                var t = s._tuning;
                bool tremor = Plugin.RemoveTremor.Value;
                bool drift = Plugin.RemoveDrift.Value;

                if (t != null && !_reported)
                {
                    _reported = true;
                    Plugin.Log.LogInfo($"Game hand settings: tremor {t.HandTremorAmplitude:0.####} @ {t.HandTremorFrequency:0.##} Hz, drift {t.HandDriftAmplitude:0.####} @ {t.HandDriftFrequency:0.##} Hz, exhaust shake {s.exhaustShakeAmplitude:0.####}.");
                }

                if (t != null)
                {
                    if (tremor) t.HandTremorAmplitude = 0f;
                    if (drift) t.HandDriftAmplitude = 0f;
                }
                // The developers' own switch for turning hand noise off.
                if (tremor && drift) s.debugNoHandNoise = true;
                if (Plugin.RemoveExhaustShake.Value) s.exhaustShakeAmplitude = 0f;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Steady failed: {e}");
            }
        }

        // Reports how far the hand actually wobbled during each peel, so the log shows whether it worked.
        private static void Watch(PeelSession s)
        {
            try
            {
                bool active = s != null && s.IsActive;
                if (active)
                {
                    float n = s._aimHandNoise.magnitude;
                    if (n > _maxNoise) _maxNoise = n;
                }
                else if (_wasActive)
                {
                    Plugin.Log.LogInfo($"Peel finished. Largest hand wobble this peel: {_maxNoise:0.######} (0 = perfectly steady).");
                    _maxNoise = 0f;
                }
                _wasActive = active;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Watch failed: {e}");
            }
        }
    }
}
