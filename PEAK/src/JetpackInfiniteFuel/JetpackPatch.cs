using System.Runtime.CompilerServices;
using HarmonyLib;

namespace JetpackInfiniteFuel
{
    [HarmonyPatch(typeof(CharacterMovement), "TryJetpack")]
    internal static class JetpackPatch
    {
        private const float FullFuel = 100f;

        private static readonly ConditionalWeakTable<CharacterMovement, OriginalJetpackSettings>
            OriginalSettings = new ConditionalWeakTable<CharacterMovement, OriginalJetpackSettings>();

        [HarmonyPrefix]
        private static void ApplyConfiguredSettings(CharacterMovement __instance)
        {
            if (__instance == null)
            {
                return;
            }

            OriginalJetpackSettings original = OriginalSettings.GetValue(
                __instance,
                movement => new OriginalJetpackSettings(
                    movement.jetpackFuelDecaySpeed,
                    movement.jetpackForce));

            float fuelMultiplier = JetpackInfiniteFuelPlugin.FuelMultiplier.Value;
            if (fuelMultiplier <= 0f)
            {
                fuelMultiplier = 1f;
            }

            float speedMultiplier = JetpackInfiniteFuelPlugin.SpeedMultiplier.Value;
            if (speedMultiplier < 0f)
            {
                speedMultiplier = 0f;
            }

            __instance.jetpackFuelDecaySpeed = JetpackInfiniteFuelPlugin.InfiniteFuel.Value
                ? 0f
                : original.FuelDecaySpeed / fuelMultiplier;
            __instance.jetpackForce = original.Force * speedMultiplier;

            if (JetpackInfiniteFuelPlugin.InfiniteFuel.Value)
            {
                RefillFuel(__instance);
            }
        }

        [HarmonyPostfix]
        private static void KeepInfiniteFuelFull(CharacterMovement __instance)
        {
            if (JetpackInfiniteFuelPlugin.InfiniteFuel.Value)
            {
                RefillFuel(__instance);
            }
        }

        private static void RefillFuel(CharacterMovement movement)
        {
            if (movement == null)
            {
                return;
            }

            Character character = Traverse.Create(movement).Field("character").GetValue<Character>();
            if (character == null || !character.IsLocal || character.player == null ||
                character.player.backpackSlot == null || character.player.backpackSlot.data == null)
            {
                return;
            }

            ItemInstanceData data = character.player.backpackSlot.data;
            if (data.TryGetDataEntry<FloatItemData>(DataEntryKey.Fuel, out FloatItemData fuel))
            {
                fuel.Value = FullFuel;
            }

            if (data.TryGetDataEntry<FloatItemData>(
                    DataEntryKey.UseRemainingPercentage,
                    out FloatItemData percentage))
            {
                percentage.Value = 1f;
            }
        }

        private sealed class OriginalJetpackSettings
        {
            internal OriginalJetpackSettings(float fuelDecaySpeed, float force)
            {
                FuelDecaySpeed = fuelDecaySpeed;
                Force = force;
            }

            internal float FuelDecaySpeed { get; }

            internal float Force { get; }
        }
    }
}
