using UnityEngine;

namespace SharedStorage
{
    // The Shared Storage Bay: a plain 10x10 storage bay with a cyan lid, so it can be told
    // apart from the vanilla ones at a glance. It takes what a vanilla storage bay takes.
    internal static class Bay
    {
        internal const string ItemId = "shared_storage_bay";

        private const string Atlas = "Items/items_ship";
        private const string BaseSprite = "storage_bay";
        private const float LidHue = 185f / 360f;

        internal static GameItem Create()
        {
            (PixelWindow window, GameInventory inventory) = DirectoryUtils.CreateInventoryWindow(10, 10);
            GameItem item = ItemDirectory.CreateEmptyItem(ItemId).SetContentWindow(window);
            if (EnsureSprite())
                item.SetSpriteAndShapeFromMod(SharedStorageMod.Id, ItemId);
            else
                item.SetSpriteAndShape(Atlas, BaseSprite);
            item.identifier = ItemId;
            item.unitValue = 300L;
            ContainerHelper.InitContainerItem(inventory, item);
            item.name = "Shared Storage Bay";
            item.shortDescription = "Shared by all your saves and runs. Put something in here and it waits in this bay in every other save; take it out and it is gone from them.";
            item.EnableTag("SYSTEM_TAG");
            item.EnableTag("SYSTEM_TAG_UTILITY");
            item.SetGameItemType("STORAGE");
            return item;
        }

        internal static bool Is(GameItem item)
        {
            return item != null && item.identifier == ItemId;
        }

        internal static GameInventory Inventory(GameItem bay)
        {
            return bay?.contentWindow?.child as GameInventory;
        }

        // Says when the bay is not in sync, which is the only time the player needs to know.
        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            if (!Is(item))
                return;
            string problem = Sync.Problem(item);
            if (problem != null)
                builder.AddLine(problem, ignoreEmpty: true, RenderHandler.ColorPalette.LightRed);
        }

        // Recolours the pale lid of the vanilla storage bay to cyan, keeping its shading, and
        // stores it as this mod's item sprite. Built once at runtime, so no game art ships.
        private static bool EnsureSprite()
        {
            LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest.ID == SharedStorageMod.Id);
            if (mod == null)
                return false;
            if (mod.Sprites.ContainsKey(ItemId))
                return true;
            try
            {
                Sprite source = RenderHandler.TryLoadFromAtlas(Atlas, BaseSprite);
                if (source == null)
                    return false;
                Rect rect = source.rect;
                int w = (int)rect.width, h = (int)rect.height;
                Color[] pixels = source.texture.GetPixels((int)rect.x, (int)rect.y, w, h);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.a <= 0f)
                        continue;
                    Color.RGBToHSV(c, out float ph, out float ps, out float pv);
                    // The lid and the frame's highlights are the only light pixels.
                    if (pv > 0.55f)
                    {
                        Color recoloured = Color.HSVToRGB(LidHue, Mathf.Max(ps, 0.5f), pv);
                        recoloured.a = c.a;
                        pixels[i] = recoloured;
                    }
                }
                Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels(pixels);
                texture.Apply();
                // Same pivot and scale the mod loader gives sprites it loads from Textures\Items.
                mod.Sprites[ItemId] = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SharedStorage] Could not recolour the storage bay sprite, using the vanilla one: " + e.Message);
                return false;
            }
        }
    }
}
