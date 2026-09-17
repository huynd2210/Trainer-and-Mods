using HarmonyLib;

namespace WoTSMoreTargets
{
    /// <summary>
    /// The whole mod, in one override.
    ///
    /// <c>CampaignAI.GetForceAvailable()</c> is called about twenty times over a single
    /// strategic decision, and its answer does double duty: the decision sequence treats a
    /// negative result as "stop", and every branch that raises a force passes the same number
    /// along as the budget that picks the force's size. Overriding it therefore covers both
    /// halves of "infinite command points" at once - the enemy never sits a decision out, and
    /// whatever it does raise is the largest tier.
    ///
    /// The negative-means-stop contract is preserved deliberately. Returning a positive number
    /// unconditionally would let one decision run the whole sequence and raise a force at every
    /// opening, which is a pacing change rather than a budget one - so it lives behind
    /// <see cref="MoreTargetsPlugin.ForcesPerDecision"/> instead of happening by accident.
    /// </summary>
    [HarmonyPatch(typeof(CampaignAI), "GetForceAvailable")]
    internal static class GetForceAvailablePatch
    {
        private static void Postfix(ref int __result)
        {
            if (!MoreTargetsPlugin.Enabled)
            {
                return;
            }
            __result = DecisionState.ForceAvailable();
        }
    }

    /// <summary>Starts the per-decision force count fresh, mirroring the game clearing its own flag.</summary>
    [HarmonyPatch(typeof(CampaignAI), "MakeStrategicDecision")]
    internal static class MakeStrategicDecisionPatch
    {
        private static void Prefix()
        {
            DecisionState.BeginDecision();
        }
    }

    /// <summary>Counts a force only when one was actually raised, never on a refusal.</summary>
    [HarmonyPatch(typeof(CampaignAI), "CreateAIMobileSea")]
    internal static class CreateAIMobileSeaPatch
    {
        private static void Postfix(bool __result)
        {
            if (__result)
            {
                DecisionState.RecordForce();
            }
        }
    }

    /// <summary>
    /// Scales the cooldown a mission imposes before the enemy's next strategic decision.
    /// Inert at the default of 100 percent.
    /// </summary>
    [HarmonyPatch(typeof(CampaignAI), "AddAIDecisionDelay")]
    internal static class AddAIDecisionDelayPatch
    {
        private static void Prefix(ref int days)
        {
            if (MoreTargetsPlugin.Enabled)
            {
                days = DecisionState.ScaleDecisionDelay(days);
            }
        }
    }

    /// <summary>
    /// Optional, and off by default: lets the enemy raise ship classes it has already lost.
    ///
    /// The game allows a class while the number of hulls of it that are unavailable, already
    /// enemy-owned or sunk is under the number of names in the class - a finite historical order
    /// of battle. Answering "available" here is deliberately chosen over clearing the sunk list:
    /// nothing in the save is modified, the campaign summary still lists every ship the player
    /// sank, and turning the option off restores the stock behaviour immediately.
    /// </summary>
    [HarmonyPatch(typeof(CampaignManager), "IsEnemyShipClassAvailable")]
    internal static class IsEnemyShipClassAvailablePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (MoreTargetsPlugin.Enabled && MoreTargetsPlugin.ReplenishSunkHulls)
            {
                __result = true;
            }
        }
    }
}
