using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using FallenAces;
using FallenAces.HUD;
using FallenAces.NewWorldGen;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallenAcesInventoryExpansion
{
    [BepInPlugin(Id, "Fallen Aces Expanded Inventory", "1.1.0")]
    public sealed class InventoryExpansionPlugin : BaseUnityPlugin
    {
        public const string Id = "local.codex.fallenacesexpandedinventory";
        private static readonly FieldInfo SlotsField = AccessTools.Field(typeof(Inventory), "_slots");
        private static readonly MethodInfo AddSlot = AccessTools.Method(typeof(Inventory), "AddSlot");
        private static readonly MethodInfo SlotKey = AccessTools.Method(typeof(PlayerInventory), "OnSlotKeyPressed");
        private ConfigEntry<int> normalSlots;
        private ConfigEntry<bool> showList;
        private Harmony harmony;
        private PlayerInventory inventory;
        private GUIStyle textStyle;
        private static readonly UnityEngine.InputSystem.Key[] ExtraKeys = { UnityEngine.InputSystem.Key.Digit6, UnityEngine.InputSystem.Key.Digit7, UnityEngine.InputSystem.Key.Digit8, UnityEngine.InputSystem.Key.Digit9, UnityEngine.InputSystem.Key.Digit0 };

        internal static List<Inventory.Slot> Slots(Inventory value) => (List<Inventory.Slot>)SlotsField.GetValue(value);

        private void Awake()
        {
            normalSlots = Config.Bind("Inventory", "NormalSlots", 15,
                new ConfigDescription("Minimum general-purpose slots, in addition to gadgets. Restart to apply. Existing saved slots are never removed.", new AcceptableValueRange<int>(3, 20)));
            showList = Config.Bind("Display", "ShowInventoryList", true, "Show the inventory list on the right during gameplay.");
            harmony = new Harmony(Id);
            harmony.PatchAll(typeof(InventoryExpansionPlugin).Assembly);
            WorldLoader.WorldStartPart3 += OnWorldReady;
            Logger.LogInfo("Loaded: " + normalSlots.Value + " general slots; mouse wheel cycles all slots; 6-0 select slots 6-10.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-inventory-smoke-test") >= 0)
            {
                var test = SmokeTest();
                while (test.MoveNext()) { }
            }
        }

        private void OnDestroy()
        {
            WorldLoader.WorldStartPart3 -= OnWorldReady;
            harmony?.UnpatchSelf();
        }

        private void OnWorldReady()
        {
            if (Player.Instance == null) return;
            inventory = Player.Instance.GetComponent<PlayerInventory>();
            if (inventory == null) return;
            // Wait until native save/transition deserialization has rebuilt gadget slot indices.
            EnsureCapacity(inventory, normalSlots.Value);
            Logger.LogInfo("Inventory ready: " + inventory.Capacity + " total slots.");
        }

        internal static void EnsureCapacity(Inventory value, int target)
        {
            int count = 0;
            foreach (var slot in Slots(value)) if (slot.Type == Inventory.Slot.TypeID.Normal) count++;
            for (; count < target; count++) AddSlot.Invoke(value, new object[] { new Inventory.Slot(Inventory.Slot.TypeID.Normal) });
        }

        private void Update()
        {
            if (inventory == null || WorldLoader.AnyWorldIsLoading) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            // Reuse the game's input gate and holster handling.
            for (int i = 0; i < 5; i++)
                if (keyboard[ExtraKeys[i]].wasPressedThisFrame)
                    SlotKey.Invoke(inventory, new object[] { i + 5 });
        }

        private void OnGUI()
        {
            if (!showList.Value || inventory == null || WorldLoader.AnyWorldIsLoading || PlayerHud.Instance == null || !PlayerHud.Instance.gameObject.activeInHierarchy) return;
            if (GlobalEventManager.TryGet(out var events) && (events.RequestValue<bool>(3) || events.RequestValue<bool>(4))) return;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = false, clipping = TextClipping.Clip };
            float width = Math.Min(290f, Screen.width * 0.32f);
            float row = Math.Min(24f, (Screen.height - 90f) / (inventory.Capacity + 2));
            float height = row * (inventory.Capacity + 2) + 12f;
            float x = Screen.width - width - 16f;
            float y = Math.Max(16f, (Screen.height - height) * 0.5f);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.Label(new Rect(x + 10, y + 6, width - 20, row), "INVENTORY  ·  Scroll to select", textStyle);
            var slots = Slots(inventory);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                string name = slot.Item.IsNullOrDestroyed() ? "Empty" : slot.Item.Name;
                if (!slot.Item.IsNullOrDestroyed() && slot.Item.StackCount > 1) name += " x" + slot.Item.StackCount;
                if (slot.Type != Inventory.Slot.TypeID.Normal) name += slot.Type == Inventory.Slot.TypeID.AmmoPouch ? " [Ammo]" : " [Food]";
                textStyle.normal.textColor = inventory.CurrentSlotHighlighted == i ? new Color(1f, 0.8f, 0.25f) : Color.white;
                GUI.Label(new Rect(x + 10, y + 6 + row * (i + 1), width - 20, row), (inventory.CurrentSlotHighlighted == i ? "> " : "  ") + (i + 1) + "  " + name, textStyle);
            }
            textStyle.normal.textColor = Color.white;
        }

        [HarmonyPatch(typeof(PlayerInventory), "Scroll")]
        private static class ScrollPatch
        {
            private static bool Prefix(PlayerInventory __instance, int direction)
            {
                int next = InventoryLogic.NextSlot(__instance.CurrentSlotHighlighted, __instance.Capacity, direction);
                if (next != __instance.CurrentSlotHighlighted) __instance.HighlightSlot(next);
                return false;
            }
        }

        // The native HUD assumes the remaining pouch is index 3 when a gadget is removed.
        [HarmonyPatch(typeof(HudBox), "OnSlotRemoved")]
        private static class GadgetHudPatch
        {
            private static void Postfix(HudBox __instance)
            {
                var player = AccessTools.Field(typeof(HudBox), "_playerInventory").GetValue(__instance) as PlayerInventory;
                var ui = AccessTools.Field(typeof(HudBox), "_slots").GetValue(__instance) as FallenAces.HUD.HudBoxNS.Slot[];
                if (player == null || ui == null || ui.Length < 5) return;
                if (player.HasSlotOfType(Inventory.Slot.TypeID.AmmoPouch, out int ammo)) ui[3].ChangeSlotNumber(ammo);
                if (player.HasSlotOfType(Inventory.Slot.TypeID.Lunchbox, out int food)) ui[4].ChangeSlotNumber(food);
            }
        }

        [HarmonyPatch(typeof(PlayerInventory), "RemoveExtraSlot")]
        private static class GadgetSelectionPatch
        {
            private static void Prefix(PlayerInventory __instance, Inventory.Slot.TypeID slotType, out int[] __state)
            {
                __instance.HasSlotOfType(slotType, out int removed);
                __state = new[] { removed, __instance.CurrentSlotHighlighted };
            }
            private static void Postfix(PlayerInventory __instance, int[] __state)
            {
                if (__state[0] < 0) return;
                if (__state[1] > __state[0]) __instance.HighlightSlot(__state[1] - 1);
                var last = AccessTools.Field(typeof(PlayerInventory), "_lastHighlightedSlotBeforeHolster");
                int index = (int)last.GetValue(__instance);
                if (index >= __state[0]) last.SetValue(__instance, index == __state[0] ? 0 : index - 1);
            }
        }

        private static void CheckSaveRoundTrip(Inventory value)
        {
            using (var stream = new MemoryStream())
            {
                value.SerializeSaveData(new DataSerializer(stream, SerializationMode.Write, 999));
                Slots(value).Clear();
                for (int i = 0; i < 3; i++) Slots(value).Add(new Inventory.Slot(Inventory.Slot.TypeID.Normal));
                stream.Position = 0;
                value.SerializeSaveData(new DataSerializer(stream, SerializationMode.Read, 999));
                if (value.Capacity != 16 || Slots(value)[3].Type != Inventory.Slot.TypeID.AmmoPouch || stream.Position != stream.Length)
                    throw new Exception("Native slot serialization round trip failed");
            }
        }

        private IEnumerator SmokeTest()
        {
            yield return null;
            GameObject test = new GameObject("Inventory expansion verification");
            test.SetActive(false);
            try
            {
                var value = test.AddComponent<Inventory>();
                var slots = Slots(value);
                slots.Clear();
                for (int i = 0; i < 3; i++) slots.Add(new Inventory.Slot(Inventory.Slot.TypeID.Normal));
                slots.Add(new Inventory.Slot(Inventory.Slot.TypeID.AmmoPouch));
                EnsureCapacity(value, 15);
                EnsureCapacity(value, 15);
                if (value.Capacity != 16 || slots[3].Type != Inventory.Slot.TypeID.AmmoPouch) throw new Exception("Capacity/gadget preservation failed");
                EnsureCapacity(value, 3);
                if (value.Capacity != 16) throw new Exception("Existing inventory was shrunk");
                CheckSaveRoundTrip(value);
                if (Harmony.GetPatchInfo(AccessTools.Method(typeof(PlayerInventory), "Scroll")) == null) throw new Exception("Scroll patch missing");
                Logger.LogInfo("SMOKE PASS: native slot addition, gadget preservation, repeated expansion, non-shrinking capacity, empty-slot save round trip and Harmony startup.");
            }
            catch (Exception e) { Logger.LogError("SMOKE FAIL: " + e); }
            finally { Destroy(test); }
            Application.Quit();
        }
    }
}

