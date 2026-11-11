using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FraudDetection
{
    // A small square in the top-right corner of an item you could buy: red with an X when a
    // label claims more than the item really is, green with a + when it has something that
    // could lie and nothing does. Items with nothing to check, and items you already own,
    // get none. Drawn the way the game draws its animal-mark square: a child of the item,
    // one art pixel per gridSize/16, on the item and on the copy being dragged.
    internal static class ItemBadge
    {
        private enum Verdict { None, Honest, Fake }

        // Item properties a label can misstate: the game gives them a fake condition or a
        // hidden value. Everything else on an item (contraband markups, discounts, plant
        // taste, shabby, second hand...) is built already matching its label, so an item
        // with only those, like a Dream Cap, has nothing to catch and gets no badge.
        private static readonly HashSet<string> FakeableCategories = new HashSet<string>
        {
            "CATEGORY_GENUINE_CIGARETTE", "CATEGORY_GENUINE_STAMP", "CATEGORY_GENUINE_INJECTOR",
            "CATEGORY_CHEMICAL_PURITY", "CATEGORY_WATER_PURITY", "CATEGORY_INGOT_PURITY",
            "CATEGORY_EQUIPMENT_CONDITION", "CATEGORY_REPLICA", "CATEGORY_WINE_QUALITY",
            "CATEGORY_FRESHNESS", "CATEGORY_EXPIRED", "CATEGORY_RUSTED", "CATEGORY_DENTED",
            "CATEGORY_OPEN_BOX", "CATEGORY_TRASH_GRADE",
        };

        private const string NodeName = "FraudDetection.Badge";
        private const int Size = 9;

        private static readonly string[] PlusArt =
        {
            "#########",
            "#.......#",
            "#...x...#",
            "#...x...#",
            "#.xxxxx.#",
            "#...x...#",
            "#...x...#",
            "#.......#",
            "#########",
        };

        private static readonly string[] CrossArt =
        {
            "#########",
            "#.......#",
            "#.x...x.#",
            "#..x.x..#",
            "#...x...#",
            "#..x.x..#",
            "#.x...x.#",
            "#.......#",
            "#########",
        };

        // The tooltip's own better/worse colours, outlined in the game's outline colour.
        private static Sprite honestSprite;
        private static Sprite fakeSprite;

        // Shown with the item's own sprite: hidden while it is picked up, and on the dragged
        // copy only while that copy is drawn. The game toggles both sprites in the same place
        // as its mark overlay.
        internal static void Refresh(GameItemElement element)
        {
            if (element == null || element.middle == null)
                return;
            Verdict verdict = element.isDebugMenu || !FraudDetectionMod.Active ? Verdict.None : Judge(element);
            Sprite sprite = verdict == Verdict.Fake ? FakeSprite()
                : verdict == Verdict.Honest ? HonestSprite()
                : null;
            Place(element.middle, sprite, element.background != null && element.background.image.enabled);
            Place(element.fakeMiddle, sprite, element.fakeBackground != null && element.fakeBackground.image.enabled);
        }

        // After the Options toggle: redraw the badge on every item in an open window, the
        // same set the game's band-select walks. Anything else updates when next touched.
        internal static void RefreshVisible()
        {
            try
            {
                WindowsHandler windows = WindowsHandler.current;
                if (windows?.visibleWindows == null)
                    return;
                foreach (PixelWindow window in windows.visibleWindows)
                {
                    if (window == null)
                        continue;
                    foreach (GameItem item in GraphUtils.FindChildrenType<GameItem>(window))
                        Refresh(item as GameItemElement);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FraudDetection] Badge refresh failed: " + e);
            }
        }

        private static Verdict Judge(GameItem item)
        {
            if (GeneralHelper.IsItemOwned(item))
                return Verdict.None;

            bool checkable = false;
            if (item.itemFeatures != null)
            {
                foreach (ItemFeature feature in item.itemFeatures)
                {
                    if (feature == null || feature.isDisabled)
                        continue;
                    if (TruthTooltip.IsLie(feature) && feature.IsRealFeatureCheaper())
                        return Verdict.Fake;
                    if (FakeableCategories.Contains(feature.GetCategory() ?? ""))
                        checkable = true;
                }
            }

            Inspectable kind = Inspectable.Of(item);
            if (kind != null)
            {
                checkable = true;
                if (kind.StillOpen(item) && kind.ClaimedGenuine(item) && kind.IssueIndex(item) >= 0)
                    return Verdict.Fake;
            }
            return checkable ? Verdict.Honest : Verdict.None;
        }

        private static void Place(TreeNodeRender parent, Sprite sprite, bool visible)
        {
            if (parent == null)
                return;
            Transform existing = parent.rectTransform.Find(NodeName);
            TreeNodeRender node = existing != null ? existing.GetComponent<TreeNodeRender>() : null;
            if (sprite == null)
            {
                if (node != null)
                    node.image.enabled = false;
                return;
            }
            if (node == null)
            {
                node = TreeNodeRender.Instantiate().SetParent(parent);
                node.gameObject.name = NodeName;
                node.rectTransform.anchorMin = new Vector2(1f, 1f);
                node.rectTransform.anchorMax = new Vector2(1f, 1f);
                node.rectTransform.pivot = new Vector2(1f, 1f);
                node.image.raycastTarget = false;
                node.image.color = Color.white;
            }
            int px = Mathf.Max(1, UISettings.current.gridSize / 16);
            node.rectTransform.SetAsLastSibling();
            node.rectTransform.sizeDelta = new Vector2(Size * px, Size * px);
            node.rectTransform.anchoredPosition = new Vector2(-px, -px);
            node.image.sprite = sprite;
            node.image.enabled = visible;
        }

        private static Sprite HonestSprite() =>
            honestSprite ?? (honestSprite = Build(PlusArt, RenderHandler.ColorPalette.LightGreen));

        private static Sprite FakeSprite() =>
            fakeSprite ?? (fakeSprite = Build(CrossArt, RenderHandler.ColorPalette.Red));

        private static Sprite Build(string[] art, RenderHandler.ColorPalette fill)
        {
            Color fillColor = RenderHandler.GetColorFromPalette(fill);
            Color outline = RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.Black);
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                    texture.SetPixel(x, Size - 1 - y, art[y][x] == '.' ? fillColor : outline);
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(1f, 1f), 100f);
        }
    }

    // The three places the game refreshes an item's overlays (its mark square among them).
    [HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ValidateSelect))]
    internal static class BadgeValidatePatch
    {
        private static void Postfix(GameItemElement __instance) => ItemBadge.Refresh(__instance);
    }

    [HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ToggleBackground))]
    internal static class BadgeBackgroundPatch
    {
        private static void Postfix(GameItemElement __instance) => ItemBadge.Refresh(__instance);
    }

    [HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ToggleFakeBackground), new[] { typeof(bool), typeof(Vector4) })]
    internal static class BadgeFakeBackgroundPatch
    {
        private static void Postfix(GameItemElement __instance) => ItemBadge.Refresh(__instance);
    }
}
