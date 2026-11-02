using System;
using System.Collections.Generic;

namespace DD2.KillTracker.Core
{
	/// <summary>
	/// Decides whether a reported death is a kill we count. Pure data and logic, no game types, so it is unit-tested.
	/// The rules are tables: a new rule is one more entry, not another branch.
	/// </summary>
	internal static class KillPolicy
	{
		/// <summary>
		/// Death types that are bookkeeping, not kills (decomp: Assets.Code.Actor.DeathType, names lower-cased by the game):
		/// team = Team.ProcessTeamDeath clearing a team that only has corpses/props left,
		/// round = a corpse expiring (ActorInstance.HandleEventBattleEndRound, m_DeathRound),
		/// capture = an actor taken away without dying, debug = cheat-menu kills.
		/// </summary>
		public static readonly HashSet<string> IgnoredDeathTypes = new HashSet<string>(StringComparer.Ordinal)
		{
			"team", "round", "capture", "debug",
		};

		/// <summary>
		/// Tags (ActorDataClass m_Tags) that make an actor "not an enemy". hero/hero_story/friendly/kingdoms_ally/prop/
		/// inanimate/corpse are the ones the game's own AudioConditionUtils.IsEnemy excludes; cover and spacer are
		/// destructible scenery that carries the monster tag in the base data.
		/// </summary>
		public static readonly HashSet<string> NonEnemyTags = new HashSet<string>(StringComparer.Ordinal)
		{
			"hero", "hero_story", "friendly", "kingdoms_ally", "prop", "inanimate", "corpse", "cover", "spacer",
		};

		/// <summary>
		/// When a monster dies the game swaps the actor for its corpse class (ActorInstance.OnActorSetClass sets
		/// m_IsLiving again) and destroying that corpse raises a second death event for the same actor. Never counted.
		/// </summary>
		public const string CorpseTag = "corpse";

		public static readonly HashSet<string> HeroTags = new HashSet<string>(StringComparer.Ordinal) { "hero", "hero_story" };

		/// <summary>The party is team index 0 (decomp: Assets.Code.Combat.BattleTeams.HERO_TEAM_INDEX = 0, ENEMY_TEAM_INDEX = 1).</summary>
		public const int PlayerTeamIndex = 0;

		public static bool IsIgnoredDeathType(string deathTypeName) =>
			deathTypeName != null && IgnoredDeathTypes.Contains(deathTypeName);

		public static bool HasAny(IReadOnlyList<string> tags, HashSet<string> wanted)
		{
			if (tags == null) return false;
			for (int i = 0; i < tags.Count; i++)
			{
				if (wanted.Contains(tags[i])) return true;
			}
			return false;
		}

		/// <summary>Enemy = not on the party's team and none of the non-enemy tags. Tags unknown: the team alone decides.</summary>
		public static bool IsEnemy(IReadOnlyList<string> tags, int teamIndex)
		{
			if (teamIndex == PlayerTeamIndex) return false;
			return !HasAny(tags, NonEnemyTags);
		}

		public static bool IsHero(IReadOnlyList<string> tags) => HasAny(tags, HeroTags);

		/// <summary>The one decision: does this death go in the ledger?</summary>
		public static bool ShouldCount(string deathTypeName, IReadOnlyList<string> tags, int teamIndex, bool countNonEnemy)
		{
			if (IsIgnoredDeathType(deathTypeName)) return false;
			if (IsCorpse(tags)) return false;
			return IsEnemy(tags, teamIndex) || countNonEnemy;
		}

		public static bool IsCorpse(IReadOnlyList<string> tags)
		{
			if (tags == null) return false;
			for (int i = 0; i < tags.Count; i++)
			{
				if (tags[i] == CorpseTag) return true;
			}
			return false;
		}
	}
}
