using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using FallenAces;
using FallenAces.HUD;
using HarmonyLib;
using UnityEngine;

namespace FallenAcesGunAccuracy
{
    [BepInPlugin(Id, "Fallen Aces Maximum Gun Accuracy", "1.0.0")]
    public sealed class GunAccuracyPlugin : BaseUnityPlugin
    {
        public const string Id = "local.codex.fallenacesgunaccuracy";
        private Harmony harmony;
        private void Awake()
        {
            harmony = new Harmony(Id);
            harmony.PatchAll(typeof(GunAccuracyPlugin).Assembly);
            Logger.LogInfo("Loaded: zero player gun spread, including shotgun pellets.");
        }
        private void OnDestroy() { harmony?.UnpatchSelf(); }
        internal static bool IsGun(Projectile p) => p.IsPlayerProjectile && p.IsSpawnedFromFirstPersonWeapon &&
            (p.Special == ProjectileDefinition.SpecialPreset.Bullet || p.Special == ProjectileDefinition.SpecialPreset.Nail);
        // This runs before Fire's immediate hitscan, not after damage has already happened.
        [HarmonyPatch(typeof(Projectile), "Fire")]
        private static class FirePatch
        {
            private static void Prefix(Projectile __instance, bool ____fired)
            {
                if (!____fired && IsGun(__instance) && Player.Instance != null)
                    __instance.transform.rotation = Player.Instance.Head.rotation;
            }
            private static float FilterSpread(float original, Projectile p) => IsGun(p) ? 0f : original;
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int matches = 0;
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                    if (instruction.Calls(AccessTools.PropertyGetter(typeof(ProjectileDefinition), "RandomSpread")))
                    {
                        matches++;
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FirePatch), "FilterSpread"));
                    }
                }
                if (matches != 1) throw new InvalidOperationException("Expected exactly one Fire spread calculation; game version unsupported.");
            }
        }
        // Keep the displayed firearm reticle consistent with the actual perfect accuracy.
        [HarmonyPatch]
        private static class BloomPatch
        {
            private static System.Reflection.MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.Inner(typeof(Crosshair), "BloomHandler"), "Update");
            private static void Postfix(object __instance, ref float ____bloom, ref float ____focusTime)
            {
                if ((bool)AccessTools.Property(__instance.GetType(), "FirearmMode").GetValue(__instance, null))
                { ____bloom = 0f; ____focusTime = 0f; }
            }
        }
    }
}
