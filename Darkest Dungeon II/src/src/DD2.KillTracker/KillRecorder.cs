using System;
using System.Collections.Generic;
using Assets.Code.Actor;
using Assets.Code.Source;
using DD2.KillTracker.Core;

namespace DD2.KillTracker
{
	/// <summary>
	/// Turns a death seen by the kill hooks (see <see cref="KillFunnelPatch"/>) into a ledger entry, after deciding whether it is a
	/// kill we count. The rules are tables, so a new rule is a new entry rather than a new branch.
	/// </summary>
	internal static class KillRecorder
	{
		/// <summary>Bounded memory of recent deaths so one death reported twice counts once.</summary>
		private static readonly DeathGuard RecentDeaths = new DeathGuard(512);

		/// <summary>The actor the Kill hook handled last; lets the event hook skip a death that was already seen.</summary>
		private static uint _lastHandledGuid;
		private static bool _hasLastHandled;

		public static void ResetGuard()
		{
			RecentDeaths.Clear();
			_hasLastHandled = false;
		}

		public static bool WasJustHandled(uint actorGuid) => _hasLastHandled && _lastHandledGuid == actorGuid;

		public static void OnActorDeath(
			ActorInstance dying, IReadOnlyList<uint> killingActorGuids,
			DeathType deathType, SourceType sourceType, IReadOnlyList<string> sourceIds)
		{
			if (!Cfg.Ready || dying == null) return;

			_lastHandledGuid = dying.ActorGuid;
			_hasLastHandled = true;

			string monsterId = dying.ActorDataId;
			if (string.IsNullOrEmpty(monsterId)) return;

			// Decided from the actor as it is right now: this runs before any listener turns it into a corpse.
			IReadOnlyList<string> tags = dying.GetActiveTags();
			if (!KillPolicy.ShouldCount(deathType?.GetName(), tags, dying.TeamIndex, Cfg.CountNonEnemyDeaths.Value)) return;

			if (!RecentDeaths.FirstTime(dying.ActorGuid + ":" + monsterId)) return;

			KillLedger ledger = Cfg.Ledger.EnsureProfile(GameBridge.ProfileKey());
			ledger.ObserveRunId(GameBridge.GameRunId());

			var rec = new KillRecord
			{
				TimeUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				MonsterId = monsterId,
				MonsterName = GameBridge.LocalizedName(monsterId),
				SourceType = sourceType?.GetName(),
				SourceId = sourceIds != null && sourceIds.Count > 0 ? sourceIds[0] : null,
				Region = GameBridge.Region(),
				RunId = ledger.RunId,
			};
			FillKiller(rec, killingActorGuids, dying.ActorGuid);

			SourceInfo source = Attribution.Resolve(monsterId);
			ledger.Record(rec, source);
			Cfg.Log.LogDebug("Kill #" + ledger.LifetimeTotal + ": " + monsterId + " [" + source.Label + "] by "
				+ (rec.KillerHeroName ?? rec.SourceType) + (rec.SourceId != null ? " (" + rec.SourceId + ")" : string.Empty));
		}

		/// <summary>
		/// The killer is recorded when the first killing actor is a hero other than the victim. DoT kills usually list the
		/// hero who applied the effect; kills with no hero behind them (corpse clean-up, self-inflicted) leave the killer
		/// fields null and are described by the source type/id.
		/// </summary>
		private static void FillKiller(KillRecord rec, IReadOnlyList<uint> killingActorGuids, uint dyingGuid)
		{
			if (killingActorGuids == null) return;
			foreach (uint guid in killingActorGuids)
			{
				if (guid == dyingGuid) continue;
				ActorInstance killer = GameBridge.FindActor(guid);
				IReadOnlyList<string> killerTags = killer?.GetActiveTags();
				if (killer == null || !KillPolicy.IsHero(killerTags)) continue;

				rec.KillerClassId = killer.ActorDataId;
				rec.KillerHeroName = string.IsNullOrEmpty(killer.ActorName) ? null : killer.ActorName;
				return;
			}
		}
	}
}
