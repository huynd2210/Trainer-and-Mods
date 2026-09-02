using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CulticLighterBrighter
{
    /// <summary>
    /// "Lighter Brighter" for CULTIC.
    ///
    /// The off-hand lighter's light is very dim: while it is out, scrPlayerControl.Update
    /// eases playerLight toward an intensity of 0.9 (+-0.1 sine flicker) with a range of
    /// only 14 (and the flashlight, when the game gives you one, toward a miserable
    /// intensity of 0.15 / range 5). In dark maps that reads as "can't see anything".
    ///
    /// This mod re-scales those lights after every game update:
    ///  - The lighter's light keeps its warm color and native flicker (we multiply the
    ///     game's own per-frame target values, flicker included), but with configurable
    ///     intensity and range multipliers.
    ///  - Optionally the pocket-lamp/flashlight beam gets fixed sane targets
    ///    instead of vanilla's. This is a separate Light from playerLight.
    ///
    /// Implementation notes:
    ///  - We run in a Harmony postfix on scrPlayerControl.Update, AFTER the game has
    ///     written its values, so ours win each frame. While the mod is on we ease
    ///     toward our boosted targets; when it turns off (or the lighter stows) the
    ///     game's own lerp smoothly takes the light back down - no popping.
    ///  - The patch runs per scrPlayerControl instance, so in online co-op every
    ///     player's lighter brightens, not just the local one.
    ///  - lighterState / lighterLightCycle are private; we read them by reflection,
    ///     cached once, to reproduce the game's exact "lighter is lit" condition:
    ///     offHandItem == Lighter AND (lighterState == Busy OR a swap is queued).
    /// </summary>
    [BepInPlugin("local.codex.culticlighterbrighter", "CULTIC Lighter Brighter", "1.1.0")]
    public sealed class LighterBrighterPlugin : BaseUnityPlugin
    {
        // Vanilla targets written by scrPlayerControl.Update while the lighter is out.
        private const float VanillaLighterIntensity = 0.9f;
        private const float VanillaLighterFlicker = 0.1f;
        private const float VanillaLighterRange = 14f;

        // Matches the game's own easing pace (~0.2 at 60 fps).
        private const float EaseRate = 12f;

        private static LighterBrighterPlugin instance;

        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<float> lighterIntensityMult;
        private ConfigEntry<float> lighterRangeMult;
        private ConfigEntry<bool> boostFlashlight;
        private ConfigEntry<float> flashlightIntensityTarget;
        private ConfigEntry<float> flashlightRangeTarget;

        private bool modEnabled;
        private string statusText = "";
        private float statusUntil;
        private Harmony harmony;

        private FieldInfo lighterStateField;
        private FieldInfo lighterCycleField;

        private sealed class PocketLampBaseline
        {
            public Light light;
            public float range;
        }

        private readonly System.Collections.Generic.Dictionary<int, PocketLampBaseline>
            pocketLampBaselines =
                new System.Collections.Generic.Dictionary<int, PocketLampBaseline>();

        private void Awake()
        {
            instance = this;

            enabledOnStart = Config.Bind("General", "EnabledOnStart", true,
                "Whether the boost is active when the game starts.");
            toggleKey = Config.Bind("Hotkeys", "ToggleBoost",
                new KeyboardShortcut(KeyCode.F6),
                "Toggle the light boost.");
            lighterIntensityMult = Config.Bind("Lighter", "IntensityMultiplier", 3.5f,
                new ConfigDescription(
                    "Multiplies the vanilla lighter flame intensity (~0.9, with flicker).",
                    new AcceptableValueRange<float>(1f, 10f)));
            lighterRangeMult = Config.Bind("Lighter", "RangeMultiplier", 2f,
                new ConfigDescription(
                    "Multiplies the vanilla lighter light radius (14).",
                    new AcceptableValueRange<float>(1f, 4f)));
            boostFlashlight = Config.Bind("Flashlight", "Enabled", true,
                "Also strengthen the pocket-lamp/flashlight beam.");
            flashlightIntensityTarget = Config.Bind("Flashlight", "IntensityTarget", 1.2f,
                new ConfigDescription(
                    "Minimum intensity while the pocket lamp is on (vanilla is about 0.15).",
                    new AcceptableValueRange<float>(0.15f, 5f)));
            flashlightRangeTarget = Config.Bind("Flashlight", "RangeTarget", 20f,
                new ConfigDescription(
                    "Minimum beam range while the pocket lamp is on.",
                    new AcceptableValueRange<float>(5f, 100f)));

            modEnabled = enabledOnStart.Value;

            lighterStateField = AccessTools.Field(typeof(scrPlayerControl), "lighterState");
            lighterCycleField = AccessTools.Field(typeof(scrPlayerControl), "lighterLightCycle");

            harmony = new Harmony("local.codex.culticlighterbrighter");
            harmony.PatchAll();
            Logger.LogInfo("CULTIC Lighter Brighter loaded. " + toggleKey.Value + " toggles." +
                " Lighter: x" + lighterIntensityMult.Value + " intensity, x" + lighterRangeMult.Value + " range." +
                (boostFlashlight.Value ? " Flashlight boost on." : ""));
        }

        private void OnDestroy()
        {
            RestoreAllPocketLampRanges();
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
            instance = null;
        }

        private void Update()
        {
            if (toggleKey.Value.IsDown())
            {
                Toggle();
            }
        }

        private void Toggle()
        {
            modEnabled = !modEnabled;
            ShowStatus(modEnabled ? "ON" : "OFF");
        }

        private void ShowStatus(string text)
        {
            statusText = "[Lighter Brighter] " + text;
            statusUntil = Time.unscaledTime + 2f;
            Logger.LogInfo(text);
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > statusUntil || string.IsNullOrEmpty(statusText))
            {
                return;
            }

            GUI.Label(new Rect(18f, 40f, 520f, 32f), statusText);
        }

        private int LighterState(scrPlayerControl player)
        {
            if (lighterStateField == null)
            {
                return 0;
            }

            object value = lighterStateField.GetValue(player);
            return value is int ? (int)value : 0;
        }

        private float LighterCycle(scrPlayerControl player)
        {
            if (lighterCycleField == null)
            {
                return 0f;
            }

            object value = lighterCycleField.GetValue(player);
            return value is float ? (float)value : 0f;
        }

        private void Apply(scrPlayerControl player)
        {
            if (player == null)
            {
                return;
            }

            Light light = player.playerLight;
            if (!modEnabled)
            {
                RestorePocketLampRange(player.playerFlashlight);
                return;
            }

            // Same condition the game uses to drive the lighter flame light.
            bool lighterLit = player.offHandItem == scrPlayerControl.OffHandItem.Lighter &&
                (LighterState(player) == 1 || player.offHandItemIsQueued);

            float ease = Mathf.Clamp01(Time.deltaTime * EaseRate);

            if (lighterLit && light != null)
            {
                // Scale the game's own per-frame target, so the native sine flicker
                // and the draw/stow transitions survive, just brighter.
                float flicker = VanillaLighterIntensity +
                    Mathf.Sin(LighterCycle(player)) * VanillaLighterFlicker;
                float intensityTarget = flicker * lighterIntensityMult.Value;
                float rangeTarget = VanillaLighterRange * lighterRangeMult.Value;

                light.intensity = Mathf.Lerp(light.intensity, intensityTarget, ease);
                light.range = Mathf.Lerp(light.range, rangeTarget, ease);
            }
            Light pocketLamp = player.playerFlashlight;
            if (boostFlashlight.Value && player.flashlightActive && pocketLamp != null)
            {
                PocketLampBaseline baseline = RememberPocketLampBaseline(pocketLamp);
                // scrPlayerControl writes the beam's AnimationCurve intensity
                // immediately before this postfix. Enforce a minimum on that
                // real beam and preserve any brighter curve values.
                pocketLamp.intensity = Mathf.Max(pocketLamp.intensity,
                    flashlightIntensityTarget.Value);
                pocketLamp.range = Mathf.Max(baseline.range,
                    flashlightRangeTarget.Value);
            }
            else
            {
                RestorePocketLampRange(pocketLamp);
            }
        }

        private PocketLampBaseline RememberPocketLampBaseline(Light light)
        {
            int id = light.GetInstanceID();
            PocketLampBaseline baseline;
            if (!pocketLampBaselines.TryGetValue(id, out baseline) ||
                baseline == null || baseline.light == null)
            {
                baseline = new PocketLampBaseline { light = light, range = light.range };
                pocketLampBaselines[id] = baseline;
            }
            return baseline;
        }

        private void RestorePocketLampRange(Light light)
        {
            if (light == null)
            {
                return;
            }
            int id = light.GetInstanceID();
            PocketLampBaseline baseline;
            if (pocketLampBaselines.TryGetValue(id, out baseline) && baseline != null)
            {
                light.range = baseline.range;
            }
        }

        private void RestoreAllPocketLampRanges()
        {
            foreach (PocketLampBaseline baseline in pocketLampBaselines.Values)
            {
                if (baseline != null && baseline.light != null)
                {
                    baseline.light.range = baseline.range;
                }
            }
            pocketLampBaselines.Clear();
        }

        [HarmonyPatch(typeof(scrPlayerControl), "Update")]
        private static class PlayerUpdatePatch
        {
            private static void Postfix(scrPlayerControl __instance)
            {
                LighterBrighterPlugin plugin = instance;
                if (plugin != null)
                {
                    plugin.Apply(__instance);
                }
            }
        }
    }
}
