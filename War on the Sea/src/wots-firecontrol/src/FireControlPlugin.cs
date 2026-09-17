using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoTSFireControl
{
    /// <summary>
    /// Fire Control — your gun crews stop guessing.
    ///
    /// Two independent halves, both player-side only, both toggleable on their own.
    ///
    ///   ANTI-AIRCRAFT. Close-in AA is a particle stream aimed at an exact intercept point, so
    ///   its misses come entirely from the emitter's cone plus a mount that re-aims only a few
    ///   times a second. Both are tightened. No guidance is added — the rounds still go exactly
    ///   where the mount points them, there is just no longer a spray and a stale aim point.
    ///
    ///   NAVAL GUNNERY. The fire-control solution converges to its ceiling on the first tick
    ///   instead of being walked up over a minute of shooting. Shell dispersion is untouched;
    ///   the guns still scatter, they simply stop aiming at a guess.
    /// </summary>
    [BepInPlugin(PluginGuid, "War on the Sea — Fire Control", PluginVersion)]
    public class FireControlPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wots.firecontrol";
        public const string PluginVersion = "1.0.0";

        /// <summary>Master switch. Both halves are gated on it, so toggling off restores vanilla.</summary>
        public static bool Enabled;

        // --- anti-aircraft ---------------------------------------------------------------

        public static bool AntiAirEnabled = true;

        /// <summary>
        /// Emitter cone half-angle in degrees. The logic harness puts every particle on target
        /// at 0.25 degrees once the aim rate below is also tightened.
        /// </summary>
        public static float AaaConeDegrees = 0.25f;

        /// <summary>Scales the emitter's muzzle radius; the cone angle does most of the work.</summary>
        public static float AaaMuzzleSpreadScale = 0.25f;

        /// <summary>
        /// Seconds between mount re-aims. Stock is 0.5 s on hull batteries and 0.2 s on directed
        /// mounts. 0.1 s is where the harness stops improving; going lower buys nothing and just
        /// churns the firing coroutines.
        /// </summary>
        public static float AaaAimInterval = 0.1f;

        // --- naval gunnery ---------------------------------------------------------------

        public static bool GunnerySolutionEnabled = true;

        /// <summary>
        /// Solution gained per director tick. The director ticks once a second and the solution
        /// is clamped to 0.99, so anything at or above 1 means a full solution on the first tick.
        /// </summary>
        public static float SolutionGainPerTick = 1f;

        /// <summary>
        /// Raise the ceiling as well as the rate, so range, weather, smoke and manoeuvring stop
        /// capping how good the solution can get. Turn this off to keep the conditions meaningful
        /// and only remove the time it takes to get there.
        /// </summary>
        public static bool ForceMaximumSolution = true;

        /// <summary>
        /// The ceiling handed to the director before it applies its own multipliers and
        /// penalties. Generous on purpose: it has to survive being multiplied by a night
        /// visibility factor and having range and smoke penalties subtracted, and still leave the
        /// game's own 0.99 clamp as the thing that decides the maximum.
        /// </summary>
        public static float SolutionCeilingHeadroom = 100f;

        private ConfigEntry<KeyCode> toggleKey;
        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<bool> showStatus;
        private ConfigEntry<bool> antiAir;
        private ConfigEntry<float> coneDegrees;
        private ConfigEntry<float> muzzleScale;
        private ConfigEntry<float> aimInterval;
        private ConfigEntry<bool> gunnery;
        private ConfigEntry<float> solutionGain;
        private ConfigEntry<bool> forceMax;

        private GUIStyle statusStyle;
        private bool styleInitialised;

        private void Awake()
        {
            toggleKey = base.Config.Bind("Controls", "ToggleKey", KeyCode.F8,
                "Key that toggles Fire Control on and off in game.");
            enabledOnStart = base.Config.Bind("General", "EnabledOnStart", true,
                "Start with Fire Control already on.");
            showStatus = base.Config.Bind("General", "ShowStatusLine", true,
                "Draw a small status line in the top-left corner while the mod is on.");

            antiAir = base.Config.Bind("AntiAir", "Enabled", true,
                "Tighten close-in anti-aircraft fire.");
            coneDegrees = base.Config.Bind("AntiAir", "ConeDegrees", 0.25f,
                "Emitter cone half-angle, in degrees. This is where AA misses come from: the " +
                "mount already aims at an exact intercept, the cone is what sprays the rounds " +
                "off it. Tightening this WITHOUT also lowering AimIntervalSeconds makes AA " +
                "worse, not better - see README.");
            muzzleScale = base.Config.Bind("AntiAir", "MuzzleSpreadScale", 0.25f,
                "Scales the emitter's muzzle radius, as a fraction of its stock value. Minor " +
                "next to the cone angle; 1.0 leaves it alone.");
            aimInterval = base.Config.Bind("AntiAir", "AimIntervalSeconds", 0.1f,
                "Seconds between mount re-aims. Stock is 0.5 on hull batteries and 0.2 on " +
                "directed mounts, and between updates every round is fired at a point going " +
                "stale at the aircraft's speed. 0.1 is where the measured improvement stops.");

            gunnery = base.Config.Bind("Gunnery", "Enabled", true,
                "Make the naval fire-control solution converge immediately.");
            solutionGain = base.Config.Bind("Gunnery", "SolutionGainPerTick", 1f,
                "Solution gained per director tick. The director ticks once a second and the " +
                "solution is capped at 0.99, so 1.0 means a full solution on the first tick. " +
                "Lower it for a fast-but-not-instant acquisition - 0.1 takes about ten seconds.");
            forceMax = base.Config.Bind("Gunnery", "ForceMaximumSolution", true,
                "Raise the solution's ceiling as well as its rate, so range, weather, smoke and " +
                "manoeuvring stop capping how good it can get. Turn this OFF to keep those " +
                "conditions meaningful and only remove the time it takes to acquire.");

            ApplySettings();
            Enabled = enabledOnStart.Value;

            new Harmony(PluginGuid).PatchAll();

            Logger.LogInfo("[FireControl] Loaded. " + toggleKey.Value + " toggles. Currently " +
                           (Enabled ? "ON" : "OFF") + ". " + Describe());
        }

        private void ApplySettings()
        {
            AntiAirEnabled = antiAir.Value;
            AaaConeDegrees = Mathf.Clamp(coneDegrees.Value, 0f, 45f);
            AaaMuzzleSpreadScale = Mathf.Clamp(muzzleScale.Value, 0f, 4f);
            AaaAimInterval = Mathf.Clamp(aimInterval.Value, 0.02f, 1f);

            GunnerySolutionEnabled = gunnery.Value;
            SolutionGainPerTick = Mathf.Clamp(solutionGain.Value, 0.001f, 10f);
            ForceMaximumSolution = forceMax.Value;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(toggleKey.Value))
            {
                return;
            }
            Enabled = !Enabled;

            // Mounts already built keep whatever cone they were given, so the switch has to go
            // round and change them. Without this, throwing it mid-battle would do nothing to
            // the ships already afloat.
            AaaTuning.ApplyToEveryPlayerShip(Enabled && AntiAirEnabled);

            Logger.LogInfo("[FireControl] " + (Enabled ? "ON" : "OFF"));
        }

        private string Describe()
        {
            string air = AntiAirEnabled
                ? "AA cone " + AaaConeDegrees + " deg @ " + AaaAimInterval + "s"
                : "AA stock";
            string guns = GunnerySolutionEnabled
                ? (ForceMaximumSolution ? "solution max instantly" : "solution instant to ceiling")
                : "solution stock";
            return air + ", " + guns;
        }

        private void OnGUI()
        {
            if (!Enabled || !showStatus.Value)
            {
                return;
            }
            if (!styleInitialised)
            {
                statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(0.55f, 0.85f, 1f) }
                };
                styleInitialised = true;
            }
            // Sits below the Trainer (y=8), Ace Aviators (y=30) and More Targets (y=52); all
            // four are meant to be installable together.
            GUI.Label(new Rect(10f, 74f, 700f, 22f),
                "FIRE CONTROL  [" + toggleKey.Value + "]   " + Describe(), statusStyle);
        }
    }
}
