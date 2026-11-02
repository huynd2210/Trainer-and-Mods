using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PrecisionPipettes
{
    public class PrecisionPipettesMod : IMod
    {
        internal const string Id = "PrecisionPipettes";

        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            // The game builds its mod item directory when the game scene loads. If that has
            // already happened, register straight away as well.
            ModHook.OnModItemDirectoryInit += Register;
            ModItemDirectory live = Object.FindObjectOfType<ModItemDirectory>();
            if (live != null && live.factoryDictionary != null)
                Register(live);
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= Register;
            ModItemDirectory live = Object.FindObjectOfType<ModItemDirectory>();
            if (live != null && live.factoryDictionary != null)
                foreach (Pipette pipette in Pipette.All)
                    live.Remove(pipette.ItemId);
        }

        private static void Register(ModItemDirectory directory)
        {
            foreach (Pipette pipette in Pipette.All)
            {
                Pipette captured = pipette;
                directory.Remove(captured.ItemId);
                directory.Add(captured.ItemId, captured.Create);
            }
        }
    }

    // One entry per pipette; everything else is shared. Volumes are in the game's liquid
    // parts (1 ml = 1000), which is what every transfer works in, so sub-millilitre
    // capacities need nothing special beyond the tooltip.
    internal class Pipette
    {
        internal static readonly List<Pipette> All = new List<Pipette>
        {
            new Pipette("dropper_05ml", "0.5ml", 500, hue: 125f / 360f, price: 150),
            new Pipette("dropper_01ml", "0.1ml", 100, hue: 0f, price: 200),
        };

        // Both are drawn from the vanilla 1 ml dropper, whose bulb is teal; its liquid
        // overlay fits them unchanged.
        private const string Atlas = "Items/water_container3";
        private const string BaseSprite = "1ml";

        internal readonly string ItemId;
        private readonly string label;
        private readonly int capacityParts;
        private readonly float hue;
        private readonly int price;

        private Pipette(string itemId, string label, int capacityParts, float hue, int price)
        {
            ItemId = itemId;
            this.label = label;
            this.capacityParts = capacityParts;
            this.hue = hue;
            this.price = price;
        }

        internal static Pipette Find(GameItem item)
        {
            return item == null ? null : All.Find(p => p.ItemId == item.identifier);
        }

        internal GameItem Create()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(ItemId).SetName(Name());
            if (EnsureSprite())
                item.SetSpriteAndShapeFromMod(PrecisionPipettesMod.Id, ItemId);
            else
                item.SetSpriteAndShape(Atlas, BaseSprite);
            item.unitValue = price;
            item.identifier = ItemId;
            item.flavorText = "For doses smaller than a millilitre. Draws from a container when empty, drops all of it when full.";

            // Same set-up as the vanilla droppers, then the real (sub-ml) capacity.
            WaterHelper.InitLiquidContainerItem(item, 1, price, isFilterContainer: false, isGraduated: true, isDropper: true);
            int parts = capacityParts;
            item.ModifyTag("LIQUID_CONTAINER_CAPACITY", state => { state.Enable().SetInt(parts); });

            // The vanilla overlay looks up "<sprite>_<colour>" in the atlas, and our sprite
            // isn't in it; use the 1 ml dropper's overlay, which has the same shape.
            item.getItemSpriteModifierForItemFunc = delegate(GameItem gameItem)
            {
                // By volume, not the vanilla percent check: a few hundredths of a ml round to 0%.
                if (WaterHelper.GetTotalVolume(gameItem) == 0)
                    return null;
                string color = WaterHelper.GetLiquidContainerColor(gameItem);
                Sprite fill = RenderHandler.LoadFromAtlas(Atlas, BaseSprite + "_" + Liquid.GetColorBase(color));
                return new List<ItemSpriteModifier>
                {
                    new ItemSpriteModifier(fill, Vector2.zero, 0, isFlipped: false, 0, haveMask: false, MetaHelper.HexToColor(color))
                };
            };
            AudioHelper.InitPlasticBottle(item);
            return item;
        }

        // The game's own 1 ml dropper name with the volume swapped, so it reads like the
        // vanilla ones in any language that writes it as "1ml" or "1 ml".
        private string Name()
        {
            string vanilla = LocHelper.GetLocalizedItem("item_1ml_dropper_name") ?? "";
            if (vanilla.Contains("1ml"))
                return vanilla.Replace("1ml", label);
            if (vanilla.Contains("1 ml"))
                return vanilla.Replace("1 ml", label.Replace("ml", " ml"));
            return label + " Pipette";
        }

        // Recolours the teal bulb of the vanilla sprite to this pipette's hue, keeping its
        // shading, and stores it as one of this mod's item sprites. Built once, at runtime,
        // so no game art ships with the mod.
        private bool EnsureSprite()
        {
            LoadedMod mod = ModLoader.Mods.Find(m => m.Manifest.ID == PrecisionPipettesMod.Id);
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
                    // Teal: the bulb's three shades sit at 175-178 degrees.
                    if (ps > 0.2f && ph > 0.40f && ph < 0.58f)
                    {
                        Color recoloured = Color.HSVToRGB(hue, ps, pv);
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
                Debug.LogWarning("[PrecisionPipettes] Could not recolour the dropper sprite, using the vanilla one: " + e.Message);
                return false;
            }
        }

        // The vanilla tooltip prints volumes with no decimals, which shows 0.5 ml as "0 ml".
        internal void AddVolumeTooltip(RichTextBuilder builder, GameItem item)
        {
            int capacity = WaterHelper.GetTotalCapacity(item);
            int current = WaterHelper.GetTotalVolume(item);
            builder.AddProgressBar(capacity > 0 ? (float)current / capacity : 0f, 10, RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ");
            builder.AddLine(" " + Ml(current) + " ml / " + Ml(capacity) + " ml", ignoreEmpty: true, RenderHandler.ColorPalette.LightCyan);
        }

        private static string Ml(int parts)
        {
            return ((float)parts / 1000f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // Vanilla refreshes the liquid overlay only for droppers it knows by name.
    [HarmonyPatch(typeof(SpriteHelper), nameof(SpriteHelper.UpdateWaterContainerSprite))]
    internal static class SpriteRefreshPatch
    {
        private static void Postfix(GameItem gameItem)
        {
            if (Pipette.Find(gameItem) == null || !(gameItem is GameItemElement element))
                return;
            element.HandleInnateModSprite();
            WaterHelper.UpdateWaterBottleSound(gameItem);
        }
    }

    [HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.CreateWaterContainerTooltip))]
    internal static class TooltipPatch
    {
        // Replaces only the volume lines; the rest of the vanilla tooltip is rebuilt as is.
        private static bool Prefix(RichTextBuilder builder, GameItem item)
        {
            Pipette pipette = Pipette.Find(item);
            if (pipette == null)
                return true;
            pipette.AddVolumeTooltip(builder, item);
            if (item.IsTag("LAST_SCAN_RESULT_TAG"))
            {
                string scan = item.GetTagReadonly("LAST_SCAN_RESULT_TAG").GetString();
                if (!string.IsNullOrEmpty(scan))
                    builder.AddLine(scan, ignoreEmpty: true, RenderHandler.ColorPalette.LightYellow);
            }
            if (PlayerStore.Instance != null && PlayerStore.Instance.devMode)
            {
                ItemFeature feature = WaterFeatureHelper.GetWaterFeature(item);
                builder.AddLine("[Debug]Real Condition: " + feature.GetBaseRealDisplay());
                builder.AddLine("[Debug]Fake Condition: " + feature.GetBaseFakeDisplay());
                builder.AddLine("Liquid Base: " + Liquid.GetColorBase(WaterHelper.GetLiquidContainerColor(item)));
            }
            return false;
        }
    }

    // Sold wherever the 1 ml dropper is: the Retired Chemist offers both alongside it.
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.TryAddDirectSellingItemToTable), new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(int) })]
    internal static class ShopPatch
    {
        private static void Postfix(PlayerStore __instance, string identifier, bool __result)
        {
            if (!__result || identifier != "1ml_dropper")
                return;
            foreach (Pipette pipette in Pipette.All)
                __instance.TryAddDirectSellingItemToTable(pipette.ItemId);
        }
    }
}
