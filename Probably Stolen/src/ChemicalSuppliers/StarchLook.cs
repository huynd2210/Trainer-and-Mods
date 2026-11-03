using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ChemicalSuppliers
{
    // Starch has no colour of its own in the game, so it inherits the default water teal.
    // The liquid overlays only come in teal ("blue"), purple ("dark") and pink ("red"), and
    // the colour is multiplied onto one of them, so no colour alone can read as white. Starch
    // gets an off-white colour of its own and a "starch" overlay family: each one is the teal
    // overlay with its shading kept and its hue taken out, built at runtime on first use, so
    // no game art ships with the mod. Only the colour changes; a save holds nothing new.
    internal static class StarchLook
    {
        private const string StarchId = "starch";
        // Not "ffffff": the game treats that one as plain water.
        private const string StarchColor = "f2f2f2";
        internal const string OverlayBase = "starch";
        private const string OverlayToken = "_" + OverlayBase;
        // Lifts the darkest shade so the liquid reads milky rather than grey.
        private const float Lift = 0.3f;

        private static readonly Dictionary<string, Sprite> overlays = new Dictionary<string, Sprite>();
        private static string originalColor;
        internal static bool Active;

        internal static void Apply()
        {
            if (!Liquid.LiquidDict.TryGetValue(StarchId, out Liquid starch))
                return;
            if (!Active)
                originalColor = starch.color;
            starch.color = StarchColor;
            Active = true;
        }

        internal static void Restore()
        {
            if (Active && Liquid.LiquidDict.TryGetValue(StarchId, out Liquid starch))
                starch.color = originalColor;
            Active = false;
        }

        // Only a container of pure starch has exactly this colour; a mix blends it with the
        // other liquids and keeps the vanilla overlay.
        internal static bool IsStarchColor(string hexColor)
        {
            return Active && string.Equals(hexColor, StarchColor, System.StringComparison.OrdinalIgnoreCase);
        }

        // "large_starch_100" -> the "large_blue_100" overlay with its hue removed. Null when the
        // name isn't one of ours or the teal one doesn't exist.
        internal static Sprite Overlay(string atlasPath, string name)
        {
            if (name == null || !name.Contains(OverlayToken))
                return null;
            string key = atlasPath + "/" + name;
            if (overlays.TryGetValue(key, out Sprite cached))
                return cached;

            Sprite source = RenderHandler.TryLoadFromAtlas(atlasPath, name.Replace(OverlayToken, "_blue"));
            if (source == null)
                return null;
            Sprite sprite = source;
            try
            {
                Rect rect = source.rect;
                int w = (int)rect.width, h = (int)rect.height;
                Color[] pixels = source.texture.GetPixels((int)rect.x, (int)rect.y, w, h);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.a <= 0f)
                        continue;
                    float y = Lift + (1f - Lift) * c.grayscale;
                    pixels[i] = new Color(y, y, y, c.a);
                }
                Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels(pixels);
                texture.Apply();
                // Same pivot and scale as the teal overlay, so it sits and sizes the same.
                sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h),
                    new Vector2(source.pivot.x / w, source.pivot.y / h), source.pixelsPerUnit);
                sprite.name = name;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ChemicalSuppliers] Could not build the white starch overlay, using the teal one: " + e.Message);
            }
            overlays[key] = sprite;
            return sprite;
        }
    }

    [HarmonyPatch(typeof(Liquid), nameof(Liquid.GetColorBase))]
    internal static class StarchColorBasePatch
    {
        private static bool Prefix(string hexColor, ref string __result)
        {
            if (!StarchLook.IsStarchColor(hexColor))
                return true;
            __result = StarchLook.OverlayBase;
            return false;
        }
    }

    [HarmonyPatch(typeof(RenderHandler), nameof(RenderHandler.LoadFromAtlas))]
    internal static class StarchOverlayPatch
    {
        private static bool Prefix(string atlasPath, string name, ref Sprite __result)
        {
            Sprite overlay = StarchLook.Overlay(atlasPath, name);
            if (overlay == null)
                return true;
            __result = overlay;
            return false;
        }
    }
}
