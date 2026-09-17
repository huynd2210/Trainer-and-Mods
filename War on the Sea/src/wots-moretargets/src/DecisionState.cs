namespace WoTSMoreTargets
{
    /// <summary>
    /// How many task forces the enemy has raised in the strategic decision currently being made.
    ///
    /// The game tracks the same thing with a bool, <c>CampaignAI.strategicMMOCreated</c>, which
    /// is read in exactly one place: the budget. Counting instead of flagging is what lets the
    /// cap be something other than one without changing how the AI decides to stop.
    ///
    /// Deliberately free of Harmony and game types so it can be exercised directly by the
    /// logic harness.
    /// </summary>
    internal static class DecisionState
    {
        internal static int ForcesRaised;

        /// <summary>Called as a strategic decision begins, mirroring the game clearing its own flag.</summary>
        internal static void BeginDecision()
        {
            ForcesRaised = 0;
        }

        /// <summary>
        /// Called only when a force was actually raised. The game's creation call returns false
        /// on several refusals - no mission, no hulls available, unsafe route - and a refusal
        /// must not consume the decision's allowance, or the enemy would go quiet for the
        /// opposite of the intended reason.
        /// </summary>
        internal static void RecordForce()
        {
            ForcesRaised++;
        }

        /// <summary>True once this decision has raised everything it is allowed to.</summary>
        internal static bool BudgetExhausted
        {
            get { return ForcesRaised >= MoreTargetsPlugin.ForcesPerDecision; }
        }

        /// <summary>
        /// What the enemy AI should be told its budget is. Negative is the game's own
        /// "stop deciding" signal, which is preserved so the decision sequence still terminates
        /// the way it always did.
        /// </summary>
        internal static int ForceAvailable()
        {
            return BudgetExhausted ? -1 : MoreTargetsPlugin.EnemyCommandPoints;
        }

        /// <summary>Scales a mission's cooldown before the enemy's next decision.</summary>
        internal static int ScaleDecisionDelay(int days)
        {
            if (MoreTargetsPlugin.DecisionDelayPercent == 100)
            {
                return days;
            }
            int scaled = (int)System.Math.Round(days * (MoreTargetsPlugin.DecisionDelayPercent / 100.0));
            return scaled < 0 ? 0 : scaled;
        }
    }
}
