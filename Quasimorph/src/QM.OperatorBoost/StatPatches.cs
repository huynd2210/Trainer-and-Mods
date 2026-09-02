using System;
using HarmonyLib;
using MGSC;

namespace QM.OperatorBoost;

/// <summary>
/// Every boost is a postfix that adds to what the game already worked out, so the operator keeps
/// their profile stats, perks, implants, augmentations, wounds and buffs and simply ends up higher.
/// Nothing here writes to <see cref="CreatureData"/>, so no boost is ever baked into the save:
/// clear the setting and the stat is back to stock the moment the value is next read.
///
/// The one exception is maximum health, which the game caches in HealthInfo; see
/// <see cref="HealthRefresher"/> for how that is kept in step without touching BaseHealth.
/// </summary>
internal static class StatPatches
{
	/// <summary>Reports a patch that threw so a broken boost cannot take the game down with it.</summary>
	private static void Report(string patch, Exception e)
	{
		Cfg.Log?.LogError($"{patch} patch failed: {e}");
	}

	// --- survivability ---------------------------------------------------------

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetMaxHealthBonus))]
	internal static class MaxHealthPatch
	{
		private static void Postfix(CreatureData __instance, ref int __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.MaxHealth.Value;
				}
			}
			catch (Exception e) { Report("MaxHealth", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.GetResist),
		typeof(CreatureData), typeof(string), typeof(Mercenary))]
	internal static class ResistPatch
	{
		private static void Postfix(CreatureData cd, ref float __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(cd))
				{
					__result += Boosts.AllResists.Value;
				}
			}
			catch (Exception e) { Report("AllResists", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetDodge))]
	internal static class DodgePatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.Dodge.Value;
				}
			}
			catch (Exception e) { Report("Dodge", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetHealthRegenBonus))]
	internal static class HealthRegenPatch
	{
		private static void Postfix(CreatureData __instance, ref int __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.HealthRegenPerTurn.Value;
				}
			}
			catch (Exception e) { Report("HealthRegenPerTurn", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetPainRegen))]
	internal static class PainRegenPatch
	{
		private static void Postfix(CreatureData __instance, ref int __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.PainRegen.Value;
				}
			}
			catch (Exception e) { Report("PainRegen", e); }
		}
	}

	// --- offence ---------------------------------------------------------------

	// Accuracy is left alone when the game returned zero: that is how BrokenFocus and similar
	// effects express "you cannot aim at all", and a flat bonus must not undo it.

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetMeleeAccuracyNorm))]
	internal static class MeleeAccuracyPatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (__result > 0f && OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.MeleeAccuracy.Value;
				}
			}
			catch (Exception e) { Report("MeleeAccuracy", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetRangeAccuracyNorm))]
	internal static class RangeAccuracyPatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (__result > 0f && OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.RangeAccuracy.Value;
				}
			}
			catch (Exception e) { Report("RangeAccuracy", e); }
		}
	}

	// GetCritChanceBonus feeds the melee, ranged and thrown crit paths, each of which reads it
	// once, so boosting it here raises all three without stacking with itself.
	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetCritChanceBonus))]
	internal static class CritChancePatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.CritChance.Value;
				}
			}
			catch (Exception e) { Report("CritChance", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetCritDamageBonus))]
	internal static class CritDamagePatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.CritDamage.Value;
				}
			}
			catch (Exception e) { Report("CritDamage", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetArmorPenetration))]
	internal static class ArmorPenetrationPatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.ArmorPenetration.Value;
				}
			}
			catch (Exception e) { Report("ArmorPenetration", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetTotalPerkMeleeDamageBonus))]
	internal static class MeleeDamagePatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.MeleeDamage.Value;
				}
			}
			catch (Exception e) { Report("MeleeDamage", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetTotalPerkRangeDamageBonus))]
	internal static class RangeDamagePatch
	{
		private static void Postfix(CreatureData __instance, ref float __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.RangeDamage.Value;
				}
			}
			catch (Exception e) { Report("RangeDamage", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetMeleeAddedFlatDamage))]
	internal static class MeleeFlatDamagePatch
	{
		private static void Postfix(CreatureData __instance, ref int __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.MeleeFlatDamage.Value;
				}
			}
			catch (Exception e) { Report("MeleeFlatDamage", e); }
		}
	}

	// --- utility ---------------------------------------------------------------

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetBonusActionPoints))]
	internal static class ActionPointsPatch
	{
		private static void Postfix(CreatureData __instance, ref int __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.ActionPoints.Value;
				}
			}
			catch (Exception e) { Report("ActionPoints", e); }
		}
	}

	// The single-argument GetLos(Creature) overload delegates to this one, so patching here
	// covers both. A returned 1 means Blind, which a sight bonus must not paper over.
	[HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.GetLos),
		typeof(CreatureData), typeof(Mercenary))]
	internal static class SightRangePatch
	{
		private static void Postfix(CreatureData cd, ref int __result)
		{
			try
			{
				if (!cd.EffectsController.HasAnyEffect<Blind>() && OperatorRoster.IsOperator(cd))
				{
					__result = Math.Max(1, __result + Boosts.SightRange.Value);
				}
			}
			catch (Exception e) { Report("SightRange", e); }
		}
	}

	[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetFirearmRangeBonus))]
	internal static class FirearmRangePatch
	{
		private static void Postfix(CreatureData __instance, ref int __result)
		{
			try
			{
				if (OperatorRoster.IsOperator(__instance))
				{
					__result += Boosts.FirearmRange.Value;
				}
			}
			catch (Exception e) { Report("FirearmRange", e); }
		}
	}
}
