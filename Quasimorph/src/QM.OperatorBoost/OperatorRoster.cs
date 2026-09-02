using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MGSC;
using UnityEngine;

namespace QM.OperatorBoost;

/// <summary>
/// Decides whether a given <see cref="CreatureData"/> belongs to one of the player's operators.
///
/// This matters because every stat this mod boosts lives on <see cref="CreatureData"/>, which
/// monsters, allies and turrets share with your operators. The test is deliberately identity
/// based: an operator is a creature that some <see cref="Mercenary"/> in the roster is actually
/// holding. Checking the alliance instead would also catch converted monsters and summons.
///
/// In a raid, <c>Player.CreatureData</c> is the very same object as the deployed mercenary's
/// <c>CreatureData</c> (Player.ConfigureMercenary passes it straight through), so the raid
/// operator is covered by the roster lookup without a special case.
/// </summary>
internal static class OperatorRoster
{
	/// <summary>
	/// The roster changes rarely — hiring, cloning, death — but the stat getters below are called
	/// several times per frame by the UI, so the set is rebuilt on a timer rather than per call.
	/// </summary>
	private const float RebuildIntervalSeconds = 0.5f;

	private static readonly HashSet<CreatureData> _operators =
		new HashSet<CreatureData>(ReferenceComparer<CreatureData>.Instance);

	private static readonly HashSet<Inventory> _inventories =
		new HashSet<Inventory>(ReferenceComparer<Inventory>.Instance);

	private static readonly List<Mercenary> _mercenaries = new List<Mercenary>();

	private static float _nextRebuild;
	private static bool _loggedFailure;

	/// <summary>True if this creature is one of the player's operators and boosts should apply.</summary>
	public static bool IsOperator(CreatureData creatureData)
	{
		if (creatureData == null || !Cfg.Active)
		{
			return false;
		}

		try
		{
			Refresh();
			if (!_operators.Contains(creatureData))
			{
				return false;
			}

			if (Cfg.OnlyOperatorInRaid.Value)
			{
				Mercenary inRaid = GameState.Get<Mercenaries>()?.MercenaryInRaid;
				return inRaid != null && ReferenceEquals(inRaid.CreatureData, creatureData);
			}

			return true;
		}
		catch (Exception e)
		{
			LogFailureOnce("Roster lookup failed: " + e);
			return false;
		}
	}

	/// <summary>
	/// True if this inventory belongs to one of the player's operators. Inventory has no back
	/// reference to its creature, so the roster keeps a parallel set rather than searching.
	/// </summary>
	public static bool IsOperatorInventory(Inventory inventory)
	{
		if (inventory == null || !Cfg.Active)
		{
			return false;
		}

		try
		{
			Refresh();
			if (!_inventories.Contains(inventory))
			{
				return false;
			}

			if (Cfg.OnlyOperatorInRaid.Value)
			{
				Mercenary inRaid = GameState.Get<Mercenaries>()?.MercenaryInRaid;
				return inRaid != null && ReferenceEquals(inRaid.CreatureData?.Inventory, inventory);
			}

			return true;
		}
		catch (Exception e)
		{
			LogFailureOnce("Inventory lookup failed: " + e);
			return false;
		}
	}

	/// <summary>Every operator the mod is currently boosting. Empty outside a campaign.</summary>
	public static IReadOnlyList<Mercenary> Current
	{
		get
		{
			try
			{
				Refresh();
			}
			catch (Exception e)
			{
				LogFailureOnce("Roster refresh failed: " + e);
			}
			return _mercenaries;
		}
	}

	/// <summary>Forces the next lookup to re-read the roster, e.g. after a save is loaded.</summary>
	public static void Invalidate() => _nextRebuild = 0f;

	private static void Refresh()
	{
		float now = Time.realtimeSinceStartup;
		if (now < _nextRebuild)
		{
			return;
		}
		_nextRebuild = now + RebuildIntervalSeconds;

		_operators.Clear();
		_inventories.Clear();
		_mercenaries.Clear();

		// Null outside a campaign; the state object is also replaced wholesale on load, which is
		// exactly why nothing here may be cached beyond the rebuild interval.
		Mercenaries roster = GameState.Get<Mercenaries>();
		if (roster == null)
		{
			return;
		}

		foreach (Mercenary mercenary in roster.Values)
		{
			Add(mercenary);
		}

		// Both are player-controlled operators that live outside the roster list.
		Add(roster.MutatedQuasimorph);
		Add(roster.ChangedMercenary);
	}

	private static void Add(Mercenary mercenary)
	{
		CreatureData data = mercenary?.CreatureData;
		if (data == null)
		{
			return;
		}
		if (!_operators.Add(data))
		{
			return;
		}
		_mercenaries.Add(mercenary);
		if (data.Inventory != null)
		{
			_inventories.Add(data.Inventory);
		}
	}

	private static void LogFailureOnce(string message)
	{
		if (_loggedFailure)
		{
			return;
		}
		_loggedFailure = true;
		Cfg.Log?.LogError(message);
	}

	/// <summary>
	/// Identity comparison. These game types do not override Equals, but relying on the default
	/// is not something to leave implicit when the whole filter depends on it.
	/// </summary>
	private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

		public bool Equals(T x, T y) => ReferenceEquals(x, y);

		public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
