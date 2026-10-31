using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheaterCommand.Game.UI
{
    /// <summary>The game's own fonts, colours and sprites, looked up by name so the screen matches it.</summary>
    public static class Style
    {
        public static readonly Color Gold = Hex("FCB341");
        public static readonly Color Text = Hex("DADADA");
        public static readonly Color TextDim = Hex("A6A6A6");
        public static readonly Color Disabled = new Color(1f, 1f, 1f, 0.2f);
        public static readonly Color Blue = Hex("5CA0E3");
        public static readonly Color BlueLight = Hex("66BBFF");
        // Overwritten with the game's own defeat colour when the screen is built.
        public static Color Red = Hex("D9534F");
        public static readonly Color Panel = new Color(0.07f, 0.075f, 0.08f, 0.92f);

        private static Dictionary<string, Sprite> sprites;
        private static TMP_FontAsset light;
        private static TMP_FontAsset regular;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Sprite Sprite(string name)
        {
            if (sprites == null)
            {
                sprites = new Dictionary<string, Sprite>();
                foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (!sprites.ContainsKey(s.name))
                    {
                        sprites[s.name] = s;
                    }
                }
            }
            return sprites.TryGetValue(name, out Sprite sp) ? sp : null;
        }

        private static TMP_FontAsset Font(bool isLight)
        {
            if (light == null || regular == null)
            {
                TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                light = fonts.FirstOrDefault(f => f.name == "Rubik-Light SDF");
                regular = fonts.FirstOrDefault(f => f.name == "Rubik-Regular SDF");
            }
            return isLight ? light : regular;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            bool isLight = false, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset f = Font(isLight);
            if (f != null)
            {
                t.font = f;
            }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            // Otherwise TMP resizes the rect to the text, which undoes alignment and ellipsis.
            t.autoSizeTextContainer = false;
            return t;
        }

        public static Image Image(Transform parent, string name, string sprite, Color color, bool sliced = false)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? Sprite(sprite) : null;
            img.type = sliced && img.sprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
