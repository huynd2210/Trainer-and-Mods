using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PreciseTemperature
{
    public class PreciseTemperatureMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable() { }

        public void OnDisable() { }
    }

    // Vanilla Heating Plate (v0.46D) already caps the liquid at its target temperature, but:
    //  - with 0 Chemical Equipment Modules the target is forced to 99 °C, with 1 to 160 °C,
    //    and the -/+ buttons only appear from 2 modules, in 5 °C steps;
    //  - it tags the plate (not the container) as heating, so ambient cooling still pulls
    //    the liquid a degree or so below the target every second.
    // This builds the same plate with the target settable at any module count (1 °C per
    // click, 10 °C with Shift), and holds the liquid exactly at the target. Max temperature
    // (99 °C without modules, 160 °C with) and heating speed still follow the modules.
    // Loading a save rebuilds machines through this factory, so placed plates get it too;
    // the target is a saved tag, so it survives a save/load.
    [HarmonyPatch(typeof(MachineHeatingPlate), nameof(MachineHeatingPlate.HeatingPlate))]
    internal static class HeatingPlateFactoryPatch
    {
        private static bool Prefix(ref GameItem __result)
        {
            __result = PreciseHeatingPlate.Create();
            return false;
        }
    }

    internal static class PreciseHeatingPlate
    {
        private const int MinTemp = 26; // ambient: the plate can't cool
        private const int SmallStep = 1;
        private const int ShiftStep = 10;

        internal static GameItem Create()
        {
            (MachineryPanelRow row, PixelWindow itemWindow, GameGridInventory moduleGrid, GameSlotInventory containerSlot, MachineryText tempLabel, MachineryRadioList modes) =
                MachineHeatingPlate.CreateMachineInventoryWindow(8, 8);
            MachineryButton buttonMinus = new MachineryButton(LocHelper.GetLocalizedMechanic("mech_heating_plate_btn_minus"));
            MachineryText tempControl = new MachineryText(MetaHelper.FormatTemperature(99), 9, HorizontalAlignmentOptions.Center, uppercase: false).SetFillWidth(fill: false);
            MachineryButton buttonPlus = new MachineryButton(LocHelper.GetLocalizedMechanic("mech_heating_plate_btn_plus"));
            buttonMinus.SetFillWidth(fill: false);
            buttonPlus.SetFillWidth(fill: false);
            buttonMinus.SetDescription("-1 °C. Shift: -10 °C.");
            buttonPlus.SetDescription("+1 °C. Shift: +10 °C.");
            row.Add(MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_heating_plate_label_target_temp"), MachineryStack.Horizontal(buttonMinus, tempControl, buttonPlus), "target"));
            containerSlot.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
            containerSlot.SetBackgroundFadeSprite("Items/water_container4", "xl");

            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(itemWindow).SetSpriteAndShape("Items/items_tool2", "heating_plate");
            containerSlot.onSlotAddItemEndFunc = delegate
            {
                machine.SetSpriteAndShape("Items/items_tool2", "heating_plate_b");
            };
            containerSlot.onSlotRemoveItemEndFunc = delegate
            {
                machine.SetSpriteAndShape("Items/items_tool2", "heating_plate");
            };
            MachineBlender.SetupChemModuleGrid(moduleGrid, machine);
            machine.name = LocHelper.GetLocalizedItem("item_heating_plate_name");
            machine.shortDescription = LocHelper.GetLocalizedItem("item_heating_plate_desc");
            machine.flavorText = "";
            machine.unitValue = 250L;
            machine.EnableTag("CONTAINER_TAG");
            machine.SetGameItemType("MACHINE");
            AudioHelper.InitMetalMachine(machine);
            SetTarget(machine, 99);
            modes.group.onChanged = delegate(int index)
            {
                machine.ModifyTag("HEATING_PLATE_STATE", delegate(TagState state)
                {
                    state.Enable().SetInt(index);
                });
            };
            int initialState = machine.GetTagReadonly("HEATING_PLATE_STATE").GetInt();
            modes.Select((initialState == 1 || initialState == 2) ? initialState : 0);

            machine.onOneSecondItemFunc = delegate
            {
                int state = machine.GetTagReadonly("HEATING_PLATE_STATE").GetInt();
                if (state >= 0 && state <= 2 && modes.selected != state)
                    modes.Select(state);

                // Removing modules lowers the ceiling; pull the target down with it.
                int target = GetTarget(machine);
                int clamped = Clamp(machine, target);
                if (clamped != target)
                    SetTarget(machine, clamped);

                tempControl.SetText(MetaHelper.FormatTemperature(GetTarget(machine)));
                GameItem container = containerSlot.childItem;
                if (container != null && itemWindow.IsVisible())
                    tempLabel.SetText(MetaHelper.FormatTemperature((int)container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble()));
                else
                    tempLabel.SetText(LocHelper.GetLocalizedMechanic("mech_heating_plate_temp_na"));

                if (state != 0 && container != null && !WaterHelper.IsEmpty(container))
                {
                    MachineHeatingPlate.HandleHeating(containerSlot, state, machine);
                    // At or below the target the plate is holding it: mark the container as
                    // heated so ambient cooling skips it this tick. Above the target (after
                    // lowering it) the burner is idle and the liquid cools down normally.
                    if (container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble() <= GetTarget(machine))
                        container.EnableTag("HEATING_TAG", skipValidate: true);
                }
            };
            buttonMinus.SetCallback(delegate
            {
                Step(machine, tempControl, -1);
            });
            buttonPlus.SetCallback(delegate
            {
                Step(machine, tempControl, +1);
            });
            return machine;
        }

        private static void Step(GameItem machine, MachineryText tempControl, int direction)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            int step = shift ? ShiftStep : SmallStep;
            SetTarget(machine, Clamp(machine, GetTarget(machine) + direction * step));
            tempControl.SetText(MetaHelper.FormatTemperature(GetTarget(machine)));
        }

        private static int Clamp(GameItem machine, int target)
        {
            int max = MachineBlender.GetUpgradeCount(machine) == 0 ? MachineHeatingPlate.LOW_TEMP : MachineHeatingPlate.HIGH_TEMP;
            return Math.Max(MinTemp, Math.Min(target, max));
        }

        private static int GetTarget(GameItem machine)
        {
            return machine.GetTagReadonly(MachineHeatingPlate.CURRENT_TARGET_TEMP).GetInt();
        }

        private static void SetTarget(GameItem machine, int value)
        {
            machine.ModifyTag(MachineHeatingPlate.CURRENT_TARGET_TEMP, delegate(TagState state)
            {
                state.Enable().SetInt(value);
            }, skipValidate: true);
        }
    }
}
