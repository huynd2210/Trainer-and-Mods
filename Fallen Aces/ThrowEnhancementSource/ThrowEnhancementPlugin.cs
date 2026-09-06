using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using FallenAces;
using FallenAces.PlayerHands;
using HarmonyLib;
using UnityEngine;

namespace FallenAcesThrowEnhancement
{
    [BepInPlugin(Id, "Fallen Aces Enhanced Throwing", "1.0.0")]
    public sealed class ThrowEnhancementPlugin : BaseUnityPlugin
    {
        public const string Id = "local.codex.fallenacesthrowenhancement";
        internal static ConfigEntry<float> Speed, Distance;
        private Harmony harmony;
        [ThreadStatic] internal static int ThrowDepth;
        private static ProjectileDefinition genericThrow;
        private static readonly FieldInfo Preview = AccessTools.Field(typeof(PlayerRightHand), "_projectileTrajectory");
        private static readonly FieldInfo PreviewDefinition = AccessTools.Field(typeof(PlayerRightHand), "_projectileTrajectoryProjectile");
        private void Awake()
        {
            Speed = Config.Bind("Throwing", "SpeedMultiplier", 3f, new ConfigDescription("Player throw launch-speed multiplier.", new AcceptableValueRange<float>(1f, 10f)));
            Distance = Config.Bind("Throwing", "DistanceMultiplier", 9f, new ConfigDescription("Projectile travel-distance limit multiplier. Actual range also depends on gravity and obstacles.", new AcceptableValueRange<float>(1f, 30f)));
            harmony = new Harmony(Id);
            harmony.PatchAll(typeof(ThrowEnhancementPlugin).Assembly);
            Logger.LogInfo("Loaded: " + Speed.Value + "x throw speed, " + Distance.Value + "x distance limit; matching preview; damage unchanged.");
        }
        private void OnDestroy() { harmony?.UnpatchSelf(); }
        [HarmonyPatch(typeof(PlayerProjectileSpawner), "Awake")]
        private static class SpawnerPatch
        {
            private static void Postfix(ProjectileDefinition ____genericThrownPropProjectile) { genericThrow = ____genericThrownPropProjectile; }
        }
        [HarmonyPatch(typeof(PlayerProjectileSpawner), "OnSpawnThrownProjectileEvent")]
        private static class ThrowScope
        {
            private static void Prefix(out int __state) { __state = ThrowDepth; ThrowDepth++; }
            private static void Finalizer(int __state) { ThrowDepth = __state; }
        }
        [HarmonyPatch(typeof(Projectile), "Fire")]
        private static class FirePatch
        {
            private static void Prefix(Projectile __instance, bool ____fired, ref float ____speed, ref float ____maxDistance)
            {
                if (____fired || ThrowDepth == 0 || !__instance.IsPlayerProjectile) return;
                ____speed *= Speed.Value;
                ____maxDistance *= Distance.Value;
            }
        }
        [HarmonyPatch]
        private static class BodyPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (var type in new[] { typeof(EnemyPickupableHandler), typeof(GenericPickupableBodyHandler) })
                {
                    var map = type.GetInterfaceMap(typeof(IPickupableBody));
                    for (int i = 0; i < map.InterfaceMethods.Length; i++)
                        if (map.InterfaceMethods[i].Name == "GetThrown") yield return map.TargetMethods[i];
                }
            }
            private static float ScaleForce(float force) => ThrowDepth > 0 ? force * Speed.Value : force;
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int matches = 0;
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                    if (instruction.opcode == OpCodes.Ldc_R4 && ((float)instruction.operand == 25f || (float)instruction.operand == 12.5f))
                    {
                        matches++;
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BodyPatch), "ScaleForce"));
                    }
                }
                if (matches != 1) throw new InvalidOperationException("Body throw force calculation changed; game version unsupported.");
            }
        }
        [HarmonyPatch(typeof(PlayerRightHand), "CalculateProjectileTrajectory")]
        private static class PreviewPatch
        {
            private static bool Prefix(PlayerRightHand __instance)
            {
                var trajectory = (PlayerProjectileTrajectory)Preview.GetValue(__instance);
                ProjectileDefinition definition = genericThrow ?? (ProjectileDefinition)PreviewDefinition.GetValue(__instance);
                var item = __instance.CurrentItemEquipped;
                if (item != null && item.GameObject != null && item.GameObject.TryGetComponent<Prop>(out var prop))
                {
                    var overridden = prop.Definition.GetOverriddenThrownProjectile(item.WeaponInfo.CurrentlyInAlternateVariant, prop.IsBroken);
                    if (overridden != null) definition = overridden;
                }
                if (trajectory == null || definition == null || Player.Instance == null) return true;
                float speed = definition.Speed * Speed.Value * (Player.Instance.HasStrengthBoost ? 1.25f : 1f);
                float distance = definition.MaxDistance * Distance.Value;
                // Native weight adjustments do not change speed/gravity/distance. Native strength changes speed only.
                trajectory.CalculatePoints(Player.PlayerInterpolatedHeadPosition, Player.PlayerHeadDirection,
                    Player.PlayerHeadSideDirection, speed, distance, definition.Gravity, true, 5f);
                return false;
            }
        }
    }
}
