using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoTSMoreTargets
{
    /// <summary>
    /// More Targets — the enemy campaign AI never runs out of command points.
    ///
    /// In campaign, the enemy's building budget is
    ///     commandPoints[0] + commandBonusPoints[0] - commandPointSpent[1]
    /// read through the private <c>CampaignAI.GetForceAvailable()</c>. That one number does
    /// two jobs: it decides whether the AI makes a strategic decision at all, and it picks how
    /// big a force that decision buys (<c>cpThresholdsForGroupSize</c> = 10 / 50 / 100). Run it
    /// into deficit and the AI simply stops sending anything until its weekly income recovers.
    ///
    /// This mod overrides that one reading. The enemy therefore never skips a decision for lack
    /// of funds and always buys the largest force tier. Nothing else in the economy is touched:
    /// in particular the player's own pool, which is a different index of the same arrays and
    /// is what the campaign UI displays, is left exactly alone.
    ///
    /// Two further throttles exist and are NOT changed by default, because they are pacing
    /// rather than budget — see <see cref="ForcesPerDecision"/> and
    /// <see cref="DecisionDelayPercent"/>. A third limit is the enemy's finite order of battle:
    /// see <see cref="ReplenishSunkHulls"/>.
    /// </summary>
    [BepInPlugin(PluginGuid, "War on the Sea — More Targets", PluginVersion)]
    public class MoreTargetsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wots.moretargets";
        public const string PluginVersion = "1.0.0";

        /// <summary>Master switch. Every patch is gated on it, so toggling off restores vanilla.</summary>
        public static bool Enabled;

        /// <summary>The budget the enemy AI is told it has, every time it asks.</summary>
        public static int EnemyCommandPoints = 100000;

        /// <summary>
        /// How many strategic forces the AI may raise in a single decision. 1 is the game's own
        /// behaviour: <c>CreateAIMobileSea</c> sets <c>strategicMMOCreated</c>, which makes the
        /// budget read as -1 and ends the decision there.
        /// </summary>
        public static int ForcesPerDecision = 1;

        /// <summary>
        /// Scales the cooldown a mission imposes before the AI's next decision, as a percentage.
        /// 100 is the game's own pacing; 50 makes the enemy decide twice as often; 0 removes the
        /// cooldown entirely.
        /// </summary>
        public static int DecisionDelayPercent = 100;

        /// <summary>
        /// Let the enemy raise ship classes it has already lost. Off by default: it changes what
        /// a campaign means, because sinking the enemy's order of battle permanently is normally
        /// how you win. It does not touch the sunk-ships record, so the campaign summary still
        /// shows every hull you put on the bottom.
        /// </summary>
        public static bool ReplenishSunkHulls;

        private ConfigEntry<KeyCode> toggleKey;
        private ConfigEntry<bool> enabledOnStart;
        private ConfigEntry<bool> showStatus;
        private ConfigEntry<int> commandPoints;
        private ConfigEntry<int> forcesPerDecision;
        private ConfigEntry<int> decisionDelayPercent;
        private ConfigEntry<bool> replenishHulls;

        private GUIStyle statusStyle;
        private bool styleInitialised;

        private void Awake()
        {
            toggleKey = base.Config.Bind("Controls", "ToggleKey", KeyCode.F7,
                "Key that toggles More Targets on and off in game.");
            enabledOnStart = base.Config.Bind("General", "EnabledOnStart", true,
                "Start with More Targets already on.");
            showStatus = base.Config.Bind("General", "ShowStatusLine", true,
                "Draw a small status line in the top-left corner while the mod is on.");

            commandPoints = base.Config.Bind("Budget", "EnemyCommandPoints", 100000,
                "The command-point budget the enemy AI is told it has whenever it asks. This is " +
                "a gate, not a wallet - the game compares it against thresholds of 10, 50 and 100 " +
                "to choose a force size, and never subtracts from it. Anything above 100 means " +
                "the enemy always raises the largest force tier and never sits out a decision " +
                "for lack of points.");

            forcesPerDecision = base.Config.Bind("Pacing", "ForcesPerDecision", 1,
                "How many task forces the enemy may raise in one strategic decision. 1 is the " +
                "game's own behaviour and is what the budget alone gives you. Raise it if you " +
                "want more enemy groups at sea rather than just bigger ones; the game's decision " +
                "sequence offers at most about ten openings per decision, so that is the ceiling.");

            decisionDelayPercent = base.Config.Bind("Pacing", "DecisionDelayPercent", 100,
                "Scales the cooldown each mission imposes before the enemy's next strategic " +
                "decision, as a percentage. 100 is the game's own pacing, 50 is twice as often, " +
                "0 removes the cooldown. This is pacing, not budget, which is why it is separate.");

            replenishHulls = base.Config.Bind("Supply", "ReplenishSunkHulls", false,
                "Let the enemy raise ship classes it has already lost. OFF by default because it " +
                "changes what a campaign means: sinking the enemy's finite order of battle is " +
                "normally how you win it. Turn it on if you want an endless supply of targets. " +
                "Your sunk-ships record is not touched either way.");

            ApplySettings();
            Enabled = enabledOnStart.Value;

            new Harmony(PluginGuid).PatchAll();

            Logger.LogInfo("[MoreTargets] Loaded. " + toggleKey.Value + " toggles. Currently " +
                           (Enabled ? "ON" : "OFF") + ". " + Describe());
        }

        private void ApplySettings()
        {
            EnemyCommandPoints = Mathf.Max(0, commandPoints.Value);
            ForcesPerDecision = Mathf.Clamp(forcesPerDecision.Value, 1, 32);
            DecisionDelayPercent = Mathf.Clamp(decisionDelayPercent.Value, 0, 1000);
            ReplenishSunkHulls = replenishHulls.Value;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey.Value))
            {
                Enabled = !Enabled;
                Logger.LogInfo("[MoreTargets] " + (Enabled ? "ON" : "OFF"));
            }
        }

        private string Describe()
        {
            string text = "budget " + EnemyCommandPoints;
            if (ForcesPerDecision != 1)
            {
                text += ", " + ForcesPerDecision + " forces/decision";
            }
            if (DecisionDelayPercent != 100)
            {
                text += ", delays at " + DecisionDelayPercent + "%";
            }
            if (ReplenishSunkHulls)
            {
                text += ", sunk hulls replenish";
            }
            return text;
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
                    normal = { textColor = new Color(1f, 0.55f, 0.4f) }
                };
                styleInitialised = true;
            }
            // Sits below the War on the Sea Trainer (y=8) and Ace Aviators (y=30); all three
            // are meant to be installable together.
            GUI.Label(new Rect(10f, 52f, 600f, 22f),
                "MORE TARGETS  [" + toggleKey.Value + "]   " + Describe(), statusStyle);
        }
    }
}
