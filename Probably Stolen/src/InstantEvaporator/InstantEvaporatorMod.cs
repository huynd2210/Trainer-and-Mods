using System;
using HarmonyLib;

namespace InstantEvaporator
{
    public class InstantEvaporatorMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable() { }

        public void OnDisable() { }
    }

    // The Evaporator does all its work in onCycleEndSlotItemFunc, which the game runs once
    // per night: up to 300 ml (plus performance modules), one night of Energy Cell power,
    // distillate by quality bonus. This runs that same step every one-second tick as well,
    // so a night's worth of evaporating happens each tick. The step itself refuses to run
    // (and draws no power) when there's no cell or nothing evaporative left, so an idle or
    // dry evaporator costs nothing. Loading a save rebuilds machines through this factory,
    // so evaporators already placed get it too.
    [HarmonyPatch(typeof(MachineEvaporator), nameof(MachineEvaporator.Evaporator))]
    internal static class EvaporatorFactoryPatch
    {
        private static void Postfix(GameItem __result)
        {
            GameItem machine = __result;
            Action<GameItem, GameInventory, SlotMarker> previous = machine.onOneSecondItemFunc;
            machine.onOneSecondItemFunc = delegate(GameItem item, GameInventory inventory, SlotMarker slot)
            {
                previous?.Invoke(item, inventory, slot);
                machine.onCycleEndSlotItemFunc?.Invoke(machine, machine.parentInventory, machine.GetIncompleteSlotInInventory());
            };
        }
    }
}
