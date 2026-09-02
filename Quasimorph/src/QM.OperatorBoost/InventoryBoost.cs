using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QM.OperatorBoost;

/// <summary>
/// Grows an operator's backpack and vest grids.
///
/// Inventory size is not like the other boosts. The stat boosts patch a method the game calls
/// every time it wants the value, so nothing is stored and clearing a setting undoes it instantly.
/// A grid is different: its width and height are saved state, and the items inside are placed
/// relative to them. So this boost genuinely writes to the save, and — more importantly — making a
/// grid *smaller* can destroy items, because the game's own resize hands anything that no longer
/// fits to the floor, or deletes it outright when there is no floor to hand it to.
///
/// The rule here is therefore asymmetric on purpose. Growing is always safe and happens
/// immediately. Shrinking only happens when the storage is empty; otherwise the mod leaves the
/// grid alone and says so. Losing a player's loot to a config edit is not an acceptable trade.
/// </summary>
internal static class InventoryBoost
{
	private const float IntervalSeconds = 1f;

	private static readonly Dictionary<string, string> _deferredShrinks = new Dictionary<string, string>();

	private static FieldInfo _defaultBackpackWidth;
	private static FieldInfo _defaultBackpackHeight;
	private static FieldInfo _defaultVestWidth;
	private static FieldInfo _backpackMode;
	private static bool _lookupFailed;

	private static float _nextRun;

	/// <summary>Storages the mod wants to shrink but will not while they hold items.</summary>
	public static IEnumerable<string> DeferredShrinks => _deferredShrinks.Values;

	public static void Tick()
	{
		if (!Cfg.Ready || Time.realtimeSinceStartup < _nextRun)
		{
			return;
		}
		_nextRun = Time.realtimeSinceStartup + IntervalSeconds;

		try
		{
			foreach (Mercenary mercenary in OperatorRoster.Current)
			{
				Apply(mercenary);
			}
		}
		catch (Exception e)
		{
			Cfg.Log.LogError("Inventory refresh failed: " + e);
		}
	}

	private static void Apply(Mercenary mercenary)
	{
		Inventory inventory = mercenary?.CreatureData?.Inventory;
		if (inventory == null || !ResolveFields())
		{
			return;
		}

		string who = mercenary.AgentName;

		if (TryGetBackpackTarget(inventory, out int backpackWidth, out int backpackHeight))
		{
			Resize(inventory, inventory.BackpackStore, backpackWidth, backpackHeight, who, "backpack");
		}

		if (TryGetVestTarget(inventory, out int vestWidth))
		{
			Resize(inventory, inventory.VestStore, vestWidth, 1, who, "vest");
		}
	}

	/// <summary>
	/// The size the backpack grid should have: whatever the game would give it, plus the boost.
	/// This mirrors Inventory.BackpackSlotOnItemAdded / BackpackSlotOnItemRemoved — an equipped
	/// backpack dictates the size, an empty slot falls back to the operator's default grid.
	/// </summary>
	public static bool TryGetBackpackTarget(Inventory inventory, out int width, out int height)
	{
		width = 0;
		height = 0;

		// An endless backpack has no grid to grow; leave that mode entirely alone.
		if ((Inventory.BackpackMode)_backpackMode.GetValue(inventory) == Inventory.BackpackMode.Endless)
		{
			return false;
		}

		if (inventory.BackpackSlot.Empty)
		{
			width = (int)_defaultBackpackWidth.GetValue(inventory);
			height = (int)_defaultBackpackHeight.GetValue(inventory);
		}
		else
		{
			BasePickupItem equipped = inventory.BackpackSlot.First;
			BackpackRecord record = equipped.Record<BackpackRecord>();
			if (record == null)
			{
				return false;
			}
			width = record.Width;
			height = ItemInteractionSystem.GetBackpackHeight(equipped);
		}

		width = Mathf.Max(1, width + Boosts.BackpackWidth.Value);
		height = Mathf.Max(1, height + Boosts.BackpackHeight.Value);
		return true;
	}

	/// <summary>
	/// The width the vest grid should have. Mirrors Inventory.UpdateTotalBonusVestSize, including
	/// the perk and wound-effect bonuses the game already tracks, plus the boost.
	/// </summary>
	public static bool TryGetVestTarget(Inventory inventory, out int width)
	{
		width = 0;
		if (!inventory.CanHaveVest)
		{
			return false;
		}

		int stock = inventory.VestSlot.Empty
			? (int)_defaultVestWidth.GetValue(inventory)
			: inventory.VestSlot.First.Record<VestRecord>().SlotCapacity;

		width = Mathf.Max(1, stock + inventory.TotalBonusVestSize + Boosts.VestSlots.Value);
		return true;
	}

	private static void Resize(Inventory inventory, ItemStorage storage, int width, int height,
		string who, string label)
	{
		if (storage == null || (storage.Width == width && storage.Height == height))
		{
			_deferredShrinks.Remove(who + "/" + label);
			return;
		}

		// Growing repacks the same items into a strictly larger grid, so nothing can fall out.
		// Anything else can, and is only allowed when there is nothing in there to lose.
		bool shrinking = width < storage.Width || height < storage.Height;
		if (shrinking && !storage.Empty)
		{
			string key = who + "/" + label;
			string note = $"{who}'s {label} is waiting to shrink to {width}x{height}; empty it first";
			if (!_deferredShrinks.ContainsKey(key))
			{
				Cfg.Log.LogWarning($"[inventory] {note}. Items are never dropped to make room, "
					+ $"so the {label} stays {storage.Width}x{storage.Height} until it is empty.");
			}
			_deferredShrinks[key] = note;
			return;
		}

		_deferredShrinks.Remove(who + "/" + label);

		int oldWidth = storage.Width;
		int oldHeight = storage.Height;
		inventory.ResizeStorage(storage, width, height, null);

		if (Cfg.Verbose)
		{
			Cfg.Log.LogInfo($"[inventory] {who}'s {label} {oldWidth}x{oldHeight} -> {width}x{height}");
		}
	}

	private static bool ResolveFields()
	{
		if (_lookupFailed)
		{
			return false;
		}
		if (_defaultBackpackWidth != null)
		{
			return true;
		}

		try
		{
			_defaultBackpackWidth = AccessTools.Field(typeof(Inventory), "_defaultBackpackWidth");
			_defaultBackpackHeight = AccessTools.Field(typeof(Inventory), "_defaultBackpackHeight");
			_defaultVestWidth = AccessTools.Field(typeof(Inventory), "_defaultVestWidth");
			_backpackMode = AccessTools.Field(typeof(Inventory), "_backpackMode");

			if (_defaultBackpackWidth == null || _defaultBackpackHeight == null
				|| _defaultVestWidth == null || _backpackMode == null)
			{
				_lookupFailed = true;
				_defaultBackpackWidth = null;
				Cfg.Log.LogError("Could not read Inventory's default grid sizes; the inventory "
					+ "boosts will do nothing. The game version may have changed.");
				return false;
			}
			return true;
		}
		catch (Exception e)
		{
			_lookupFailed = true;
			_defaultBackpackWidth = null;
			Cfg.Log.LogError("Failed to resolve Inventory fields: " + e);
			return false;
		}
	}
}
