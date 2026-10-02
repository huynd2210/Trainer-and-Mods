using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NuclearOptionAutoMissileDefense
{
    public sealed class NativeDefenseButton : MonoBehaviour
    {
        static NativeDefenseButton current;
        internal HUDOptions_ToggleButton native;
        internal Text label;
        bool previousArmed;
        string previousText;

        internal static NativeDefenseButton Create(HUDOptions_ToggleButton template, Transform parent)
        {
            var clone = Object.Instantiate(template, parent, false);
            clone.name = "Auto-missile Defense";
            clone.settings = null;
            clone.listDefinitions.Clear();
            AccessTools.Field(typeof(HUDOptions_ToggleButton), "category").SetValue(clone, null);
            var marker = clone.gameObject.AddComponent<NativeDefenseButton>();
            marker.native = clone;
            marker.label = (Text)AccessTools.Field(typeof(HUDOptions_ToggleButton), "label").GetValue(clone);
            var icon = (Image)AccessTools.Field(typeof(HUDOptions_ToggleButton), "image").GetValue(clone);
            if (icon != null) icon.enabled = false;
            var layout = clone.GetComponent<LayoutElement>();
            if (layout != null) layout.ignoreLayout = true;
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -62f);
            rect.sizeDelta = new Vector2(350f, 32f);
            rect.localScale = Vector3.one;
            marker.label.gameObject.SetActive(true);
            marker.label.enabled = true;
            marker.label.rectTransform.anchorMin = Vector2.zero;
            marker.label.rectTransform.anchorMax = Vector2.one;
            marker.label.rectTransform.offsetMin = new Vector2(4f, 0f);
            marker.label.rectTransform.offsetMax = new Vector2(-4f, 0f);
            marker.label.alignment = TextAnchor.MiddleCenter;
            marker.label.resizeTextForBestFit = true;
            marker.label.resizeTextMinSize = 12;
            marker.label.resizeTextMaxSize = 18;
            marker.label.raycastTarget = true;
            clone.gameObject.SetActive(true);
            return marker;
        }

        internal static void Ensure(CombatHUD hud)
        {
            if (current != null) return;
            var options = SceneSingleton<HUDOptions>.i;
            if (options == null || options.buttonPrefab == null) return;
            var canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            current = Create(options.buttonPrefab.GetComponent<HUDOptions_ToggleButton>(), canvas.transform);
        }

        internal void SetState(bool armed, string key)
        {
            string text = "AUTO-MISSILE DEFENSE: " + (armed ? "ON" : "OFF") + " [" + key + "]";
            if (text == previousText && previousArmed == armed) return;
            previousArmed = armed;
            previousText = text;
            label.text = text;
            native.Set(armed); // Actual game toggle uses its own ON/OFF palette.
        }

        internal static void RefreshAll()
        {
            if (current != null) current.SetState(AutoMissileDefensePlugin.Armed, AutoMissileDefensePlugin.ToggleKey.Value.ToString());
        }
        internal static void SetVisible(bool visible) { if (current != null) current.gameObject.SetActive(visible); }
        internal static void Clear() { if (current != null) Object.Destroy(current.gameObject); current = null; }
    }
}
