using System;
using WoTSMoreTargets;

/// <summary>
/// Drives the real decision-allowance logic (src\DecisionState.cs is compiled in, not copied)
/// through a replica of the game's strategic decision sequence.
///
/// There is no physics to measure here, but there is one rule that would fail silently if it
/// were wrong: a creation the game refuses - no mission, no hulls left, unsafe route - must not
/// consume the decision's allowance. Get that backwards and the enemy goes quiet for exactly the
/// opposite of the intended reason, which looks identical to the mod not being installed.
///
/// The replica mirrors CampaignAI.MakeStrategicDecision: a fixed sequence of openings, each of
/// which may raise a force, with the budget consulted before and after every one and a negative
/// answer ending the decision.
/// </summary>
internal static class Logic
{
    /// <summary>How many openings CampaignAI.MakeStrategicDecision offers in one pass.</summary>
    private const int OpeningsPerDecision = 10;

    private static int failures;

    private static int Main()
    {
        Console.WriteLine("More Targets decision-allowance harness");
        Console.WriteLine();

        Console.WriteLine("-- the default must reproduce the game's own pacing --");
        Expect("1 force per decision when every opening succeeds",
            RunDecision(forcesPerDecision: 1, refusals: 0), 1);
        Expect("still 1 when the first three openings are refused",
            RunDecision(forcesPerDecision: 1, refusals: 3), 1);
        Expect("0 when every opening is refused",
            RunDecision(forcesPerDecision: 1, refusals: OpeningsPerDecision), 0);

        Console.WriteLine();
        Console.WriteLine("-- a raised allowance must be spent on forces, not on refusals --");
        Expect("3 forces per decision when every opening succeeds",
            RunDecision(forcesPerDecision: 3, refusals: 0), 3);
        Expect("still 3 when the first five openings are refused",
            RunDecision(forcesPerDecision: 3, refusals: 5), 3);
        Expect("2 when refusals leave only two openings",
            RunDecision(forcesPerDecision: 3, refusals: 8), 2);
        Expect("capped by the sequence, not the allowance, when the allowance is huge",
            RunDecision(forcesPerDecision: 32, refusals: 0), OpeningsPerDecision);

        Console.WriteLine();
        Console.WriteLine("-- the budget handed to the AI --");
        MoreTargetsPlugin.ForcesPerDecision = 1;
        MoreTargetsPlugin.EnemyCommandPoints = 100000;
        DecisionState.BeginDecision();
        ExpectTrue("a fresh decision is funded above the largest force threshold of 100",
            DecisionState.ForceAvailable() > 100);
        DecisionState.RecordForce();
        ExpectTrue("an exhausted decision reports negative, the game's own stop signal",
            DecisionState.ForceAvailable() < 0);

        Console.WriteLine();
        Console.WriteLine("-- mission cooldown scaling --");
        MoreTargetsPlugin.DecisionDelayPercent = 100;
        Expect("100 percent leaves a 7 day delay alone", DecisionState.ScaleDecisionDelay(7), 7);
        MoreTargetsPlugin.DecisionDelayPercent = 50;
        Expect("50 percent halves a 10 day delay", DecisionState.ScaleDecisionDelay(10), 5);
        MoreTargetsPlugin.DecisionDelayPercent = 0;
        Expect("0 percent removes a delay entirely", DecisionState.ScaleDecisionDelay(9), 0);
        MoreTargetsPlugin.DecisionDelayPercent = 25;
        Expect("a delay never rounds below zero", DecisionState.ScaleDecisionDelay(1), 0);
        MoreTargetsPlugin.DecisionDelayPercent = 100;

        Console.WriteLine();
        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " check(s) FAILED.");
            return 1;
        }
        Console.WriteLine("All decision-allowance checks passed.");
        return 0;
    }

    /// <summary>
    /// Replays one strategic decision and returns how many forces were raised.
    /// <paramref name="refusals"/> openings refuse before any succeed, standing in for the
    /// game's own reasons a force cannot be created.
    /// </summary>
    private static int RunDecision(int forcesPerDecision, int refusals)
    {
        MoreTargetsPlugin.ForcesPerDecision = forcesPerDecision;
        MoreTargetsPlugin.EnemyCommandPoints = 100000;

        DecisionState.BeginDecision();
        int raised = 0;

        for (int opening = 0; opening < OpeningsPerDecision; opening++)
        {
            // The game checks the budget before acting and abandons the decision on a negative.
            if (DecisionState.ForceAvailable() < 0)
            {
                break;
            }

            bool created = opening >= refusals;
            if (created)
            {
                DecisionState.RecordForce();
                raised++;
            }

            if (DecisionState.ForceAvailable() < 0)
            {
                break;
            }
        }
        return raised;
    }

    private static void Expect(string description, int actual, int expected)
    {
        if (actual == expected)
        {
            Console.WriteLine("  PASS  " + description + " (" + actual + ")");
        }
        else
        {
            Console.WriteLine("  FAIL  " + description + " - expected " + expected + ", got " + actual);
            failures++;
        }
    }

    private static void ExpectTrue(string description, bool condition)
    {
        if (condition)
        {
            Console.WriteLine("  PASS  " + description);
        }
        else
        {
            Console.WriteLine("  FAIL  " + description);
            failures++;
        }
    }
}

namespace WoTSMoreTargets
{
    /// <summary>
    /// Stand-in for the plugin's settings. The real class carries BepInEx types that have no
    /// business in a headless harness; only these values reach the decision logic.
    /// </summary>
    internal static class MoreTargetsPlugin
    {
        internal static int EnemyCommandPoints = 100000;
        internal static int ForcesPerDecision = 1;
        internal static int DecisionDelayPercent = 100;
    }
}
