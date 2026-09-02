using System;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QM.OperatorBoost;

/// <summary>
/// Keeps the boost applied when the game itself resizes a grid — equipping or removing a backpack
/// or vest, a perk changing the vest bonus, a wound adding a slot. Every one of those paths ends
/// in ResizeBackpack or ResizeVest, so widening the requested size here means the operator never
/// sees their grid snap back to stock for a second before <see cref="InventoryBoost"/> catches up.
///
/// These are prefixes rather than the refresher doing all the work because the game passes the
/// floor storage down these paths, which is what lets it place items that no longer fit instead of
/// destroying them.
/// </summary>
internal static class InventoryPatches
{
	[HarmonyPatch(typeof(Inventory), "ResizeBackpack")]
	internal static class BackpackResizePatch
	{
		private static void Prefix(Inventory __instance, ref int width, ref int height)
		{
			try
			{
				if (!OperatorRoster.IsOperatorInventory(__instance))
				{
					return;
				}
				width = Mathf.Max(1, width + Boosts.BackpackWidth.Value);
				height = Mathf.Max(1, height + Boosts.BackpackHeight.Value);
			}
			catch (Exception e)
			{
				Cfg.Log?.LogError("Backpack resize patch failed: " + e);
			}
		}
	}

	[HarmonyPatch(typeof(Inventory), "ResizeVest")]
	internal static class VestResizePatch
	{
		private static void Prefix(Inventory __instance, ref int width)
		{
			try
			{
				if (!OperatorRoster.IsOperatorInventory(__instance))
				{
					return;
				}
				width = Mathf.Max(1, width + Boosts.VestSlots.Value);
			}
			catch (Exception e)
			{
				Cfg.Log?.LogError("Vest resize patch failed: " + e);
			}
		}
	}
}
