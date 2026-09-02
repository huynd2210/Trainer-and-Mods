using System;
using HarmonyLib;
using MGSC;

namespace QM.KillTracker;

/// <summary>
/// Counts every creature death that reaches <see cref="CreatureSystem.KillMonster"/> — the same
/// single funnel the vanilla game uses for its own kill stat, so the mod's grand total agrees with
/// the game's counter. Deliberately no filtering: everything the game counts, we count.
/// </summary>
[HarmonyPatch]
internal static class KillPatches
{
	[HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.KillMonster))]
	[HarmonyPostfix]
	private static void CountKill(Creature creature, Creatures creatures)
	{
		try
		{
			if (!Cfg.Ready || creature == null || creature.CreatureData == null)
			{
				return;
			}

			if (!Cfg.TrackAllyKills.Value && IsAllyOfPlayer(creature, creatures))
			{
				return;
			}

			KillLedger.Instance?.Record(creature.CreatureData);
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("KillMonster postfix failed: " + e);
		}
	}

	/// <summary>Failure to determine ally status is treated as "not an ally", i.e. the kill still counts.</summary>
	private static bool IsAllyOfPlayer(Creature creature, Creatures creatures)
	{
		try
		{
			if (creatures == null || creatures.Player == null)
			{
				return false;
			}
			return creature.IsAlly(creatures.Player);
		}
		catch (Exception e)
		{
			Cfg.Log.LogWarning("Ally check failed, treating the creature as hostile: " + e);
			return false;
		}
	}
}
