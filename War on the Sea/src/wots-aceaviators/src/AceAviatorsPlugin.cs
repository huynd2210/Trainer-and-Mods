using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoTSAceAviators
{
    /// <summary>
    /// Ace Aviators — player strike aircraft aim true, and optionally cannot miss.
    ///
    /// Everything here applies only to ordnance released by a PLAYER aircraft. Enemy
    /// aircraft are never affected, and naval gunnery is deliberately NOT touched — see
    /// README.txt.
    ///
    /// Both modes remove the random aim error. The game rolls a random horizontal offset
    /// into every attack run (<see cref="EngagementAI"/>.SetAttackOffset) — up to 70 m on a
    /// level bombing run — and that roll is forced to zero, so the approach, the dive and
    /// the release point are aimed true.
    ///
    /// <see cref="AssistMode.AimOnly"/>, the default, stops there. The ordnance then flies
    /// on the game's own physics, which means a target that manoeuvres after the release
    /// can still get out from under it. Perfect aim, honest outcome.
    ///
    /// <see cref="AssistMode.AimAndGuidance"/> additionally steers released ordnance onto
    /// the target for the rest of its flight — bombs and aerial depth charges by trimming
    /// their horizontal velocity, aerial torpedoes by steering their gyro — and suppresses
    /// the dud roll on a round that lands. The round is physically flown onto the hull, so
    /// the hit is a real hit: the game's own collision, armour, penetration and damage code
    /// runs untouched.
    /// </summary>
    [BepInPlugin(PluginGuid, "War on the Sea — Ace Aviators", PluginVersion)]
    public class AceAviatorsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wots.aceaviators";
        public const string PluginVersion = "1.0.0";

        /// <summary>Master switch. Everything this mod does is gated on it.</summary>
        public static bool Enabled;

        /// <summary>How much help the aircraft get. See <see cref="AssistMode"/>.</summary>
        public static AssistMode Mode = AssistMode.AimOnly;

        /// <summary>True when released ordnance should be steered onto the target.</summary>
        public static bool GuidanceActive
        {
            get { return Enabled && Mode == AssistMode.AimAndGuidance; }
        }

        /// <summary>Peak lateral correction a guided bomb may pull, in world units/s².</summary>
        public static float BombGuidanceAuthority = 40f;

        /// <summary>Peak turn rate of a guided aerial torpedo, in degrees/s.</summary>
        public static float TorpedoTurnRate = 25f;

        /// <summary>Spread a stick of bombs along the target's hull instead of stacking it on one point.</summary>
        public static bool SpreadSticksAlongHull = true;

        /// <summary>Log every guided release to the BepInEx console.</summary>
        public static bool VerboseLogging;

        internal static ManualLogSourceShim Log;

        private ConfigEntry<KeyCode> toggleKey;
        private ConfigEntry<KeyCode> modeKey;
        private ConfigEntry<AssistMode> mode;
        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<bool> showStatus;
        private ConfigEntry<float> bombAuthority;
        private ConfigEntry<float> torpedoTurn;
        private ConfigEntry<bool> spreadSticks;
        private ConfigEntry<bool> verbose;

        private GUIStyle statusStyle;
        private bool styleInitialised;

        private void Awake()
        {
            toggleKey = base.Config.Bind("Controls", "ToggleKey", KeyCode.F5,
                "Key that toggles Ace Aviators on and off in game.");
            modeKey = base.Config.Bind("Controls", "ModeKey", KeyCode.F6,
                "Key that switches between AimOnly and AimAndGuidance in game.");
            mode = base.Config.Bind("General", "Mode", AssistMode.AimOnly,
                "AimOnly       - remove the random aim error and stop there. The ordnance then " +
                "flies on the game's own physics, so a target that manoeuvres after the release " +
                "can still get out from under it.\n" +
                "AimAndGuidance - also steer released ordnance onto the target for the rest of " +
                "its flight, and suppress duds. Nothing gets away.");
            enabledOnStart = base.Config.Bind("General", "EnabledOnStart", true,
                "Start with Ace Aviators already on.");
            showStatus = base.Config.Bind("General", "ShowStatusLine", true,
                "Draw a small status line in the top-left corner while the mod is on.");
            bombAuthority = base.Config.Bind("Tuning", "BombGuidanceAuthority", 40f,
                "How hard a guided bomb may steer, in world units per second squared. The game world " +
                "is 1 unit = 10 metres. Lower values look more like a plain falling bomb but leave " +
                "less room to correct a bad release; 40 is comfortably enough to always connect.");
            torpedoTurn = base.Config.Bind("Tuning", "TorpedoTurnRate", 25f,
                "How fast a guided aerial torpedo may turn, in degrees per second. Lower looks more " +
                "like a real torpedo run; too low and a hard-manoeuvring target can still slip it.");
            spreadSticks = base.Config.Bind("Tuning", "SpreadSticksAlongHull", true,
                "Spread a multi-bomb stick along the target's hull instead of aiming every bomb at " +
                "the same point. Cosmetic: every bomb still hits either way.");
            verbose = base.Config.Bind("Debug", "VerboseLogging", false,
                "Log every guided release and every guidance handover to the BepInEx console.");

            BombGuidanceAuthority = Mathf.Max(1f, bombAuthority.Value);
            TorpedoTurnRate = Mathf.Max(1f, torpedoTurn.Value);
            SpreadSticksAlongHull = spreadSticks.Value;
            VerboseLogging = verbose.Value;
            Enabled = enabledOnStart.Value;
            Mode = mode.Value;

            Log = new ManualLogSourceShim(Logger);

            new Harmony(PluginGuid).PatchAll();

            Logger.LogInfo("[AceAviators] Loaded. " + toggleKey.Value + " toggles, " + modeKey.Value +
                           " switches mode. Currently " + (Enabled ? "ON" : "OFF") + ", " + Describe(Mode) + ".");
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey.Value))
            {
                Enabled = !Enabled;
                ReleaseRoundsInFlight();
                Logger.LogInfo("[AceAviators] " + (Enabled ? "ON" : "OFF"));
            }
            if (Input.GetKeyDown(modeKey.Value))
            {
                Mode = Mode == AssistMode.AimOnly ? AssistMode.AimAndGuidance : AssistMode.AimOnly;
                mode.Value = Mode;
                ReleaseRoundsInFlight();
                Logger.LogInfo("[AceAviators] " + Describe(Mode));
            }
        }

        /// <summary>
        /// Stops steering anything already in the air. Those rounds finish their flight
        /// ballistically, which is what the player just asked for either way — switching off,
        /// or switching to the mode that does not steer.
        /// </summary>
        private static void ReleaseRoundsInFlight()
        {
            if (!GuidanceActive)
            {
                GuidanceTracker.Clear();
            }
        }

        private static string Describe(AssistMode assistMode)
        {
            return assistMode == AssistMode.AimAndGuidance ? "aim + guidance" : "aim only";
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
                    normal = { textColor = new Color(1f, 0.84f, 0.35f) }
                };
                styleInitialised = true;
            }
            string text = "ACE AVIATORS  " + Describe(Mode) + "  [" + toggleKey.Value + " / " + modeKey.Value + "]";
            int tracked = GuidanceTracker.Count;
            if (tracked > 0)
            {
                text += "   guiding " + tracked;
            }
            // Offset below the War on the Sea Trainer's own status line, which draws at y=8.
            // The two mods are meant to be installable together, so they must not overlap.
            GUI.Label(new Rect(10f, 30f, 500f, 22f), text, statusStyle);
        }
    }

    /// <summary>
    /// Thin logging indirection so the static patch and guidance classes can log without
    /// holding a reference to the plugin instance.
    /// </summary>
    internal sealed class ManualLogSourceShim
    {
        private readonly BepInEx.Logging.ManualLogSource source;

        internal ManualLogSourceShim(BepInEx.Logging.ManualLogSource source)
        {
            this.source = source;
        }

        internal void Info(string message)
        {
            source.LogInfo("[AceAviators] " + message);
        }

        internal void Warn(string message)
        {
            source.LogWarning("[AceAviators] " + message);
        }
    }
}
