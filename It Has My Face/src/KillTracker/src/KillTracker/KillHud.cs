using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KillTracker
{
    /// <summary>
    /// Two pieces of screen, both in the game's own font: a line under the crosshair for a few seconds after
    /// each kill, and the full table while the peek key is held. Nothing is on screen otherwise.
    /// </summary>
    internal sealed class KillHud : MonoBehaviour
    {
        private const float LineHoldSeconds = 1.75f;
        private const float LineFadeSeconds = 0.75f;
        private const float Padding = 14f;
        private static readonly Color Backing = new Color(0f, 0f, 0f, 0.55f);

        private RectTransform lineBox;
        private CanvasGroup lineGroup;
        private TextMeshProUGUI lineText;
        private RectTransform tableBox;
        private TextMeshProUGUI tableText;
        private bool fontResolved;

        private string lineKind;
        private int lineBurst;
        private float lineShownAt = -100f;
        private bool peeking;

        private void Awake()
        {
            var canvasObject = new GameObject("KillTracker HUD");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Under the crosshair, where the player is already looking when they kill.
            lineBox = Box(canvasObject.transform, "Kill line", new Vector2(0.5f, 0.5f), new Vector2(0f, -90f));
            lineGroup = lineBox.gameObject.AddComponent<CanvasGroup>();
            lineGroup.alpha = 0f;
            lineText = Text(lineBox, 24f, TextAlignmentOptions.Center);

            tableBox = Box(canvasObject.transform, "Kill table", new Vector2(0f, 1f), new Vector2(40f, -40f));
            tableText = Text(tableBox, 22f, TextAlignmentOptions.TopLeft);
            tableBox.gameObject.SetActive(false);

            KillSources.Killed += OnKilled;
        }

        private void OnDestroy()
        {
            KillSources.Killed -= OnKilled;
        }

        private void Update()
        {
            ReadInput();

            float age = Time.unscaledTime - lineShownAt;
            lineGroup.alpha = age < LineHoldSeconds ? 1f : Mathf.Clamp01(1f - (age - LineHoldSeconds) / LineFadeSeconds);

            tableBox.gameObject.SetActive(peeking);
            if (peeking)
            {
                ResolveFont();
                SetText(tableBox, tableText, TableText());
            }
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            Key key = Plugin.PeekKey.Value;
            if (keyboard == null || key == Key.None)
            {
                return;
            }

            var control = keyboard[key];
            if (Plugin.HoldToPeek.Value)
            {
                peeking = control.isPressed;
            }
            else if (control.wasPressedThisFrame)
            {
                peeking = !peeking;
            }
        }

        private void OnKilled(string kind)
        {
            if (!Plugin.ShowKillLine.Value)
            {
                return;
            }

            // A burst of the same kind (an explosion, a spree) reads as one line: "Innocent +3".
            bool sameBurst = kind == lineKind && Time.unscaledTime - lineShownAt < LineHoldSeconds + LineFadeSeconds;
            lineBurst = sameBurst ? lineBurst + 1 : 1;
            lineKind = kind;
            lineShownAt = Time.unscaledTime;

            Ledger ledger = Plugin.Ledger;
            ResolveFont();
            SetText(lineBox, lineText,
                $"<b>{kind}</b> +{lineBurst}<alpha=#99>    level {ledger.Level(kind)}   run {ledger.Run(KillSources.Mode, kind)}" +
                $"   lifetime {ledger.Lifetime(kind)}");
        }

        private static string TableText()
        {
            Ledger ledger = Plugin.Ledger;
            string mode = KillSources.Mode;
            var text = new StringBuilder();
            // TMP has no </alpha>: dimming lasts until <alpha=#FF>.
            text.Append("<b>Kills</b><alpha=#99>   ").Append(mode).Append(" run\n");
            text.Append("<pos=230>level<pos=330>run<pos=420>lifetime<alpha=#FF>\n");

            bool any = false;
            foreach (string kind in ledger.Kinds)
            {
                any = true;
                Row(text, kind, ledger.Level(kind), ledger.Run(mode, kind), ledger.Lifetime(kind));
            }

            if (!any)
            {
                text.Append("<alpha=#99>No kills yet.");
                return text.ToString();
            }

            Row(text, "<b>Total</b>", ledger.LevelTotal, ledger.RunTotal(mode), ledger.LifetimeTotal);
            return text.ToString().TrimEnd('\n');
        }

        private static void Row(StringBuilder text, string label, int level, int run, int lifetime)
        {
            text.Append(label)
                .Append("<pos=230>").Append(level)
                .Append("<pos=330>").Append(run)
                .Append("<pos=420>").Append(lifetime)
                .Append('\n');
        }

        private static void SetText(RectTransform box, TextMeshProUGUI label, string value)
        {
            label.text = value;
            Vector2 size = label.GetPreferredValues(value);
            box.sizeDelta = size + new Vector2(Padding * 2f, Padding * 2f);
        }

        private void ResolveFont()
        {
            if (fontResolved)
            {
                return;
            }

            TMP_FontAsset font = null;
            try
            {
                font = TextLocalizer.GetCurrentLocaleFont();
            }
            catch (System.Exception)
            {
                // The localizer is not up yet (main menu boot); borrow the font of any text on screen instead.
            }

            if (font == null)
            {
                TextMeshProUGUI any = FindAnyObjectByType<TextMeshProUGUI>();
                font = any != null && any != lineText && any != tableText ? any.font : null;
            }

            if (font != null)
            {
                lineText.font = font;
                tableText.font = font;
                fontResolved = true;
            }
        }

        private static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 position)
        {
            var box = new GameObject(name).AddComponent<RectTransform>();
            box.SetParent(parent, false);
            box.anchorMin = box.anchorMax = box.pivot = anchor;
            box.anchoredPosition = position;
            var backing = box.gameObject.AddComponent<Image>();
            backing.color = Backing;
            backing.raycastTarget = false;
            return box;
        }

        private static TextMeshProUGUI Text(RectTransform box, float size, TextAlignmentOptions alignment)
        {
            var text = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            var rect = text.rectTransform;
            rect.SetParent(box, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(Padding, Padding);
            rect.offsetMax = new Vector2(-Padding, -Padding);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
