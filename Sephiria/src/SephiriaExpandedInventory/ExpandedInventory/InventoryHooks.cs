using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpandedInventory
{
    public static class InventoryHooks
    {
        private static int extraRows = -1;

        public static void ExpandPlayerInventory(GridInventory inventory)
        {
            if (inventory == null || !inventory.isServer || !(inventory.UnitAvatar is PlayerAvatar avatar)
                || avatar.Inventory != inventory)
                return;

            if (extraRows < 0)
                extraRows = ReadExtraRows();

            int extraSlots = inventory.Width * extraRows;
            if (extraSlots > 0 && inventory.CurrentInventoryStorage <= short.MaxValue - extraSlots)
            {
                inventory.AddStorage((short)extraSlots);
                Debug.Log("[Expanded Inventory] Added " + extraSlots + " slots (" + extraRows + " rows).");
            }
        }

        public static void ConfigurePanel(UI_CharacterStatusPanel panel)
        {
            if (panel == null || panel.inventoryScrollParent == null)
                return;

            RectTransform? viewport = panel.inventoryScrollParent.parent as RectTransform;
            if (viewport == null)
                return;

            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            ScrollRect? scroll = viewport.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.content = panel.inventoryScrollParent;
                scroll.viewport = viewport;
                scroll.vertical = true;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 26f;
            }

            ConfigureIcons(panel);
        }

        public static void ConfigureIcons(UI_CharacterStatusPanel panel)
        {
            if (panel == null || panel.inventoryZone == null || panel.inventoryScrollParent == null)
                return;

            UI_InventoryIconMergedData[] icons = panel.inventoryZone.GetComponentsInChildren<UI_InventoryIconMergedData>(true);
            foreach (UI_InventoryIconMergedData icon in icons)
                icon.scrollContent = panel.inventoryScrollParent;
        }

        private static int ReadExtraRows()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SephiriaExpandedInventory.cfg"));
            try
            {
                if (File.Exists(path))
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                            continue;
                        string[] parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2 && parts[0].Trim().Equals("ExtraRows", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(parts[1].Trim(), out int rows) && rows >= 1 && rows <= 16)
                            return rows;
                    }
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Expanded Inventory] Could not read config: " + error.Message);
            }
            return 4;
        }
    }
}
